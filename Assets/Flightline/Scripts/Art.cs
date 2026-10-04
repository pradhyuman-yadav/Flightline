using System.Collections.Generic;
using UnityEngine;

namespace Flightline
{
    // Simple "over" compositor for procedural pixels.
    public struct Px
    {
        public Color c;
        public Px(Color baseColor) { c = baseColor; c.a = 0f; }
        public void Add(Color col, float cov)
        {
            float a = col.a * Mathf.Clamp01(cov);
            if (a <= 0f) return;
            float k = c.a * (1f - a);
            float oa = a + k;
            c = new Color((col.r * a + c.r * k) / oa, (col.g * a + c.g * k) / oa, (col.b * a + c.b * k) / oa, oa);
        }
    }

    // All sprites are generated at runtime from signed distance fields. No imported art.
    public static class Art
    {
        public delegate Color PixFn(Vector2 p, float px);
        static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        static Color Hx(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }
        static Color G(float v, float a = 1f) => new Color(v, v, v, a);
        static readonly Color Wt = Color.white;

        public static Sprite Make(string key, int w, int h, float ppu, PixFn fn, Vector2? pivot = null, Vector4 border = default)
        {
            if (Cache.TryGetValue(key, out var s) && s != null) return s;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = key };
            var pix = new Color[w * h];
            float half = h * 0.5f, pxs = 2f / h;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    pix[y * w + x] = fn(new Vector2((x + 0.5f - w * 0.5f) / half, (y + 0.5f - half) / half), pxs);
            tex.SetPixels(pix);
            tex.Apply(false, true);
            s = Sprite.Create(tex, new Rect(0, 0, w, h), pivot ?? new Vector2(0.5f, 0.5f), ppu, 0, SpriteMeshType.FullRect, border);
            s.name = key;
            Cache[key] = s;
            return s;
        }

        // ---------- SDF helpers ----------
        public static float Cov(float d, float px) => Mathf.Clamp01(0.5f - d / px);
        public static float Circ(Vector2 p, Vector2 c, float r) => (p - c).magnitude - r;
        public static float Box(Vector2 p, Vector2 c, Vector2 half, float rad = 0f)
        {
            float dx = Mathf.Abs(p.x - c.x) - half.x + rad, dy = Mathf.Abs(p.y - c.y) - half.y + rad;
            return new Vector2(Mathf.Max(dx, 0f), Mathf.Max(dy, 0f)).magnitude + Mathf.Min(Mathf.Max(dx, dy), 0f) - rad;
        }
        public static float Seg(Vector2 p, Vector2 a, Vector2 b, float r)
        {
            Vector2 pa = p - a, ba = b - a;
            float h = Mathf.Clamp01(Vector2.Dot(pa, ba) / ba.sqrMagnitude);
            return (pa - ba * h).magnitude - r;
        }
        public static float Poly(Vector2 p, Vector2[] v)
        {
            float d = (p - v[0]).sqrMagnitude, s = 1f;
            for (int i = 0, j = v.Length - 1; i < v.Length; j = i, i++)
            {
                Vector2 e = v[j] - v[i], w = p - v[i];
                Vector2 b = w - e * Mathf.Clamp01(Vector2.Dot(w, e) / e.sqrMagnitude);
                d = Mathf.Min(d, b.sqrMagnitude);
                bool c1 = p.y >= v[i].y, c2 = p.y < v[j].y, c3 = e.x * w.y > e.y * w.x;
                if ((c1 && c2 && c3) || (!c1 && !c2 && !c3)) s = -s;
            }
            return s * Mathf.Sqrt(d);
        }
        static float SMin(float a, float b, float k)
        {
            float h = Mathf.Clamp01(0.5f + 0.5f * (b - a) / k);
            return Mathf.Lerp(b, a, h) - k * h * (1f - h);
        }
        static float Hash(Vector2 p) { float h = Mathf.Sin(p.x * 127.1f + p.y * 311.7f) * 43758.5453f; return h - Mathf.Floor(h) - 0.5f; }

