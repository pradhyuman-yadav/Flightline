using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

namespace Flightline
{
    // Flightline type scale.
    public enum TS { DisplayXL, DisplayL, Heading, Button, Title, Body, Caption, Label, HudScore, Flap, Readout }

    public static class Fonts
    {
        static readonly Dictionary<string, TMP_FontAsset> Map = new Dictionary<string, TMP_FontAsset>();
        public static TMP_FontAsset Get(string file)
        {
            if (Map.TryGetValue(file, out var fa) && fa != null) return fa;
            var font = Resources.Load<Font>("FlightlineFonts/" + file);
            fa = font != null
                ? TMP_FontAsset.CreateFontAsset(font, 72, 8, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true)
                : TMP_Settings.defaultFontAsset;
            if (fa != null) fa.name = file;
            Map[file] = fa;
            return fa;
        }
    }

    public static class UI
    {
        public const float RMd = 2f;   // radius-md 6px with the 12px rounded sprite
        public const float RSm = 6f;   // radius-sm 2px
        public static float PillM(float h) => 62f / h;
        public const float RPanel = 1.2f; // ~10px corners for panels: softer than signage-square

        public static float Back(float x, float s = 1.4f) { x = Mathf.Clamp01(x) - 1f; return 1f + (s + 1f) * x * x * x + s * x * x; }
        public static float OutCubic(float x) { x = 1f - Mathf.Clamp01(x); return 1f - x * x * x; }

        public static RectTransform Rect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.layer = 5;
            var r = (RectTransform)go.transform; r.SetParent(parent, false); return r;
        }

        public static RectTransform Fill(RectTransform r, float l = 0, float t = 0, float rt = 0, float b = 0)
        {
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.pivot = new Vector2(0.5f, 0.5f);
            r.offsetMin = new Vector2(l, b); r.offsetMax = new Vector2(-rt, -t); return r;
        }

        public static RectTransform Anchor(RectTransform r, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            r.anchorMin = r.anchorMax = anchor; r.pivot = pivot; r.anchoredPosition = pos; r.sizeDelta = size; return r;
        }

        public static Image Img(Transform parent, string name, Tok tok, Sprite sprite = null, float ppum = 1f, float alpha = 1f, bool ray = false)
        {
            var r = Rect(parent, name); var img = r.gameObject.AddComponent<Image>();
            img.sprite = sprite; img.raycastTarget = ray;
            if (sprite != null && sprite.border != Vector4.zero) { img.type = Image.Type.Sliced; img.pixelsPerUnitMultiplier = ppum; }
            r.gameObject.AddComponent<Themed>().Set(tok, alpha);
            return img;
        }

        public static Image Pic(Transform parent, Sprite s, float size)
        {
            var r = Rect(parent, "Picto"); var img = r.gameObject.AddComponent<Image>();
            img.sprite = s; img.raycastTarget = false; img.preserveAspect = true; r.sizeDelta = new Vector2(size, size);
            var le = r.gameObject.AddComponent<LayoutElement>(); le.preferredWidth = le.minWidth = size; le.preferredHeight = le.minHeight = size;
            return img;
        }

        public static TextMeshProUGUI Text(Transform parent, string s, TS style, Tok tok, TextAlignmentOptions align = TextAlignmentOptions.Left, bool wrap = false)
        {
            var r = Rect(parent, "Text");
            var t = r.gameObject.AddComponent<TextMeshProUGUI>();
            t.raycastTarget = false; t.alignment = align;
            t.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Overflow;
            Style(t, style); t.text = s;
            r.gameObject.AddComponent<Themed>().Set(tok);
            return t;
        }

