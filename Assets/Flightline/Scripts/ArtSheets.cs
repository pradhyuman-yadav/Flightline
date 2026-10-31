using System.Collections.Generic;
using UnityEngine;

namespace Flightline
{
    // Pixel-art animations (Resources/Sheets/<name>.png): a horizontal strip of square frames, made in Pixel Lab.
    // Every airframe, pickup and hazard is drawn from these; the generated sprites in Art.cs are UI, sky and effects.
    public static partial class Art
    {
        public const float SheetFps = 1f / 0.13f; // Pixel Lab's export timing, so the loops play as they were made
        static readonly Dictionary<string, Sprite[]> Sheets = new Dictionary<string, Sprite[]>();

        // size: world units across one frame (sized to match the sprite it replaced); pivot in frame space.
        // Frames are square unless frameW is given (the runway is one tall frame per step).
        public static Sprite[] Sheet(string name, float size, Vector2? pivot = null, int frameW = 0)
        {
            if (Sheets.TryGetValue(name, out var f) && f[0] != null) return f; // same survival check as Make's cache
            var tex = Resources.Load<Texture2D>("Sheets/" + name);
            tex.filterMode = FilterMode.Point;
            int h = tex.height, w = frameW > 0 ? frameW : h, n = Mathf.Max(1, tex.width / w);
            f = new Sprite[n];
            for (int k = 0; k < n; k++)
            {
                f[k] = Sprite.Create(tex, new Rect(k * w, 0, w, h), pivot ?? new Vector2(0.5f, 0.5f), w / size, 0, SpriteMeshType.FullRect);
                f[k].name = name + k;
            }
            return Sheets[name] = f;
        }

        public static int SheetIndex(int n, float time) { int k = (int)(time * SheetFps) % n; return k < 0 ? k + n : k; }
        public static Sprite SheetFrame(Sprite[] f, float time) => f[SheetIndex(f.Length, time)];

        // ---------- Fleet: top-down and nose up, in a 1.5-unit frame (engine and contrail points are in that frame's -1..1 space)
        public static Sprite[] PlaneFrames(int i) => Sheet(Prog.Planes[Mathf.Clamp(i, 0, Prog.Planes.Length - 1)].art, 1.5f);
        public static Sprite PlaneArt(int i) => PlaneFrames(i)[0];

        // Crash debris: the airframe's first frame cut along jagged break lines into wings, nose, centre section and tail.
        // Each piece pivots on its own centre (so it tumbles in place) and is scorched along the tear.
        public static readonly Vector2[] PieceCenter = { new Vector2(-0.55f, 0.02f), new Vector2(0.55f, 0.02f), new Vector2(0f, 0.42f), new Vector2(0f, -0.15f), new Vector2(0f, -0.72f) };
        static readonly Color CChar = Rgb(0x2b2522);

        static int PieceOf(Vector2 p)
        {
            float jx = 0.035f * Mathf.Sin(p.y * 37f), jy = 0.03f * Mathf.Sin(p.x * 41f + 1f);
            if (p.y < -0.48f + jy) return 4;
            if (p.x < -0.17f + jx) return 0;
            if (p.x > 0.17f + jx) return 1;
            return p.y > 0.14f + jy ? 2 : 3;
        }

        public static Sprite PlanePiece(int plane, int i)
        {
            var src = PlaneArt(plane); var tex = src.texture; var r = src.rect; int n = (int)r.height;
            var s = Make("planep" + plane + "_" + i, n, n, n / 1.5f, (p, px) =>
            {
                if (PieceOf(p) != i) return new Color(1, 1, 1, 0);
                var c = tex.GetPixel((int)r.x + Mathf.Clamp((int)((p.x + 1f) * 0.5f * n), 0, n - 1), (int)r.y + Mathf.Clamp((int)((p.y + 1f) * 0.5f * n), 0, n - 1));
                const float e = 0.035f;
                bool edge = PieceOf(p + new Vector2(e, 0)) != i || PieceOf(p - new Vector2(e, 0)) != i || PieceOf(p + new Vector2(0, e)) != i || PieceOf(p - new Vector2(0, e)) != i;
                if (edge) { float a = c.a; c = Color.Lerp(c, CChar, 0.75f); c.a = a; }
                return c;
            }, new Vector2(0.5f + PieceCenter[i].x * 0.5f, 0.5f + PieceCenter[i].y * 0.5f));
            s.texture.filterMode = FilterMode.Point;
            return s;
        }

        // ---------- Scenery
        // Runway: one 64 x 244 frame per step (start with the runway number, two lit sections, far-end threshold), 4 units wide.
        // Pivot at the start end, so its position is where the runway begins.
        public static Sprite[] RunwayFrames => Sheet("runway", 4f, new Vector2(0.5f, 0f), 64);
        public static Sprite[] CloudFrames => Sheet("cloud", 2.6f);

        // ---------- Pickups and hazards
        public static Sprite[] CoinFrames => Sheet("coin", 0.7f);
        public static Sprite[] FuelFrames => Sheet("fuel", 1.05f);
        public static Sprite[] ShieldFrames => Sheet("shield", 1f);
        public static Sprite[] MagnetFrames => Sheet("magnet", 1f);
        public static Sprite[] BoostFrames => Sheet("boost", 1f);
        public static Sprite[] DroneFrames => Sheet("drone", 1.3f);
        public static Sprite[] StormFrames => Sheet("storm", 1.6f);
        public static Sprite[] BalloonFrames => Sheet("balloon", 1.5f, new Vector2(0.5f, 0.695f)); // pivot on the envelope, where the hit circle sits

        // Charging storm cell: idle grumble, a crackling charge, then the burst (played once from BurstFrom at the strike).
        public static Sprite[] CellIdleFrames => Sheet("cellidle", 1.6f);
        public static Sprite[] CellChargeFrames => Sheet("cellcharge", 1.6f);
        public static Sprite[] CellBurstFrames => Sheet("cellburst", 1.6f);
        public const int CellBurstFrom = 8; // the white flash; earlier frames are the sheet's own charge-up

        // Live arc: emitter clouds, the spark riding a sweeping arm, and the beam texture laid along the arc.
        public static Sprite[] ArcNodeFrames => Sheet("arcnode", 1.1f);
        public static Sprite[] ArcTipFrames => Sheet("arctip", 0.6f);

        // Beam: a vertical atlas of 64x40 slices (1px clear padding above and below each), cut into one repeating texture
        // per frame so a frame can tile along the line (the sprite shader ignores texture offsets, so no atlas scrolling).
        public const int BeamSlice = 42, BeamBand = 40;
        public const float BeamWidth = 0.5f;
        static Texture2D[] beam;
        public static Texture2D[] BeamFrames
        {
            get
            {
                if (beam != null && beam[0] != null) return beam;
                var atlas = Resources.Load<Texture2D>("Sheets/arcbeam");
                int w = atlas.width, n = atlas.height / BeamSlice;
                beam = new Texture2D[n];
                for (int k = 0; k < n; k++)
                {
                    var t = new Texture2D(w, BeamBand, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapModeU = TextureWrapMode.Repeat, wrapModeV = TextureWrapMode.Clamp, name = "arcbeam" + k };
                    t.SetPixels(atlas.GetPixels(0, atlas.height - (k * BeamSlice + 1) - BeamBand, w, BeamBand));
                    t.Apply(false, true);
                    beam[k] = t;
                }
                return beam;
            }
        }
    }
}
