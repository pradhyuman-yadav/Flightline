using UnityEngine;

namespace Flightline
{
    // Storm cloud ambience: random flashes that light the cloud from inside, spider bolts crawling through it, and
    // strikes that start deep inside and fork out past the edge. Each event flickers like real return strokes.
    // Purely visual: the storm's hit circle is unchanged.
    public partial class Game
    {
        static Color StormColor() => Theme.Night ? new Color(0.42f, 0.45f, 0.52f) : new Color(0.36f, 0.38f, 0.43f);
        static readonly Color BoltCore = new Color(0.94f, 0.96f, 1f), BoltGlow = new Color(0.62f, 0.72f, 1f), StormLit = new Color(0.78f, 0.81f, 0.92f);

        // storm fa[]: 0 = event length, 1 = bolt seed, 2/3 = branch angles. mode: 0 idle, 1 flash, 2 intra-cloud, 3 strike.
        void CreateStorm(Ent e)
        {
            e.childSr = SR(e.t, "Glow", Art.Soft, 6, Color.clear); e.child = e.childSr.transform;
            e.lines = new LineRenderer[4];
            e.lines[0] = Line(e.t, "BoltGlow", 7, 0.22f, LineMat, 12, Taper);
            e.lines[1] = Line(e.t, "Bolt", 8, 0.06f, LineMat, 12, Taper);
            e.lines[2] = Line(e.t, "Fork", 8, 0.035f, LineMat, 6, Taper);
            e.lines[3] = Line(e.t, "Fork", 8, 0.035f, LineMat, 6, Taper);
            e.fa = new float[4];
        }

        void InitStorm(Ent e)
        {
            e.sr.sprite = Art.StormVol(Random.Range(0, 3)); e.sr.color = StormColor(); e.sr.flipX = Random.value < 0.5f;
            e.childSr.color = Color.clear; foreach (var l in e.lines) l.enabled = false;
            e.mode = 0; e.t2 = Random.Range(0.2f, 1.8f); // stagger, so a wall of storms never flashes in sync
        }

        static Vector2 InCloud(float rx, float ry) { var r = Random.insideUnitCircle; return new Vector2(r.x * rx, r.y * ry); }

        void UpdateStormFx(Ent e, Vector3 p, float dt)
        {
            if (e.mode == 0)
            {
                e.sr.color = StormColor();
                e.childSr.color = Color.clear; foreach (var l in e.lines) l.enabled = false;
                if ((e.t2 -= dt) > 0f) return;
                float r = Random.value;
                e.mode = r < 0.45f ? 1 : r < 0.75f ? 2 : 3; e.h = 0f;
                e.fa[0] = Random.Range(0.25f, 0.45f); e.fa[1] = Random.Range(1, 100000);
                e.fa[2] = Random.Range(25f, 45f); e.fa[3] = -Random.Range(25f, 45f);
                e.a = InCloud(0.5f, 0.22f);
                if (e.mode == 2) e.b = -e.a * 0.6f + InCloud(0.25f, 0.12f);
                else if (e.mode == 3) { float ang = Random.Range(0f, Mathf.PI * 2f); e.b = e.a * 0.4f + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang) * 0.75f) * Random.Range(1.0f, 1.4f); }
            }

            e.h += dt;
            float u = e.h / e.fa[0];
            if (u >= 1f) { e.mode = 0; e.t2 = Random.Range(0.5f, 2.2f); return; }
            // leader, dim gap, restrike, then a fading glow
            float I = u < 0.15f ? 1f : u < 0.28f ? 0.25f : u < 0.42f ? 0.85f : (1f - u) * 0.9f;
            I *= 0.85f + 0.15f * Random.value;
            int pulse = u < 0.28f ? 0 : 1;

            float s = e.t.localScale.x;
            e.sr.color = Color.Lerp(StormColor(), StormLit, 0.65f * I);
            e.child.localPosition = e.a;
            e.child.localScale = Vector3.one * (e.mode == 1 ? 1.6f : 1.2f);
            e.childSr.color = new Color(BoltGlow.r, BoltGlow.g, BoltGlow.b, 0.55f * I);

            bool bolt = e.mode >= 2 && I > 0.15f;
            foreach (var l in e.lines) l.enabled = bolt;
            if (!bolt) return;
            // keep one bolt shape per stroke (seeded), so it flickers instead of wriggling; restore the game's RNG after
            var rs = Random.state; Random.InitState((int)e.fa[1] + pulse * 7919);
            var A = p + (Vector3)(e.a * s); var B = p + (Vector3)(e.b * s);
            var mid = (A + B) * 0.5f + (Vector3)(Random.insideUnitCircle * 0.25f * s);
            Zap(e.lines[1], A, mid, B, 0.14f * s);
            var buf = PosBuf(12);
            e.lines[0].SetPositions(buf);
            Vector3 f1 = buf[5], f2 = buf[8], dir = (B - A).normalized;
            for (int i = 0; i < 2; i++)
            {
                var from = i == 0 ? f1 : f2;
                var d = Quaternion.Euler(0, 0, e.fa[2 + i]) * dir * Random.Range(0.35f, 0.6f) * s;
                Zap(e.lines[2 + i], from, from + d * 0.5f + (Vector3)(Random.insideUnitCircle * 0.08f), from + d, 0.06f * s);
            }
            Random.state = rs;
            var core = BoltCore; core.a = I; Tint(e.lines[1], core);
            var glow = BoltGlow; glow.a = 0.35f * I; Tint(e.lines[0], glow);
            core.a = 0.8f * I; Tint(e.lines[2], core); Tint(e.lines[3], core);
        }
    }
}