        public static void Style(TextMeshProUGUI t, TS s)
        {
            switch (s)
            {
                case TS.DisplayXL: Set(t, "BarlowCondensed-ExtraBold", 72, -1, true); t.lineSpacing = -18; break;
                case TS.DisplayL: Set(t, "BarlowCondensed-Bold", 48, 0, true); t.lineSpacing = -14; break;
                case TS.Heading: Set(t, "BarlowCondensed-Bold", 28, 0, false); break;
                case TS.Button: Set(t, "BarlowCondensed-Bold", 20, 4, true); break;
                case TS.Title: Set(t, "B612-Bold", 18, 0, false); break;
                case TS.Body: Set(t, "B612-Regular", 16, 0, false); break;
                case TS.Caption: Set(t, "B612-Regular", 13, 0, false); break;
                case TS.Label: Set(t, "B612-Bold", 12, 8, true); break;
                case TS.HudScore: Set(t, "B612Mono-Bold", 40, 0, false); break;
                case TS.Flap: Set(t, "B612Mono-Bold", 22, 0, true); break;
                case TS.Readout: Set(t, "B612Mono-Regular", 14, 0, false); break;
            }
        }

        static void Set(TextMeshProUGUI t, string font, float size, float spacing, bool upper)
        {
            t.font = Fonts.Get(font); t.fontSize = size; t.characterSpacing = spacing;
            t.fontStyle = upper ? FontStyles.UpperCase : FontStyles.Normal;
        }

        public static VerticalLayoutGroup V(GameObject go, float spacing, RectOffset pad = null, TextAnchor align = TextAnchor.UpperLeft)
        {
            var v = go.AddComponent<VerticalLayoutGroup>(); v.spacing = spacing; v.padding = pad ?? new RectOffset(); v.childAlignment = align;
            v.childControlWidth = true; v.childControlHeight = true; v.childForceExpandWidth = true; v.childForceExpandHeight = false; return v;
        }

        public static HorizontalLayoutGroup H(GameObject go, float spacing, RectOffset pad = null, TextAnchor align = TextAnchor.MiddleLeft)
        {
            var h = go.AddComponent<HorizontalLayoutGroup>(); h.spacing = spacing; h.padding = pad ?? new RectOffset(); h.childAlignment = align;
            h.childControlWidth = true; h.childControlHeight = true; h.childForceExpandWidth = false; h.childForceExpandHeight = false; return h;
        }

        public static LayoutElement LE(Component c, float w = -1, float h = -1, float flexW = -1, float flexH = -1)
        {
            var le = c.GetComponent<LayoutElement>(); if (le == null) le = c.gameObject.AddComponent<LayoutElement>();
            if (w >= 0) le.preferredWidth = w; if (h >= 0) le.preferredHeight = h;
            if (flexW >= 0) le.flexibleWidth = flexW; if (flexH >= 0) le.flexibleHeight = flexH;
            return le;
        }

        public static ContentSizeFitter Fit(Component c, bool w, bool h)
        {
            var f = c.gameObject.AddComponent<ContentSizeFitter>();
            f.horizontalFit = w ? ContentSizeFitter.FitMode.PreferredSize : ContentSizeFitter.FitMode.Unconstrained;
            f.verticalFit = h ? ContentSizeFitter.FitMode.PreferredSize : ContentSizeFitter.FitMode.Unconstrained;
            return f;
        }

        public static RectOffset Pad(int a) => new RectOffset(a, a, a, a);
        public static RectOffset Pad(int h, int v) => new RectOffset(h, h, v, v);
        public static RectTransform Row(Transform parent, float spacing = 8, float h = -1)
        {
            var r = Rect(parent, "Row"); H(r.gameObject, spacing); if (h > 0) LE(r, -1, h); return r;
        }
        public static RectTransform Spacer(Transform parent) { var r = Rect(parent, "Spacer"); LE(r, 0, 0, 1); return r; }

