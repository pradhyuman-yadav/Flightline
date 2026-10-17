using UnityEngine;

namespace Flightline
{
    // Breakup on impact: a white-hot flash and shock ring, a fireball that cools into rolling smoke, sparks, and the
    // airframe torn into wings, nose, centre section and tail. Pieces tumble away and shrink as they fall toward the
    // ground, each trailing fire and then smoke.
    public partial class Game
    {
        class Debris { public SpriteRenderer sr; public Vector2 v; public float spin, s0, smokeT; }
        readonly Debris[] debris = new Debris[5];
        static readonly Vector2[] PieceDir = { new Vector2(-1f, 0.1f), new Vector2(1f, 0.1f), new Vector2(0.15f, 1f), new Vector2(-0.25f, -0.3f), new Vector2(0f, -1f) };
        static readonly Color Char = new Color(0.3f, 0.28f, 0.27f);
        float debrisT = -1f;

        void BuildDebris()
        {
            for (int i = 0; i < debris.Length; i++)
            {
                debris[i] = new Debris { sr = SR(world, "Debris", Art.PlanePiece(0, i), 10, Color.white) };
                debris[i].sr.enabled = false;
            }
        }

        void ResetDebris() { foreach (var d in debris) d.sr.enabled = false; debrisT = -1f; }

        void Explode()
        {
            Vector3 c = plane.position; var rot = plane.rotation; float sc = plane.localScale.y;
            planeSr.enabled = shadowSr.enabled = shieldSr.enabled = magnetSr.enabled = propSr.enabled = false;

            Emit(c, Vector2.zero, new Color(1f, 0.98f, 0.88f, 1f), 0.22f, 0.6f, 3.4f, true, 23, 0f, Art.Soft, new Color(1f, 0.75f, 0.3f, 1f)); // flash
            Emit(c, Vector2.zero, new Color(1f, 1f, 1f, 0.75f), 0.4f, 0.5f, 4f, true, 22, 0f, Art.Ring); // shock ring
            for (int i = 0; i < 12; i++) // fireball: yellow-white cooling to deep red
            {
                var dir = Random.insideUnitCircle.normalized;
                Emit(c + (Vector3)(dir * 0.15f), dir * Random.Range(0.8f, 3.4f), new Color(1f, 0.88f, 0.45f, 1f), Random.Range(0.45f, 0.8f),
                     Random.Range(0.35f, 0.6f), Random.Range(1.1f, 1.8f), true, 21, 3f, Art.Soft, new Color(0.4f, 0.1f, 0.04f, 1f));
            }
            for (int i = 0; i < 14; i++) // black smoke billowing out and greying
            {
                var dir = Random.insideUnitCircle;
                Emit(c + (Vector3)(dir * 0.3f), dir * Random.Range(0.6f, 1.8f), new Color(0.16f, 0.15f, 0.15f, 0.8f), Random.Range(1.2f, 1.9f),
                     Random.Range(0.4f, 0.7f), Random.Range(1.8f, 2.6f), true, 19, 1.5f, Art.Soft, new Color(0.42f, 0.42f, 0.44f, 0.8f));
            }
            for (int i = 0; i < 18; i++) // sparks
                Emit(c, Random.insideUnitCircle.normalized * Random.Range(3f, 8f), new Color(1f, 0.85f, 0.4f, 1f), Random.Range(0.25f, 0.5f), 0.08f, 0.02f, true, 24, 2f, null, new Color(1f, 0.4f, 0.1f, 1f));

            for (int i = 0; i < debris.Length; i++)
            {
                var d = debris[i]; var t = d.sr.transform;
                t.SetPositionAndRotation(plane.TransformPoint(Art.PieceCenter[i] * (128f / 170f)), rot); // sprite p-space -> plane local units
                t.localScale = Vector3.one * sc; d.s0 = sc;
                var dir = ((Vector2)(rot * (Vector3)PieceDir[i])).normalized;
                d.v = dir * Random.Range(1.4f, 2.8f) + Random.insideUnitCircle * 0.4f + new Vector2(planeVX * 0.25f, 0f);
                d.spin = Random.Range(160f, 460f) * (Random.value < 0.5f ? -1f : 1f);
                d.smokeT = 0f; d.sr.color = Color.white; d.sr.enabled = true;
            }
            debrisT = 0f;
        }

        void UpdateDebris(float dt)
        {
            if (debrisT < 0f || dt <= 0f) return;
            debrisT += dt;
            float k = debrisT / 1.7f;
            if (k >= 1f) { ResetDebris(); return; }
            bool fire = debrisT < 0.55f;
            foreach (var d in debris)
            {
                var t = d.sr.transform; var p = t.position;
                p += (Vector3)(d.v * dt); p.y -= speed * dt;
                d.v *= Mathf.Exp(-1.2f * dt); d.v.y -= 1.4f * dt; // falls behind as it drops away
                t.position = p; t.Rotate(0f, 0f, d.spin * dt);
                float sz = d.s0 * Mathf.Lerp(1f, 0.25f, Mathf.Pow(k, 1.2f)); // shrinking = falling toward the ground
                t.localScale = Vector3.one * sz;
                var col = Color.Lerp(Color.white, Char, Mathf.Clamp01(debrisT / 0.6f)); // scorched
                col.a = 1f - Mathf.Clamp01((k - 0.7f) / 0.3f);
                d.sr.color = col;
                d.smokeT += dt;
                while (d.smokeT > 0.035f)
                {
                    d.smokeT -= 0.035f;
                    if (fire) Emit(p, Random.insideUnitCircle * 0.3f, new Color(1f, 0.6f, 0.2f, 0.85f), 0.35f, 0.2f * sz, 0.55f * sz, true, 9, 1f, Art.Soft, new Color(0.25f, 0.22f, 0.22f, 0.6f));
                    else Emit(p, Random.insideUnitCircle * 0.3f, new Color(0.2f, 0.2f, 0.21f, 0.55f * (1f - k)), 0.9f, 0.2f * sz, 0.75f * sz, true, 9, 1f, Art.Soft, new Color(0.45f, 0.45f, 0.47f, 0.4f * (1f - k)));
                }
            }
        }
    }
}
