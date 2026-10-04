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
        static readonly Color Asphalt = Hex("#262d34"), Fids = Hex("#121417"), Signal = Hex("#ffcc00"), InkInv = Hex("#f5f8fa"),
                              InkInvMuted = Hex("#a9b6c2"), Sky = Hex("#4aa8e0"), Horizon = Hex("#cdeaf9");
        static Color Hex(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }

        [MenuItem("Flightline/Branding/Generate Icons, Splash and Store Art")]
        public static void Generate()
        {
            Directory.CreateDirectory(Dir); Directory.CreateDirectory(Dir + "/Store");
            Write(Paint(1024, IconBg), Dir + "/icon_adaptive_bg.png");
            Write(Paint(1024, (p, px) => Mark(p, px, 0.5f, true)), Dir + "/icon_adaptive_fg.png");
            Write(Paint(1024, (p, px) => Composite(p, px, true)), Dir + "/icon_legacy.png");
            Write(Paint(512, (p, px) => Composite(p, px, false)), Dir + "/Store/play_icon_512.png");
            // iOS: square, no alpha channel (App Store rejects icons with one); the system rounds the corners
            var ios = Paint(1024, (p, px) => Composite(p, px, false));
            var rgb = new Texture2D(1024, 1024, TextureFormat.RGB24, false); rgb.SetPixels(ios.GetPixels()); rgb.Apply();
            UnityEngine.Object.DestroyImmediate(ios); Write(rgb, Dir + "/icon_ios_1024.png");
            var mark = Paint(512, (p, px) => Mark(p, px, 0.8f, true));
            Write(RenderUI(1200, 760, Fids, root => SplashLogo(root, mark), true), Dir + "/splash_logo.png");
            var bigMark = Paint(768, (p, px) => Mark(p, px, 0.85f, true));
            Write(RenderUI(1024, 500, Horizon, root => Feature(root, bigMark, false)), Dir + "/Store/feature_graphic_1024x500.png");
            // Promo copies with the NO ADS sticker: for your site / socials only. Google Play rejects "No Ads" on icons and graphics.
            Directory.CreateDirectory(Dir + "/Promo");
            Write(RenderUI(1024, 500, Horizon, root => Feature(root, bigMark, true)), Dir + "/Promo/banner_noads_1024x500.png");
            var icon = Paint(512, (p, px) => Composite(p, px, true));
            Write(RenderUI(512, 512, Color.clear, root => PromoIcon(root, icon), true), Dir + "/Promo/icon_noads_512.png");
            UnityEngine.Object.DestroyImmediate(icon);
            UnityEngine.Object.DestroyImmediate(mark); UnityEngine.Object.DestroyImmediate(bigMark);
            AssetDatabase.Refresh();
            ConfigureImporters();
            Debug.Log("Flightline: branding generated in " + Dir);
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

        static void SplashLogo(RectTransform root, Texture2D mark)
        {
            var m = Node(root, "Mark", new Vector2(0, 170), new Vector2(340, 340)).gameObject.AddComponent<RawImage>(); m.texture = mark;
            Txt(root, "FLIGHTLINE", "BarlowCondensed-ExtraBold", 190, Signal, new Vector2(0, -95), new Vector2(1200, 200), -1);
            Txt(root, "BY PRADHYUMAN", "B612-Bold", 36, InkInvMuted, new Vector2(0, -228), new Vector2(1200, 60), 18);
            UI.Sticker(root, 1.55f, -8f).anchoredPosition = new Vector2(250, 95);
        }

        static void PromoIcon(RectTransform root, Texture2D icon)
        {
            Node(root, "Icon", Vector2.zero, new Vector2(512, 512)).gameObject.AddComponent<RawImage>().texture = icon;
            UI.Sticker(root, 1.75f, -10f).anchoredPosition = new Vector2(92, -168);
        }

        static void Feature(RectTransform root, Texture2D mark, bool sticker)
        {
            var bg = Node(root, "Bg", Vector2.zero, new Vector2(1024, 500)).gameObject.AddComponent<Image>(); bg.color = Horizon;
            var grad = Node(root, "Grad", Vector2.zero, new Vector2(1024, 500)).gameObject.AddComponent<Image>(); grad.sprite = Art.Gradient; grad.color = Sky;
            foreach (var (x, y, s) in new[] { (-430f, -150f, 1.3f), (330f, 170f, 0.9f), (120f, -190f, 1.5f), (460f, -40f, 1.0f) })
            {
                var c = Node(root, "Cloud", new Vector2(x, y), new Vector2(300, 188) * s).gameObject.AddComponent<Image>(); c.sprite = Art.Cloud; c.color = new Color(1, 1, 1, 0.9f);
            }
            var m = Node(root, "Mark", new Vector2(270, 10), new Vector2(470, 470)).gameObject.AddComponent<RawImage>(); m.texture = mark;
            var sign = Node(root, "Sign", new Vector2(-215, 20), new Vector2(500, 230)).gameObject.AddComponent<Image>();
            sign.sprite = Art.RoundRect; sign.type = Image.Type.Sliced; sign.pixelsPerUnitMultiplier = 0.6f; sign.color = Asphalt;
            Txt(sign.transform, "FLIGHTLINE", "BarlowCondensed-ExtraBold", 118, Signal, new Vector2(0, 28), new Vector2(500, 130), -1);
            Txt(sign.transform, "Endless flight. Dodge the weather.", "B612-Regular", 22, InkInvMuted, new Vector2(0, -62), new Vector2(480, 40));
            if (sticker) UI.Sticker(root, 1.35f, -8f).anchoredPosition = new Vector2(-45, 128);
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
            var si = (TextureImporter)AssetImporter.GetAtPath(Dir + "/splash_logo.png");
            if (si != null) { si.textureType = TextureImporterType.Sprite; si.spriteImportMode = SpriteImportMode.Single; si.mipmapEnabled = false; si.alphaIsTransparency = true; si.textureCompression = TextureImporterCompression.Uncompressed; si.SaveAndReimport(); }
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
            // launch screen: board colour with the logo, so it hands over to the Unity splash without a flash
            var logo = AssetDatabase.LoadAssetAtPath<Texture2D>(Dir + "/splash_logo.png");
            var so = new SerializedObject(Unsupported.GetSerializedAssetInterfaceSingleton("PlayerSettings"));
            so.FindProperty("iOSLaunchScreenType").intValue = 1; // ImageAndBackgroundRelative
            so.FindProperty("iOSLaunchScreenPortrait").objectReferenceValue = logo;
            so.FindProperty("iOSLaunchScreenBackgroundColor").colorValue = Fids;
            so.FindProperty("iOSLaunchScreenFillPct").floatValue = 70f;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void ApplyToPlayerSettings()
        {
            var legacy = AssetDatabase.LoadAssetAtPath<Texture2D>(Dir + "/icon_legacy.png");
            if (legacy == null) { Generate(); legacy = AssetDatabase.LoadAssetAtPath<Texture2D>(Dir + "/icon_legacy.png"); }
            var bg = AssetDatabase.LoadAssetAtPath<Texture2D>(Dir + "/icon_adaptive_bg.png");
            var fg = AssetDatabase.LoadAssetAtPath<Texture2D>(Dir + "/icon_adaptive_fg.png");
            var logo = AssetDatabase.LoadAssetAtPath<Sprite>(Dir + "/splash_logo.png");

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
            PlayerSettings.SplashScreen.backgroundColor = Fids;
            PlayerSettings.SplashScreen.background = null;
            PlayerSettings.SplashScreen.overlayOpacity = 0f;
            PlayerSettings.SplashScreen.animationMode = PlayerSettings.SplashScreen.AnimationMode.Dolly;
            PlayerSettings.SplashScreen.drawMode = PlayerSettings.SplashScreen.DrawMode.AllSequential;
            if (logo != null) PlayerSettings.SplashScreen.logos = new[] { PlayerSettings.SplashScreenLogo.Create(2.2f, logo) };
            Debug.Log($"Flightline: icons applied, splash {(logo != null ? "logo set" : "logo missing")}, Unity logo shown: {PlayerSettings.SplashScreen.showUnityLogo}");
        }
    }
}
