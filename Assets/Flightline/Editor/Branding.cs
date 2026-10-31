using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Flightline.EditorTools
{
    // Generates the app icon (adaptive + legacy), splash logo and Play Store art from the game's own shapes and fonts.
    public static class Branding
    {
        public const string Dir = "Assets/Flightline/Branding";
        static readonly Color Asphalt = Hex("#262d34"), Signal = Hex("#ffcc00"), InkInv = Hex("#f5f8fa"),
                              InkInvMuted = Hex("#a9b6c2"), Sky = Hex("#4aa8e0"), Horizon = Hex("#cdeaf9");
        static Color Hex(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }

        [MenuItem("Flightline/Branding/Generate Icons, Splash and Store Art")]
        public static void Generate()
        {
            Directory.CreateDirectory(Dir); Directory.CreateDirectory(Dir + "/Store");
            if (File.Exists(PlaneSource)) IconsFromPlane();
            else if (File.Exists(IconSource)) IconsFromArt();
            else
            {
                Write(Paint(1024, IconBg), Dir + "/icon_adaptive_bg.png");
                Write(Paint(1024, (p, px) => Mark(p, px, 0.5f, true)), Dir + "/icon_adaptive_fg.png");
                Write(Paint(1024, (p, px) => Composite(p, px, true)), Dir + "/icon_legacy.png");
                Write(Paint(512, (p, px) => Composite(p, px, false)), Dir + "/Store/play_icon_512.png");
                // iOS: square, no alpha channel (App Store rejects icons with one); the system rounds the corners
                var ios = Paint(1024, (p, px) => Composite(p, px, false));
                var rgb = new Texture2D(1024, 1024, TextureFormat.RGB24, false); rgb.SetPixels(ios.GetPixels()); rgb.Apply();
                UnityEngine.Object.DestroyImmediate(ios); Write(rgb, Dir + "/icon_ios_1024.png");
            }
            var sky = SplashSky(SplashW / SkyPx, SplashH / SkyPx);
            var plane = Load(PlaneSource) ?? Load(IconSource) ?? Paint(512, (p, px) => Composite(p, px, false));
            sky.filterMode = plane.filterMode = FilterMode.Point;
            Write(RenderUI(SplashW, SplashH, SkyBands[2], root => Splash(root, sky, plane)), SplashPath);
            UnityEngine.Object.DestroyImmediate(sky); UnityEngine.Object.DestroyImmediate(plane);
            var bigMark = Paint(768, (p, px) => Mark(p, px, 0.85f, true));
            Write(RenderUI(1024, 500, Horizon, root => Feature(root, bigMark)), Dir + "/Store/feature_graphic_1024x500.png");
            UnityEngine.Object.DestroyImmediate(bigMark);
            AssetDatabase.Refresh();
            ConfigureImporters();
            Debug.Log("Flightline: branding generated in " + Dir);
        }

        static Texture2D Load(string path)
        {
            if (!File.Exists(path)) return null;
            var t = new Texture2D(2, 2); t.LoadImage(File.ReadAllBytes(path)); return t;
        }

        // ---------------------------------------------------------------- icon: the plane on plain sky
        // Source/plane.png is the icon's plane cut out of its sky (transparent around it). The icons put it on flat sky
        // blue with a hard pixel shadow, scaled up nearest-neighbour so its pixels stay crisp.
        const string PlaneSource = Dir + "/Source/plane.png";
        static readonly Color SkyShade = Hex("#2f86c8");

        // The opaque part of a sprite: its bounding box in pixels.
        static RectInt Bounds(Texture2D t)
        {
            var px = t.GetPixels32(); int n = t.width, x0 = n, x1 = -1, y0 = t.height, y1 = -1;
            for (int i = 0; i < px.Length; i++)
                if (px[i].a > 0) { int x = i % n, y = i / n; x0 = Mathf.Min(x0, x); x1 = Mathf.Max(x1, x); y0 = Mathf.Min(y0, y); y1 = Mathf.Max(y1, y); }
            return x1 < 0 ? new RectInt(0, 0, n, t.height) : new RectInt(x0, y0, x1 - x0 + 1, y1 - y0 + 1);
        }

        static void IconsFromPlane()
        {
            var src = Load(PlaneSource); var px = src.GetPixels(); int n = src.width, h = src.height; var box = Bounds(src);
            Color At(int x, int y) => x < 0 || y < 0 || x >= n || y >= h ? Color.clear : px[y * n + x];

            // size x size with the plane `scale` screen pixels per art pixel in the middle; the shadow falls 3 art
            // pixels down and right. No background colour leaves it transparent (the adaptive foreground).
            Texture2D Draw(int size, float scale, Color? bg, Color shadow, TextureFormat fmt)
            {
                var o = new Color[size * size];
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        int ax = Mathf.FloorToInt((x + 0.5f - size * 0.5f) / scale + box.center.x), ay = Mathf.FloorToInt((y + 0.5f - size * 0.5f) / scale + box.center.y);
                        var c = Over(bg ?? Color.clear, new Color(shadow.r, shadow.g, shadow.b, shadow.a * At(ax - 3, ay + 3).a));
                        o[y * size + x] = Over(c, At(ax, ay));
                    }
                var t = new Texture2D(size, size, fmt, false); t.SetPixels(o); t.Apply(); return t;
            }

            float full = 0.74f / Mathf.Max(box.width, box.height); // the plane spans about 3/4 of the icon
            Write(Draw(1024, Mathf.Round(1024 * full), Sky, SkyShade, TextureFormat.RGB24), Dir + "/icon_ios_1024.png"); // no alpha: App Store rule
            Write(Draw(1024, Mathf.Round(1024 * full), Sky, SkyShade, TextureFormat.RGBA32), Dir + "/icon_legacy.png");
            Write(Draw(512, Mathf.Round(1024 * full) / 2f, Sky, SkyShade, TextureFormat.RGBA32), Dir + "/Store/play_icon_512.png");
            // Android adaptive: flat sky behind; the plane on its own layer, small enough that the launcher's circle or
            // squircle crop (the middle 2/3 of the layer) never clips a wingtip
            var bg = new Texture2D(1024, 1024, TextureFormat.RGBA32, false); var fill = new Color[1024 * 1024];
            for (int i = 0; i < fill.Length; i++) fill[i] = Sky;
            bg.SetPixels(fill); bg.Apply(); Write(bg, Dir + "/icon_adaptive_bg.png");
            Write(Draw(1024, 3f, null, new Color(0, 0, 0, 0.2f), TextureFormat.RGBA32), Dir + "/icon_adaptive_fg.png");
            UnityEngine.Object.DestroyImmediate(src);
        }

        // ---------------------------------------------------------------- icon from pixel art (Pixel Lab)
        // A square pixel-art icon in Source/icon.png replaces the drawn one. It is scaled up in whole steps (nearest
        // neighbour, so the pixels stay crisp) and flattened onto opaque colour: stores reject icons with transparency.
        const string IconSource = Dir + "/Source/icon.png";

        static void IconsFromArt()
        {
            var src = new Texture2D(2, 2); src.LoadImage(File.ReadAllBytes(IconSource));
            var px = src.GetPixels(); int n = src.width;
            Color At(int x, int y) { var c = px[Mathf.Clamp(y, 0, n - 1) * n + Mathf.Clamp(x, 0, n - 1)]; c.a = 1f; return c; }

            // size x size, the art drawn `art` pixels across in the middle; outside it the edge pixels carry on
            Texture2D Scaled(int size, int art, TextureFormat fmt)
            {
                var t = new Texture2D(size, size, fmt, false); var o = new Color[size * size]; int off = (size - art) / 2;
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                        o[y * size + x] = At(Mathf.FloorToInt((x - off) * (float)n / art), Mathf.FloorToInt((y - off) * (float)n / art));
                t.SetPixels(o); t.Apply(); return t;
            }

            Write(Scaled(1024, 1024, TextureFormat.RGB24), Dir + "/icon_ios_1024.png");
            Write(Scaled(1024, 1024, TextureFormat.RGBA32), Dir + "/icon_legacy.png");
            Write(Scaled(512, 512, TextureFormat.RGBA32), Dir + "/Store/play_icon_512.png");
            // Android adaptive: launchers show only the middle 2/3 of the layer, then mask it to a circle or squircle,
            // so the whole picture goes in the background at 3/4 of the layer (the plane stays clear of the crop) and
            // the foreground is left empty.
            Write(Scaled(1024, 768, TextureFormat.RGBA32), Dir + "/icon_adaptive_bg.png");
            var fg = new Texture2D(1024, 1024, TextureFormat.RGBA32, false); fg.SetPixels(new Color[1024 * 1024]); fg.Apply();
            Write(fg, Dir + "/icon_adaptive_fg.png");
            UnityEngine.Object.DestroyImmediate(src);
        }

        // ---------------------------------------------------------------- CPU painting (same SDF helpers as the game art)
        delegate Color PixFn(Vector2 p, float px);

        static Texture2D Paint(int size, PixFn fn)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pix = new Color[size * size]; float h = size * 0.5f, px = 2f / size;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    pix[y * size + x] = fn(new Vector2((x + 0.5f - h) / h, (y + 0.5f - h) / h), px);
            tex.SetPixels(pix); tex.Apply(false, false);
            return tex;
        }

        static void Write(Texture2D t, string path) { File.WriteAllBytes(path, t.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(t); }

        static readonly Vector2[] Wing = { new Vector2(-0.95f, -0.08f), new Vector2(-0.1f, 0.32f), new Vector2(0.1f, 0.32f), new Vector2(0.95f, -0.08f), new Vector2(0.95f, -0.2f), new Vector2(0.1f, 0.02f), new Vector2(-0.1f, 0.02f), new Vector2(-0.95f, -0.2f) };
        static readonly Vector2[] Tail = { new Vector2(-0.36f, -0.8f), new Vector2(-0.05f, -0.56f), new Vector2(0.05f, -0.56f), new Vector2(0.36f, -0.8f), new Vector2(0.36f, -0.9f), new Vector2(-0.36f, -0.9f) };

        static Vector2 Rot(Vector2 p, float deg) { float r = deg * Mathf.Deg2Rad, c = Mathf.Cos(r), s = Mathf.Sin(r); return new Vector2(c * p.x - s * p.y, s * p.x + c * p.y); }

        static Color IconBg(Vector2 p, float px)
        {
            var c = Color.Lerp(Hex("#1b2127"), Hex("#34414c"), Mathf.SmoothStep(0f, 1f, (p.y + 1f) * 0.5f));
            var o = new Px(c); o.Add(c, 1f);
            o.Add(new Color(Signal.r, Signal.g, Signal.b, 0.09f), Mathf.Clamp01(1f - (p - new Vector2(0.05f, 0.05f)).magnitude / 0.75f));
            // faint runway dashes along the flight path
            var q = Rot(p, 35f);
            if (Mathf.Abs(q.x) < 0.018f && Mathf.Repeat(q.y + 2f, 0.28f) < 0.14f && q.y < -0.35f) o.Add(new Color(1, 1, 1, 0.18f), 1f);
            return o.c;
        }

        // Plane mark: signal-yellow airliner climbing to the upper right, with two contrails.
        static Color Mark(Vector2 p, float px, float scale, bool trails)
        {
            var q = Rot(p, 35f) / scale; float lpx = px / scale;
            var o = new Px(Signal);
            if (trails)
                foreach (float sx in new[] { -0.42f, 0.42f })
                {
                    float t = Mathf.Clamp01((0.05f - q.y) / 1.5f);
                    float d = Art.Seg(q, new Vector2(sx, 0.02f), new Vector2(sx, -1.55f), 0.07f + 0.05f * t);
                    o.Add(new Color(1, 1, 1, 0.55f * (1f - t)), Art.Cov(d, lpx * 3f));
                }
            float fus = Art.Seg(q, new Vector2(0, -0.72f), new Vector2(0, 0.74f), 0.13f);
            float body = Mathf.Min(fus, Mathf.Min(Art.Poly(q, Wing), Art.Poly(q, Tail)));
            o.Add(Signal, Art.Cov(body, lpx));
            o.Add(Hex("#e0b300"), Art.Cov(Mathf.Max(fus, -(q.x - 0.01f)), lpx) * 0.6f);
            o.Add(Asphalt, Art.Cov(Art.Seg(q, new Vector2(-0.04f, 0.6f), new Vector2(0.04f, 0.6f), 0.055f), lpx));
            return o.c;
        }

        static Color Over(Color under, Color top)
        {
            float a = top.a + under.a * (1 - top.a); if (a <= 0f) return new Color(0, 0, 0, 0);
            var c = (top * top.a + under * under.a * (1 - top.a)) / a; c.a = a; return c;
        }

        static Color Composite(Vector2 p, float px, bool rounded)
        {
            var c = Over(IconBg(p, px), Mark(p, px, 0.6f, true));
            if (rounded) c.a *= Art.Cov(Art.Box(p, Vector2.zero, new Vector2(0.98f, 0.98f), 0.42f), px);
            return c;
        }

        // ---------------------------------------------------------------- UI rendering (for anything with type)
        // transparent: render on black and on white, then solve for exact alpha (no halo against the splash colour)
        static Texture2D RenderUI(int w, int h, Color clear, Action<RectTransform> build, bool transparent = false)
        {
            const int layer = 31;
            var camGo = new GameObject("BrandCam") { hideFlags = HideFlags.HideAndDontSave };
            var cam = camGo.AddComponent<Camera>(); cam.enabled = false; cam.orthographic = true; cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = clear; cam.cullingMask = 1 << layer; cam.transform.position = new Vector3(0, 0, -10); cam.allowHDR = false;
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32, transparent ? RenderTextureReadWrite.Linear : RenderTextureReadWrite.Default) { antiAliasing = 4 };
            cam.targetTexture = rt;
            var cgo = new GameObject("BrandCanvas", typeof(RectTransform)) { hideFlags = HideFlags.HideAndDontSave }; cgo.layer = layer;
            var canvas = cgo.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = cam; canvas.planeDistance = 5;
            var sc = cgo.AddComponent<CanvasScaler>(); sc.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize; sc.scaleFactor = 1;
            build((RectTransform)cgo.transform);
            foreach (var tr in cgo.GetComponentsInChildren<Transform>(true)) { tr.gameObject.layer = layer; tr.gameObject.hideFlags = HideFlags.HideAndDontSave; }
            Canvas.ForceUpdateCanvases();
            foreach (var t in cgo.GetComponentsInChildren<TextMeshProUGUI>(true)) t.ForceMeshUpdate(true, true);
            Canvas.ForceUpdateCanvases();
            var prev = RenderTexture.active;
            Texture2D Grab(Color bg)
            {
                cam.backgroundColor = bg; cam.Render(); cam.Render();
                RenderTexture.active = rt;
                var g = new Texture2D(w, h, TextureFormat.RGBA32, false, transparent);
                g.ReadPixels(new Rect(0, 0, w, h), 0, 0); g.Apply(); return g;
            }
            Texture2D tex;
            if (!transparent) tex = Grab(clear);
            else
            {
                var onBlack = Grab(Color.black); var onWhite = Grab(Color.white);
                var a = onBlack.GetPixels(); var b = onWhite.GetPixels(); var o = new Color[a.Length];
                for (int i = 0; i < a.Length; i++)
                {
                    float alpha = Mathf.Clamp01(1f - ((b[i].r - a[i].r) + (b[i].g - a[i].g) + (b[i].b - a[i].b)) / 3f);
                    var c = alpha > 0.001f ? new Color(a[i].r / alpha, a[i].g / alpha, a[i].b / alpha) : Color.black;
                    c = new Color(Mathf.Clamp01(c.r), Mathf.Clamp01(c.g), Mathf.Clamp01(c.b)).gamma; c.a = alpha; o[i] = c;
                }
                tex = new Texture2D(w, h, TextureFormat.RGBA32, false); tex.SetPixels(o); tex.Apply();
                UnityEngine.Object.DestroyImmediate(onBlack); UnityEngine.Object.DestroyImmediate(onWhite);
            }
            RenderTexture.active = prev; cam.targetTexture = null;
            UnityEngine.Object.DestroyImmediate(cgo); UnityEngine.Object.DestroyImmediate(camGo); rt.Release(); UnityEngine.Object.DestroyImmediate(rt);
            return tex;
        }

        static RectTransform Node(Transform parent, string name, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform)); var r = (RectTransform)go.transform; r.SetParent(parent, false);
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f); r.pivot = new Vector2(0.5f, 0.5f); r.anchoredPosition = pos; r.sizeDelta = size; return r;
        }

        static TextMeshProUGUI Txt(Transform parent, string s, string font, float size, Color c, Vector2 pos, Vector2 box, float spacing = 0, TextAlignmentOptions al = TextAlignmentOptions.Center)
        {
            var t = Node(parent, "T", pos, box).gameObject.AddComponent<TextMeshProUGUI>();
            t.font = Fonts.Get(font); t.fontSize = size; t.color = c; t.alignment = al; t.characterSpacing = spacing; t.textWrappingMode = TextWrappingModes.NoWrap; t.text = s;
            return t;
        }

        // ---------------------------------------------------------------- loading screen
        // One full-screen picture, in the arcade style of the game: a banded pixel sky with puffy clouds, the icon in a
        // chunky frame and the name in the arcade font. The iOS launch screen, Unity's splash and the game's own boot
        // screen all show this same image, so starting the game is one steady screen that fades into the title.
        public const string SplashPath = "Assets/Flightline/Resources/Splash.png";
        const int SplashW = 1080, SplashH = 2340, SkyPx = 6; // tall phone shape; the sky is painted at 1/6 size and scaled up crisp
        static readonly Color Navy = Hex("#1b2348");
        static readonly Color[] SkyBands = { Hex("#2a6fcf"), Hex("#3487d9"), Hex("#459fe2"), Hex("#5cb4ea"), Hex("#7cc6f0"), Hex("#a3d8f5") };

        static Texture2D SplashSky(int w, int h)
        {
            var rng = new System.Random(7);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            var o = new Color[w * h];
            // sky: hard bands, darkest at the top, with a checker row where two bands meet and two faint light rays
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float f = (1f - (y + 0.5f) / h) / 0.75f * SkyBands.Length;
                    int b = (int)f; if (f - b > 0.82f && ((x + y) & 1) == 0) b++;
                    float d = x * 0.55f + y; if (Mathf.Abs(d - 380f) < 6f || Mathf.Abs(d - 405f) < 2.5f) b++;
                    o[y * w + x] = SkyBands[Mathf.Clamp(b, 0, SkyBands.Length - 1)];
                }

            // clouds: unions of circles in three flat tones like the icon's clouds, lit from above: white along the top,
            // lavender further down. A puff has a flat floor; a bank is solid below `fill`. Banks are stacked front to
            // back, so each row's white tops sit over the shaded body of the row behind.
            void Cloud(System.Collections.Generic.List<Vector3> cs, float floor, float fill, Color dark, Color mid, Color lit)
            {
                bool In(Vector2 p)
                {
                    if (p.y < floor) return false;
                    if (p.y < fill) return true;
                    foreach (var c in cs) if ((p - (Vector2)c).sqrMagnitude < c.z * c.z) return true;
                    return false;
                }
                var up = new Vector2(-0.35f, 1f); // towards the light
                for (int y = Mathf.Max(0, (int)floor); y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        var p = new Vector2(x + 0.5f, y + 0.5f);
                        if (!In(p)) continue;
                        o[y * w + x] = !In(p + up * 8f) ? lit : !In(p + up * 15f) ? mid : dark;
                    }
            }
            System.Collections.Generic.List<Vector3> Puff(float cx, float cy, float s) => new System.Collections.Generic.List<Vector3>
            {
                new Vector3(cx, cy, 6 * s), new Vector3(cx - 7 * s, cy - 2 * s, 4.5f * s), new Vector3(cx + 7 * s, cy - 2 * s, 5 * s),
                new Vector3(cx + 3 * s, cy + 3 * s, 4.5f * s), new Vector3(cx - 12 * s, cy - 4 * s, 3 * s), new Vector3(cx + 12 * s, cy - 4 * s, 3 * s)
            };
            var back = new System.Collections.Generic.List<Vector3>();
            for (float x = -10; x < w + 20; x += R(12, 18)) back.Add(new Vector3(x, R(92, 112), R(12, 20)));
            Cloud(back, -999, 100, Hex("#9cc9ef"), Hex("#bfdcf6"), Hex("#dcecfb"));
            foreach (float top in new[] { 74f, 47f, 20f })
            {
                var row = new System.Collections.Generic.List<Vector3>();
                for (float x = R(-20, -8); x < w + 20; x += R(14, 22)) { float r = R(11, 23); row.Add(new Vector3(x, top - r + R(-4, 4), r)); }
                Cloud(row, -999, top - 16, Hex("#b7b5ec"), Hex("#dddbf8"), Color.white);
            }
            // a few small clouds drifting past the edges, clear of the logo
            Cloud(Puff(12, 262, 1.3f), 262 - 5.2f, -999, Hex("#a9a7e6"), Hex("#d3d1f6"), Color.white);
            Cloud(Puff(166, 318, 1.1f), 318 - 4.4f, -999, Hex("#a9a7e6"), Hex("#d3d1f6"), Color.white);
            Cloud(Puff(52, 352, 0.75f), 352 - 3f, -999, Hex("#a9a7e6"), Hex("#d3d1f6"), Color.white);

            // sparkles in the upper sky
            for (int i = 0; i < 9; i++)
            {
                int sx = (int)R(4, w - 4), sy = (int)R(h * 0.66f, h - 4);
                if (sx > 36 && sx < 144 && sy < 300) continue; // not behind the icon
                o[sy * w + sx] = Color.white;
                foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) }) o[(sy + dy) * w + sx + dx] = Hex("#dff1fd");
            }
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false); t.SetPixels(o); t.Apply(); return t;
        }

        static void Splash(RectTransform root, Texture2D sky, Texture2D plane)
        {
            var bg = Node(root, "Sky", Vector2.zero, new Vector2(SplashW, SplashH)).gameObject.AddComponent<RawImage>(); bg.texture = sky;
            // the plane, 4 screen pixels per art pixel, with a hard shadow: the same plane as the app icon
            var box = Bounds(plane); const float scale = 4;
            var uv = new Rect((float)box.x / plane.width, (float)box.y / plane.height, (float)box.width / plane.width, (float)box.height / plane.height);
            var size = new Vector2(box.width, box.height) * scale; var at = new Vector2(0, 250);
            var shadowPx = plane.GetPixels32(); for (int i = 0; i < shadowPx.Length; i++) shadowPx[i] = new Color32(255, 255, 255, shadowPx[i].a);
            var silhouette = new Texture2D(plane.width, plane.height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, hideFlags = HideFlags.HideAndDontSave };
            silhouette.SetPixels32(shadowPx); silhouette.Apply();
            var sh = Node(root, "Shadow", at + new Vector2(4, -4) * scale, size).gameObject.AddComponent<RawImage>();
            sh.texture = silhouette; sh.uvRect = uv; sh.color = new Color(Navy.r, Navy.g, Navy.b, 0.25f);
            var pl = Node(root, "Plane", at, size).gameObject.AddComponent<RawImage>(); pl.texture = plane; pl.uvRect = uv;
            // name: navy outline one font pixel wide plus a drop below it, then the face in a yellow-to-orange ramp
            Arcade(root, "FLIGHTLINE", 96, new Vector2(0, -160), Hex("#ffe14d"), Hex("#ff9a1f"));
            Arcade(root, "LOADING...", 40, new Vector2(0, -330), Color.white, Hex("#dff1fd"));
        }

        static void Arcade(RectTransform root, string s, float size, Vector2 pos, Color top, Color bottom)
        {
            float px = size / 8f; // Press Start 2P draws on an 8x8 grid
            var box = new Vector2(SplashW, size * 1.5f);
            // half steps too: the soft edges where neighbouring copies meet would otherwise show as faint seams
            for (float oy = 1f; oy >= -2f; oy -= 0.5f)
                for (float ox = -1f; ox <= 1f; ox += 0.5f)
                    if (ox != 0 || oy != 0) Txt(root, s, "PressStart2P-Regular", size, Navy, pos + new Vector2(ox, oy) * px, box);
            var face = Txt(root, s, "PressStart2P-Regular", size, Color.white, pos, box);
            face.enableVertexGradient = true; face.colorGradient = new VertexGradient(top, top, bottom, bottom);
        }

        static void Feature(RectTransform root, Texture2D mark)
        {
            var bg = Node(root, "Bg", Vector2.zero, new Vector2(1024, 500)).gameObject.AddComponent<Image>(); bg.color = Horizon;
            var grad = Node(root, "Grad", Vector2.zero, new Vector2(1024, 500)).gameObject.AddComponent<Image>(); grad.sprite = Art.Gradient; grad.color = Sky;
            foreach (var (x, y, s) in new[] { (-430f, -150f, 1.3f), (330f, 170f, 0.9f), (120f, -190f, 1.5f), (460f, -40f, 1.0f) })
            {
                var c = Node(root, "Cloud", new Vector2(x, y), new Vector2(260, 260) * s).gameObject.AddComponent<Image>(); c.sprite = Art.CloudFrames[0]; c.color = new Color(1, 1, 1, 0.9f);
            }
            var m = Node(root, "Mark", new Vector2(270, 10), new Vector2(470, 470)).gameObject.AddComponent<RawImage>(); m.texture = mark;
            var sign = Node(root, "Sign", new Vector2(-215, 20), new Vector2(500, 230)).gameObject.AddComponent<Image>();
            sign.sprite = Art.RoundRect; sign.type = Image.Type.Sliced; sign.pixelsPerUnitMultiplier = 0.6f; sign.color = Asphalt;
            Txt(sign.transform, "FLIGHTLINE", "BarlowCondensed-ExtraBold", 118, Signal, new Vector2(0, 28), new Vector2(500, 130), -1);
            Txt(sign.transform, "Endless flight. Dodge the weather.", "B612-Regular", 22, InkInvMuted, new Vector2(0, -62), new Vector2(480, 40));
        }

        // ---------------------------------------------------------------- import + apply
        static void ConfigureImporters()
        {
            foreach (var f in new[] { "icon_adaptive_bg.png", "icon_adaptive_fg.png", "icon_legacy.png", "icon_ios_1024.png" })
            {
                var ti = (TextureImporter)AssetImporter.GetAtPath(Dir + "/" + f); if (ti == null) continue;
                ti.textureType = TextureImporterType.Default; ti.alphaIsTransparency = true; ti.mipmapEnabled = false; ti.npotScale = TextureImporterNPOTScale.None;
                ti.textureCompression = TextureImporterCompression.Uncompressed;
                if (f == "icon_ios_1024.png") { ti.alphaSource = TextureImporterAlphaSource.None; ti.alphaIsTransparency = false; }
                ti.SaveAndReimport();
            }
            var si = (TextureImporter)AssetImporter.GetAtPath(SplashPath);
            if (si != null)
            {
                si.textureType = TextureImporterType.Sprite; si.spriteImportMode = SpriteImportMode.Single; si.mipmapEnabled = false;
                si.maxTextureSize = 4096; si.textureCompression = TextureImporterCompression.CompressedHQ; si.SaveAndReimport();
            }
        }

        public static void ApplyIos()
        {
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(Dir + "/icon_ios_1024.png");
            if (icon == null) { Generate(); icon = AssetDatabase.LoadAssetAtPath<Texture2D>(Dir + "/icon_ios_1024.png"); }
            var t = UnityEditor.Build.NamedBuildTarget.iOS;
            foreach (var kind in PlayerSettings.GetSupportedIconKinds(t))
            {
                var icons = PlayerSettings.GetPlatformIcons(t, kind);
                foreach (var ic in icons) ic.SetTexture(icon);
                PlayerSettings.SetPlatformIcons(t, kind, icons);
            }
            // launch screen: the loading picture full screen, so it hands over to the Unity splash (the same picture) unchanged
            var splash = AssetDatabase.LoadAssetAtPath<Texture2D>(SplashPath);
            var so = new SerializedObject(Unsupported.GetSerializedAssetInterfaceSingleton("PlayerSettings"));
            so.FindProperty("iOSLaunchScreenType").intValue = 1; // ImageAndBackgroundRelative
            so.FindProperty("iOSLaunchScreenPortrait").objectReferenceValue = splash;
            so.FindProperty("iOSLaunchScreenBackgroundColor").colorValue = SkyBands[2];
            so.FindProperty("iOSLaunchScreenFillPct").floatValue = 100f;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void ApplyToPlayerSettings()
        {
            var legacy = AssetDatabase.LoadAssetAtPath<Texture2D>(Dir + "/icon_legacy.png");
            if (legacy == null) { Generate(); legacy = AssetDatabase.LoadAssetAtPath<Texture2D>(Dir + "/icon_legacy.png"); }
            var bg = AssetDatabase.LoadAssetAtPath<Texture2D>(Dir + "/icon_adaptive_bg.png");
            var fg = AssetDatabase.LoadAssetAtPath<Texture2D>(Dir + "/icon_adaptive_fg.png");
            var splash = AssetDatabase.LoadAssetAtPath<Sprite>(SplashPath);

            PlayerSettings.SetIcons(UnityEditor.Build.NamedBuildTarget.Unknown, new[] { legacy }, IconKind.Any);
            var t = UnityEditor.Build.NamedBuildTarget.Android;
            foreach (var kind in PlayerSettings.GetSupportedIconKinds(t))
            {
                var icons = PlayerSettings.GetPlatformIcons(t, kind);
                foreach (var ic in icons) { if (ic.maxLayerCount >= 2) ic.SetTextures(bg, fg); else ic.SetTexture(legacy); }
                PlayerSettings.SetPlatformIcons(t, kind, icons);
            }

            PlayerSettings.SplashScreen.show = true;
            PlayerSettings.SplashScreen.showUnityLogo = false;
            // the loading picture is the whole splash: shown still (no zoom) for a moment, with no logo drawn over it
            PlayerSettings.SplashScreen.backgroundColor = SkyBands[2];
            PlayerSettings.SplashScreen.background = splash;
            PlayerSettings.SplashScreen.backgroundPortrait = splash;
            PlayerSettings.SplashScreen.blurBackgroundImage = false; // on by default, and it smears the pixel art
            PlayerSettings.SplashScreen.overlayOpacity = 0f;
            PlayerSettings.SplashScreen.animationMode = PlayerSettings.SplashScreen.AnimationMode.Static;
            PlayerSettings.SplashScreen.drawMode = PlayerSettings.SplashScreen.DrawMode.AllSequential;
            PlayerSettings.SplashScreen.logos = new[] { PlayerSettings.SplashScreenLogo.Create(1.5f, null) };
            Debug.Log($"Flightline: icons applied, splash {(splash != null ? "picture set" : "picture missing")}, Unity logo shown: {PlayerSettings.SplashScreen.showUnityLogo}");
        }
    }
}