        // ---------- UI shapes ----------
        public static Sprite White => Make("white", 4, 4, 4, (p, px) => Wt);
        public static Sprite RoundRect => Make("rr", 64, 64, 100, (p, px) => G(1, Cov(Box(p, Vector2.zero, new Vector2(0.99f, 0.99f), 0.375f), px)), null, new Vector4(16, 16, 16, 16));
        public static Sprite RoundTop => Make("rrtop", 64, 64, 100, (p, px) =>
        {
            float d = p.y > 0 ? Box(p, Vector2.zero, new Vector2(0.99f, 0.99f), 0.375f) : Box(p, Vector2.zero, new Vector2(0.99f, 0.99f));
            return G(1, Cov(d, px));
        }, null, new Vector4(16, 16, 16, 16));
        public static Sprite RoundOutline => Make("rro", 64, 64, 100, (p, px) =>
        {
            float d = Box(p, Vector2.zero, new Vector2(0.99f, 0.99f), 0.375f);
            return G(1, Cov(Mathf.Abs(d + 0.0625f) - 0.0625f, px));
        }, null, new Vector4(16, 16, 16, 16));
        public static Sprite Pill => Make("pill", 64, 64, 100, (p, px) => G(1, Cov(Circ(p, Vector2.zero, 0.98f), px)), null, new Vector4(31, 31, 31, 31));
        public static Sprite Dash => Make("dash", 8, 4, 100, (p, px) => G(1, p.x < 0 ? 1 : 0));

        // Repeating soft dash used to animate wind streamlines (LineRenderer, Tile mode).
        static Texture2D windTex;
        public static Texture2D WindDash
        {
            get
            {
                if (windTex != null) return windTex;
                windTex = new Texture2D(64, 4, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear, name = "windDash" };
                var px = new Color[64 * 4];
                for (int x = 0; x < 64; x++)
                {
                    float u = (x + 0.5f) / 64f;
                    float a = Mathf.SmoothStep(0f, 1f, u / 0.12f) * (1f - Mathf.SmoothStep(0f, 1f, (u - 0.38f) / 0.22f));
                    for (int y = 0; y < 4; y++) px[y * 64 + x] = new Color(1, 1, 1, Mathf.Clamp01(a));
                }
                windTex.SetPixels(px); windTex.Apply(false, true);
                return windTex;
            }
        }

        // ---------- World ----------
        public static Sprite Circle => Make("circle", 64, 64, 64, (p, px) => G(1, Cov(p.magnitude - 0.96f, px)));
        public static Sprite Soft => Make("soft", 64, 64, 64, (p, px) => { float a = Mathf.Clamp01(1f - p.magnitude); return G(1, a * a); });
        public static Sprite Ring => Make("ring", 128, 128, 128, (p, px) =>
        {
            var o = new Px(Wt);
            o.Add(G(1, 0.16f), Cov(p.magnitude - 0.9f, px));
            o.Add(Wt, Cov(Mathf.Abs(p.magnitude - 0.9f) - 0.05f, px));
            return o.c;
        });
        public static Sprite Gradient => Make("grad", 4, 256, 256, (p, px) => { float v = Mathf.Clamp01(p.y * 0.5f + 0.5f); return G(1, Mathf.SmoothStep(0f, 1f, v)); });
        public static Sprite Streak => Make("streak", 8, 128, 64, (p, px) =>
        {
            float u = Mathf.Abs(p.x) / (8f / 128f);
            float v = p.y * 0.5f + 0.5f;
            return G(1, Mathf.Sin(Mathf.PI * v) * Mathf.Clamp01(1f - u));
        });

        public static Sprite Coin => Make("coin", 128, 128, 206, (p, px) =>
        {
            Color sig = Hx("#ffcc00"), dk = Hx("#e0b300"), deep = Hx("#8a6e00");
            var o = new Px(sig); float r = p.magnitude;
            o.Add(deep, Cov(r - 0.96f, px));
            o.Add(sig, Cov(r - 0.84f, px));
            o.Add(dk, Cov(Mathf.Abs(r - 0.64f) - 0.05f, px));
            o.Add(dk, Cov(Seg(p, new Vector2(0, -0.3f), new Vector2(0, 0.3f), 0.1f), px));
            o.Add(G(1, 0.6f), Cov(Seg(p, new Vector2(-0.5f, 0.2f), new Vector2(-0.28f, 0.48f), 0.06f), px));
            return o.c;
        });

