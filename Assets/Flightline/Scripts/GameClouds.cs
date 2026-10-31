using UnityEngine;

namespace Flightline
{
    // Cloud banks at the plane's own altitude. The body sits under the plane like any cloud (and under hazards, so
    // nothing is hidden), but as the plane enters, a veil copy fades in above it, wisps stream past, the screen edges
    // mist over and the contrails stop forming: it reads as flying through the cloud, not over it.
    public partial class Game
    {
        class Bank { public SpriteRenderer body, veil; public bool on; }
        readonly Bank[] banks = new Bank[2];
        SpriteRenderer fog;
        float cloudK, nextBankAt, wispT;

        void BuildBanks()
        {
            for (int i = 0; i < banks.Length; i++)
            {
                var b = new Bank { body = SR(world, "CloudBank", Art.CloudFrames[0], 1, Color.white) };
                b.veil = SR(b.body.transform, "Veil", Art.CloudFrames[0], 12, Color.clear);
                b.body.enabled = b.veil.enabled = false; banks[i] = b;
            }
            fog = SR(null, "CloudFog", Art.Vignette, 40, Color.clear); fog.enabled = false;
        }

        void ResetBanks()
        {
            foreach (var b in banks) { b.on = false; b.body.enabled = b.veil.enabled = false; }
            cloudK = 0; wispT = 0; fog.enabled = false; nextBankAt = Random.Range(60f, 90f);
        }

        void SpawnBank(Bank b)
        {
            float s = Random.Range(1.5f, 2.1f);
            var t = b.body.transform;
            t.localScale = Vector3.one * s;
            t.position = new Vector3(Random.Range(-PlayHalfW * 0.55f, PlayHalfW * 0.55f), camY + halfH + 1.1f * s, 0);
            b.body.flipX = b.veil.flipX = Random.value < 0.5f;
            b.on = true; b.body.enabled = true; b.veil.enabled = false;
        }

        void UpdateBanks(float dt, Color cloudC)
        {
            if (state == GState.Playing && travel >= nextBankAt)
            {
                nextBankAt = travel + Random.Range(70f, 140f);
                foreach (var b in banks) if (!b.on) { SpawnBank(b); break; }
            }
            float target = 0f; var pp = plane.position;
            bool inAir = state == GState.Playing || state == GState.Crashing;
            for (int i = 0; i < banks.Length; i++)
            {
                var b = banks[i];
                if (!b.on) continue;
                b.body.sprite = b.veil.sprite = Art.SheetFrame(Art.CloudFrames, Time.time + i * 0.9f);
                var t = b.body.transform; var p = t.position;
                p.y -= speed * 1.05f * dt; p.x += windPush * 0.25f * dt; // at our altitude, riding the same wind
                t.position = p;
                float s = t.localScale.y;
                if (p.y < camY - halfH - 1.2f * s) { b.on = false; b.body.enabled = b.veil.enabled = false; continue; }
                // immersion: a soft ellipse around the cloud's dense core
                float ex = (pp.x - p.x) / (1.0f * s), ey = (pp.y - p.y) / (0.95f * s); // the cloud sheet is about round
                float k = inAir ? 1f - Mathf.SmoothStep(0f, 1f, (Mathf.Sqrt(ex * ex + ey * ey) - 0.45f) / 0.55f) : 0f;
                target = Mathf.Max(target, k);
                var c = cloudC; c.a = 0.95f; b.body.color = c;
                c.a = 0.45f * k; b.veil.color = c; b.veil.enabled = k > 0.001f;
            }
            cloudK = Mathf.Lerp(cloudK, target, 1f - Mathf.Exp(-7f * dt));

            fog.enabled = cloudK > 0.01f;
            if (fog.enabled)
            {
                fog.transform.SetPositionAndRotation(new Vector3(camX, camY, 0), Quaternion.Euler(0, 0, camRoll));
                fog.transform.localScale = new Vector3(halfW * 2f + 1f, halfH + 0.5f, 1f);
                var c = cloudC; c.a = 0.6f * cloudK; fog.color = c;
            }

            // wisps of cloud rushing past, above the plane
            if (state != GState.Playing || cloudK < 0.05f) { wispT = 0; return; }
            wispT += dt;
            while (wispT > 0.025f)
            {
                wispT -= 0.025f;
                var c = cloudC; c.a = 0.3f * cloudK;
                var at = pp + new Vector3(Random.Range(-1.8f, 1.8f), Random.Range(0.2f, 2.8f), 0);
                Emit(at, new Vector2(0, -speed * 0.7f), c, 0.45f, Random.Range(0.5f, 0.9f), 1.3f, true, 13, 0f, Art.Soft);
            }
        }
    }
}
