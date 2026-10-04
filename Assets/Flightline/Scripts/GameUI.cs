using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Flightline
{
    public static class Credits
    {
        public const string Author = "Pradhyuman";
        public const string Line = "A game by Pradhyuman.";
    }

    public class GameUI : MonoBehaviour
    {
        public static GameUI I;
        Game game;
        RectTransform root, safeRt; Rect lastSafe;
        RectTransform scrRate, scrTitle, scrHud, scrPause, scrOver, scrHangar, scrLog, toastLayer, popLayer;
        RectTransform current, slideFrom, slideTo; Coroutine slideCo;
        int overGen; // bumps whenever the game-over screen is left or re-shown, so its delayed actions never fire late
        readonly Dictionary<RectTransform, Coroutine> staggerCos = new Dictionary<RectTransform, Coroutine>();

        // HUD
        TextMeshProUGUI hudScore, hudMiles, hudMult, hudCoins; Gauge fuelG, shieldG, magnetG, boostG; int lastScore = -1;
        // HUD change caches: strings are rebuilt only when the displayed value changes (TMP skips identical strings).
        readonly char[] scoreChars = new char[6]; long lastMiles = -1; int lastMult = -1, lastCoins = -1;
        int shieldTenths = -1, magnetTenths = -1, boostTenths = -1, headMiles = -1; string shieldStr = "", magnetStr = "", boostStr = "", headStr = "";
        Springy scoreSpring, coinSpring, multSpring; RectTransform hudCoinIcon;
        public Vector2 CoinTargetScreen => hudCoinIcon != null ? RectTransformUtility.WorldToScreenPoint(null, hudCoinIcon.position) : new Vector2(Screen.width * 0.8f, Screen.height * 0.9f);
        public void PunchCoins() { if (coinSpring) coinSpring.Punch(5f); }
        public void PunchMult() { if (multSpring) multSpring.Punch(9f); }
        // Home
        TextMeshProUGUI homeLevel, homeCoins, homeBest; Image homeXp; RectTransform homeBestChip, homeCoinChip; Springy homeCoinSpring;
        Badge badgeMissions, badgeHangar;
        RectTransform scrMissions;
        TextMeshProUGUI ovMissions;
        // Pause
        FButton soundBtn;
        // Over
        TextMeshProUGUI ovOrigin, ovOriginName, ovFlight, ovDest, ovDestName, ovReason, ovMiles, ovCoins, ovNear, ovRank, ovXp; FlapText ovScore; Image ovBestTag, ovXpFill; RectTransform ovCard;
        // Hangar
        TextMeshProUGUI hangarCoins; readonly List<Action> hangarRefresh = new List<Action>();
        // Logbook
        TextMeshProUGUI logCoins, logMiles, logRuns, logBest, logCount; readonly List<Action> logRefresh = new List<Action>();
        // Toast
        RectTransform toastRt; CanvasGroup toastCg; TextMeshProUGUI toastMain, toastSub; readonly Queue<(string, string)> toasts = new Queue<(string, string)>(); bool toasting;
        readonly List<PopChip> pops = new List<PopChip>();

        static string N(long n) => Prog.N(n);

        // =========================================================== build
        public void Build(Game g)
        {
            I = this; game = g;
            var cgo = new GameObject("UI", typeof(RectTransform)); cgo.layer = 5;
            var canvas = cgo.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 10;
            var sc = cgo.AddComponent<CanvasScaler>(); sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            sc.referenceResolution = new Vector2(390, 844); sc.matchWidthOrHeight = 0.5f; sc.referencePixelsPerUnit = 100;
            cgo.AddComponent<GraphicRaycaster>();
            root = (RectTransform)cgo.transform;
            safeRt = UI.Fill(UI.Rect(root, "Safe"));
            if (FindAnyObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem", typeof(EventSystem));
                es.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }
            scrHud = BuildHud();
            scrTitle = BuildTitle();
            scrHangar = BuildHangar();
            scrLog = BuildLog();
            scrMissions = BuildMissions();
            scrPause = BuildPause();
            scrOver = BuildOver();
            scrRate = BuildRateCard();
            popLayer = UI.Fill(UI.Rect(safeRt, "Pops"));
            toastLayer = UI.Fill(UI.Rect(safeRt, "Toasts"));
            BuildToast();
            // boot: start on the splash colour and fade into the game, so the splash hands off without a flash
            var boot = UI.Img(root, "Boot", Tok.Fids, null, 1, 1, true); UI.Fill(boot.rectTransform, -50, -50, -50, -50);
            StartCoroutine(BootFade(boot));
        }

        IEnumerator BootFade(Image boot)
        {
            var cg = boot.gameObject.AddComponent<CanvasGroup>();
            yield return new WaitForSecondsRealtime(0.15f);
            float t = 0;
            while (t < 1f) { t += Time.unscaledDeltaTime / 0.6f; cg.alpha = 1f - UI.OutCubic(t); yield return null; }
            Destroy(boot.gameObject);
        }

        // Android back button (Escape): menus step back, home quits. In flight the game handles pause/resume.
        void OnBack()
        {
            if (game.state == GState.Playing || game.state == GState.Paused || game.state == GState.Crashing) return;
            if (scrRate.gameObject.activeSelf) { scrRate.gameObject.SetActive(false); return; }
            if (scrOver.gameObject.activeSelf) { game.ToMenu(); return; }
            if (current == scrHangar || current == scrLog || current == scrMissions) { Sfx.Click(); Go(scrTitle, -1); return; }
            if (current == scrTitle) Application.Quit();
        }

        RectTransform MakeScreen(string name)
        {
            var r = UI.Fill(UI.Rect(safeRt, name)); r.gameObject.AddComponent<CanvasGroup>(); r.gameObject.SetActive(false); return r;
        }

        void Bg(RectTransform s, Tok tok, float alpha = 1f, bool ray = true)
        {
            var b = UI.Img(s, "Bg", tok, null, 1, alpha, ray); UI.Fill(b.rectTransform, -800, -800, -800, -800);
        }

        void Update()
        {
            var sa = Screen.safeArea;
            if (sa != lastSafe && Screen.width > 0 && Screen.height > 0)
            {
                lastSafe = sa;
                safeRt.anchorMin = new Vector2(sa.xMin / Screen.width, sa.yMin / Screen.height);
                safeRt.anchorMax = new Vector2(sa.xMax / Screen.width, sa.yMax / Screen.height);
                safeRt.offsetMin = safeRt.offsetMax = Vector2.zero;
            }
            if (scrHud.gameObject.activeSelf) UpdateHud();
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame) OnBack();
            if (scrMissions.gameObject.activeSelf && (msTick -= Time.unscaledDeltaTime) <= 0) { msTick = 1f; if (Missions.EnsureToday()) RefreshMissionsScreen(); else msReset.text = "RESETS " + Missions.ResetsIn; }
            if (routePulse != null && scrLog.gameObject.activeSelf) routePulse.localScale = Vector3.one * (1f + 0.12f * Mathf.Sin(Time.unscaledTime * 5f));
        }

        // =========================================================== HUD
        RectTransform BuildHud()
        {
            var s = MakeScreen("HUD");
            var sp = UI.Img(s, "ScorePanel", Tok.Asphalt, Art.RoundRect, UI.RPanel); UI.Anchor(sp.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(16, -16), Vector2.zero);
            UI.V(sp.gameObject, 2, new RectOffset(12, 12, 10, 10)); UI.Fit(sp, true, true); UI.HudShadow(sp);
            UI.Text(sp.transform, "SCORE", TS.Label, Tok.InkInverseMuted);
            hudScore = UI.Text(sp.transform, "000000", TS.HudScore, Tok.InkInverse); scoreSpring = hudScore.gameObject.AddComponent<Springy>();
            var r = UI.Row(sp.transform, 10);
            hudMiles = UI.Text(r, "0 mi", TS.Readout, Tok.InkInverseMuted); UI.LE(hudMiles, -1, -1, 1);
            hudMult = UI.Text(r, "x1", TS.Readout, Tok.InkInverseMuted, TextAlignmentOptions.Right); multSpring = hudMult.gameObject.AddComponent<Springy>();

            var pb = UI.Img(s, "PauseBtn", Tok.Asphalt, Art.RoundRect, UI.RPanel, 1, true); UI.Anchor(pb.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-16, -16), new Vector2(48, 48)); UI.HudShadow(pb);
            var pic = UI.Pic(pb.transform, Art.Picto("pause"), 40); UI.Fill(pic.rectTransform, 4, 4, 4, 4);
            var btn = pb.gameObject.AddComponent<Button>(); btn.transition = Selectable.Transition.None; btn.onClick.AddListener(() => { Sfx.Click(); game.Pause(true); });

            var gp = UI.Img(s, "Gauges", Tok.Asphalt, Art.RoundRect, UI.RPanel); UI.Anchor(gp.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-16, -72), new Vector2(150, 0));
            UI.V(gp.gameObject, 8, new RectOffset(12, 12, 10, 12)); UI.Fit(gp, false, true); UI.HudShadow(gp);
            var cr = UI.Row(gp.transform, 6);
            hudCoinIcon = UI.Pic(cr, Art.Picto("coin"), 20).rectTransform; coinSpring = hudCoinIcon.gameObject.AddComponent<Springy>();
            hudCoins = UI.Text(cr, "0", TS.Readout, Tok.Signal); UI.LE(hudCoins, -1, -1, 1);
            fuelG = Gauge.Make(gp.transform, "FUEL");
            shieldG = Gauge.Make(gp.transform, "SHIELD");
            magnetG = Gauge.Make(gp.transform, "MAGNET");
            boostG = Gauge.Make(gp.transform, "BOOST");
            return s;
        }

        void UpdateHud()
        {
            int sc = Mathf.FloorToInt(game.score);
            if (sc != lastScore) { if (lastScore >= 0 && sc - lastScore >= 40) scoreSpring.Punch(Mathf.Min(6f, (sc - lastScore) / 25f)); lastScore = sc; hudScore.text = sc.ToString("D6"); }
            long mi = (long)game.miles; if (mi != lastMiles) { lastMiles = mi; hudMiles.text = N(mi) + " mi"; }
            int mu = game.multiplier; if (mu != lastMult) { lastMult = mu; hudMult.text = "x" + mu; hudMult.color = Theme.Get(mu > 1 ? Tok.Signal : Tok.InkInverseMuted); }
            int co = game.run.coins; if (co != lastCoins) { lastCoins = co; hudCoins.text = N(co); }

            float f = game.fuel;
            if (f > 0.3f) fuelG.Set(f, "OK", Tok.HudGo);
            else if (f > 0.12f) fuelG.Set(f, "LOW", Tok.HudCaution);
            else fuelG.Set(f, "EMPTY", Tok.HudStop, Mathf.Repeat(Time.unscaledTime, 1f) < 0.5f);

            SetPower(shieldG, game.shieldT, game.shieldMax, ref shieldTenths, ref shieldStr);
            SetPower(magnetG, game.magnetT, game.magnetMax, ref magnetTenths, ref magnetStr);
            bool burner = game.boostT > 0 || game.headStart;
            boostG.root.gameObject.SetActive(burner);
            if (burner)
            {
                if (game.headStart) { int hm = (int)game.HeadStartMilesLeft; if (hm != headMiles) { headMiles = hm; headStr = N(hm) + " MI"; } boostG.Set(game.HeadStartFrac, headStr, Tok.Signal); }
                else boostG.Set(game.boostT / game.boostMax, Secs(game.boostT, ref boostTenths, ref boostStr), Tok.Signal);
            }
        }

        void SetPower(Gauge g, float t, float max, ref int tenths, ref string str)
        {
            bool on = t > 0; if (g.root.gameObject.activeSelf != on) g.root.gameObject.SetActive(on);
            if (on) g.Set(t / max, Secs(t, ref tenths, ref str), t < 1.5f ? Tok.HudCaution : Tok.HudGo);
        }

        // "{t:0.0} S", rebuilt only when the shown tenth changes (same rounding as the 0.0 format).
        static string Secs(float t, ref int tenths, ref string str)
        {
            int k = Mathf.FloorToInt(t * 10f + 0.5f);
            if (k != tenths) { tenths = k; str = $"{t:0.0} S"; }
            return str;
        }

        // =========================================================== Title (home): logo, best, play, three doors
        RectTransform BuildTitle()
        {
            var s = MakeScreen("Title");
            var col = UI.Fill(UI.Rect(s, "Col"), 16, 16, 16, 16);

            // top bar: level (left), coins (right)
            var lv = UI.Img(col, "Level", Tok.Asphalt, Art.Pill, UI.PillM(40), 1, true);
            UI.Anchor(lv.rectTransform, new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(0, 40));
            UI.H(lv.gameObject, 8, new RectOffset(14, 16, 0, 0)); UI.Fit(lv, true, false); UI.HudShadow(lv);
            homeLevel = UI.Text(lv.transform, "LV 1", TS.Button, Tok.InkInverse);
            var track = UI.Img(lv.transform, "Xp", Tok.Fids, Art.Pill, UI.PillM(6)); UI.LE(track, 56, 6);
            homeXp = UI.Img(track.transform, "Fill", Tok.Signal, Art.Pill, UI.PillM(6));
            var xr = homeXp.rectTransform; xr.anchorMin = Vector2.zero; xr.anchorMax = new Vector2(0.3f, 1); xr.offsetMin = xr.offsetMax = Vector2.zero;
            var lvb = lv.gameObject.AddComponent<Button>(); lvb.transition = Selectable.Transition.None; lvb.onClick.AddListener(() => { Sfx.Click(); Go(scrLog, 1); });
            lv.gameObject.AddComponent<PressFx>();

            var cp = UI.Img(col, "Coins", Tok.Asphalt, Art.Pill, UI.PillM(40));
            UI.Anchor(cp.rectTransform, new Vector2(1, 1), new Vector2(1, 1), Vector2.zero, new Vector2(0, 40));
            UI.H(cp.gameObject, 8, new RectOffset(12, 16, 0, 0)); UI.Fit(cp, true, false); UI.HudShadow(cp);
            UI.Pic(cp.transform, Art.Glyph("coin"), 22);
            homeCoins = UI.Text(cp.transform, "0", TS.Readout, Tok.Signal); homeCoins.fontSize = 17;
            homeCoinChip = cp.rectTransform; homeCoinSpring = cp.gameObject.AddComponent<Springy>();

            // logo + best
            var head = UI.Rect(col, "Head"); UI.Anchor(head, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -60), Vector2.zero);
            var hv = UI.V(head.gameObject, 10, null, TextAnchor.UpperCenter); hv.childForceExpandWidth = false; UI.Fit(head, true, true);
            var logo = UI.Img(head, "Logo", Tok.Asphalt, Art.RoundRect, UI.RPanel);
            var lvg = UI.V(logo.gameObject, 0, new RectOffset(22, 22, 8, 12), TextAnchor.UpperCenter); UI.HudShadow(logo);
            var t = UI.Text(logo.transform, "FLIGHTLINE", TS.DisplayXL, Tok.Signal, TextAlignmentOptions.Center); UI.LE(t, -1, 70);
            UI.Text(logo.transform, "BY " + Credits.Author, TS.Label, Tok.InkInverseMuted, TextAlignmentOptions.Center);
            var stk = UI.Sticker(logo.transform, 0.62f, -9f, false); stk.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            stk.anchorMin = stk.anchorMax = new Vector2(1, 0); stk.pivot = new Vector2(0.5f, 0.5f); stk.anchoredPosition = new Vector2(-36, 0);
            var best = UI.Img(head, "Best", Tok.Fids, Art.Pill, UI.PillM(32));
            UI.H(best.gameObject, 8, new RectOffset(14, 14, 7, 7)); UI.HudShadow(best);
            UI.Text(best.transform, "BEST", TS.Label, Tok.InkInverseMuted);
            homeBest = UI.Text(best.transform, "0", TS.Readout, Tok.Signal);
            homeBestChip = best.rectTransform;

            // bottom: three doors and the one primary action
            var foot = UI.Rect(col, "Foot"); foot.anchorMin = new Vector2(0, 0); foot.anchorMax = new Vector2(1, 0); foot.pivot = new Vector2(0.5f, 0);
            foot.anchoredPosition = Vector2.zero; foot.sizeDelta = Vector2.zero;
            UI.V(foot.gameObject, 12); UI.Fit(foot, false, true);
            var nav = UI.Row(foot, 10, 76); nav.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = true;
            badgeMissions = NavTile(nav, "flag", "MISSIONS", () => Go(scrMissions, 1));
            badgeHangar = NavTile(nav, "wrench", "HANGAR", () => Go(scrHangar, 1));
            NavTile(nav, "trophy", "LOGBOOK", () => Go(scrLog, 1));
            FButton.Make(foot, "TAKEOFF", BK.Primary, () => game.StartRun(), 60);
            return s;
        }

        class Badge
        {
            public RectTransform rt; public TextMeshProUGUI text;
            public void Set(int n) { if (rt == null) return; rt.gameObject.SetActive(n > 0); text.text = n > 9 ? "9+" : n.ToString(); }
        }

        Badge NavTile(Transform row, string glyph, string label, Action onClick)
        {
            var tile = UI.Img(row, "Nav " + label, Tok.Asphalt, Art.RoundRect, UI.RPanel, 1, true); UI.LE(tile, -1, 76, 1); UI.HudShadow(tile);
            var v = UI.V(tile.gameObject, 6, new RectOffset(4, 4, 12, 10), TextAnchor.MiddleCenter); v.childForceExpandWidth = false;
            UI.Pic(tile.transform, Art.Glyph(glyph), 32);
            UI.Text(tile.transform, label, TS.Label, Tok.InkInverse, TextAlignmentOptions.Center);
            var b = tile.gameObject.AddComponent<Button>(); b.transition = Selectable.Transition.None;
            b.onClick.AddListener(() => { Sfx.Click(); onClick(); });
            tile.gameObject.AddComponent<PressFx>();
            var badge = new Badge();
            var bi = UI.Img(tile.transform, "Badge", Tok.Signal, Art.Pill, UI.PillM(22)); bi.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            var br = bi.rectTransform; br.anchorMin = br.anchorMax = new Vector2(1, 1); br.pivot = new Vector2(0.5f, 0.5f); br.anchoredPosition = new Vector2(-14, -14); br.sizeDelta = new Vector2(24, 22);
            badge.text = UI.Text(bi.transform, "1", TS.Readout, Tok.OnSignal, TextAlignmentOptions.Center); badge.text.fontSize = 13; UI.Fill(badge.text.rectTransform);
            badge.rt = br; bi.gameObject.AddComponent<Springy>();
            bi.gameObject.SetActive(false);
            return badge;
        }

        TextMeshProUGUI Cell(Transform row, string s, TS style, Tok tok, float w, float flex = -1, TextAlignmentOptions al = TextAlignmentOptions.Left)
        {
            var t = UI.Text(row, s, style, tok, al); UI.LE(t, w, -1, flex); return t;
        }

        void RefreshTitle()
        {
            var d = Save.D; Missions.EnsureToday();
            homeLevel.text = "LV " + d.level;
            homeXp.rectTransform.anchorMax = new Vector2(Mathf.Clamp01((float)d.xp / Prog.XpToNext(d.level)), 1);
            homeCoins.text = N(d.coins);
            homeBestChip.gameObject.SetActive(d.bestScore > 0); homeBest.text = N(d.bestScore);
            badgeMissions.Set(Missions.Claimable);
            badgeHangar.Set(Prog.Affordable);
        }

        // =========================================================== Pause
        RectTransform BuildPause()
        {
            var s = MakeScreen("Pause"); Bg(s, Tok.Fids, 0.72f);
            var card = Card(s, 320);
            UI.Text(card, "HOLDING PATTERN", TS.DisplayL, Tok.Ink);
            UI.Text(card, "Your flight is paused. Take your time.", TS.Body, Tok.InkMuted, TextAlignmentOptions.Left, true);
            UI.Text(card, Credits.Line, TS.Caption, Tok.InkMuted);
            FButton.Make(card, "RESUME", BK.Primary, () => game.Pause(false));
            soundBtn = FButton.Make(card, SoundLabel(), BK.Ghost, () => { Save.D.sound = !Save.D.sound; Save.Write(); soundBtn.SetText(SoundLabel()); });
            FButton.Make(card, "END FLIGHT", BK.Ghost, () => game.EndFromPause());
            return s;
        }

        string SoundLabel() => Save.D.sound ? "SOUND: ON" : "SOUND: OFF";

        Transform Card(RectTransform parent, float width)
        {
            var c = UI.Img(parent, "Card", Tok.SurfaceRaised, Art.RoundRect, UI.RMd, 1, true);
            UI.Anchor(c.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(width, 0));
            UI.V(c.gameObject, 12, UI.Pad(20)); UI.Fit(c, false, true); UI.CardShadow(c);
            return c.transform;
        }

        // A one-time personal note asking for a rating. Android only: Apple allows only its own prompt (guideline 5.6.1).
        RectTransform BuildRateCard()
        {
            var s = MakeScreen("Rate"); Bg(s, Tok.Fids, 0.72f);
            var card = Card(s, 330);
            UI.Text(card, "A NOTE FROM " + Credits.Author.ToUpper(), TS.Label, Tok.InkMuted);
            var ty = UI.Text(card, "THANKS FOR FLYING", TS.DisplayL, Tok.Ink); ty.enableAutoSizing = true; ty.fontSizeMax = ty.fontSize; ty.fontSizeMin = 18;
            UI.Text(card, "Flightline is my first game, and I made it on my own. An honest rating on Google Play helps other players find it, and your review tells me what to build next.", TS.Body, Tok.InkMuted, TextAlignmentOptions.Left, true);
            FButton.Make(card, "RATE ON GOOGLE PLAY", BK.Primary, () => { Review.OpenStore(); scrRate.gameObject.SetActive(false); });
            FButton.Make(card, "NOT NOW", BK.Ghost, () => scrRate.gameObject.SetActive(false));
            return s;
        }

        public void ShowPause(bool on) { if (on) Overlay(scrPause); else scrPause.gameObject.SetActive(false); }

        // =========================================================== Game over: boarding pass
        RectTransform BuildOver()
        {
            var s = MakeScreen("Over"); Bg(s, Tok.Fids, 0.72f);
            var col = UI.Rect(s, "Col"); UI.Anchor(col, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 10), new Vector2(358, 0));
            UI.V(col.gameObject, 12); UI.Fit(col, false, true); ovCard = col;

            var pass = UI.Img(col, "Pass", Tok.SurfaceRaised, Art.RoundRect, UI.RMd); UI.V(pass.gameObject, 0); UI.CardShadow(pass);
            var hdr = UI.Img(pass.transform, "Hdr", Tok.Asphalt, Art.RoundTop, UI.RMd); UI.H(hdr.gameObject, 8, new RectOffset(16, 16, 10, 10));
            var hl = UI.Text(hdr.transform, "BOARDING PASS", TS.Label, Tok.InkInverseMuted); UI.LE(hl, -1, -1, 1);
            ovFlight = UI.Text(hdr.transform, "FL-207", TS.Readout, Tok.Signal);

            var route = UI.Rect(pass.transform, "Route"); UI.H(route.gameObject, 8, new RectOffset(20, 20, 14, 6));
            var o = UI.Rect(route, "Origin"); UI.V(o.gameObject, 0);
            ovOrigin = UI.Text(o, "LAX", TS.DisplayL, Tok.Ink); ovOriginName = UI.Text(o, "LOS ANGELES", TS.Caption, Tok.InkMuted);
            UI.Spacer(route); UI.Pic(route, Art.Picto("plane"), 32); UI.Spacer(route);
            var dst = UI.Rect(route, "Dest"); UI.V(dst.gameObject, 0, null, TextAnchor.UpperRight);
            ovDest = UI.Text(dst, "CNE", TS.DisplayL, Tok.Ink, TextAlignmentOptions.Right); ovDestName = UI.Text(dst, "CLOUD NINE", TS.Caption, Tok.InkMuted, TextAlignmentOptions.Right);

            var st = UI.Rect(pass.transform, "Status"); UI.V(st.gameObject, 6, new RectOffset(20, 20, 6, 10));
            var tags = UI.Row(st, 6); UI.Tag(tags, "CANCELLED", Tok.Stop, Tok.OnStop); ovBestTag = UI.Tag(tags, "New best!", Tok.Signal, Tok.OnSignal); UI.Spacer(tags);
            ovReason = UI.Text(st, "", TS.Caption, Tok.InkMuted, TextAlignmentOptions.Left, true);

            // perforation
            var perf = UI.Rect(pass.transform, "Perf"); UI.LE(perf, -1, 20);
            var dash = UI.Img(perf, "Dash", Tok.Border, Art.Dash); dash.type = Image.Type.Tiled; dash.pixelsPerUnitMultiplier = 1;
            var dr = dash.rectTransform; dr.anchorMin = new Vector2(0, 0.5f); dr.anchorMax = new Vector2(1, 0.5f); dr.sizeDelta = new Vector2(-40, 2); dr.anchoredPosition = Vector2.zero;
            foreach (var x in new[] { 0f, 1f })
            {
                var n = UI.Img(perf, "Notch", Tok.Fids, Art.Circle); var nr = n.rectTransform; nr.anchorMin = nr.anchorMax = new Vector2(x, 0.5f); nr.sizeDelta = new Vector2(20, 20); nr.anchoredPosition = Vector2.zero;
            }

            var stats = UI.Rect(pass.transform, "Stats"); UI.V(stats.gameObject, 10, new RectOffset(20, 20, 6, 18));
            var sr1 = UI.Row(stats, 12);
            var sc = UI.Rect(sr1, "Score"); UI.V(sc.gameObject, 4); UI.LE(sc, -1, -1, 1); UI.Text(sc, "SCORE", TS.Label, Tok.InkMuted); ovScore = FlapText.Make(sc, 6, 26, 34);
            var mc = UI.Rect(sr1, "Miles"); UI.V(mc.gameObject, 4, null, TextAnchor.UpperRight); UI.Text(mc, "MILES", TS.Label, Tok.InkMuted, TextAlignmentOptions.Right); ovMiles = UI.Text(mc, "0", TS.Flap, Tok.Ink, TextAlignmentOptions.Right);
            var sr2 = UI.Row(stats, 12);
            ovCoins = Stat(sr2, "COINS EARNED"); ovNear = Stat(sr2, "CLOSE CALLS");
            var mRow = UI.Row(stats, 8);
            var ml = UI.Text(mRow, "DAILY MISSIONS", TS.Label, Tok.InkMuted); UI.LE(ml, -1, -1, 1);
            ovMissions = UI.Text(mRow, "0 / 3", TS.Readout, Tok.SkyDeep, TextAlignmentOptions.Right);
            var xpRow = UI.Row(stats, 8);
            ovRank = UI.Text(xpRow, "", TS.Caption, Tok.InkMuted); UI.LE(ovRank, -1, -1, 1);
            ovXp = UI.Text(xpRow, "", TS.Readout, Tok.SkyDeep, TextAlignmentOptions.Right);
            var track = UI.Img(stats, "XpTrack", Tok.Border, Art.Pill, UI.PillM(8)); UI.LE(track, -1, 8);
            ovXpFill = UI.Img(track.transform, "Fill", Tok.SkyDeep, Art.Pill, UI.PillM(8)); var fr = ovXpFill.rectTransform; fr.anchorMin = Vector2.zero; fr.anchorMax = new Vector2(0.3f, 1); fr.offsetMin = fr.offsetMax = Vector2.zero;

            FButton.Make(col, "PLAY AGAIN", BK.Primary, () => game.StartRun(), 56);
            var row = UI.Row(col, 8, 48); row.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = true;
            var hb = FButton.Make(row, "HANGAR", BK.GhostInverse, () => { game.ToMenu(); Go(scrHangar, 1); }, 48); UI.LE(hb, -1, -1, 1);
            var mb = FButton.Make(row, "MENU", BK.GhostInverse, () => game.ToMenu(), 48); UI.LE(mb, -1, -1, 1);
            return s;
        }

        TextMeshProUGUI Stat(Transform row, string label)
        {
            var c = UI.Rect(row, label); UI.V(c.gameObject, 2); UI.LE(c, -1, -1, 1);
            UI.Text(c, label, TS.Label, Tok.InkMuted);
            return UI.Text(c, "0", TS.Flap, Tok.Ink);
        }

        public void ShowGameOver(RunResult r)
        {
            scrHud.gameObject.SetActive(false);
            var z = Prog.Zones[r.zone];
            ovFlight.text = "FL-" + r.flight;
            ovDest.text = z.code; ovDestName.text = z.name;
            ovOrigin.text = HomePort.Code; ovOriginName.text = HomePort.City;
            ovReason.text = r.reason == "fuel" ? "Flight cancelled. You ran out of fuel."
                : r.reason == "abort" ? "Flight cancelled. You turned back to the gate."
                : "Flight cancelled. Weather was not on your side.";
            ovBestTag.gameObject.SetActive(r.best);
            ovMiles.text = N((long)r.miles);
            ovCoins.text = r.earned > r.coins ? $"+{N(r.earned)}" : $"+{N(r.coins)}";
            ovNear.text = N(r.near);
            var d = Save.D; int need = Prog.XpToNext(d.level);
            ovRank.text = $"{Prog.Rank(d.level)} · LV {d.level}";
            ovXp.text = $"+{N(r.xp)} XP";
            ovXpFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01((float)d.xp / need), 1);
            int toClaim = Missions.Claimable;
            ovMissions.text = toClaim > 0 ? $"{Missions.DoneCount} / 3 · {toClaim} TO CLAIM" : $"{Missions.DoneCount} / 3";
            Overlay(scrOver);
            ovScore.Set("000000", true);
            int g = ++overGen; // FlapText.Set starts a coroutine on its own object: never call it once the screen is gone
            StartCoroutine(Delay(0.25f, () => { if (g == overGen && scrOver.gameObject.activeInHierarchy) ovScore.Set(r.score.ToString("D6")); }));
            if (r.reason != "abort") StartCoroutine(Delay(1.8f, () => { if (g == overGen && scrOver.gameObject.activeSelf) Review.MaybeAsk(r.best || r.newZone); }));
        }

        IEnumerator Delay(float t, Action a) { yield return new WaitForSecondsRealtime(t); a(); }

        // =========================================================== shared: header + flying coins
        TextMeshProUGUI Header(Transform col, string title)
        {
            var h = UI.Row(col, 12, 52);
            var b = FButton.Make(h, "BACK", BK.Ghost, () => Go(scrTitle, -1), 44); UI.LE(b, 104, 48);
            var t = UI.Text(h, title, TS.DisplayL, Tok.Ink); UI.LE(t, -1, -1, 1);
            var chip = UI.Img(h, "Coins", Tok.Asphalt, Art.Pill, UI.PillM(34)); UI.H(chip.gameObject, 6, new RectOffset(10, 14, 6, 6));
            chip.gameObject.AddComponent<Springy>();
            UI.Pic(chip.transform, Art.Glyph("coin"), 20);
            return UI.Text(chip.transform, "0", TS.Readout, Tok.Signal);
        }

        // Coins arc from a button into a coin counter, one blip each.
        void CoinBurst(RectTransform from, RectTransform to, int n)
        {
            for (int i = 0; i < n; i++) StartCoroutine(FlyUiCoin(from, to, i * 0.05f));
        }

        Vector2 LocalIn(RectTransform space, RectTransform r)
        {
            var sp = RectTransformUtility.WorldToScreenPoint(null, r.position);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(space, sp, null, out var lp); return lp;
        }

        IEnumerator FlyUiCoin(RectTransform from, RectTransform to, float delay)
        {
            yield return new WaitForSecondsRealtime(delay);
            var img = UI.Pic(toastLayer, Art.Glyph("coin"), 24); var rt = img.rectTransform; rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            Vector2 a = LocalIn(toastLayer, from), b = LocalIn(toastLayer, to);
            Vector2 c = Vector2.Lerp(a, b, 0.3f) + new Vector2(UnityEngine.Random.Range(-90f, 90f), UnityEngine.Random.Range(40f, 120f));
            float t = 0;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / 0.55f; float e = Mathf.Clamp01(t); e = e * e * (3f - 2f * e);
                rt.anchoredPosition = (1 - e) * (1 - e) * a + 2 * (1 - e) * e * c + e * e * b;
                rt.localScale = new Vector3(Mathf.Abs(Mathf.Cos(t * 10f)) * 0.7f + 0.3f, 1, 1) * Mathf.Lerp(1.2f, 0.7f, e);
                yield return null;
            }
            Destroy(img.gameObject);
            var sp = to.GetComponent<Springy>(); if (sp) sp.Punch(4f);
            if (Sfx.I) Sfx.I.Coin(1);
        }

        // =========================================================== Missions
        TextMeshProUGUI msCoins, msStreak, msReset, msBonusState; FButton msBonusBtn; Image msBonusCheck;
        readonly List<Themed> streakPips = new List<Themed>(); readonly List<Action> msRefresh = new List<Action>(); float msTick;

        RectTransform BuildMissions()
        {
            var s = MakeScreen("Missions"); Bg(s, Tok.Surface);
            var col = UI.Fill(UI.Rect(s, "Col"), 16, 16, 16, 16); UI.V(col.gameObject, 12);
            msCoins = Header(col, "MISSIONS");

            var sk = UI.Img(col, "Streak", Tok.Asphalt, Art.RoundRect, UI.RPanel); UI.H(sk.gameObject, 12, new RectOffset(16, 16, 12, 12)); UI.HudShadow(sk);
            var sl = UI.Rect(sk.transform, "L"); UI.V(sl.gameObject, 2); UI.LE(sl, 0, -1, 1);
            UI.Text(sl, "DAILY STREAK", TS.Label, Tok.InkInverseMuted);
            msStreak = UI.Text(sl, "START TODAY", TS.Heading, Tok.InkInverse);
            var sr = UI.Rect(sk.transform, "R"); var srv = UI.V(sr.gameObject, 8, null, TextAnchor.MiddleRight); srv.childForceExpandWidth = false; UI.LE(sr, -1, -1, 0);
            var pr = UI.Row(sr, 5);
            for (int p = 0; p < 7; p++) { var pip = UI.Img(pr, "Day", Tok.FlapSplit, Art.Circle); UI.LE(pip, 12, 12); streakPips.Add(pip.GetComponent<Themed>()); }
            msReset = UI.Text(sr, "RESETS 00:00", TS.Readout, Tok.InkInverseMuted, TextAlignmentOptions.Right); msReset.fontSize = 12;

            for (int i = 0; i < 3; i++) MissionCard(col, i);

            var bc = UI.Img(col, "Bonus", Tok.Asphalt, Art.RoundRect, UI.RPanel); UI.H(bc.gameObject, 12, new RectOffset(14, 14, 12, 12)); UI.HudShadow(bc);
            var bi = UI.Img(bc.transform, "Icon", Tok.Fids, Art.Pill, UI.PillM(44)); UI.LE(bi, 44, 44);
            var bg = UI.Pic(bi.transform, Art.Glyph("trophy"), 26); UI.Anchor(bg.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(26, 26));
            var bm = UI.Rect(bc.transform, "M"); UI.V(bm.gameObject, 2); UI.LE(bm, 0, -1, 1);
            UI.Text(bm, "DAILY BONUS", TS.Label, Tok.InkInverseMuted);
            msBonusState = UI.Text(bm, "", TS.Caption, Tok.InkInverse, TextAlignmentOptions.Left, true);
            msBonusBtn = FButton.Make(bc.transform, "+250", BK.Primary, null, 40, Art.Picto("coin")); UI.LE(msBonusBtn, 116, 44);
            msBonusBtn.onClick = () =>
            {
                int b = Missions.ClaimBonus(); if (b <= 0) return;
                CoinBurst((RectTransform)msBonusBtn.transform, (RectTransform)msCoins.transform.parent, 12);
                Toast($"DAILY BONUS · STREAK {Save.D.streak}", $"+{N(b)} coins.");
                Sfx.I.Chime(); RefreshMissionsScreen();
            };
            msBonusCheck = UI.Pic(bc.transform, Art.Glyph("check"), 30);
            return s;
        }

        void MissionCard(Transform col, int i)
        {
            var card = UI.Img(col, "Mission", Tok.SurfaceRaised, Art.RoundRect, UI.RMd); UI.H(card.gameObject, 12, new RectOffset(14, 14, 12, 12)); UI.CardShadow(card);
            var ic = UI.Img(card.transform, "Icon", Tok.Asphalt, Art.Pill, UI.PillM(44)); UI.LE(ic, 44, 44);
            var glyph = UI.Pic(ic.transform, Art.Glyph("coin"), 26); UI.Anchor(glyph.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(26, 26));
            var mid = UI.Rect(card.transform, "Mid"); UI.V(mid.gameObject, 6); UI.LE(mid, 0, -1, 1);
            var title = UI.Text(mid, "", TS.Title, Tok.Ink, TextAlignmentOptions.Left, true); title.fontSize = 15;
            var track = UI.Img(mid, "Track", Tok.Border, Art.Pill, UI.PillM(8)); UI.LE(track, -1, 8);
            var fill = UI.Img(track.transform, "Fill", Tok.SkyDeep, Art.Pill, UI.PillM(8));
            var fr = fill.rectTransform; fr.anchorMin = Vector2.zero; fr.anchorMax = new Vector2(0f, 1); fr.offsetMin = fr.offsetMax = Vector2.zero;
            var prog = UI.Text(mid, "", TS.Readout, Tok.InkMuted); prog.fontSize = 12;

            var right = UI.Rect(card.transform, "Right"); UI.V(right.gameObject, 6, null, TextAnchor.MiddleCenter); UI.LE(right, 92, -1, 0).minWidth = 92;
            var reward = UI.Row(right, 4); reward.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;
            UI.Pic(reward, Art.Picto("coin"), 18); var rt = UI.Text(reward, "+60", TS.Readout, Tok.Ink);
            var claim = FButton.Make(right, "CLAIM", BK.Secondary, null, 44);
            var swap = FButton.Make(right, "SWAP", BK.Ghost, null, 44); swap.label.fontSize = 14;
            var done = UI.Row(right, 4); done.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;
            UI.Pic(done, Art.Picto("check"), 20); UI.Text(done, "DONE", TS.Label, Tok.Go);

            claim.onClick = () =>
            {
                int r = Missions.Claim(i); if (r <= 0) return;
                CoinBurst((RectTransform)claim.transform, (RectTransform)msCoins.transform.parent, Mathf.Clamp(r / 15, 5, 12));
                Sfx.I.Chime(); RefreshMissionsScreen();
            };
            swap.onClick = () => { Missions.Swap(i); Sfx.I.Power(); RefreshMissionsScreen(); };
            var fillT = fill.GetComponent<Themed>();
            msRefresh.Add(() =>
            {
                var m = Missions.List[i];
                glyph.sprite = Art.Glyph(Missions.Icon(m));
                title.text = Missions.Describe(m);
                fr.anchorMax = new Vector2(m.target > 0 ? Mathf.Clamp01((float)m.progress / m.target) : 0f, 1);
                fill.enabled = m.progress > 0; fillT.Set(m.done ? Tok.Go : Tok.SkyDeep);
                prog.text = Missions.ProgressText(m);
                rt.text = "+" + N(m.reward);
                claim.gameObject.SetActive(m.done && !m.claimed);
                swap.gameObject.SetActive(Missions.CanSwap(i));
                done.gameObject.SetActive(m.claimed);
                reward.gameObject.SetActive(!m.claimed);
            });
        }

        void RefreshMissionsScreen()
        {
            var d = Save.D; Missions.EnsureToday();
            msCoins.text = N(d.coins);
            int st = Missions.Streak;
            msStreak.text = st == 0 ? "START TODAY" : st == 1 ? "1 DAY" : $"{st} DAYS";
            for (int p = 0; p < streakPips.Count; p++) streakPips[p].Set(p < Mathf.Min(st, 7) ? Tok.Signal : Tok.FlapSplit);
            msReset.text = "RESETS " + Missions.ResetsIn;
            foreach (var a in msRefresh) a();
            bool ready = Missions.BonusReady;
            int claimed = 0; foreach (var m in Missions.List) if (m.claimed) claimed++;
            msBonusBtn.gameObject.SetActive(ready); msBonusBtn.SetText("+" + N(Missions.NextBonus));
            msBonusCheck.gameObject.SetActive(d.bonusClaimed);
            msBonusState.text = d.bonusClaimed ? "Collected. See you tomorrow." : ready ? "Ready to collect." : $"Claim all 3 · {claimed}/3 · +{N(Missions.NextBonus)}";
        }

        // =========================================================== Hangar
        RectTransform BuildHangar()
        {
            var s = MakeScreen("Hangar"); Bg(s, Tok.Surface);
            var col = UI.Fill(UI.Rect(s, "Col"), 16, 16, 16, 16); UI.V(col.gameObject, 10);
            hangarCoins = Header(col, "HANGAR");
            for (int i = 0; i < Prog.Upgrades.Length; i++) UpgradeCard(col, i);
            return s;
        }

        void UpgradeCard(Transform col, int i)
        {
            var def = Prog.Upgrades[i];
            var card = UI.Img(col, "Up " + def.name, Tok.SurfaceRaised, Art.RoundRect, UI.RMd); UI.H(card.gameObject, 12, new RectOffset(14, 14, 12, 12)); UI.CardShadow(card);
            var ic = UI.Img(card.transform, "Icon", Tok.Asphalt, Art.Pill, UI.PillM(48)); UI.LE(ic, 48, 48);
            var g = UI.Pic(ic.transform, Art.Glyph(def.icon), 28); UI.Anchor(g.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(28, 28));
            var mid = UI.Rect(card.transform, "Mid"); UI.V(mid.gameObject, 4); UI.LE(mid, 0, -1, 1);
            var nm = UI.Text(mid, def.name, TS.Title, Tok.Ink); nm.fontSize = 16;
            UI.Text(mid, def.desc, TS.Caption, Tok.InkMuted);
            var pipsRt = UI.Rect(mid, "Pips"); var ph = UI.H(pipsRt.gameObject, 3); ph.childForceExpandWidth = true; UI.LE(pipsRt, -1, 6);
            var pips = new List<Themed>();
            for (int p = 0; p < def.max; p++) { var pip = UI.Img(pipsRt, "Pip", Tok.Border, Art.Pill, UI.PillM(6)); UI.LE(pip, -1, 6, 1); pips.Add(pip.GetComponent<Themed>()); }

            var right = UI.Rect(card.transform, "Right"); UI.V(right.gameObject, 6, null, TextAnchor.MiddleRight); UI.LE(right, 112, -1, 0).minWidth = 112;
            var eff = UI.Text(right, "", TS.Readout, Tok.SkyDeep, TextAlignmentOptions.Right); eff.fontSize = 12;
            var maxTag = UI.Tag(right, "MAXED", Tok.Go, Tok.OnGo);
            var buy = FButton.Make(right, "0", BK.Secondary, null, 44, Art.Picto("coin"));
            buy.onClick = () =>
            {
                var d = Save.D; int lvl = d.upgrades[i]; int cost = Prog.Cost(i, lvl);
                if (lvl >= def.max || d.coins < cost) return;
                d.coins -= cost; d.upgrades[i]++;
                foreach (var a in Prog.Check(null)) AchToast(a);
                Save.Write(); Sfx.I.Power(); RefreshHangar();
                var sp = card.GetComponent<Springy>(); if (sp == null) sp = card.gameObject.AddComponent<Springy>(); sp.Punch(2.5f);
            };
            hangarRefresh.Add(() =>
            {
                var d = Save.D; int lvl = d.upgrades[i]; bool maxed = lvl >= def.max; int cost = maxed ? 0 : Prog.Cost(i, lvl);
                eff.text = maxed ? def.effect(lvl) : $"{def.effect(lvl)} > {def.effect(lvl + 1)}";
                for (int p = 0; p < pips.Count; p++) pips[p].Set(p < lvl ? Tok.Signal : Tok.Border);
                maxTag.gameObject.SetActive(maxed); buy.gameObject.SetActive(!maxed);
                buy.SetText(N(cost)); buy.Interactable = d.coins >= cost;
            });
        }

        void RefreshHangar() { hangarCoins.text = N(Save.D.coins); foreach (var a in hangarRefresh) a(); }

        // =========================================================== Logbook: rank, route map, achievements
        TextMeshProUGUI logRank, logRankXp, routeNext, routeName; Image logXpFill, routeLine;
        readonly List<(Image node, Themed nodeT, Themed codeT)> routeNodes = new List<(Image, Themed, Themed)>();
        RectTransform routePulse;

        RectTransform BuildLog()
        {
            var s = MakeScreen("Logbook"); Bg(s, Tok.Surface);
            var col = UI.Fill(UI.Rect(s, "Col"), 16, 16, 16, 16); UI.V(col.gameObject, 10);
            logCoins = Header(col, "LOGBOOK");

            // pilot rank
            var rank = UI.Img(col, "Rank", Tok.Asphalt, Art.RoundRect, UI.RPanel); UI.V(rank.gameObject, 6, new RectOffset(16, 16, 12, 14)); UI.HudShadow(rank);
            var rr = UI.Row(rank.transform, 8);
            logRank = UI.Text(rr, "CADET · LV 1", TS.Heading, Tok.InkInverse); UI.LE(logRank, -1, -1, 1);
            logRankXp = UI.Text(rr, "", TS.Readout, Tok.Signal, TextAlignmentOptions.Right); logRankXp.fontSize = 12;
            var xt = UI.Img(rank.transform, "Xp", Tok.Fids, Art.Pill, UI.PillM(8)); UI.LE(xt, -1, 8);
            logXpFill = UI.Img(xt.transform, "Fill", Tok.Signal, Art.Pill, UI.PillM(8));
            var xf = logXpFill.rectTransform; xf.anchorMin = Vector2.zero; xf.anchorMax = new Vector2(0.3f, 1); xf.offsetMin = xf.offsetMax = Vector2.zero;
            var sr = UI.Row(rank.transform, 8); sr.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = true;
            logMiles = MiniStat(sr, "MILES"); logRuns = MiniStat(sr, "FLIGHTS"); logBest = MiniStat(sr, "BEST");

            // route map
            var route = UI.Img(col, "Route", Tok.Fids, Art.RoundRect, UI.RPanel); UI.V(route.gameObject, 8, new RectOffset(14, 14, 12, 14)); UI.HudShadow(route);
            var rh = UI.Row(route.transform, 8);
            var rl = UI.Text(rh, "ROUTE", TS.Label, Tok.InkInverseMuted); UI.LE(rl, -1, -1, 1);
            routeNext = UI.Text(rh, "", TS.Readout, Tok.Signal, TextAlignmentOptions.Right); routeNext.fontSize = 12;
            var trk = UI.Rect(route.transform, "Track"); UI.LE(trk, -1, 40);
            var baseLine = UI.Img(trk, "Line", Tok.FlapSplit); var bl = baseLine.rectTransform;
            bl.anchorMin = new Vector2(0.06f, 0.5f); bl.anchorMax = new Vector2(0.94f, 0.5f); bl.sizeDelta = new Vector2(0, 3); bl.anchoredPosition = Vector2.zero;
            routeLine = UI.Img(trk, "Done", Tok.Signal); var dl = routeLine.rectTransform;
            dl.anchorMin = new Vector2(0.06f, 0.5f); dl.anchorMax = new Vector2(0.06f, 0.5f); dl.sizeDelta = new Vector2(0, 3); dl.anchoredPosition = Vector2.zero;
            for (int i = 0; i < Prog.Zones.Length; i++)
            {
                var node = UI.Img(trk, "Node", Tok.FlapSplit, Art.Circle); var nr = node.rectTransform;
                float x = Mathf.Lerp(0.06f, 0.94f, i / (float)(Prog.Zones.Length - 1));
                nr.anchorMin = nr.anchorMax = new Vector2(x, 0.5f); nr.sizeDelta = new Vector2(30, 30); nr.anchoredPosition = Vector2.zero;
                var code = UI.Text(node.transform, Prog.Zones[i].gate, TS.Readout, Tok.InkInverseMuted, TextAlignmentOptions.Center); code.fontSize = 11; UI.Fill(code.rectTransform);
                routeNodes.Add((node, node.GetComponent<Themed>(), code.GetComponent<Themed>()));
            }
            routeName = UI.Text(route.transform, "", TS.Caption, Tok.InkInverse, TextAlignmentOptions.Center);

            // achievements
            var ah = UI.Row(col, 8);
            var al = UI.Text(ah, "ACHIEVEMENTS", TS.Label, Tok.InkMuted); UI.LE(al, -1, -1, 1);
            logCount = UI.Text(ah, "0 / 0", TS.Readout, Tok.SkyDeep, TextAlignmentOptions.Right);
            var vp = UI.Img(col, "Viewport", Tok.Surface, null, 1, 0f, true); UI.LE(vp, -1, -1, -1, 1); vp.gameObject.AddComponent<RectMask2D>();
            var content = UI.Rect(vp.transform, "Content"); content.anchorMin = new Vector2(0, 1); content.anchorMax = new Vector2(1, 1); content.pivot = new Vector2(0.5f, 1); content.sizeDelta = Vector2.zero;
            UI.V(content.gameObject, 8, new RectOffset(0, 0, 2, 12)); UI.Fit(content, false, true);
            var scroll = vp.gameObject.AddComponent<ScrollRect>(); scroll.viewport = vp.rectTransform; scroll.content = content; scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Elastic; scroll.scrollSensitivity = 30;
            RectTransform row = null;
            for (int i = 0; i < Prog.Achs.Length; i++)
            {
                if (i % 2 == 0) { row = UI.Row(content, 8); var hg = row.GetComponent<HorizontalLayoutGroup>(); hg.childForceExpandWidth = true; hg.childAlignment = TextAnchor.UpperLeft; }
                AchTile(row, Prog.Achs[i]);
            }

            // footer: rate + privacy
            var foot = UI.Row(col, 10, 44); foot.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = true;
            var rate = FButton.Make(foot, "RATE FLIGHTLINE", BK.Secondary, Review.OpenStore, 44); UI.LE(rate, 0, 44, 1); rate.gameObject.SetActive(Review.HasStorePage);
            var priv = FButton.Make(foot, "PRIVACY", BK.Ghost, Review.OpenPrivacy, 44); UI.LE(priv, 0, 44, 1);
            return s;
        }

        TextMeshProUGUI MiniStat(Transform p, string label)
        {
            var c = UI.Rect(p, label); UI.V(c.gameObject, 0); UI.LE(c, 0, -1, 1);
            var v = UI.Text(c, "0", TS.Readout, Tok.InkInverse); v.fontSize = 15;
            UI.Text(c, label, TS.Label, Tok.InkInverseMuted);
            return v;
        }

        void AchTile(Transform row, AchDef a)
        {
            var tile = UI.Img(row, a.id, Tok.SurfaceRaised, Art.RoundRect, UI.RMd); UI.LE(tile, 0, -1, 1); UI.CardShadow(tile);
            var h = UI.H(tile.gameObject, 10, new RectOffset(10, 10, 10, 10), TextAnchor.UpperLeft);
            var ic = UI.Img(tile.transform, "Icon", Tok.Asphalt, Art.Pill, UI.PillM(34)); UI.LE(ic, 34, 34);
            var g = UI.Pic(ic.transform, Art.Glyph("lock"), 20); UI.Anchor(g.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(20, 20));
            var v = UI.Rect(tile.transform, "Text"); UI.V(v.gameObject, 2); UI.LE(v, 0, -1, 1);
            var nm = UI.Text(v, a.name, TS.Title, Tok.Ink, TextAlignmentOptions.Left, true); nm.fontSize = 13;
            var sub = UI.Text(v, "", TS.Caption, Tok.InkMuted, TextAlignmentOptions.Left, true); sub.fontSize = 11;
            var nT = nm.GetComponent<Themed>(); var icT = ic.GetComponent<Themed>();
            logRefresh.Add(() =>
            {
                bool done = Save.D.ach.Contains(a.id);
                g.sprite = Art.Glyph(done ? "trophy" : "lock");
                icT.Set(done ? Tok.Asphalt : Tok.BorderStrong);
                nT.Set(done ? Tok.Ink : Tok.InkMuted);
                sub.text = done ? $"+{N(a.reward)} earned" : $"{a.desc} +{N(a.reward)}";
            });
        }

        void RefreshLog()
        {
            var d = Save.D;
            logCoins.text = N(d.coins); logMiles.text = N(d.totalMiles); logRuns.text = N(d.runs); logBest.text = N(d.bestScore);
            int need = Prog.XpToNext(d.level);
            logRank.text = $"{Prog.Rank(d.level)} · LV {d.level}";
            logRankXp.text = $"{N(d.xp)}/{N(need)} XP";
            logXpFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01((float)d.xp / need), 1);
            logCount.text = $"{d.ach.Count} / {Prog.Achs.Length}";

            int reached = Mathf.Clamp(d.maxZone, 0, Prog.Zones.Length - 1), next = reached + 1;
            routePulse = null;
            for (int i = 0; i < routeNodes.Count; i++)
            {
                var (node, nodeT, codeT) = routeNodes[i];
                node.rectTransform.localScale = Vector3.one;
                if (i <= reached) { nodeT.Set(Tok.Signal); codeT.Set(Tok.OnSignal); }
                else if (i == next) { nodeT.Set(Tok.Asphalt); codeT.Set(Tok.Signal); routePulse = node.rectTransform; }
                else { nodeT.Set(Tok.FlapSplit); codeT.Set(Tok.InkInverseMuted); }
            }
            float rx = Mathf.Lerp(0.06f, 0.94f, reached / (float)(Prog.Zones.Length - 1));
            routeLine.rectTransform.anchorMax = new Vector2(rx, 0.5f);
            if (next < Prog.Zones.Length)
            {
                var z = Prog.Zones[next];
                routeNext.text = $"NEXT {N(z.start)} MI";
                routeName.text = $"{z.name} · +{N(Prog.ArrivalReward(next))} first arrival";
            }
            else { routeNext.text = "ALL FLOWN"; routeName.text = "Every route flown. Chase your best."; }
            foreach (var a in logRefresh) a();
        }

        // =========================================================== navigation
        public void ShowTitle()
        {
            bool fromFlight = scrOver.gameObject.activeSelf; int g = ++overGen;
            scrHud.gameObject.SetActive(false); scrPause.gameObject.SetActive(false); scrOver.gameObject.SetActive(false);
            Go(scrTitle, current == null ? 0 : -1);
            if (fromFlight && Review.ShouldShowCard()) StartCoroutine(Delay(0.6f, () => { if (g == overGen && current == scrTitle && game.state == GState.Menu) { Review.MarkCardShown(); Overlay(scrRate); } }));
        }

        public void OnRunStart()
        {
            FinishSlide(); overGen++;
            foreach (var s in new[] { scrTitle, scrHangar, scrLog, scrMissions, scrOver, scrPause, scrRate }) { s.gameObject.SetActive(false); s.anchoredPosition = Vector2.zero; }
            current = null; lastScore = -1; lastMiles = -1; lastMult = -1; lastCoins = -1;
            shieldTenths = magnetTenths = boostTenths = headMiles = -1;
            scrHud.gameObject.SetActive(true);
            shieldG.root.gameObject.SetActive(false); magnetG.root.gameObject.SetActive(false); boostG.root.gameObject.SetActive(false);
        }

        public void OnCrash() { }

        public void Go(RectTransform to, int dir)
        {
            if (to == scrHangar) RefreshHangar();
            if (to == scrLog) RefreshLog();
            if (to == scrTitle) RefreshTitle();
            if (to == scrMissions) RefreshMissionsScreen();
            FinishSlide();
            var from = current; current = to;
            if (from == to) return;
            to.gameObject.SetActive(true);
            if (dir == 0 || from == null)
            {
                to.anchoredPosition = Vector2.zero; to.GetComponent<CanvasGroup>().alpha = 1;
                if (from != null) from.gameObject.SetActive(false);
                RunStagger(to.Find("Col") as RectTransform, 0f);
                return;
            }
            slideFrom = from; slideTo = to; slideCo = StartCoroutine(Slide(from, to, dir));
            RunStagger(to.Find("Col") as RectTransform, 0.08f);
        }

        // One stagger per column: a rapid re-entry restarts it instead of two fighting over alpha/scale.
        void RunStagger(RectTransform col, float delay)
        {
            if (col == null) return;
            if (staggerCos.TryGetValue(col, out var old) && old != null) StopCoroutine(old);
            staggerCos[col] = StartCoroutine(Stagger(col, delay));
        }

        // Panels settle in one after another with a soft overshoot.
        IEnumerator Stagger(RectTransform col, float delay)
        {
            if (col == null) yield break;
            int n = col.childCount; var items = new Transform[n]; var cgs = new CanvasGroup[n];
            for (int i = 0; i < n; i++)
            {
                items[i] = col.GetChild(i);
                if (items[i].GetComponent<FButton>() == null) { cgs[i] = items[i].GetComponent<CanvasGroup>(); if (cgs[i] == null) cgs[i] = items[i].gameObject.AddComponent<CanvasGroup>(); cgs[i].alpha = 0f; }
                items[i].localScale = Vector3.one * 0.94f;
            }
            float t = 0, total = delay + n * 0.045f + 0.35f;
            while (t < total)
            {
                t += Time.unscaledDeltaTime;
                for (int i = 0; i < n; i++)
                {
                    float k = Mathf.Clamp01((t - delay - i * 0.045f) / 0.35f);
                    if (cgs[i]) cgs[i].alpha = Mathf.Clamp01(k * 2f);
                    items[i].localScale = Vector3.one * Mathf.LerpUnclamped(0.94f, 1f, UI.Back(k, 1.6f));
                }
                yield return null;
            }
            for (int i = 0; i < n; i++) { if (cgs[i]) cgs[i].alpha = 1f; items[i].localScale = Vector3.one; }
        }

        void FinishSlide()
        {
            if (slideCo == null) return;
            StopCoroutine(slideCo); slideCo = null;
            if (slideTo) slideTo.anchoredPosition = Vector2.zero;
            if (slideFrom) { slideFrom.gameObject.SetActive(false); slideFrom.anchoredPosition = Vector2.zero; var c = slideFrom.GetComponent<CanvasGroup>(); if (c) c.alpha = 1f; }
        }

        IEnumerator Slide(RectTransform from, RectTransform to, int dir)
        {
            float w = safeRt.rect.width, t = 0;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / 0.32f; float e = UI.Back(t, 0.9f), eo = UI.OutCubic(t);
                to.anchoredPosition = new Vector2(dir * w * (1 - e), 0); from.anchoredPosition = new Vector2(-dir * w * eo * 0.35f, 0);
                var fcg = from.GetComponent<CanvasGroup>(); if (fcg) fcg.alpha = 1f - eo;
                yield return null;
            }
            to.anchoredPosition = Vector2.zero; from.gameObject.SetActive(false); from.anchoredPosition = Vector2.zero; slideCo = null;
            var cgf = from.GetComponent<CanvasGroup>(); if (cgf) cgf.alpha = 1f;
        }

        void Overlay(RectTransform s)
        {
            s.gameObject.SetActive(true); s.SetAsLastSibling(); popLayer.SetAsLastSibling(); toastLayer.SetAsLastSibling();
            StartCoroutine(Fade(s));
        }

        IEnumerator Fade(RectTransform s)
        {
            var cg = s.GetComponent<CanvasGroup>(); float t = 0;
            Transform card = s.Find("Card"); if (card == null) card = s.Find("Col");
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / 0.38f;
                cg.alpha = UI.OutCubic(t * 1.6f);
                if (card) { card.localScale = Vector3.one * Mathf.LerpUnclamped(0.88f, 1f, UI.Back(t, 1.5f)); card.localRotation = Quaternion.Euler(0, 0, (1f - UI.OutCubic(t)) * -2.5f); }
                yield return null;
            }
            cg.alpha = 1; if (card) { card.localScale = Vector3.one; card.localRotation = Quaternion.identity; }
        }

        // =========================================================== toasts + pops
        void BuildToast()
        {
            var p = UI.Img(toastLayer, "Toast", Tok.Fids, Art.Pill, 1.1f); toastRt = p.rectTransform;
            UI.Anchor(toastRt, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -140), Vector2.zero);
            UI.V(p.gameObject, 2, new RectOffset(28, 28, 10, 12), TextAnchor.UpperCenter); UI.Fit(p, true, true); UI.HudShadow(p);
            toastMain = UI.Text(p.transform, "", TS.Button, Tok.Signal, TextAlignmentOptions.Center);
            toastSub = UI.Text(p.transform, "", TS.Caption, Tok.InkInverseMuted, TextAlignmentOptions.Center);
            toastCg = p.gameObject.AddComponent<CanvasGroup>(); toastCg.blocksRaycasts = false; toastCg.interactable = false;
            p.gameObject.SetActive(false);
        }

        public void Toast(string main, string sub) { toasts.Enqueue((main, sub)); if (!toasting) StartCoroutine(RunToasts()); }

        // Toast rest height for the current context, read every frame so a menu toast glides down below the HUD when a run starts.
        float toastY;
        float ToastBaseY
        {
            get
            {
                float target = scrOver.gameObject.activeSelf || scrTitle.gameObject.activeSelf ? -20f : -290f; // in flight: below the HUD gauges
                toastY = Mathf.Lerp(toastY, target, 1f - Mathf.Exp(-12f * Time.unscaledDeltaTime));
                return toastY;
            }
        }

        public void AchToast(AchDef a) => Toast($"LOGBOOK · {a.name}", $"{a.desc} +{N(a.reward)} coins.");

        IEnumerator RunToasts()
        {
            toasting = true;
            while (toasts.Count > 0)
            {
                var (m, s) = toasts.Dequeue();
                toastMain.text = m; toastSub.text = s; toastSub.gameObject.SetActive(!string.IsNullOrEmpty(s));
                toastRt.gameObject.SetActive(true);
                toastY = scrOver.gameObject.activeSelf || scrTitle.gameObject.activeSelf ? -20f : -290f;
                float t = 0;
                while (t < 1f)
                {
                    t += Time.unscaledDeltaTime / 0.42f;
                    toastCg.alpha = UI.OutCubic(t * 2f);
                    toastRt.anchoredPosition = new Vector2(0, ToastBaseY + 26f * (1f - UI.Back(t, 1.8f)));
                    toastRt.localScale = Vector3.one * Mathf.LerpUnclamped(0.8f, 1f, UI.Back(t, 2.2f));
                    yield return null;
                }
                toastRt.localScale = Vector3.one;
                float hold = toasts.Count > 0 ? 1.4f : 2.2f, ht = 0;
                while (ht < hold) { ht += Time.unscaledDeltaTime; toastRt.anchoredPosition = new Vector2(0, ToastBaseY + Mathf.Sin(ht * 2.4f) * 2f); yield return null; } // gentle float
                t = 0f;
                while (t < 1f) { t += Time.unscaledDeltaTime / 0.3f; toastCg.alpha = 1f - UI.OutCubic(t); toastRt.anchoredPosition = new Vector2(0, ToastBaseY + 14f * UI.OutCubic(t)); toastRt.localScale = Vector3.one * Mathf.Lerp(1f, 0.94f, t); yield return null; }
                toastRt.gameObject.SetActive(false);
            }
            toasting = false;
        }

        public void Pop(Vector3 world, string text, Tok tok)
        {
            PopChip chip = null;
            foreach (var p in pops) if (!p.gameObject.activeSelf) { chip = p; break; }
            if (chip == null)
            {
                var img = UI.Img(popLayer, "Pop", Tok.Asphalt, Art.Pill, UI.PillM(24));
                UI.H(img.gameObject, 0, new RectOffset(12, 12, 5, 5), TextAnchor.MiddleCenter); UI.Fit(img, true, true);
                var rt = img.rectTransform; rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f); rt.pivot = new Vector2(0.5f, 0.5f);
                chip = img.gameObject.AddComponent<PopChip>(); chip.rt = rt; chip.cg = img.gameObject.AddComponent<CanvasGroup>(); chip.cg.blocksRaycasts = false;
                chip.text = UI.Text(img.transform, "", TS.Label, tok, TextAlignmentOptions.Center);
                pops.Add(chip);
            }
            var sp = game.cam.WorldToScreenPoint(world);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(popLayer, sp, null, out var lp);
            int stack = 0; foreach (var p in pops) if (p != chip && p.gameObject.activeSelf) stack++;
            lp.y += 30f * stack; // stack instead of overlapping
            float hw = popLayer.rect.width * 0.5f - 100f; lp.x = Mathf.Clamp(lp.x, -hw, hw); // keep on screen
            chip.Show(lp, text, tok);
        }
    }
}