        static readonly Vector2[] WingPts = { new Vector2(-0.95f, -0.08f), new Vector2(-0.1f, 0.32f), new Vector2(0.1f, 0.32f), new Vector2(0.95f, -0.08f), new Vector2(0.95f, -0.2f), new Vector2(0.1f, 0.02f), new Vector2(-0.1f, 0.02f), new Vector2(-0.95f, -0.2f) };
        static readonly Vector2[] TailPts = { new Vector2(-0.36f, -0.8f), new Vector2(-0.05f, -0.56f), new Vector2(0.05f, -0.56f), new Vector2(0.36f, -0.8f), new Vector2(0.36f, -0.9f), new Vector2(-0.36f, -0.9f) };

        public static Sprite Plane => Make("plane", 256, 256, 170, (p, px) =>
        {
            Color body = Hx("#f5f8fa"), wing = Hx("#d9e1e8"), dark = Hx("#262d34"), sig = Hx("#ffcc00");
            var o = new Px(body);
            float fus = Seg(p, new Vector2(0, -0.72f), new Vector2(0, 0.74f), 0.13f);
            o.Add(wing, Cov(Poly(p, WingPts), px));
            o.Add(dark, Cov(Box(p, new Vector2(-0.42f, 0.1f), new Vector2(0.06f, 0.13f), 0.05f), px));
            o.Add(dark, Cov(Box(p, new Vector2(0.42f, 0.1f), new Vector2(0.06f, 0.13f), 0.05f), px));
            o.Add(sig, Cov(Poly(p, TailPts), px));
            o.Add(body, Cov(fus, px));
            o.Add(G(0.62f, 0.25f), Cov(Mathf.Max(fus, -(p.x - 0.01f)), px));
            o.Add(sig, Cov(Seg(p, new Vector2(0, -0.8f), new Vector2(0, -0.5f), 0.045f), px));
            o.Add(dark, Cov(Seg(p, new Vector2(-0.04f, 0.6f), new Vector2(0.04f, 0.6f), 0.05f), px));
            return o.c;
        });

        static float CloudSdf(Vector2 p)
        {
            float d = Circ(p, new Vector2(-0.95f, -0.25f), 0.42f);
            d = SMin(d, Circ(p, new Vector2(-0.4f, 0.08f), 0.6f), 0.15f);
            d = SMin(d, Circ(p, new Vector2(0.3f, 0.18f), 0.58f), 0.15f);
            d = SMin(d, Circ(p, new Vector2(0.9f, -0.18f), 0.45f), 0.15f);
            d = SMin(d, Box(p, new Vector2(0, -0.45f), new Vector2(1.05f, 0.3f), 0.3f), 0.15f);
            return d;
        }

        public static Sprite Cloud => Make("cloud", 256, 160, 85, (p, px) =>
        {
            float sh = Mathf.Lerp(0.86f, 1f, Mathf.Clamp01((p.y + 0.7f) / 1.3f));
            return G(sh, Cov(CloudSdf(p), px * 7f));
        });

        public static Sprite Storm => Make("storm", 256, 160, 142, (p, px) =>
        {
            float d = CloudSdf(p);
            float sh = Mathf.Lerp(0.7f, 1f, Mathf.Clamp01((p.y + 0.6f) / 1.3f));
            var o = new Px(G(sh));
            o.Add(G(sh), Cov(d, px * 1.5f));
            o.Add(G(0.62f), Cov(Mathf.Max(d + 0.12f, -(d + 0.04f)), px) * 0.0f);
            return o.c;
        });

        static readonly Vector2[] BoltPts = { new Vector2(0.12f, 0.95f), new Vector2(-0.34f, 0.0f), new Vector2(-0.02f, 0.0f), new Vector2(-0.18f, -0.95f), new Vector2(0.36f, 0.14f), new Vector2(0.04f, 0.14f), new Vector2(0.3f, 0.95f) };
        public static Sprite Bolt => Make("bolt", 64, 128, 160, (p, px) => G(1, Cov(Poly(p, BoltPts), px)));

