using UnityEngine;

namespace Flightline
{
    // The fleet, top-down and nose up, all in the same 256px / 1.5-unit frame as the original plane.
    public static partial class Art
    {
        public static Sprite PlaneArt(int i) => Make("fleet" + i, 256, 256, 170, (p, px) => FleetPx(i, p, px));

        // Propeller seen spinning: a faint blur disc, two blades and a spinner. Rotated at runtime.
        public static Sprite Prop => Make("prop", 64, 64, 64, (p, px) =>
        {
            var o = new Px(CDark);
            o.Add(G(0.15f, 0.18f), Cov(p.magnitude - 0.95f, px));
            o.Add(CDark, Cov(Seg(p, new Vector2(-0.9f, 0), new Vector2(0.9f, 0), 0.085f), px));
            o.Add(G(0.85f), Cov(p.magnitude - 0.16f, px));
            return o.c;
        });

        static readonly Color CPaper = Rgb(0xf7f7f2), CFold = Rgb(0xcfd1cd), CWood = Rgb(0xdcb680), CWoodDk = Rgb(0xa77a4c), CClay = Rgb(0xb8483a),
            CRed = Rgb(0xd9342b), CYellow = Rgb(0xf3cc4d), CGlass = Rgb(0x3a5468), CGlassLt = Rgb(0x8db3c9), COlive = Rgb(0x5b6b47), COliveLt = Rgb(0x75855c),
            CNavy = Rgb(0x1a2e5c), CBlue = Rgb(0x2e5fa8), COrange = Rgb(0xf08a24), CMetal = Rgb(0xa9b0b8), CEngine = Rgb(0x8c939c), CStealth = Rgb(0x5a6068), CGold = Rgb(0xc9a14a);

        static readonly Vector2[] PaperHalf = { new Vector2(0f, 0.95f), new Vector2(0.62f, -0.72f), new Vector2(0.5f, -0.8f), new Vector2(0f, -0.58f) };
        static readonly Vector2[] BizWing = { new Vector2(0.1f, 0.12f), new Vector2(0.86f, -0.24f), new Vector2(0.9f, -0.32f), new Vector2(0.1f, -0.1f) };
        static readonly Vector2[] BizStab = { new Vector2(0.04f, -0.74f), new Vector2(0.36f, -0.86f), new Vector2(0.36f, -0.92f), new Vector2(0.04f, -0.86f) };
        static readonly Vector2[] JumboWing = { new Vector2(0.12f, 0.22f), new Vector2(0.98f, -0.34f), new Vector2(0.98f, -0.42f), new Vector2(0.12f, -0.2f) };
        static readonly Vector2[] JumboStab = { new Vector2(0.08f, -0.6f), new Vector2(0.42f, -0.8f), new Vector2(0.42f, -0.87f), new Vector2(0.08f, -0.76f) };
        static readonly Vector2[] StealthPts =
        {
            new Vector2(0f, 0.95f), new Vector2(0.18f, 0.45f), new Vector2(0.85f, -0.25f), new Vector2(0.85f, -0.38f), new Vector2(0.35f, -0.42f), new Vector2(0.5f, -0.82f),
            new Vector2(0.28f, -0.9f), new Vector2(0.12f, -0.72f), new Vector2(-0.12f, -0.72f), new Vector2(-0.28f, -0.9f), new Vector2(-0.5f, -0.82f), new Vector2(-0.35f, -0.42f),
            new Vector2(-0.85f, -0.38f), new Vector2(-0.85f, -0.25f), new Vector2(-0.18f, 0.45f),
        };

        static float Sym(Vector2 p, Vector2[] half) => Poly(new Vector2(Mathf.Abs(p.x), p.y), half); // mirrored shape
        static Color Mul(Color c, float k) => new Color(c.r * k, c.g * k, c.b * k, c.a);
        static float Ellipse(Vector2 p, Vector2 c, Vector2 r) { var q = new Vector2((p.x - c.x) / r.x, (p.y - c.y) / r.y); return (q.magnitude - 1f) * Mathf.Min(r.x, r.y); }
        // the sun is to the upper left: the right side of a rounded fuselage sits in shade
        static void ShadeRight(ref Px o, Vector2 p, float d, float px) => o.Add(G(0.3f, 0.22f), Cov(Mathf.Max(d, -(p.x - 0.01f)), px));

