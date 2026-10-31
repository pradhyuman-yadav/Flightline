using UnityEngine;

namespace Flightline
{
    // Vapour trails drawn as soft ribbons rather than rows of dots. Each trail lays a point at its nozzle every step;
    // points scroll with the world, then spread, meander and thin out with age the way real condensation does.
    public partial class Game
    {
        const int TrailMax = 72; const float TrailStep = 1f / 60f;

        class Contrail
        {
            public Mesh mesh; public float life, w0, w1, alpha, wobble, gap, acc; public int n;
            public readonly Vector3[] p = new Vector3[TrailMax];
            public readonly float[] age = new float[TrailMax], seed = new float[TrailMax], str = new float[TrailMax];
        }

        // 0,1 = engine contrails; 2,3 = wingtip vortices that only condense in hard banks
        readonly Contrail[] trails = new Contrail[4];
        static readonly Vector3[] TrailSrc = { new Vector3(-0.32f, -0.06f, 0), new Vector3(0.32f, -0.06f, 0), new Vector3(-0.7f, -0.16f, 0), new Vector3(0.7f, -0.16f, 0) };
        static Material trailMat; static int[] trailIdx;
        static readonly Vector3[] tVerts = new Vector3[TrailMax * 2]; static readonly Color[] tCols = new Color[TrailMax * 2];
        static readonly Vector2[] tUvs = new Vector2[TrailMax * 2], tPts = new Vector2[TrailMax];
        float trailClock;

        void BuildTrails()
        {
            if (trailMat == null) trailMat = new Material(LineMat) { name = "FL Contrail", mainTexture = Art.Vapour };
            if (trailIdx == null)
            {
                trailIdx = new int[(TrailMax - 1) * 6];
                for (int i = 0, v = 0; i < TrailMax - 1; i++, v += 2)
                {
                    int o = i * 6;
                    trailIdx[o] = v; trailIdx[o + 1] = v + 1; trailIdx[o + 2] = v + 2;
                    trailIdx[o + 3] = v + 1; trailIdx[o + 4] = v + 3; trailIdx[o + 5] = v + 2;
                }
            }
            for (int i = 0; i < trails.Length; i++)
            {
                bool tip = i >= 2;
                var go = new GameObject(tip ? "Vortex" : "Contrail"); go.transform.SetParent(world, false);
                var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = trailMat; mr.sortingOrder = -50;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false;
                var mesh = new Mesh { name = go.name }; mesh.MarkDynamic(); go.AddComponent<MeshFilter>().sharedMesh = mesh;
                trails[i] = tip ? new Contrail { mesh = mesh, life = 0.45f, w0 = 0.02f, w1 = 0.1f, alpha = 0.45f, wobble = 0.03f, gap = 0.015f }
                                : new Contrail { mesh = mesh, life = 0.9f, w0 = 0.07f, w1 = 0.42f, alpha = 0.6f, wobble = 0.14f, gap = 0.05f };
            }
        }

        void ClearTrails() { foreach (var c in trails) { c.n = 0; c.acc = 0; c.mesh.Clear(); } }

        void UpdateTrails(float dt)
        {
            if (dt <= 0f) return;
            trailClock += dt;
            float dy = speed * dt, dx = windPush * 0.45f * dt; // contrails bend with the wind
            bool emit = state == GState.Playing, burner = boostT > 0 || headStart;
            // vapour only forms once the jet is climbing out
            float form = Mathf.SmoothStep(0f, 1f, (planeAlt - 0.35f) / 0.55f) * (1f - cloudK); // and not inside cloud
            float tipK = form * Mathf.SmoothStep(0f, 1f, (Mathf.Abs(bank) - 18f) / 22f);
            var col = Theme.Get(Tok.RunwayWhite);
            for (int i = 0; i < trails.Length; i++)
            {
                var c = trails[i];
                for (int j = 0; j < c.n; j++) { c.age[j] += dt; c.p[j].y -= dy; c.p[j].x += dx; }
                while (c.n > 0 && c.age[c.n - 1] >= c.life) c.n--;

                float s = i < 2 ? (planeJet ? form * (burner ? 1.15f : 1f) : 0f) : tipK; // engine contrails are a jet thing
                c.acc += dt;
                // keep laying (zero-strength) points while a trail is alive, so a broken trail never bridges its gap
                if (emit && c.acc >= TrailStep && (s > 0.001f || c.n > 0))
                {
                    c.acc = Mathf.Min(c.acc - TrailStep, TrailStep);
                    int m = Mathf.Min(c.n, TrailMax - 1);
                    System.Array.Copy(c.p, 0, c.p, 1, m); System.Array.Copy(c.age, 0, c.age, 1, m);
                    System.Array.Copy(c.seed, 0, c.seed, 1, m); System.Array.Copy(c.str, 0, c.str, 1, m);
                    c.p[0] = plane.TransformPoint(TrailSrc[i]); c.age[0] = 0f; c.seed[0] = trailClock * 1.6f + i * 17.3f; c.str[0] = s;
                    c.n = m + 1;
                }
                DrawTrail(c, col);
            }
        }

        void DrawTrail(Contrail c, Color col)
        {
            int n = c.n;
            c.mesh.Clear();
            if (n < 2) return;
            // older vapour meanders: noise is keyed on emission time, so neighbouring points drift together
            for (int j = 0; j < n; j++)
            {
                float k = c.age[j] / c.life;
                tPts[j] = new Vector2(c.p[j].x + (Mathf.PerlinNoise(c.seed[j], c.age[j] * 0.8f) - 0.5f) * 2f * c.wobble * k, c.p[j].y);
            }
            for (int j = 0; j < n; j++)
            {
                float k = c.age[j] / c.life;
                var d = tPts[Mathf.Max(j - 1, 0)] - tPts[Mathf.Min(j + 1, n - 1)];
                var nrm = d.sqrMagnitude > 1e-8f ? new Vector2(-d.y, d.x).normalized : Vector2.right;
                float puff = 0.85f + 0.3f * Mathf.PerlinNoise(c.seed[j] * 3.1f, 0.5f); // lumpy, not a perfect tube
                float half = Mathf.Lerp(c.w0, c.w1, 1f - (1f - k) * (1f - k)) * puff * 0.5f; // spreads fast, then slowly
                // a short clear gap behind the nozzle before the vapour condenses, then a thinning fade
                float a = c.alpha * c.str[j] * Mathf.SmoothStep(0f, 1f, c.age[j] / c.gap) * Mathf.Pow(1f - k, 1.4f);
                var cc = col; cc.a *= a;
                tVerts[j * 2] = tPts[j] + nrm * half; tVerts[j * 2 + 1] = tPts[j] - nrm * half;
                tCols[j * 2] = tCols[j * 2 + 1] = cc;
                tUvs[j * 2] = new Vector2(k, 0f); tUvs[j * 2 + 1] = new Vector2(k, 1f);
            }
            c.mesh.SetVertices(tVerts, 0, n * 2);
            c.mesh.SetColors(tCols, 0, n * 2);
            c.mesh.SetUVs(0, tUvs, 0, n * 2);
            c.mesh.SetIndices(trailIdx, 0, (n - 1) * 6, MeshTopology.Triangles, 0, true);
        }
    }
}