        static readonly Vector2[] NeckPts = { new Vector2(-0.4f, -0.05f), new Vector2(0.4f, -0.05f), new Vector2(0.08f, -0.32f), new Vector2(-0.08f, -0.32f) };
        public static Sprite Balloon => Make("balloon", 128, 192, 128, (p, px) =>
        {
            var o = new Px(G(0.85f));
            o.Add(G(0.55f), Cov(Seg(p, new Vector2(0, -0.3f), new Vector2(0, -0.72f), 0.02f), px));
            o.Add(G(0.5f), Cov(Box(p, new Vector2(0, -0.84f), new Vector2(0.12f, 0.09f), 0.03f), px));
            float bd = Mathf.Min(Circ(p, new Vector2(0, 0.3f), 0.6f), Poly(p, NeckPts));
            o.Add(G(0.85f), Cov(bd, px));
            float stripe = Mathf.Abs(Mathf.Abs(p.x) - 0.24f) - 0.05f;
            o.Add(G(0.7f), Cov(Mathf.Max(bd + 0.02f, stripe), px));
            o.Add(Wt, Cov(Circ(p, new Vector2(-0.24f, 0.55f), 0.12f), px * 4f));
            return o.c;
        }, new Vector2(0.5f, 0.65f));

        public static Sprite Drone => Make("drone", 160, 160, 133, (p, px) =>
        {
            var o = new Px(G(0.85f)); var arm = G(0.55f);
            o.Add(arm, Cov(Seg(p, new Vector2(-0.62f, -0.62f), new Vector2(0.62f, 0.62f), 0.07f), px));
            o.Add(arm, Cov(Seg(p, new Vector2(-0.62f, 0.62f), new Vector2(0.62f, -0.62f), 0.07f), px));
            for (int i = 0; i < 4; i++)
            {
                var c = new Vector2(i % 2 == 0 ? -0.62f : 0.62f, i < 2 ? -0.62f : 0.62f);
                o.Add(G(0.95f, 0.45f), Cov(Circ(p, c, 0.3f), px));
                o.Add(arm, Cov(Mathf.Abs(Circ(p, c, 0.3f)) - 0.025f, px));
            }
            o.Add(G(0.85f), Cov(Box(p, Vector2.zero, new Vector2(0.24f, 0.24f), 0.08f), px));
            o.Add(G(0.25f), Cov(Circ(p, new Vector2(0, 0.06f), 0.08f), px));
            return o.c;
        });

        public static Sprite Fuel => Make("fuel", 128, 160, 160, (p, px) =>
        {
            var o = new Px(Wt);
            float body = Box(p, new Vector2(0, -0.12f), new Vector2(0.62f, 0.8f), 0.16f);
            float handle = Mathf.Max(Box(p, new Vector2(-0.12f, 0.76f), new Vector2(0.36f, 0.16f), 0.08f), -Box(p, new Vector2(-0.12f, 0.74f), new Vector2(0.22f, 0.06f), 0.04f));
            float spout = Seg(p, new Vector2(0.36f, 0.7f), new Vector2(0.55f, 0.9f), 0.08f);
            o.Add(G(0.8f), Cov(Mathf.Min(handle, spout), px));
            o.Add(Wt, Cov(body, px));
            float x1 = Seg(p, new Vector2(-0.4f, -0.7f), new Vector2(0.4f, 0.46f), 0.06f), x2 = Seg(p, new Vector2(-0.4f, 0.46f), new Vector2(0.4f, -0.7f), 0.06f);
            o.Add(G(0.75f), Cov(Mathf.Min(x1, x2), px));
            return o.c;
        });

        public static Sprite Runway => Make("runway", 256, 2048, 64, (p, px) =>
        {
            float u = p.x / 0.125f * 2f;          // -2..2 units across
            float v = (p.y * 0.5f + 0.5f) * 32f;  // 0..32 units along
            var asph = Hx("#262d34"); float n = Hash(p) * 0.04f;
            var o = new Px(asph);
            o.Add(new Color(asph.r + n, asph.g + n, asph.b + n, 1f), 1f);
            var w = Hx("#ffffff");
            if (Mathf.Abs(Mathf.Abs(u) - 1.75f) < 0.05f) o.Add(w, 0.9f);
            if (Mathf.Abs(u) < 0.06f && v > 3.5f && v < 28.5f && Mathf.Repeat(v, 2f) < 1.1f) o.Add(w, 0.9f);
            bool thr = (v > 0.6f && v < 2.4f) || (v > 29.6f && v < 31.4f);
            if (thr && Mathf.Abs(u) < 1.55f && Mathf.Abs(u) > 0.2f && Mathf.Repeat(u + 1.6f, 0.4f) < 0.22f) o.Add(w, 0.95f);
            return o.c;
        });