        // "NO ADS" die-cut sticker: fixed colours (a physical sticker looks the same day or night).
        public static readonly Color StickerGreen = new Color32(0x18, 0x79, 0x4a, 0xff);
        public static RectTransform Sticker(Transform parent, float scale = 1f, float angle = -8f, bool sub = true)
        {
            float w = sub ? 150f : 128f, h = sub ? 62f : 46f;
            var root = Rect(parent, "Sticker"); root.sizeDelta = new Vector2(w, h) * scale; root.localRotation = Quaternion.Euler(0, 0, angle);
            Image P(string n, Color c, Vector2 size, Vector2 off)
            {
                var r = Rect(root, n); r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f); r.sizeDelta = size * scale; r.anchoredPosition = off * scale;
                var i = r.gameObject.AddComponent<Image>(); i.sprite = Art.Pill; i.type = Image.Type.Sliced; i.pixelsPerUnitMultiplier = PillM(size.y * scale);
                i.color = c; i.raycastTarget = false; return i;
            }
            void T(string s, string font, float size, Color c, float y, float spacing)
            {
                var r = Rect(root, "T"); r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f); r.sizeDelta = new Vector2(w, size * 1.3f) * scale; r.anchoredPosition = new Vector2(0, y) * scale;
                var t = r.gameObject.AddComponent<TextMeshProUGUI>(); t.font = Fonts.Get(font); t.fontSize = size * scale; t.color = c; t.characterSpacing = spacing;
                t.alignment = TextAlignmentOptions.Center; t.textWrappingMode = TextWrappingModes.NoWrap; t.raycastTarget = false; t.text = s;
            }
            P("Shade", new Color(0, 0, 0, 0.3f), new Vector2(w, h), new Vector2(0, -3));
            P("Edge", Color.white, new Vector2(w, h), Vector2.zero);
            P("Face", StickerGreen, new Vector2(w - 9, h - 9), Vector2.zero);
            T("NO ADS", "BarlowCondensed-ExtraBold", 31, Color.white, sub ? 6 : 1, 2);
            if (sub) T("PLAYS OFFLINE", "B612-Bold", 9.5f, new Color(1, 1, 1, 0.85f), -15, 14);
            return root;
        }

        public static void HudShadow(Component c) { var s = c.gameObject.AddComponent<Shadow>(); s.effectColor = new Color(0, 0, 0, Theme.Night ? 0.5f : 0.25f); s.effectDistance = new Vector2(0, -2); }
        public static void CardShadow(Component c) { var s = c.gameObject.AddComponent<Shadow>(); s.effectColor = new Color(0.07f, 0.09f, 0.13f, 0.16f); s.effectDistance = new Vector2(0, -6); }

        // FIDS status word on a light ground (filled tag).
        public static Image Tag(Transform parent, string word, Tok fill, Tok text)
        {
            var img = Img(parent, "Tag", fill, Art.RoundRect, RSm);
            H(img.gameObject, 0, new RectOffset(8, 8, 4, 4), TextAnchor.MiddleCenter);
            Text(img.transform, word, TS.Label, text, TextAlignmentOptions.Center);
            return img;
        }

        public static Button GateSign(Transform parent, string gate, string label, bool right, Action onClick)
        {
            var img = Img(parent, "Gate " + label, Tok.Asphalt, Art.RoundRect, RPanel, 1, true); LE(img, -1, 64, 1);
            H(img.gameObject, 10, new RectOffset(12, 12, 12, 12));
            if (!right) Pic(img.transform, Art.Picto("arrowL"), 32);
            var tile = Img(img.transform, "Code", Tok.Signal, Art.RoundRect, RSm); LE(tile, 34, 30);
            var tt = Text(tile.transform, gate, TS.Flap, Tok.OnSignal, TextAlignmentOptions.Center); tt.fontSize = 18; Fill(tt.rectTransform);
            var t = Text(img.transform, label, TS.Button, Tok.InkInverse); LE(t, -1, -1, 1);
            if (right) Pic(img.transform, Art.Picto("arrowR"), 32);
            var b = img.gameObject.AddComponent<Button>(); b.transition = Selectable.Transition.None;
            b.onClick.AddListener(() => { Sfx.Click(); onClick?.Invoke(); });
            var u = Img(img.transform, "Underline", Tok.Signal); var ur = u.rectTransform;
            ur.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            ur.anchorMin = new Vector2(0, 0); ur.anchorMax = new Vector2(1, 0); ur.pivot = new Vector2(0.5f, 0); ur.sizeDelta = new Vector2(-24, 3); ur.anchoredPosition = new Vector2(0, 6);
            img.gameObject.AddComponent<PressFx>().underline = u;
            return b;
        }
    }

    // Hover underline + press squash for gate signs.
    public class PressFx : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerEnterHandler, IPointerExitHandler
    {
        public Image underline; bool down, hover; float sc = 1f, sv;
        public void OnPointerDown(PointerEventData e) => down = true;
        public void OnPointerUp(PointerEventData e) { if (down) sv += 1.2f; down = false; }
        public void OnPointerEnter(PointerEventData e) => hover = true;
        public void OnPointerExit(PointerEventData e) { hover = false; down = false; }
        void OnDisable() { down = hover = false; sc = 1f; sv = 0f; }
        void Update()
        {
            if (underline) underline.enabled = hover || down;
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            sv += (-320f * (sc - (down ? 0.96f : 1f)) - 16f * sv) * dt; sc += sv * dt;
            transform.localScale = Vector3.one * sc;
        }
    }

    public enum BK { Primary, Secondary, Ghost, GhostInverse }

    public class FButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerEnterHandler, IPointerExitHandler
    {
        public Button button; public TextMeshProUGUI label; public Action onClick;
        Image face, shadow, outline; RectTransform faceRt; CanvasGroup cg; BK kind; bool down, hover; float press, sc = 1f, sv;

        public static FButton Make(Transform parent, string text, BK kind, Action onClick, float height = 52f, Sprite icon = null)
        {
            var r = UI.Rect(parent, "Btn " + text);
            UI.LE(r, -1, height + (kind == BK.Primary ? 4 : 0));
            var b = r.gameObject.AddComponent<FButton>(); b.kind = kind; b.onClick = onClick;
            b.cg = r.gameObject.AddComponent<CanvasGroup>();
            if (kind == BK.Primary)
            {
                b.shadow = UI.Img(r, "Shadow", Tok.PressShadow, Art.RoundRect, UI.RMd);
                var s = b.shadow.rectTransform; s.anchorMin = new Vector2(0, 0); s.anchorMax = new Vector2(1, 0); s.pivot = new Vector2(0.5f, 0); s.anchoredPosition = Vector2.zero; s.sizeDelta = new Vector2(0, height);
            }
            b.face = UI.Img(r, "Face", Tok.Signal, Art.RoundRect, UI.RMd, 1, true);
            b.faceRt = b.face.rectTransform; b.faceRt.anchorMin = new Vector2(0, 1); b.faceRt.anchorMax = new Vector2(1, 1); b.faceRt.pivot = new Vector2(0.5f, 1);
            b.faceRt.anchoredPosition = Vector2.zero; b.faceRt.sizeDelta = new Vector2(0, height);
            if (kind == BK.Ghost || kind == BK.GhostInverse) { b.outline = UI.Img(b.faceRt, "Outline", Tok.BorderStrong, Art.RoundOutline, UI.RMd); UI.Fill(b.outline.rectTransform); }
            var row = UI.Rect(b.faceRt, "Row"); UI.Fill(row, 16, 0, 16, 0); UI.H(row.gameObject, 8, null, TextAnchor.MiddleCenter);
            if (icon != null) UI.Pic(row, icon, 24);
            b.label = UI.Text(row, text, TS.Button, Tok.OnSignal, TextAlignmentOptions.Center);
            b.button = r.gameObject.AddComponent<Button>(); b.button.transition = Selectable.Transition.None; b.button.targetGraphic = b.face;
            var nav = b.button.navigation; nav.mode = Navigation.Mode.None; b.button.navigation = nav;
            b.button.onClick.AddListener(() => { Sfx.Click(); b.onClick?.Invoke(); });
            b.Refresh();
            return b;
        }

        public bool Interactable { get => button.interactable; set => button.interactable = value; }
        public void SetText(string s) => label.text = s;

        public void OnPointerDown(PointerEventData e) => down = true;
        public void OnPointerUp(PointerEventData e) { if (down) sv += 1.4f; down = false; }
        public void OnPointerEnter(PointerEventData e) => hover = true;
        public void OnPointerExit(PointerEventData e) { hover = false; down = false; }
        void OnDisable() { down = hover = false; press = 0; sc = 1f; sv = 0f; if (faceRt) faceRt.localScale = Vector3.one; }

        void LateUpdate() => Refresh();

        void Refresh()
        {
            bool on = button == null || button.interactable;
            press = Mathf.MoveTowards(press, (down && on) ? 1f : 0f, Time.unscaledDeltaTime / 0.08f);
            faceRt.anchoredPosition = new Vector2(0, kind == BK.Primary ? -4f * press : 0f);
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            sv += (-300f * (sc - (down && on ? 0.97f : 1f)) - 15f * sv) * dt; sc += sv * dt;
            faceRt.localScale = new Vector3(sc, sc, 1);
            cg.alpha = on ? 1f : 0.45f;
            if (shadow) shadow.enabled = on;
            bool act = (down || hover) && on;
            Color fill, txt, ol = Color.clear;
            switch (kind)
            {
                case BK.Primary: fill = Theme.Get(act ? Tok.SignalPressed : Tok.Signal); txt = Theme.Get(Tok.OnSignal); break;
                case BK.Secondary: fill = Theme.Get(Tok.SkyDeep); if (act) fill = Color.Lerp(fill, Color.black, 0.15f); txt = Theme.Get(Tok.OnSkyDeep); break;
                case BK.Ghost: fill = down && on ? Theme.Get(Tok.Border) : Color.clear; txt = Theme.Get(Tok.Ink); ol = Theme.Get(act ? Tok.Ink : Tok.BorderStrong); break;
                default: fill = new Color(1, 1, 1, down && on ? 0.08f : 0f); txt = Theme.Get(Tok.InkInverse); ol = Theme.Get(act ? Tok.InkInverse : Tok.InkInverseMuted); break;
            }
            face.color = fill; label.color = txt; if (outline) outline.color = ol;
        }
    }

    // Split-flap characters. Only changed characters flip, staggered left to right.
    public class FlapText : MonoBehaviour
    {
        readonly List<TextMeshProUGUI> chars = new List<TextMeshProUGUI>();
        readonly List<RectTransform> tiles = new List<RectTransform>();
        char[] cur; int len;

        public static FlapText Make(Transform parent, int len, float w = 24, float h = 32, Tok tok = Tok.Signal)
        {
            var r = UI.Rect(parent, "Flap"); var f = r.gameObject.AddComponent<FlapText>(); f.len = len; f.cur = new string(' ', len).ToCharArray();
            var hl = r.gameObject.AddComponent<HorizontalLayoutGroup>(); hl.spacing = 4; hl.childAlignment = TextAnchor.MiddleLeft;
            hl.childControlWidth = hl.childControlHeight = false; hl.childForceExpandWidth = hl.childForceExpandHeight = false;
            UI.LE(r, len * w + (len - 1) * 4, h);
            for (int i = 0; i < len; i++)
            {
                var tile = UI.Img(r, "Tile", Tok.Fids, Art.RoundRect, UI.RSm); tile.rectTransform.sizeDelta = new Vector2(w, h);
                var t = UI.Text(tile.transform, " ", TS.Flap, tok, TextAlignmentOptions.Center); UI.Fill(t.rectTransform); t.fontSize = h * 0.68f;
                var hinge = UI.Img(tile.transform, "Hinge", Tok.FlapSplit); var hr = hinge.rectTransform;
                hr.anchorMin = new Vector2(0, 0.5f); hr.anchorMax = new Vector2(1, 0.5f); hr.sizeDelta = new Vector2(0, 1.5f); hr.anchoredPosition = Vector2.zero;
                f.tiles.Add(tile.rectTransform); f.chars.Add(t);
            }
            return f;
        }

        public void Set(string s, bool instant = false)
        {
            s = s.Length > len ? s.Substring(s.Length - len) : s.PadLeft(len);
            for (int i = 0; i < len; i++)
            {
                if (cur[i] == s[i]) continue;
                cur[i] = s[i];
                if (instant || !isActiveAndEnabled) { chars[i].text = s[i].ToString(); tiles[i].localScale = Vector3.one; }
                else StartCoroutine(Flip(i, s[i], i * 0.05f));
            }
        }

        IEnumerator Flip(int i, char c, float delay)
        {
            if (delay > 0) yield return new WaitForSecondsRealtime(delay);
            float t = 0;
            while (t < 0.03f) { t += Time.unscaledDeltaTime; tiles[i].localScale = new Vector3(1, Mathf.Max(0, 1 - t / 0.03f), 1); yield return null; }
            chars[i].text = c.ToString(); t = 0;
            while (t < 0.03f) { t += Time.unscaledDeltaTime; tiles[i].localScale = new Vector3(1, Mathf.Min(1, t / 0.03f), 1); yield return null; }
            tiles[i].localScale = Vector3.one;
        }

        void OnDisable() { foreach (var t in tiles) t.localScale = Vector3.one; for (int i = 0; i < len; i++) chars[i].text = cur[i].ToString(); }
    }

    // HUD gauge: label + status word + pill track.
    public class Gauge
    {
        public RectTransform root; TextMeshProUGUI word; Image fill; Themed wordT, fillT; TextMeshProUGUI label; float shown = -1f;

        public static Gauge Make(Transform parent, string label)
        {
            var g = new Gauge(); g.root = UI.Rect(parent, "Gauge " + label); UI.V(g.root.gameObject, 4);
            var row = UI.Row(g.root, 4);
            g.label = UI.Text(row, label, TS.Label, Tok.InkInverseMuted); UI.LE(g.label, -1, -1, 1);
            g.word = UI.Text(row, "", TS.Label, Tok.HudGo, TextAlignmentOptions.Right); g.wordT = g.word.GetComponent<Themed>();
            var track = UI.Img(g.root, "Track", Tok.Fids, Art.Pill, UI.PillM(8)); UI.LE(track, -1, 8);
            g.fill = UI.Img(track.transform, "Fill", Tok.HudGo, Art.Pill, UI.PillM(8)); g.fillT = g.fill.GetComponent<Themed>();
            var fr = g.fill.rectTransform; fr.anchorMin = Vector2.zero; fr.anchorMax = Vector2.one; fr.pivot = new Vector2(0, 0.5f); fr.offsetMin = fr.offsetMax = Vector2.zero;
            return g;
        }

        public void Set(float v, string w, Tok tok, bool showWord = true)
        {
            v = Mathf.Clamp01(v);
            shown = shown < 0f || !root.gameObject.activeInHierarchy ? v : Mathf.Lerp(shown, v, 1f - Mathf.Exp(-10f * Time.unscaledDeltaTime));
            fill.rectTransform.anchorMax = new Vector2(shown, 1);
            fill.enabled = shown > 0.005f;
            word.text = w; word.enabled = showWord;
            if (wordT.tok != tok) { wordT.Set(tok); fillT.Set(tok); }
        }
        public void SetLabel(string s) => label.text = s;
    }

    // Floating chip near the plane: "CLOSE CALL +50".
    public class PopChip : MonoBehaviour
    {
        public TextMeshProUGUI text; public CanvasGroup cg; public RectTransform rt; float t, swayDir; Vector2 start;
        public void Show(Vector2 pos, string s, Tok tok)
        {
            text.text = s; text.GetComponent<Themed>().Set(tok); start = pos; rt.anchoredPosition = pos; t = 0;
            swayDir = UnityEngine.Random.value < 0.5f ? -1f : 1f; rt.localScale = Vector3.one * 0.4f; gameObject.SetActive(true);
        }
        void Update()
        {
            t += Time.unscaledDeltaTime;
            float k = t / 1.0f;
            float rise = UI.OutCubic(k);
            rt.anchoredPosition = start + new Vector2(Mathf.Sin(k * Mathf.PI) * 14f * swayDir, 56f * rise);
            rt.localScale = Vector3.one * Mathf.Lerp(0.4f, 1f, UI.Back(k / 0.3f, 2.2f));
            rt.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(k * Mathf.PI) * 4f * -swayDir);
            cg.alpha = k < 0.65f ? 1f : Mathf.Clamp01(1 - (k - 0.65f) / 0.35f);
            if (k >= 1) gameObject.SetActive(false);
        }
    }

    // Scale spring for HUD numbers: Punch() kicks it, it settles back to 1 with a little wobble.
    public class Springy : MonoBehaviour
    {
        float s = 1f, v;
        public void Punch(float amount) { v += amount; }
        void Update()
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            v += (-280f * (s - 1f) - 13f * v) * dt; s += v * dt;
            transform.localScale = new Vector3(s, s, 1);
        }
    }
}
