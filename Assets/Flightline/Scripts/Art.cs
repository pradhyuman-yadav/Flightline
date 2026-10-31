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

    // UI, sky and effect sprites are generated at runtime from signed distance fields. Airframes, pickups and hazards are
    // imported pixel art (ArtSheets.cs).
    public static partial class Art
    {
        public delegate Color PixFn(Vector2 p, float px);
        static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        static Color Hx(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }
        static Color G(float v, float a = 1f) => new Color(v, v, v, a);
        static readonly Color Wt = Color.white;
        // Palette hoisted out of per-pixel lambdas (was parsed with TryParseHtmlString per pixel: millions of calls at startup).
        static Color Rgb(int v) => new Color(((v >> 16) & 255) / 255f, ((v >> 8) & 255) / 255f, (v & 255) / 255f, 1f);
        static readonly Color CSig = Rgb(0xffcc00), CSigDk = Rgb(0xe0b300), CDeep = Rgb(0x8a6e00), CBody = Rgb(0xf5f8fa), CWing = Rgb(0xd9e1e8), CDark = Rgb(0x262d34);

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
        // Arcade frames: pixel art drawn texel by texel, point-filtered and 9-sliced. The white body takes the tint; the
        // near-black rim and the darker bottom lip survive any tint, so every panel and button reads as a chunky block.
        // UI.RMd / RSm / RPanel / PillM keep one texel the same size on screen everywhere.
        public static Sprite RoundRect => Frame("rr", 12, 4, 2, true, true, true);
        public static Sprite RoundTop => Frame("rrtop", 12, 4, 2, true, false, false);
        public static Sprite RoundOutline => Frame("rro", 12, 4, 2, false, true, false);
        public static Sprite Pill => Frame("pill", 12, 5, 3, true, true, true);

        static readonly Color Rim = new Color(0.08f, 0.09f, 0.11f, 1f), Lip = new Color(0.7f, 0.7f, 0.72f, 1f);

        // n x n texels, sliced at `border`; corners stepped to radius r. body: filled (else rim only); bottom: rounded and
        // rimmed at the bottom too (a header's bottom runs on into the panel below it); lip: two shaded rows along the bottom.
        static Sprite Frame(string key, int n, int border, int r, bool body, bool bottom, bool lip)
        {
            if (Cache.TryGetValue(key, out var s) && s != null) return s;
            bool In(int x, int y)
            {
                if (x < 0 || y < 0 || x >= n || y >= n) return !bottom && y < 0 && x >= 0 && x < n;
                int dx = Mathf.Min(x, n - 1 - x), dy = bottom ? Mathf.Min(y, n - 1 - y) : n - 1 - y;
                if (dx >= r || dy >= r) return true;
                float ex = r - dx - 0.5f, ey = r - dy - 0.5f; return ex * ex + ey * ey <= r * r;
            }
            var pix = new Color[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    if (!In(x, y)) continue;
                    bool rim = !In(x - 1, y) || !In(x + 1, y) || !In(x, y - 1) || !In(x, y + 1);
                    pix[y * n + x] = rim ? Rim : !body ? Color.clear : lip && y <= 2 ? Lip : Wt;
                }
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Point, name = key };
            tex.SetPixels(pix); tex.Apply(false, true);
            s = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect, new Vector4(border, border, border, border));
            s.name = key; Cache[key] = s;
            return s;
        }
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

        // Soft cross-section for contrail ribbons: dense core, feathered edges (v runs across the ribbon).
        static Texture2D vapourTex;
        public static Texture2D Vapour
        {
            get
            {
                if (vapourTex != null) return vapourTex;
                vapourTex = new Texture2D(4, 32, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "vapour" };
                var px = new Color[4 * 32];
                for (int y = 0; y < 32; y++)
                {
                    float v = (y + 0.5f) / 16f - 1f, a = Mathf.Clamp01(1f - v * v);
                    for (int x = 0; x < 4; x++) px[y * 4 + x] = new Color(1, 1, 1, a * a);
                }
                vapourTex.SetPixels(px); vapourTex.Apply(false, true);
                return vapourTex;
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

        // Screen-edge mist while flying through cloud: clear centre, foggy rim.
        public static Sprite Vignette => Make("vignette", 64, 128, 64, (p, px) =>
        {
            float r = new Vector2(p.x * 2f, p.y).magnitude;
            return G(1, Mathf.SmoothStep(0f, 1f, (r - 0.45f) / 0.9f));
        });

        // ---------- Pictograms: asphalt tile + one glyph (signal for actions, ink-inverse for objects) ----------
        static readonly Vector2[] ShieldPts = { new Vector2(-0.4f, 0.48f), new Vector2(0.4f, 0.48f), new Vector2(0.4f, 0.05f), new Vector2(0f, -0.52f), new Vector2(-0.4f, 0.05f) };
        static readonly Vector2[] PWing = { new Vector2(-0.55f, -0.02f), new Vector2(-0.06f, 0.22f), new Vector2(0.06f, 0.22f), new Vector2(0.55f, -0.02f), new Vector2(0.55f, -0.12f), new Vector2(0.06f, 0.04f), new Vector2(-0.06f, 0.04f), new Vector2(-0.55f, -0.12f) };
        static readonly Vector2[] PTail = { new Vector2(-0.22f, -0.48f), new Vector2(-0.04f, -0.34f), new Vector2(0.04f, -0.34f), new Vector2(0.22f, -0.48f), new Vector2(0.22f, -0.55f), new Vector2(-0.22f, -0.55f) };

        static float Arrow(Vector2 p) => Mathf.Min(Seg(p, new Vector2(-0.42f, 0), new Vector2(0.32f, 0), 0.08f),
            Mathf.Min(Seg(p, new Vector2(0.06f, 0.3f), new Vector2(0.38f, 0), 0.08f), Seg(p, new Vector2(0.06f, -0.3f), new Vector2(0.38f, 0), 0.08f)));

        public static Sprite Picto(string g) => ItemIcon(g) ?? Icon(g, true);
        public static Sprite Glyph(string g) => ItemIcon(g) ?? Icon(g, false); // same glyph, no tile: for round buttons and cards

        // Pickups are marked everywhere in the UI by the first frame of their own sheet, so the icon matches what you grab.
        static Sprite ItemIcon(string g) => g switch
        {
            "coin" => CoinFrames[0], "fuel" => FuelFrames[0], "shield" => ShieldFrames[0], "magnet" => MagnetFrames[0], "boost" => BoostFrames[0],
            _ => null
        };

        // UI icons: the same shapes on a 24-texel grid with hard edges, point-filtered, to sit with the pixel-art sprites.
        static Sprite Icon(string g, bool tile)
        {
            var s = IconArt(g, tile); s.texture.filterMode = FilterMode.Point; return s;
        }

        static Sprite IconArt(string g, bool tile) => Make((tile ? "p_" : "g_") + g, 24, 24, 24, (p, px) =>
        {
            px *= 0.02f; // no anti-aliasing: whole texels only
            Color asph = CDark, sig = CSig, inv = CBody;
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
