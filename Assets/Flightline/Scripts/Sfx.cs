using UnityEngine;

namespace Flightline
{
    // Procedurally synthesized sound effects. No audio assets needed.
    public class Sfx : MonoBehaviour
    {
        public static Sfx I;
        AudioSource wind;
        // Small voice pool: each one-shot gets its own source so a pitch change on one sound
        // never retunes sounds already playing, and polyphony is bounded (oldest voice is stolen).
        const int Voices = 8;
        readonly AudioSource[] voices = new AudioSource[Voices];
        readonly float[] voiceStart = new float[Voices];
        static readonly float[] PowerNotes = { 523, 659, 784, 1046 };
        AudioClip coin, fuel, power, whoosh, crash, click, chime, sputter, smash;
        const int SR = 44100;

        public void Init()
        {
            I = this;
            for (int i = 0; i < Voices; i++) { var v = gameObject.AddComponent<AudioSource>(); v.playOnAwake = false; voices[i] = v; }
            wind = gameObject.AddComponent<AudioSource>(); wind.playOnAwake = false; wind.loop = true; wind.volume = 0f;
            if (FindAnyObjectByType<AudioListener>() == null) gameObject.AddComponent<AudioListener>();

            coin = Make("coin", 0.16f, t => (t < 0.05f ? Tone(t, 988) : Tone(t, 1319)) * Env(t, 0.16f, 0.003f) * 0.22f);
            click = Make("click", 0.04f, t => Mathf.Sin(2 * Mathf.PI * 1700 * t) * Env(t, 0.04f, 0.001f) * 0.18f);
            power = Make("power", 0.34f, t => { int n = Mathf.Min(3, (int)(t / 0.07f)); return Tone(t, PowerNotes[n]) * Env(t, 0.34f, 0.004f) * 0.2f; });
            chime = Make("chime", 0.8f, t => (Mathf.Sin(2 * Mathf.PI * 784 * t) * Env(t, 0.8f, 0.004f) + (t > 0.12f ? Mathf.Sin(2 * Mathf.PI * 1175 * t) * Env(t - 0.12f, 0.68f, 0.004f) : 0f)) * 0.18f);
            fuel = Sweep("fuel", 0.24f, 320, 900, 0.2f);
            whoosh = Noise("whoosh", 0.38f, t => Mathf.Lerp(0.02f, 0.25f, Mathf.Sin(Mathf.PI * t / 0.38f)), t => Mathf.Sin(Mathf.PI * t / 0.38f) * 0.3f, 7);
            smash = Noise("smash", 0.35f, t => 0.3f, t => Mathf.Pow(1 - t / 0.35f, 2) * 0.35f, 11);
            crash = Noise("crash", 1.1f, t => Mathf.Lerp(0.25f, 0.03f, t / 1.1f), t => Mathf.Pow(1 - t / 1.1f, 1.5f) * 0.55f, 3);
            sputter = Noise("sputter", 0.9f, t => 0.08f, t => (Mathf.Repeat(t * 11f, 1f) < 0.45f ? 1f : 0.15f) * (1 - t / 0.9f) * 0.4f, 5);
            wind.clip = WindLoop();
            wind.Play();
        }

        static float Tone(float t, float f) => Mathf.Sin(2 * Mathf.PI * f * t) * 0.75f + Mathf.Sin(2 * Mathf.PI * f * 3 * t) * 0.18f;
        static float Env(float t, float dur, float atk) => Mathf.Clamp01(t / atk) * Mathf.Pow(1f - Mathf.Clamp01(t / dur), 2f);

        static AudioClip Make(string n, float dur, System.Func<float, float> f)
        {
            int len = (int)(SR * dur); var d = new float[len];
            for (int i = 0; i < len; i++) d[i] = f(i / (float)SR);
            var c = AudioClip.Create(n, len, 1, SR, false); c.SetData(d, 0); return c;
        }

        static AudioClip Sweep(string n, float dur, float f0, float f1, float vol)
        {
            int len = (int)(SR * dur); var d = new float[len]; float ph = 0;
            for (int i = 0; i < len; i++) { float t = i / (float)SR; float f = Mathf.Lerp(f0, f1, t / dur); ph += 2 * Mathf.PI * f / SR; d[i] = Mathf.Sin(ph) * Env(t, dur, 0.005f) * vol; }
            var c = AudioClip.Create(n, len, 1, SR, false); c.SetData(d, 0); return c;
        }

        static AudioClip Noise(string n, float dur, System.Func<float, float> lp, System.Func<float, float> env, int seed)
        {
            var rng = new System.Random(seed); int len = (int)(SR * dur); var d = new float[len]; float y = 0, y2 = 0;
            for (int i = 0; i < len; i++)
            {
                float t = i / (float)SR; float x = (float)(rng.NextDouble() * 2 - 1); float k = lp(t);
                y += k * (x - y); y2 += k * (y - y2);
                d[i] = y2 * 3f * env(t);
            }
            var c = AudioClip.Create(n, len, 1, SR, false); c.SetData(d, 0); return c;
        }

        static AudioClip WindLoop()
        {
            var rng = new System.Random(42); int len = SR * 4, fade = SR / 2; var raw = new float[len + fade]; float y = 0, y2 = 0;
            for (int i = 0; i < raw.Length; i++) { float x = (float)(rng.NextDouble() * 2 - 1); y += 0.02f * (x - y); y2 += 0.02f * (y - y2); raw[i] = y2 * 8f; }
            var d = new float[len];
            for (int i = 0; i < len; i++) d[i] = raw[i];
            for (int i = 0; i < fade; i++) { float a = i / (float)fade; d[i] = raw[i] * a + raw[len + i] * (1 - a); }
            var c = AudioClip.Create("wind", len, 1, SR, false); c.SetData(d, 0); return c;
        }

        void Play(AudioClip c, float vol = 1f, float pitch = 1f)
        {
            if (!Save.D.sound || c == null) return;
            float now = Time.unscaledTime; int free = -1, oldest = 0;
            for (int i = 0; i < Voices; i++)
            {
                var v = voices[i];
                if (!v.isPlaying) { if (free < 0) free = i; continue; }
                // Same clip retriggered within ~25 ms (e.g. several coins in one frame): skip, stacking in phase only clips.
                if (v.clip == c && now - voiceStart[i] < 0.025f) return;
                if (voiceStart[i] < voiceStart[oldest]) oldest = i;
            }
            int pick = free >= 0 ? free : oldest;
            var s = voices[pick];
            s.Stop(); s.clip = c; s.pitch = pitch; s.volume = vol; s.Play();
            voiceStart[pick] = now;
        }

        public static void Click() { if (I) I.Play(I.click, 0.8f); }
        public void Coin(int mult) => Play(coin, 0.9f, 1f + (mult - 1) * 0.06f);
        public void Fuel() => Play(fuel);
        public void Power() => Play(power);
        public void Whoosh() => Play(whoosh, 1f, Random.Range(0.9f, 1.15f));
        public void Smash() => Play(smash);
        public void Crash() => Play(crash);
        public void Sputter() => Play(sputter);
        public void Chime() => Play(chime);
        public void Warn() => Play(click, 1f, 0.45f);
        public void Thunder() => Play(crash, 0.8f, 0.75f);

        public void SetWind(float target)
        {
            if (!Save.D.sound) target = 0f;
            wind.volume = Mathf.MoveTowards(wind.volume, target, Time.unscaledDeltaTime * 0.3f);
        }
    }
}
