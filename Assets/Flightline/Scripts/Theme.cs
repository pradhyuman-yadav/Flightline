using System;
using UnityEngine;
using UnityEngine.UI;

namespace Flightline
{
    // Flightline design-system color tokens (Day / Night).
    public enum Tok
    {
        Surface, SurfaceRaised, Asphalt, Fids, FlapSplit, Border, BorderStrong,
        Ink, InkMuted, InkInverse, InkInverseMuted, Signal, SignalPressed, OnSignal,
        Sky, SkyHorizon, Cloud, SkyDeep, OnSkyDeep, RunwayWhite,
        Go, OnGo, Caution, OnCaution, Stop, OnStop, HudGo, HudCaution, HudStop, FocusRing, PressShadow
    }

    public static class Theme
    {
        static readonly string[] DayHex =
        {
            "#f2f5f8", "#ffffff", "#262d34", "#121417", "#2f353c", "#d5dde4", "#7b8a98",
            "#111820", "#4b5966", "#f5f8fa", "#a9b6c2", "#ffcc00", "#e0b300", "#111820",
            "#4aa8e0", "#cdeaf9", "#ffffff", "#0a5a92", "#ffffff", "#ffffff",
            "#18794a", "#ffffff", "#a85200", "#ffffff", "#c42b22", "#ffffff", "#45d98a", "#ffa345", "#ff7a6e", "#0a5a92", "#8a6e00"
        };
        static readonly string[] NightHex =
        {
            "#0b121a", "#16212c", "#2c3640", "#121417", "#2f353c", "#263341", "#6b7d8f",
            "#eef3f7", "#9db0c1", "#f5f8fa", "#b4c0cb", "#ffcc00", "#e0b300", "#111820",
            "#1d3f63", "#2a5680", "#4a6683", "#5cb8f0", "#0b121a", "#e8edf1",
            "#45d98a", "#0b121a", "#ffa345", "#0b121a", "#ff6b5e", "#0b121a", "#45d98a", "#ffa345", "#ff7a6e", "#ffcc00", "#8a6e00"
        };

        static Color[] day, night;
        public static bool Night { get; private set; }
        public static event Action Changed;

        static Color[] Parse(string[] a)
        {
            var c = new Color[a.Length];
            for (int i = 0; i < a.Length; i++) ColorUtility.TryParseHtmlString(a[i], out c[i]);
            return c;
        }

        public static Color Get(Tok t) => Get(t, Night);

        public static Color Get(Tok t, bool nightTheme)
        {
            if (day == null) { day = Parse(DayHex); night = Parse(NightHex); }
            return (nightTheme ? night : day)[(int)t];
        }

        public static Color Get(Tok t, float alpha) { var c = Get(t); c.a = alpha; return c; }

        public static void SetNight(bool n)
        {
            if (n == Night) return;
            Night = n;
            Changed?.Invoke();
        }
    }

    // Binds a Graphic or SpriteRenderer color to a token and re-applies on theme change.
    public class Themed : MonoBehaviour
    {
        public Tok tok;
        public float alpha = 1f;
        Graphic g; SpriteRenderer sr; bool init;

        void OnEnable() { Theme.Changed += Apply; Apply(); }
        void OnDisable() { Theme.Changed -= Apply; }

        public void Set(Tok t, float a = 1f) { tok = t; alpha = a; Apply(); }

        public void Apply()
        {
            if (!init) { g = GetComponent<Graphic>(); sr = GetComponent<SpriteRenderer>(); init = true; }
            var c = Theme.Get(tok); c.a *= alpha;
            if (g != null) g.color = c;
            if (sr != null) sr.color = c;
        }
    }
}