        // ---------- Pictograms: asphalt tile + one glyph (signal for actions, ink-inverse for objects) ----------
        static readonly Vector2[] ShieldPts = { new Vector2(-0.4f, 0.48f), new Vector2(0.4f, 0.48f), new Vector2(0.4f, 0.05f), new Vector2(0f, -0.52f), new Vector2(-0.4f, 0.05f) };
        static readonly Vector2[] PWing = { new Vector2(-0.55f, -0.02f), new Vector2(-0.06f, 0.22f), new Vector2(0.06f, 0.22f), new Vector2(0.55f, -0.02f), new Vector2(0.55f, -0.12f), new Vector2(0.06f, 0.04f), new Vector2(-0.06f, 0.04f), new Vector2(-0.55f, -0.12f) };
        static readonly Vector2[] PTail = { new Vector2(-0.22f, -0.48f), new Vector2(-0.04f, -0.34f), new Vector2(0.04f, -0.34f), new Vector2(0.22f, -0.48f), new Vector2(0.22f, -0.55f), new Vector2(-0.22f, -0.55f) };

        static float Arrow(Vector2 p) => Mathf.Min(Seg(p, new Vector2(-0.42f, 0), new Vector2(0.32f, 0), 0.08f),
            Mathf.Min(Seg(p, new Vector2(0.06f, 0.3f), new Vector2(0.38f, 0), 0.08f), Seg(p, new Vector2(0.06f, -0.3f), new Vector2(0.38f, 0), 0.08f)));

        public static Sprite Picto(string g) => Icon(g, true);
        public static Sprite Glyph(string g) => Icon(g, false); // same glyph, no tile: for round buttons and cards

