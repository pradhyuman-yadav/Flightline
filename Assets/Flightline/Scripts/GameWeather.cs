using UnityEngine;
using Random = UnityEngine.Random;

namespace Flightline
{
    // Organic weather hazards: charging storm cells, live electric arcs, flowing crosswinds, beacon gates.
    public partial class Game
    {
        static Material lineMat, windMatR, windMatL;
        float windScroll;

        static readonly AnimationCurve Taper = new AnimationCurve(new Keyframe(0, 0.5f), new Keyframe(0.5f, 1f), new Keyframe(1, 0.35f));
        static readonly AnimationCurve Even = new AnimationCurve(new Keyframe(0, 0.6f), new Keyframe(0.15f, 1f), new Keyframe(0.85f, 1f), new Keyframe(1, 0.6f));

        static Material LineMat
        {
            get
            {
                if (lineMat != null) return lineMat;
                var sh = Shader.Find("Sprites/Default");
                if (sh == null) sh = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
                lineMat = new Material(sh) { name = "FL Line" };
                return lineMat;
            }
        }

        static Material WindMat(bool right)
        {
            ref Material m = ref (right ? ref windMatR : ref windMatL);
            if (m == null)
            {
                m = new Material(LineMat) { name = right ? "FL WindR" : "FL WindL", mainTexture = Art.WindDash };
                m.mainTextureScale = new Vector2(0.32f, 1f); // long, soft strokes
            }
            return m;
        }

        static LineRenderer Line(Transform parent, string name, int order, float width, Material m, int points, AnimationCurve curve = null)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.sharedMaterial = m; lr.useWorldSpace = true; lr.positionCount = points;
            lr.widthMultiplier = width; if (curve != null) lr.widthCurve = curve;
            lr.sortingOrder = order; lr.numCapVertices = 4; lr.numCornerVertices = 3; lr.alignment = LineAlignment.TransformZ;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; lr.receiveShadows = false;
            lr.textureMode = LineTextureMode.Stretch;
            return lr;
        }

        static void Tint(LineRenderer lr, Color c) { lr.startColor = c; lr.endColor = c; }

        static Vector3 Bez(Vector3 a, Vector3 c, Vector3 b, float t) => (1 - t) * (1 - t) * a + 2 * (1 - t) * t * c + t * t * b;