        static Color FleetPx(int i, Vector2 p, float px)
        {
            switch (i)
            {
                case 0: // paper dart: one sheet, centre fold, the right wing angled away from the light
                {
                    var o = new Px(CPaper);
                    float b = Sym(p, PaperHalf);
                    o.Add(CPaper, Cov(b, px));
                    o.Add(Mul(CPaper, 0.88f), Cov(Mathf.Max(b, -p.x), px));
                    o.Add(CFold, Cov(Mathf.Max(b + 0.01f, Seg(p, new Vector2(0, 0.93f), new Vector2(0, -0.6f), 0.012f)), px));
                    for (int s = -1; s <= 1; s += 2)
                        o.Add(CFold, Cov(Mathf.Max(b + 0.01f, Seg(p, new Vector2(0, 0.88f), new Vector2(s * 0.27f, -0.68f), 0.008f)), px) * 0.8f);
                    return o.c;
                }
                case 1: // balsa glider: long wing, stick fuselage, clay nose weight, wood grain
                {
                    float gr = 0.96f + 0.04f * Mathf.Sin(p.y * 110f + Mathf.Sin(p.x * 6f) * 3f); // grain runs along the span
                    var o = new Px(CWood);
                    float wing = Box(p, new Vector2(0, 0.22f), new Vector2(0.96f, 0.13f), 0.12f), tail = Box(p, new Vector2(0, -0.74f), new Vector2(0.36f, 0.08f), 0.06f);
                    float fus = Seg(p, new Vector2(0, -0.88f), new Vector2(0, 0.8f), 0.055f);
                    o.Add(Mul(CWood, gr * 0.94f), Cov(tail, px));
                    o.Add(Mul(CWood, gr), Cov(wing, px));
                    o.Add(Mul(CWood, gr * 0.86f), Cov(Mathf.Max(wing, p.y - 0.14f), px)); // trailing edge
                    o.Add(CWoodDk, Cov(fus, px));
                    o.Add(CClay, Cov(Seg(p, new Vector2(0, 0.72f), new Vector2(0, 0.9f), 0.085f), px));
                    ShadeRight(ref o, p, fus, px);
                    return o.c;
                }
                case 2: // RC trainer: red high wing with a white centre panel, white fuselage, glass canopy
                {
                    var o = new Px(CPaper);
                    float wing = Box(p, new Vector2(0, 0.22f), new Vector2(0.95f, 0.15f), 0.07f), tail = Box(p, new Vector2(0, -0.74f), new Vector2(0.34f, 0.09f), 0.05f);
                    float fus = Seg(p, new Vector2(0, -0.84f), new Vector2(0, 0.8f), 0.1f);
                    o.Add(CRed, Cov(tail, px));
                    o.Add(CPaper, Cov(fus, px));
                    o.Add(CRed, Cov(Seg(p, new Vector2(0, -0.84f), new Vector2(0, 0.8f), 0.03f), px));
                    o.Add(CRed, Cov(Seg(p, new Vector2(0, 0.7f), new Vector2(0, 0.82f), 0.1f), px));
                    o.Add(CGlass, Cov(Seg(p, new Vector2(0, 0.44f), new Vector2(0, 0.56f), 0.06f), px));
                    o.Add(CRed, Cov(wing, px));
                    o.Add(CPaper, Cov(Mathf.Max(wing, Mathf.Abs(p.x) - 0.3f), px));
                    ShadeRight(ref o, p, Mathf.Min(fus, wing), px);
                    return o.c;
                }
                case 3: // biplane: lower wing peeking out behind the upper, red fuselage, open cockpit
                {
                    var o = new Px(CYellow);
                    float lower = Box(p, new Vector2(0, 0.06f), new Vector2(0.86f, 0.14f), 0.05f), upper = Box(p, new Vector2(0, 0.34f), new Vector2(0.96f, 0.15f), 0.05f);
                    float fus = Seg(p, new Vector2(0, -0.82f), new Vector2(0, 0.74f), 0.12f);
                    o.Add(Mul(CYellow, 0.72f), Cov(lower, px));
                    o.Add(CYellow, Cov(Box(p, new Vector2(0, -0.74f), new Vector2(0.33f, 0.09f), 0.04f), px));
                    o.Add(CRed, Cov(fus, px));
                    o.Add(CDark, Cov(Circ(p, new Vector2(0, -0.08f), 0.075f), px));
                    o.Add(CRed, Cov(Seg(p, new Vector2(0, -0.62f), new Vector2(0, -0.9f), 0.025f), px));
                    o.Add(CMetal, Cov(Seg(p, new Vector2(0, 0.66f), new Vector2(0, 0.8f), 0.13f), px));
                    o.Add(Mul(CYellow, 0.95f + 0.05f * Mathf.Cos(p.x * 32f)), Cov(upper, px)); // fabric over ribs
                    o.Add(CDark, Cov(Mathf.Max(upper + 0.01f, Mathf.Min(Circ(p, new Vector2(-0.6f, 0.34f), 0.025f), Circ(p, new Vector2(0.6f, 0.34f), 0.025f))), px)); // strut heads
                    ShadeRight(ref o, p, Mathf.Min(fus, upper), px);
                    return o.c;
                }
                case 4: // prop fighter: elliptical wings, mottled olive camouflage, roundels, bubble canopy
                {
                    var cam = Fbm(p * 3.5f) > 0.02f ? COliveLt : COlive;
                    var o = new Px(cam);
                    float wing = Ellipse(p, new Vector2(0, 0.12f), new Vector2(0.96f, 0.21f)), tail = Ellipse(p, new Vector2(0, -0.74f), new Vector2(0.34f, 0.1f));
                    float fus = Seg(p, new Vector2(0, -0.84f), new Vector2(0, 0.8f), 0.115f);
                    o.Add(cam, Cov(tail, px));
                    o.Add(cam, Cov(wing, px));
                    o.Add(cam, Cov(fus, px));
                    for (int s = -1; s <= 1; s += 2)
                    {
                        var rc = new Vector2(s * 0.55f, 0.12f);
                        o.Add(CBlue, Cov(Circ(p, rc, 0.085f), px)); o.Add(CPaper, Cov(Circ(p, rc, 0.055f), px)); o.Add(CRed, Cov(Circ(p, rc, 0.03f), px));
                        o.Add(CDark, Cov(Seg(p, new Vector2(s * 0.1f, 0.5f), new Vector2(s * 0.1f, 0.62f), 0.015f), px)); // exhaust stubs
                    }
                    o.Add(CGlassLt, Cov(Seg(p, new Vector2(0, 0.12f), new Vector2(0, 0.32f), 0.055f), px));
                    o.Add(CYellow, Cov(Circ(p, new Vector2(0, 0.86f), 0.06f), px));
                    ShadeRight(ref o, p, Mathf.Min(fus, wing), px);
                    return o.c;
                }
                case 5: // bush plane: white high wing with blue leading edge and orange tips
                {
                    var o = new Px(CPaper);
                    float wing = Box(p, new Vector2(0, 0.24f), new Vector2(0.97f, 0.12f), 0.04f), fus = Seg(p, new Vector2(0, -0.86f), new Vector2(0, 0.8f), 0.12f);
                    o.Add(CPaper, Cov(Box(p, new Vector2(0, -0.75f), new Vector2(0.32f, 0.08f), 0.04f), px));
                    o.Add(CPaper, Cov(fus, px));
                    o.Add(CBlue, Cov(Seg(p, new Vector2(0, -0.62f), new Vector2(0, -0.9f), 0.025f), px));
                    o.Add(CGlass, Cov(Seg(p, new Vector2(0, 0.42f), new Vector2(0, 0.5f), 0.08f), px));
                    o.Add(CGlass, Cov(Seg(p, new Vector2(0, 0.03f), new Vector2(0, 0.09f), 0.07f), px));
                    o.Add(CMetal, Cov(Seg(p, new Vector2(0, 0.68f), new Vector2(0, 0.8f), 0.12f), px));
                    o.Add(CPaper, Cov(wing, px));
                    o.Add(CBlue, Cov(Mathf.Max(wing, 0.31f - p.y), px));
                    o.Add(COrange, Cov(Mathf.Max(wing, 0.82f - Mathf.Abs(p.x)), px));
                    ShadeRight(ref o, p, Mathf.Min(fus, wing), px);
                    return o.c;
                }
                case 6: // biz jet: swept wings with winglets, tail-mounted engines, T-tail, navy spine
                {
                    var o = new Px(CPaper);
                    float wing = Sym(p, BizWing), stab = Sym(p, BizStab), fus = Seg(p, new Vector2(0, -0.84f), new Vector2(0, 0.82f), 0.12f);
                    o.Add(Mul(CPaper, 0.93f), Cov(wing, px));
                    for (int s = -1; s <= 1; s += 2) o.Add(CNavy, Cov(Seg(p, new Vector2(s * 0.86f, -0.24f), new Vector2(s * 0.9f, -0.32f), 0.025f), px));
                    o.Add(Mul(CPaper, 0.93f), Cov(stab, px));
                    o.Add(CPaper, Cov(fus, px));
                    for (int s = -1; s <= 1; s += 2)
                    {
                        o.Add(CEngine, Cov(Box(p, new Vector2(s * 0.2f, -0.5f), new Vector2(0.065f, 0.15f), 0.06f), px));
                        o.Add(CDark, Cov(Circ(p, new Vector2(s * 0.2f, -0.36f), 0.04f), px));
                    }
                    o.Add(CNavy, Cov(Seg(p, new Vector2(0, -0.9f), new Vector2(0, 0.55f), 0.025f), px));
                    o.Add(CDark, Cov(Seg(p, new Vector2(-0.05f, 0.66f), new Vector2(0.05f, 0.66f), 0.035f), px));
                    ShadeRight(ref o, p, Mathf.Min(fus, wing), px);
                    return o.c;
                }
                case 7: // jumbo jet: four underwing engines, wide body with an upper-deck hump, blue fin
                {
                    var o = new Px(CPaper);
                    float wing = Sym(p, JumboWing), fus = Mathf.Min(Seg(p, new Vector2(0, -0.9f), new Vector2(0, 0.86f), 0.15f), Seg(p, new Vector2(0, 0.4f), new Vector2(0, 0.72f), 0.165f));
                    for (int s = -1; s <= 1; s += 2)
                    {
                        o.Add(CEngine, Cov(Box(p, new Vector2(s * 0.4f, 0.08f), new Vector2(0.055f, 0.12f), 0.05f), px));
                        o.Add(CEngine, Cov(Box(p, new Vector2(s * 0.7f, -0.12f), new Vector2(0.05f, 0.11f), 0.05f), px));
                    }
                    o.Add(Mul(CPaper, 0.92f), Cov(wing, px));
                    o.Add(Mul(CPaper, 0.92f), Cov(Sym(p, JumboStab), px));
                    o.Add(CPaper, Cov(fus, px));
                    o.Add(CBlue, Cov(Seg(p, new Vector2(0, -0.6f), new Vector2(0, -0.93f), 0.035f), px));
                    o.Add(CDark, Cov(Seg(p, new Vector2(-0.06f, 0.8f), new Vector2(0.06f, 0.8f), 0.035f), px));
                    ShadeRight(ref o, p, Mathf.Min(fus, wing), px);
                    return o.c;
                }
                default: // stealth fighter: faceted planform, gold canopy, canted twin tails, panel edges
                {
                    var o = new Px(CStealth);
                    float body = Poly(p, StealthPts);
                    o.Add(CStealth, Cov(body, px));
                    o.Add(Mul(CStealth, 1.18f), Cov(Mathf.Max(body, Mathf.Abs(p.x) - 0.17f), px) * 0.6f); // raised spine
                    o.Add(Mul(CStealth, 0.82f), Cov(Mathf.Max(body, -p.x), px)); // facets facing away from the sun
                    o.Add(Mul(CStealth, 1.35f), Cov(Mathf.Abs(body + 0.015f) - 0.006f, px)); // edge alignment lines
                    for (int s = -1; s <= 1; s += 2) o.Add(Mul(CStealth, 0.7f), Cov(Seg(p, new Vector2(s * 0.18f, -0.45f), new Vector2(s * 0.26f, -0.72f), 0.03f), px));
                    o.Add(CGold, Cov(Seg(p, new Vector2(0, 0.3f), new Vector2(0, 0.56f), 0.05f), px));
                    return o.c;
                }
            }
        }
    }
}