        static Sprite Icon(string g, bool tile) => Make((tile ? "p_" : "g_") + g, 128, 128, 128, (p, px) =>
        {
            Color asph = Hx("#262d34"), sig = Hx("#ffcc00"), inv = Hx("#f5f8fa");
            if (!tile) { p *= 0.72f; px *= 0.72f; } // glyph-only icons fill their box
            var o = new Px(tile ? asph : sig);
            if (tile) o.Add(asph, Cov(Box(p, Vector2.zero, new Vector2(0.99f, 0.99f), 0.06f), px));
            Color gc = inv; float d = 9f;
            switch (g)
            {
                case "pause": gc = sig; d = Mathf.Min(Box(p, new Vector2(-0.2f, 0), new Vector2(0.08f, 0.38f), 0.02f), Box(p, new Vector2(0.2f, 0), new Vector2(0.08f, 0.38f), 0.02f)); break;
                case "coin": gc = sig; d = Mathf.Max(Circ(p, Vector2.zero, 0.5f), -Seg(p, new Vector2(0, -0.2f), new Vector2(0, 0.2f), 0.07f)); break;
                case "shield": d = Poly(p, ShieldPts); break;
                case "magnet":
                {
                    float arc = Mathf.Max(Mathf.Abs(Circ(p, new Vector2(0, 0.02f), 0.33f)) - 0.12f, p.y - 0.02f);
                    float legs = Mathf.Min(Box(p, new Vector2(-0.33f, 0.25f), new Vector2(0.12f, 0.23f)), Box(p, new Vector2(0.33f, 0.25f), new Vector2(0.12f, 0.23f)));
                    d = Mathf.Min(arc, legs); break;
                }
                case "boost":
                    gc = sig;
                    d = Mathf.Min(Mathf.Min(Seg(p, new Vector2(-0.36f, -0.08f), new Vector2(0, 0.26f), 0.08f), Seg(p, new Vector2(0, 0.26f), new Vector2(0.36f, -0.08f), 0.08f)),
                                  Mathf.Min(Seg(p, new Vector2(-0.36f, -0.4f), new Vector2(0, -0.06f), 0.08f), Seg(p, new Vector2(0, -0.06f), new Vector2(0.36f, -0.4f), 0.08f)));
                    break;
                case "plane": d = Mathf.Min(Mathf.Min(Seg(p, new Vector2(0, -0.5f), new Vector2(0, 0.52f), 0.08f), Poly(p, PWing)), Poly(p, PTail)); break;
                case "fuel":
                    d = Mathf.Min(Box(p, new Vector2(0, -0.08f), new Vector2(0.3f, 0.4f), 0.08f),
                        Mathf.Max(Box(p, new Vector2(-0.08f, 0.4f), new Vector2(0.2f, 0.1f), 0.05f), -Box(p, new Vector2(-0.08f, 0.38f), new Vector2(0.12f, 0.04f), 0.02f)));
                    break;
                case "arrowR": gc = sig; d = Arrow(p); break;
                case "arrowL": gc = sig; d = Arrow(new Vector2(-p.x, p.y)); break;
                case "arrowU": gc = sig; d = Arrow(new Vector2(p.y, -p.x)); break;
                case "flag":
                    gc = sig;
                    d = Mathf.Min(Seg(p, new Vector2(-0.3f, -0.52f), new Vector2(-0.3f, 0.5f), 0.06f), Poly(p, FlagPts));
                    break;
                case "wrench":
                    gc = sig;
                    d = Mathf.Min(Seg(p, new Vector2(-0.38f, -0.38f), new Vector2(0.12f, 0.12f), 0.1f),
                        Mathf.Max(Circ(p, new Vector2(0.22f, 0.22f), 0.26f), -Box(p, new Vector2(0.36f, 0.36f), new Vector2(0.1f, 0.1f))));
                    break;
                case "trophy":
                    gc = sig;
                    d = Mathf.Min(Mathf.Min(Box(p, new Vector2(0, 0.22f), new Vector2(0.27f, 0.24f), 0.14f), Mathf.Abs(Circ(p, new Vector2(-0.3f, 0.26f), 0.13f)) - 0.05f),
                        Mathf.Min(Mathf.Abs(Circ(p, new Vector2(0.3f, 0.26f), 0.13f)) - 0.05f,
                        Mathf.Min(Box(p, new Vector2(0, -0.14f), new Vector2(0.06f, 0.14f)), Box(p, new Vector2(0, -0.38f), new Vector2(0.24f, 0.07f), 0.03f))));
                    break;
                case "swap":
                {
                    gc = sig;
                    float ring = Mathf.Abs(Circ(p, Vector2.zero, 0.32f)) - 0.07f;
                    ring = Mathf.Max(ring, -Mathf.Min(Box(p, new Vector2(0.3f, 0.12f), new Vector2(0.14f, 0.12f)), Box(p, new Vector2(-0.3f, -0.12f), new Vector2(0.14f, 0.12f))));
                    d = Mathf.Min(ring, Mathf.Min(Poly(p, SwapA), Poly(p, SwapB)));
                    break;
                }
                case "check": gc = sig; d = Mathf.Min(Seg(p, new Vector2(-0.34f, 0.02f), new Vector2(-0.1f, -0.24f), 0.09f), Seg(p, new Vector2(-0.1f, -0.24f), new Vector2(0.36f, 0.28f), 0.09f)); break;
                case "lock":
                    d = Mathf.Min(Box(p, new Vector2(0, -0.14f), new Vector2(0.3f, 0.24f), 0.06f), Mathf.Max(Mathf.Abs(Circ(p, new Vector2(0, 0.12f), 0.2f)) - 0.06f, -p.y + 0.1f));
                    break;
            }
            o.Add(gc, Cov(d, px));
            return o.c;
        });

        static readonly Vector2[] FlagPts = { new Vector2(-0.3f, 0.5f), new Vector2(0.42f, 0.3f), new Vector2(-0.3f, 0.08f) };
        static readonly Vector2[] SwapA = { new Vector2(0.16f, 0.12f), new Vector2(0.44f, 0.12f), new Vector2(0.3f, -0.08f) };
        static readonly Vector2[] SwapB = { new Vector2(-0.16f, -0.12f), new Vector2(-0.44f, -0.12f), new Vector2(-0.3f, 0.08f) };
    }
}