        // Jagged lightning along a curved path: bezier spine + random offsets that vanish at the ends.
        static void Zap(LineRenderer lr, Vector3 a, Vector3 c, Vector3 b, float jag)
        {
            int n = lr.positionCount;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)(n - 1);
                var p = Bez(a, c, b, t);
                var tan = 2 * (1 - t) * (c - a) + 2 * t * (b - c);
                var nrm = new Vector3(-tan.y, tan.x, 0).normalized;
                lr.SetPosition(i, p + nrm * (Random.Range(-1f, 1f) * jag * Mathf.Sin(t * Mathf.PI)));
            }
        }

        Color CellColor() => Color.Lerp(Theme.Get(Tok.Stop), Color.black, 0.28f);

        // ---------------------------------------------------------------- create / init
        void CreateWeather(Ent e)
        {
            switch (e.kind)
            {
                case Kind.Lightning:
                    e.sr.sprite = Art.Storm; e.sr.sortingOrder = 6; e.r = 0.5f; e.baseScale = 1f;
                    e.partSr = new SpriteRenderer[2];
                    e.partSr[0] = SR(e.t, "Charge", Art.Ring, 3, Color.clear);
                    e.partSr[1] = SR(e.t, "Flash", Art.Soft, 2, Color.clear);
                    e.lines = new LineRenderer[8];
                    for (int i = 0; i < 8; i++) e.lines[i] = Line(e.t, "Bolt", 12, i < 6 ? 0.1f : 0.05f, LineMat, i < 6 ? 10 : 6, Taper);
                    e.fa = new float[6]; e.fb = new float[6];
                    break;
                case Kind.Arc:
                    e.sr.sprite = null; e.r = 0f;
                    e.partSr = new SpriteRenderer[3];
                    e.partSr[0] = SR(e.t, "NodeA", Art.Storm, 7, Color.white);
                    e.partSr[1] = SR(e.t, "NodeB", Art.Storm, 7, Color.white);
                    e.partSr[2] = SR(e.t, "Halo", Art.Soft, 3, Color.clear);
                    e.lines = new LineRenderer[3];
                    e.lines[0] = Line(e.t, "Glow", 10, 0.38f, LineMat, 18, Even);
                    e.lines[1] = Line(e.t, "Core", 12, 0.09f, LineMat, 18, Even);
                    e.lines[2] = Line(e.t, "Fork", 11, 0.05f, LineMat, 18, Even);
                    break;
                case Kind.Wind:
                    e.sr.sprite = null; e.r = 0f;
                    e.lines = new LineRenderer[6]; e.fa = new float[6]; e.fb = new float[6];
                    for (int i = 0; i < 6; i++) { e.lines[i] = Line(e.t, "Stream", -39, 0.08f, WindMat(true), 30); e.lines[i].textureMode = LineTextureMode.Tile; e.lines[i].numCapVertices = 0; }
                    break;
                case Kind.Gate:
                    e.sr.sprite = null; e.r = 0f;
                    e.partSr = new SpriteRenderer[15];
                    for (int i = 0; i < 15; i++) e.partSr[i] = SR(e.t, "Beacon", Art.Circle, 2, Color.white);
                    break;
            }
        }

        void InitWeather(Ent e)
        {
            switch (e.kind)
            {
                case Kind.Lightning:
                    e.sr.color = CellColor(); e.sr.flipX = Random.value < 0.5f; e.h = 1.6f; e.extent = e.h;
                    e.partSr[0].color = Color.clear; e.partSr[1].color = Color.clear;
                    foreach (var l in e.lines) l.enabled = false;
                    break;
                case Kind.Arc:
                    foreach (var n in e.partSr) { n.transform.localScale = Vector3.one * 0.62f; n.color = CellColor(); n.enabled = true; }
                    e.partSr[2].color = Color.clear; e.partSr[2].transform.localScale = Vector3.one * 2f;
                    foreach (var l in e.lines) l.enabled = true;
                    e.extent = 1.5f;
                    break;
                case Kind.Wind:
                    foreach (var l in e.lines) l.enabled = true;
                    break;
                case Kind.Gate:
                {
                    float w = PlayHalfW * 2f - 0.4f;
                    for (int i = 0; i < 15; i++)
                    {
                        float u = i / 14f, x = Mathf.Lerp(-w * 0.5f, w * 0.5f, u), y = 1.1f * (1f - Mathf.Pow(2f * u - 1f, 2f));
                        var s = e.partSr[i]; s.transform.localPosition = new Vector3(x, y, 0);
                        s.transform.localScale = Vector3.one * (i == 0 || i == 14 ? 0.36f : 0.2f);
                    }
                    e.extent = 1.2f;
                    break;
                }
            }
        }

        // ---------------------------------------------------------------- per-frame
        void UpdateWeatherShared(float dt)
        {
            windScroll += dt * 1.6f;
            if (windMatR != null) windMatR.mainTextureOffset = new Vector2(-windScroll, 0);
            if (windMatL != null) windMatL.mainTextureOffset = new Vector2(windScroll, 0);
        }

        // Storm cell: drifts in, charges for 1 s (ring grows to its reach), then discharges branching bolts.
        void UpdateCell(Ent e, Vector3 p, float dt, bool collide)
        {
            float R = e.h, Rk = CellReach(e), time = Time.time;
            var ring = e.partSr[0]; var flash = e.partSr[1];
            float pscale = e.baseScale * (1f + Mathf.Sin(time * 1.4f + e.phase) * 0.05f);
            e.t.localScale = Vector3.one * pscale;
            e.t.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(time * 0.8f + e.phase) * 5f);

            if (!e.warned)
            {
                // idle: faint ring at the exact strike reach, readable from far away
                ring.color = Theme.Get(Tok.HudCaution, 0.14f + 0.06f * Mathf.Sin(time * 3f + e.phase));
                ring.transform.localScale = Vector3.one * (Rk / 0.45f) / pscale;
                if (collide && p.y - PlaneY < Mathf.Max(speed, 6f) * 1.0f + 0.4f) { e.warned = true; e.t2 = 0; Sfx.I.Warn(); }
                return;
            }

            if (!e.struck)
            {
                e.t2 += dt;
                float k = Mathf.Clamp01(e.t2 / 1.0f), ke = 1f - Mathf.Pow(1f - k, 3f);
                float rr = Rk * ke; // charge fill grows to the reach: a countdown you can read
                ring.color = Theme.Get(Tok.HudCaution, Mathf.Lerp(0.3f, 0.85f, k) * (0.8f + 0.2f * Mathf.Sin(time * 24f)));
                ring.transform.localScale = Vector3.one * (Rk / 0.45f) / pscale;
                flash.color = Theme.Get(Tok.HudCaution, 0.45f * k);
                flash.transform.localScale = Vector3.one * (2f * Mathf.Max(rr, 0.2f)) / pscale;
                e.sr.color = Random.value < 0.18f * k ? Color.Lerp(CellColor(), Color.white, 0.55f) : CellColor();
                for (int i = 6; i < 8; i++)
                {
                    var l = e.lines[i]; l.enabled = Random.value < 0.25f + 0.6f * k;
                    if (!l.enabled) continue;
                    float ang = Random.value * Mathf.PI * 2f; var d = new Vector3(Mathf.Cos(ang), Mathf.Sin(ang)) * rr * Random.Range(0.35f, 0.85f);
                    Zap(l, p, p + d * 0.5f + (Vector3)(Random.insideUnitCircle * 0.25f), p + d, 0.1f);
                    Tint(l, Theme.Get(Tok.HudCaution, 0.95f));
                }
                if (k >= 1f) Discharge(e, p, collide);
                return;
            }

            // after the strike: bolts crackle briefly and fade, the spent cloud greys out
            e.t2 += dt;
            float f = Mathf.Clamp01(1f - e.t2 / 0.45f);
            for (int i = 0; i < 6; i++)
            {
                var l = e.lines[i]; l.enabled = f > 0f;
                if (!l.enabled) continue;
                var dir = new Vector3(Mathf.Cos(e.fa[i]), Mathf.Sin(e.fa[i]));
                var end = p + dir * Rk * e.fb[i];
                var mid = (p + end) * 0.5f + new Vector3(-dir.y, dir.x) * (e.fb[i] - 0.9f) * 1.2f;
                Zap(l, p, mid, end, 0.22f);
                Tint(l, new Color(1, 1, 1, f));
            }
            e.lines[6].enabled = e.lines[7].enabled = false;
            ring.color = Theme.Get(Tok.RunwayWhite, 0.8f * f);
            ring.transform.localScale = Vector3.one * (Rk * (1f + 0.3f * (1f - f)) / 0.45f) / pscale;
            flash.color = new Color(1, 1, 1, 0.6f * f * f);
            flash.transform.localScale = Vector3.one * Rk * 2.6f / pscale;
            var spent = Theme.Get(Tok.Cloud); spent.a = 0.85f;
            e.sr.color = Color.Lerp(spent, Color.white, f);
        }

        void Discharge(Ent e, Vector3 p, bool collide)
        {
            e.struck = true; e.t2 = 0; e.r = 0f; // spent cloud is harmless
            Sfx.I.Thunder(); shake = Mathf.Max(shake, 0.28f);
            float baseAng = Random.value * Mathf.PI * 2f;
            for (int i = 0; i < 6; i++) { e.fa[i] = baseAng + (i + Random.Range(-0.3f, 0.3f)) * Mathf.PI * 2f / 6f; e.fb[i] = Random.Range(0.9f, 1.05f); }
            Burst(p, Theme.Get(Tok.HudCaution), 10, 4f, 0.16f);
            if (!collide || state != GState.Playing) return;
            float d = Vector2.Distance(p, new Vector2(planeX, PlaneY));
            if (d < CellReach(e)) HazardContact(e, true);
            else if (d < CellReach(e) + 1.1f) NearMiss(e);
        }

        // Live arc: a curved crackling wire between two drifting nodes (mode 0), or a sweeping hand around a pivot (mode 1).
        void UpdateArc(Ent e, Vector3 p, float dt, bool collide)
        {
            float time = Time.time;
            Vector3 A, B;
            if (e.mode == 0)
            {
                A = p + (Vector3)e.a + new Vector3(Mathf.Sin(time * 0.9f + e.phase) * 0.15f, Mathf.Cos(time * 0.7f + e.phase) * 0.12f);
                B = p + (Vector3)e.b + new Vector3(Mathf.Sin(time * 0.8f + e.phase + 2f) * 0.15f, Mathf.Cos(time * 0.6f + e.phase + 1f) * 0.12f);
            }
            else
            {
                e.t2 += e.vx * dt;
                float ang = e.t2 * Mathf.Deg2Rad;
                A = p; B = p + new Vector3(Mathf.Cos(ang), Mathf.Sin(ang)) * e.h;
            }
            e.partSr[0].transform.position = A;
            e.partSr[1].transform.position = B;
            e.partSr[1].sprite = e.mode == 0 ? Art.Storm : Art.Soft;
            if (e.mode == 1) { e.partSr[1].color = Theme.Get(Tok.HudCaution, 0.9f); e.partSr[1].transform.localScale = Vector3.one * (0.9f + Mathf.Sin(time * 18f) * 0.15f); }
            e.partSr[0].transform.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(time + e.phase) * 6f);

            var dir = B - A; var nrm = new Vector3(-dir.y, dir.x, 0).normalized;
            float sag = Mathf.Sin(time * 1.7f + e.phase) * (e.mode == 0 ? 0.4f : 0.2f);
            var ctrl = (A + B) * 0.5f + nrm * sag;
            float flick = 0.75f + 0.25f * Mathf.Sin(time * 37f + e.phase);
            Zap(e.lines[0], A, ctrl, B, 0.1f); Tint(e.lines[0], Theme.Get(Tok.Stop, 0.3f * flick));
            Zap(e.lines[1], A, ctrl, B, 0.14f); Tint(e.lines[1], new Color(1, 1, 1, 0.95f));
            Zap(e.lines[2], A, ctrl + nrm * 0.15f, B, 0.24f); Tint(e.lines[2], Theme.Get(Tok.HudCaution, 0.7f * flick));
            e.partSr[2].transform.position = (A + B) * 0.5f; e.partSr[2].color = Theme.Get(Tok.Stop, 0.12f * flick);
            e.partSr[2].transform.localScale = new Vector3(dir.magnitude * 1.3f, 1.4f, 1);
            e.partSr[2].transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);

            if (!collide || state != GState.Playing) return;
            var pp = new Vector2(planeX, PlaneY);
            float best = 99f;
            for (int i = 0; i <= 14; i++) best = Mathf.Min(best, Vector2.Distance(Bez(A, ctrl, B, i / 14f), pp));
            float nodeD = Mathf.Min(Vector2.Distance(A, pp), e.mode == 0 ? Vector2.Distance(B, pp) : 99f);
            if (best < 0.2f + PlaneR * 0.5f || nodeD < 0.4f + PlaneR) { HazardContact(e, true); return; }
            if (Mathf.Abs(((A.y + B.y) * 0.5f) - PlaneY) < 2.5f) e.minD = Mathf.Min(e.minD, best);
            if (!e.passed && Mathf.Max(A.y, B.y) < PlaneY - 0.3f) { e.passed = true; if (e.minD < 1.3f) NearMiss(e); }
        }

        // Crosswind: wavy streamlines that flow with the wind. Push ramps in and out smoothly and gusts over time.
        void UpdateWind(Ent e, Vector3 p, float dt, bool collide)
        {
            float time = Time.time, W = PlayHalfW * 2f + 4f, dir = Mathf.Sign(e.vx);
            for (int i = 0; i < e.lines.Length; i++)
            {
                var l = e.lines[i]; int n = l.positionCount;
                float k = 0.55f + i * 0.08f, ph = e.phase + i * 1.3f;
                for (int j = 0; j < n; j++)
                {
                    float x = camX - W * 0.5f + W * j / (n - 1f);
                    float y = p.y + e.fa[i] + Mathf.Sin(x * k + ph - time * 1.8f * dir) * e.fb[i];
                    l.SetPosition(j, new Vector3(x, y, 0));
                }
            }
            if (collide && Random.value < dt * 5f)
            {
                float y = p.y + Random.Range(-e.h * 0.4f, e.h * 0.4f);
                if (y < halfH && y > -halfH) Emit(new Vector3(-dir * (PlayHalfW + 0.6f) + camX, y, 0), new Vector2(e.vx * 2.4f, Random.Range(-0.3f, 0.3f)), Theme.Get(Tok.RunwayWhite, 0.35f), 2.2f, 0.16f, 0.4f, true, -38, 0f);
            }
            if (!collide) return;
            float d = Mathf.Abs(p.y - PlaneY) / (e.h * 0.5f);
            if (d >= 1f) return;
            float f = 1f - d; f = f * f * (3f - 2f * f);
            float gust = 0.72f + 0.28f * Mathf.Sin(time * 2.3f + e.phase) * Mathf.Sin(time * 0.9f + e.phase * 2f);
            windPush += e.vx * f * gust;
            if (!e.warned && f > 0.3f) { e.warned = true; Pop(e.vx > 0 ? "CROSSWIND >>" : "<< CROSSWIND", Tok.InkInverse); }
        }

        void SetupWind(Ent e, float dir, float strength, float H)
        {
            e.vx = dir * strength; e.h = H; e.extent = H * 0.5f + 0.5f;
            var mat = WindMat(dir > 0);
            for (int i = 0; i < e.lines.Length; i++)
            {
                float u = (i + 0.5f) / e.lines.Length;
                e.fa[i] = Mathf.Lerp(-H * 0.42f, H * 0.42f, u) + Random.Range(-0.15f, 0.15f);
                e.fb[i] = Random.Range(0.18f, 0.38f);
                float edge = 1f - Mathf.Abs(u - 0.5f) * 2f;
                float a = Mathf.Lerp(0.12f, 0.5f, edge);
                var l = e.lines[i]; l.sharedMaterial = mat; l.widthMultiplier = Mathf.Lerp(0.05f, 0.1f, edge);
                var g = new Gradient();
                var c = Theme.Get(Tok.RunwayWhite);
                g.SetKeys(new[] { new GradientColorKey(c, 0), new GradientColorKey(c, 1) },
                          new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(a, 0.2f), new GradientAlphaKey(a, 0.8f), new GradientAlphaKey(0, 1) });
                l.colorGradient = g;
            }
        }

        void UpdateGate(Ent e, float time)
        {
            for (int i = 0; i < e.partSr.Length; i++)
            {
                float a = 0.3f + 0.7f * Mathf.Max(0f, Mathf.Sin(time * 6f - i * 0.55f));
                e.partSr[i].color = Theme.Get(Tok.Signal, a);
            }
        }

        // Horizontal reach of a hazard (for fairness checks and the editor autopilot).
        void HazardSpan(Ent e, bool useBase, out float lo, out float hi)
        {
            float x = useBase ? e.baseX : e.t.position.x;
            float r;
            switch (e.kind)
            {
                case Kind.Lightning: r = e.r > 0f ? CellReach(e) : 0f; break;
                case Kind.Arc:
                    if (e.mode == 1) r = e.h + 0.45f;
                    else { lo = x + Mathf.Min(e.a.x, e.b.x) - 0.6f; hi = x + Mathf.Max(e.a.x, e.b.x) + 0.6f; return; }
                    break;
                case Kind.Balloon: r = e.r + 0.3f; break;
                default: r = e.r; break;
            }
            lo = x - r; hi = x + r;
        }

        static float CellReach(Ent e) => e.h * 0.85f;
        float VerticalReach(Ent e) => e.kind == Kind.Lightning ? e.h : e.kind == Kind.Arc ? (e.mode == 1 ? e.h : 1f) : 0f;
    }
}
