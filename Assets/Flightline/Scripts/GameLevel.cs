using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Flightline
{
    // Per-zone tuning. One row per zone; values blend toward the next zone as you fly through.
    public struct Tune
    {
        public float speed;      // world units per second (x10 = mi/s)
        public float gapMin;     // vertical breathing room between sections
        public float gapMax;
        public float wallGap;    // opening width in storm walls
        public float fuelEvery;  // distance between fuel canisters
        public float droneSpeed;
        public float wind;       // crosswind push, units per second
        public int intensity;    // base section difficulty, 0-5
    }

    public partial class Game
    {
        // ================= TUNING TABLE: edit here to rebalance =================
        public static readonly Tune[] Tunes =
        {
            new Tune { speed = 7.0f,  gapMin = 3.0f, gapMax = 3.8f, wallGap = 2.8f,  fuelEvery = 46f, droneSpeed = 1.2f, wind = 2.6f, intensity = 1 }, // CLOUD NINE
            new Tune { speed = 8.2f,  gapMin = 2.7f, gapMax = 3.4f, wallGap = 2.5f,  fuelEvery = 54f, droneSpeed = 1.5f, wind = 3.0f, intensity = 2 }, // JET STREAM
            new Tune { speed = 9.2f,  gapMin = 2.5f, gapMax = 3.1f, wallGap = 2.25f, fuelEvery = 62f, droneSpeed = 1.8f, wind = 3.4f, intensity = 3 }, // STORM FRONT
            new Tune { speed = 10.0f, gapMin = 2.4f, gapMax = 3.0f, wallGap = 2.1f,  fuelEvery = 68f, droneSpeed = 2.1f, wind = 3.6f, intensity = 3 }, // AURORA
            new Tune { speed = 11.0f, gapMin = 2.3f, gapMax = 2.8f, wallGap = 2.0f,  fuelEvery = 74f, droneSpeed = 2.4f, wind = 4.0f, intensity = 4 }, // HIGH CIRRUS
            new Tune { speed = 12.0f, gapMin = 2.2f, gapMax = 2.7f, wallGap = 1.85f, fuelEvery = 80f, droneSpeed = 2.7f, wind = 4.4f, intensity = 5 }, // THE EDGE
        };
        const float WaveLen = 72f;       // distance per tension/release wave
        const float BreathFrac = 0.14f;  // opening slice of each wave is a breather
        const float PeakFrac = 0.55f;    // after this point the wave peaks (+1 intensity)
        // ========================================================================

        public int assist;               // 0..2 silent easing for struggling or new players
        float lastPity; int gateForZone; int lastMilesReported; string last1, last2;
        float windPush;
        string curChunk = "", killer = "";
        Chunk[] lib;
        readonly List<(Chunk, float)> picks = new List<(Chunk, float)>();
        readonly List<Vector2> spans = new List<Vector2>();

        class Chunk { public string id; public int rating, minZone, home = -1; public Func<float, Tune, float> build; }
        static Chunk C(string id, int rating, int minZone, int home, Func<float, Tune, float> b) => new Chunk { id = id, rating = rating, minZone = minZone, home = home, build = b };

        // ---------------------------------------------------------------- director
        void ResetDirector()
        {
            if (lib == null) InitChunks();
            lastPity = -999; gateForZone = 0; lastMilesReported = 0; last1 = last2 = null; windPush = 0;
            ComputeAssist();
        }

        void ComputeAssist()
        {
            var d = Save.D;
#if UNITY_EDITOR
            if (autopilot) { assist = 0; return; }
#endif
            if (d.runs < 2) { assist = 2; return; }
            var r = d.recentMiles;
            if (r == null || r.Count == 0) { assist = 0; return; }
            int n = Mathf.Min(3, r.Count); float avg = 0;
            for (int i = r.Count - n; i < r.Count; i++) avg += r[i];
            avg /= n;
            assist = avg < 900 ? 2 : avg < 1800 ? 1 : 0;
        }

        Tune CurTune()
        {
            int z = Mathf.Clamp(zone, 0, Tunes.Length - 1);
            var a = Tunes[z]; Tune t = a;
            if (z < Tunes.Length - 1)
            {
                var b = Tunes[z + 1];
                float k = Mathf.InverseLerp(Prog.Zones[z].start, Prog.Zones[z + 1].start, miles);
                t.speed = Mathf.Lerp(a.speed, b.speed, k);
                t.gapMin = Mathf.Lerp(a.gapMin, b.gapMin, k);
                t.gapMax = Mathf.Lerp(a.gapMax, b.gapMax, k);
                t.wallGap = Mathf.Lerp(a.wallGap, b.wallGap, k);
                t.fuelEvery = Mathf.Lerp(a.fuelEvery, b.fuelEvery, k);
                t.droneSpeed = Mathf.Lerp(a.droneSpeed, b.droneSpeed, k);
            }
            else
            {
                float over = Mathf.Max(0, miles - Prog.Zones[z].start);
                t.speed = Mathf.Min(a.speed + over / 2000f * 0.25f, 15f);
                t.wallGap = Mathf.Max(1.7f, a.wallGap - over / 20000f);
                t.intensity = over > 10000 ? 6 : 5;
            }
            t.speed *= 1f - 0.05f * assist;
            t.wallGap += 0.3f * assist;
            t.gapMin += 0.25f * assist; t.gapMax += 0.25f * assist;
            t.fuelEvery *= 1f - 0.12f * assist;
            t.intensity = Mathf.Max(0, t.intensity - assist);
            return t;
        }

        int TargetIntensity(Tune t)
        {
            float w = Mathf.Repeat(travel, WaveLen) / WaveLen;
            if (w < BreathFrac) return 0;
            int target = t.intensity + (w < PeakFrac ? 0 : 1);
            if (Save.D.runs < 3 && travel < 60f) target = Mathf.Min(target, 1);
            return Mathf.Clamp(target, 0, 6);
        }

        void SpawnChunk()
        {
            var t = CurTune();
            float y0 = SpawnY - (travel - nextRowAt);
            rowHaz.Clear();
            float extent;
            if (zone + 1 < Prog.Zones.Length && gateForZone < zone + 1 && miles + (y0 - PlaneY) * 10f >= Prog.Zones[zone + 1].start - 40f)
            {
                gateForZone = zone + 1; curChunk = "gate"; extent = GateChunk(y0);
            }
            else
            {
                var c = Pick(TargetIntensity(t));
                curChunk = c.id;
                extent = c.build(y0, t);
                last2 = last1; last1 = c.id;
            }
            curChunk = "pickup";
            if (travel >= nextFuelAt) { PlaceSafe(Kind.Fuel, y0 + extent * 0.5f); nextFuelAt = travel + t.fuelEvery * Random.Range(0.85f, 1.15f); }
            if (travel >= nextPowerAt) { PlaceSafe((Kind)Random.Range((int)Kind.Shield, (int)Kind.Boost + 1), y0 + extent * 0.5f + 1f); nextPowerAt = travel + Random.Range(120f, 190f); }
            // Min reaction TIME between rows: untouched at speed <= 9, then the gap scales with speed and
            // ramps to a floor of ~0.36 s of travel by speed 10.5 (steering settle ~0.2 s + reaction).
            float gap = Random.Range(t.gapMin, t.gapMax) * Mathf.Max(1f, t.speed / 9f);
            gap = Mathf.Max(gap, t.speed * 0.36f * Mathf.Clamp01((t.speed - 9f) / 1.5f));
            nextRowAt += extent + gap;
        }

#if UNITY_EDITOR
        public string forceChunk = "";
#endif
        Chunk Pick(int target)
        {
#if UNITY_EDITOR
            if (!string.IsNullOrEmpty(forceChunk)) foreach (var c in lib) if (c.id == forceChunk) return c;
#endif
            picks.Clear(); float total = 0;
            foreach (var c in lib)
            {
                if (c.minZone > zone) continue;
                if (target == 0 ? c.rating != 0 : (c.rating > target || c.rating < Mathf.Max(1, target - 1))) continue;
                float w = 1f;
                if (c.home == zone) w *= 3f;
                if (c.id == last1) w *= 0.1f; else if (c.id == last2) w *= 0.4f;
                picks.Add((c, w)); total += w;
            }
            if (picks.Count == 0) return lib[0];
            float r = Random.value * total;
            foreach (var (c, w) in picks) if ((r -= w) <= 0) return c;
            return picks[picks.Count - 1].Item1;
        }

        void PityFuel()
        {
            if (fuel > 0.25f || travel - lastPity < 30f) return;
            foreach (var e in ents) if (e.kind == Kind.Fuel && e.t.position.y > PlaneY - 0.5f) return; // a canister already behind the plane doesn't count
            // Spawn right at the top edge instead of waiting for the next chunk: a long chunk (gauntlet, zigzag) could delay it past the last of the fuel.
            lastPity = travel;
            string prev = curChunk; curChunk = "pity"; PlaceSafe(Kind.Fuel, SpawnY); curChunk = prev;
        }

        // ---------------------------------------------------------------- section library
        // rating 0 = breather, 5 = hardest. minZone = first zone it can appear in. home = zone where it is 3x as likely.
        void InitChunks()
        {
            lib = new[]
            {
                C("arc",       0, 0, -1, (y, t) => { CoinArc(RandX(1.6f), y); return 3f; }),
                C("snake",     0, 0, -1, (y, t) => CoinSnake(y)),
                C("ring",      0, 0, -1, (y, t) => { CoinRing(RandX(1.6f), y + 1.3f, 1.2f, 10); return 2.6f; }),
                C("lanes",     0, 0, -1, (y, t) => { float x = RandX(1.4f); CoinLine(x - 0.8f, y, 6); CoinLine(x + 0.8f, y + 0.4f, 6); return 4.6f; }),

                C("balloon",   1, 0, 0,  (y, t) => { Haz(Kind.Balloon, RandX(), y); int n = Random.Range(4, 7); CoinLine(FreeX(), y - 0.4f, n); return Mathf.Max(1.2f, n * 0.75f); }),
                C("pair",      1, 0, 0,  (y, t) => { float s = PlayHalfW * 0.6f; Haz(Kind.Balloon, -s, y, false); Haz(Kind.Balloon, s, y + 0.8f, false); CoinLine(0, y - 0.5f, 5); return 3.2f; }),
                C("stormside", 1, 0, -1, (y, t) => { float sd = Random.value < 0.5f ? -1 : 1; SizeStorm(Spawn(Kind.Storm, sd * (PlayHalfW - 1.1f), y), Random.Range(0.9f, 1.2f)); CoinLine(-sd * PlayHalfW * 0.4f, y - 0.5f, 5); return 3.2f; }),

                C("slalom",    2, 0, -1, (y, t) => Slalom(y, 4)),
                C("widewall",  2, 0, -1, (y, t) => Wall(y, t.wallGap + 0.6f)),
                C("scatter",   2, 0, -1, (y, t) => Scatter(y, 2)),
                C("gust",      2, 1, 1,  (y, t) => Gust(y, t, false)),
                C("drone",     2, 2, 3,  (y, t) => { Drone(RandX(0.8f), y, t.droneSpeed); CoinArc(RandX(1.6f), y + 0.6f); return 2.8f; }),

                C("wall",      3, 1, -1, (y, t) => Wall(y, t.wallGap)),
                C("double",    3, 1, 1,  (y, t) => DoubleWall(y, t.wallGap)),
                C("gustwall",  3, 1, 1,  (y, t) => Gust(y, t, true)),
                C("cell",      3, 2, 2,  (y, t) => Cells(y, 1)),
                C("wire",      3, 2, 2,  (y, t) => Wire(y)),
                C("drones",    3, 3, 3,  (y, t) => { Drone(-PlayHalfW * 0.5f, y, t.droneSpeed); Drone(PlayHalfW * 0.5f, y + 2.4f, t.droneSpeed); CoinLine(0, y - 0.3f, 5); return 4.4f; }),
                C("drift",     3, 4, 4,  (y, t) => Drift(y, 3)),

                C("zigzag",    4, 2, -1, (y, t) => Zigzag(y, t, 3)),
                C("cells",     4, 2, 2,  (y, t) => Cells(y, 2)),
                C("sweep",     4, 2, 2,  (y, t) => Sweep(y, t)),
                C("dronegate", 4, 3, 3,  (y, t) => DroneGate(y, t)),
                C("dense",     4, 3, -1, (y, t) => Scatter(y, 4)),
                C("driftwall", 4, 4, 4,  (y, t) => { float a = Wall(y, t.wallGap); return a + 1.6f + Drift(y + a + 1.6f, 2); }),

                C("gauntlet",  5, 4, 5,  (y, t) => { float a = Zigzag(y, t, 3); return a + 1.4f + Cells(y + a + 1.4f, 1); }),
                C("edgefield", 5, 5, 5,  (y, t) => { float a = Scatter(y, 5); Drone(RandX(0.8f), y + a + 1.2f, t.droneSpeed * 1.2f); return a + 1.8f; }), // drone gets its own band above the scatter rows (LeavesGap ignores drones)
                C("stormgust", 5, 2, 5,  (y, t) => Gust(y, t, true, 0.4f)),
                C("wires",     5, 3, 5,  (y, t) => { float a = Wire(y); return a + 2.4f + Wire(y + a + 2.4f); }),
            };
        }

        // ---------------------------------------------------------------- builders
        float RandX(float m = 0.7f) => Random.Range(-PlayHalfW + m, PlayHalfW - m);

        float FreeX(float margin = 0.7f)
        {
            float best = RandX(margin), bestD = -1;
            for (int i = 0; i < 12; i++)
            {
                float x = RandX(margin), md = 99;
                foreach (var h in rowHaz) md = Mathf.Min(md, Mathf.Abs(h.x - x));
                if (md > bestD) { bestD = md; best = x; }
            }
            return best;
        }

        void PlaceSafe(Kind k, float y)
        {
            float best = 0, bestD = -1;
            for (int i = 0; i < 14; i++)
            {
                float x = RandX(0.8f), md = 99;
                foreach (var e in ents)
                {
                    if (!e.Hazard || Mathf.Abs(e.t.position.y - y) > 1.6f + VerticalReach(e)) continue;
                    HazardSpan(e, true, out float lo, out float hi);
                    float dx = x < lo ? lo - x : x > hi ? x - hi : 0f;
                    md = Mathf.Min(md, dx);
                }
                if (md > bestD) { bestD = md; best = x; }
            }
            if (bestD < 0.5f)
            {
                // All samples blocked or tight: take the centre of the widest free interval in this band.
                spans.Clear();
                foreach (var e in ents)
                {
                    if (!e.Hazard || Mathf.Abs(e.t.position.y - y) > 1.6f + VerticalReach(e)) continue;
                    HazardSpan(e, true, out float lo, out float hi);
                    if (hi > lo) spans.Add(new Vector2(lo, hi));
                }
                spans.Sort((a, b) => a.x.CompareTo(b.x));
                float cursor = -PlayHalfW + 0.8f, end = PlayHalfW - 0.8f, wBest = -1f, xBest = best;
                foreach (var sp in spans)
                {
                    float hiEdge = Mathf.Min(sp.x, end);
                    if (hiEdge - cursor > wBest) { wBest = hiEdge - cursor; xBest = (cursor + hiEdge) * 0.5f; }
                    cursor = Mathf.Max(cursor, sp.y);
                }
                if (end - cursor > wBest) { wBest = end - cursor; xBest = (cursor + end) * 0.5f; }
                if (wBest * 0.5f > bestD) best = xBest;
            }
            Spawn(k, best, y);
        }

        static float HazR(Kind k) => k == Kind.Storm ? 0.55f : k == Kind.Drone ? 0.46f : k == Kind.Lightning ? 1.6f * 0.85f : 0.42f;

        // Fairness: never place a hazard that closes every path across a horizontal band.
        bool LeavesGap(float x, float y, float r)
        {
            spans.Clear(); float pad = PlaneR + 0.15f;
            spans.Add(new Vector2(x - r - 0.3f - pad, x + r + 0.3f + pad));
            foreach (var e in ents)
            {
                if (!e.Hazard || e.kind == Kind.Drone) continue;
                if (Mathf.Abs(e.t.position.y - y) > 1.4f + VerticalReach(e)) continue;
                HazardSpan(e, true, out float lo, out float hi);
                if (hi > lo) spans.Add(new Vector2(lo - pad, hi + pad));
            }
            spans.Sort((a, b) => a.x.CompareTo(b.x));
            float cursor = -PlayHalfW + 0.6f, end = PlayHalfW - 0.6f; // the plane's steering limits (Game.HandleInput), not the screen edge
            foreach (var s in spans) { if (s.x - cursor > 0.2f) return true; cursor = Mathf.Max(cursor, s.y); }
            return end - cursor > 0.2f;
        }

        Ent Haz(Kind k, float x, float y, bool validate = true)
        {
            if (validate)
            {
                int tries = 0;
                while (!LeavesGap(x, y, HazR(k)) && tries++ < 10) x = RandX();
                if (!LeavesGap(x, y, HazR(k))) return null;
            }
            var e = Spawn(k, x, y);
            if (k == Kind.Storm) SizeStorm(e, Random.Range(0.85f, 1.15f));
            return e;
        }

        // Per-cloud variety: size, mirror, slight tilt. Collision radius follows the size.
        void SizeStorm(Ent e, float s)
        {
            e.baseScale = 0.85f * s; e.t.localScale = Vector3.one * e.baseScale; e.r = 0.55f * s;
            e.sr.flipX = Random.value < 0.5f; e.t.localRotation = Quaternion.Euler(0, 0, Random.Range(-8f, 8f));
        }

        void CoinArc(float cx, float y) { for (int i = 0; i < 7; i++) { float a = Mathf.Lerp(-1f, 1f, i / 6f); Spawn(Kind.Coin, cx + a * 1.3f, y + (1 - a * a) * 1.4f); } }

        // Coin trails curve gently instead of running ruler-straight.
        void CoinLine(float x, float y, int n, float curve = 0.28f)
        {
            float ph = Random.value * 6.28f, k = Random.Range(0.45f, 0.7f);
            for (int i = 0; i < n; i++)
            {
                float cx = Mathf.Clamp(x + Mathf.Sin(ph + i * k) * curve * Mathf.Min(1f, i / 2f), -PlayHalfW + 0.5f, PlayHalfW - 0.5f);
                Spawn(Kind.Coin, cx, y + i * 0.75f);
            }
        }

        void CoinRing(float cx, float cy, float r, int n) { for (int i = 0; i < n; i++) { float a = i * Mathf.PI * 2f / n; Spawn(Kind.Coin, cx + Mathf.Cos(a) * r, cy + Mathf.Sin(a) * r); } }

        float CoinSnake(float y)
        {
            int n = Random.Range(9, 15); float amp = Random.Range(1.0f, PlayHalfW - 0.9f), f = Random.Range(0.35f, 0.55f), ph = Random.value * 6.28f;
            for (int i = 0; i < n; i++) Spawn(Kind.Coin, Mathf.Sin(ph + i * f) * amp, y + i * 0.7f);
            return n * 0.7f;
        }

        // Storm front: clouds of mixed size along a gentle curve, tapering at the opening. Returns the opening's y.
        float WallAt(float y, float gx, float gapW)
        {
            float amp = Random.Range(0.25f, 0.75f), k = Random.Range(0.45f, 0.8f), ph = Random.value * 6.28f;
            float x = -PlayHalfW - 0.5f;
            while (x <= PlayHalfW + 0.5f)
            {
                float s = Random.Range(0.78f, 1.15f);
                float edge = Mathf.Abs(x - gx) - gapW * 0.5f;
                if (edge < 1.1f) s = Mathf.Min(s, 0.88f);
                if (edge > 0.55f * s + 0.1f)
                {
                    var e = Spawn(Kind.Storm, x + Random.Range(-0.08f, 0.08f), y + Mathf.Sin(x * k + ph) * amp + Random.Range(-0.12f, 0.12f));
                    SizeStorm(e, s);
                }
                x += Random.Range(0.92f, 1.1f) * Mathf.Lerp(1f, s, 0.6f);
            }
            return y + Mathf.Sin(gx * k + ph) * amp;
        }

        float GapX(float gapW) => Random.Range(-PlayHalfW + gapW * 0.5f + 0.3f, PlayHalfW - gapW * 0.5f - 0.3f);

        float Wall(float y, float gapW)
        {
            gapW = Mathf.Min(gapW, PlayHalfW * 2f - 1.2f);
            float gx = GapX(gapW);
            float gy = WallAt(y, gx, gapW); CoinLine(gx, gy - 1.5f, 5, 0.12f);
            return 3.2f;
        }

        float DoubleWall(float y, float gapW)
        {
            float g1 = GapX(gapW);
            float g2 = Mathf.Clamp(g1 + Random.Range(1.4f, 2.4f) * (g1 > 0 ? -1 : 1), -PlayHalfW + gapW * 0.5f + 0.3f, PlayHalfW - gapW * 0.5f - 0.3f);
            float y1 = WallAt(y, g1, gapW), y2 = WallAt(y + 3.8f, g2, gapW);
            // coins trace the S-curve between the two openings
            for (int i = 0; i <= 6; i++) { float u = i / 6f, e = u * u * (3f - 2f * u); Spawn(Kind.Coin, Mathf.Lerp(g1, g2, e), Mathf.Lerp(y1 - 0.6f, y2 + 0.2f, u)); }
            return 6.6f;
        }

        float Zigzag(float y, Tune t, int n)
        {
            float step = 3.8f + t.speed * 0.08f, side = Random.value < 0.5f ? -1f : 1f;
            float prevX = 0, prevY = y - step * 0.5f;
            for (int i = 0; i < n; i++)
            {
                float gx = side * PlayHalfW * 0.45f;
                float gy = WallAt(y + i * step, gx, t.wallGap);
                for (int j = 1; j <= 3; j++) { float u = j / 3f, e = u * u * (3f - 2f * u); Spawn(Kind.Coin, Mathf.Lerp(prevX, gx, e), Mathf.Lerp(prevY, gy, u)); }
                prevX = gx; prevY = gy; side = -side;
            }
            return n * step + 0.6f;
        }

        // Balloons alternate sides; the coin trail marks the safe weave (never a lure into a balloon).
        float Slalom(float y, int n)
        {
            float side = Random.value < 0.5f ? -1f : 1f, step = 3.0f;
            for (int i = 0; i < n; i++)
            {
                float yy = y + i * step;
                Spawn(Kind.Balloon, side * PlayHalfW * 0.42f, yy);
                Spawn(Kind.Coin, -side * PlayHalfW * 0.3f, yy);
                if (i < n - 1) Spawn(Kind.Coin, 0, yy + step * 0.5f);
                side = -side;
            }
            return n * step;
        }

        float Scatter(float y, int n)
        {
            for (int i = 0; i < n; i++) Haz(Random.value < 0.5f ? Kind.Storm : Kind.Balloon, RandX(), y + i * 1.5f + Random.Range(-0.3f, 0.3f));
            int c = Random.Range(4, 7);
            CoinLine(FreeX(), y - 0.4f, c);
            return Mathf.Max(n * 1.5f, c * 0.75f);
        }

        Ent Drone(float x, float y, float sp)
        {
            var e = Spawn(Kind.Drone, x, y);
            e.vx = sp * Random.Range(0.85f, 1.15f) * (Random.value < 0.5f ? -1f : 1f);
            return e;
        }

        float DroneGate(float y, Tune t)
        {
            float gapW = Mathf.Min(t.wallGap + 1.4f, PlayHalfW * 2f - 1.2f), gx = GapX(gapW);
            float gy = WallAt(y, gx, gapW);
            var d = Drone(gx, gy, t.droneSpeed);
            d.minX = gx - gapW * 0.5f + 0.25f; d.maxX = gx + gapW * 0.5f - 0.25f;
            CoinLine(gx, gy - 1.8f, 3, 0.1f);
            return 3f;
        }

        float Drift(float y, int n)
        {
            for (int i = 0; i < n; i++)
            {
                var e = Haz(Kind.Balloon, RandX(), y + i * 1.9f);
                if (e != null) e.vx = Random.Range(0.8f, 1.6f) * (Random.value < 0.5f ? -1f : 1f);
            }
            CoinLine(FreeX(), y, 4);
            return n * 1.9f;
        }

        // Charging storm cells, staggered left/right. Coins sit just outside the strike reach.
        float Cells(float y, int n)
        {
            float side = Random.value < 0.5f ? -1f : 1f, step = 4.8f;
            for (int i = 0; i < n; i++)
            {
                float x = side * Random.Range(0.4f, PlayHalfW - 1.4f);
                var e = Haz(Kind.Lightning, x, y + i * step);
                if (e != null)
                {
                    // coins only where they're safe: an arc if there's room beyond the reach, else a short trail at the edge
                    float ex = e.t.position.x, reach = CellReach(e) + PlaneR + 0.3f;
                    float arcX = ex - side * (reach + 1.4f);
                    if (Mathf.Abs(arcX) <= PlayHalfW - 1.4f) CoinArc(arcX, y + i * step - 0.6f);
                    else
                    {
                        float lx = Mathf.Clamp(ex - side * (reach + 0.2f), -PlayHalfW + 0.6f, PlayHalfW - 0.6f);
                        if (Mathf.Abs(lx - ex) > reach) CoinLine(lx, y + i * step - 1.2f, 4, 0.1f);
                    }
                }
                side = -side;
            }
            return n * step;
        }

        // Live wire between two drifting storm nodes, tilted, with clear sky on one side.
        float Wire(float y)
        {
            float span = Random.Range(2.2f, 3.0f), free = 1.8f, side = Random.value < 0.5f ? -1f : 1f;
            float lo = -PlayHalfW + span * 0.5f + 0.3f, hi = PlayHalfW - free - span * 0.5f - 0.5f;
            float cx = Random.Range(lo, Mathf.Max(lo, hi)) * side;
            float tilt = Random.Range(-0.7f, 0.7f);
            var e = Spawn(Kind.Arc, cx, y + 0.8f);
            e.mode = 0; e.a = new Vector2(-span * 0.5f, -tilt); e.b = new Vector2(span * 0.5f, tilt); e.extent = 1.6f;
            float fx = cx + side * (span * 0.5f + 0.5f + free * 0.5f);
            CoinLine(Mathf.Clamp(fx, -PlayHalfW + 0.6f, PlayHalfW - 0.6f), y - 0.4f, 4, 0.15f);
            return 2.4f;
        }

        // A hand of lightning sweeping around a pivot. Always a safe spot beyond its reach.
        float Sweep(float y, Tune t)
        {
            float L = Random.Range(1.6f, 2.0f), px = Random.Range(-1.0f, 1.0f);
            var e = Spawn(Kind.Arc, px, y + L);
            e.mode = 1; e.h = L; e.extent = L + 0.6f; e.t2 = Random.value * 360f;
            e.vx = Random.Range(45f, 65f) * (Random.value < 0.5f ? -1f : 1f) * (0.85f + t.speed * 0.02f); // slow enough to read
            float fx = px > 0 ? -PlayHalfW + 1.0f : PlayHalfW - 1.0f;
            CoinLine(fx, y + 0.2f, 5, 0.2f);
            return L * 2f + 0.8f;
        }

        float Gust(float y, Tune t, bool withWall, float extraGap = 0.3f)
        {
            float dir = Random.value < 0.5f ? -1f : 1f, H = 5f;
            var e = Spawn(Kind.Wind, 0, y + H * 0.5f);
            SetupWind(e, dir, t.wind, H);
            // coins ride the wind: a curve drifting downwind
            float x0 = -dir * PlayHalfW * 0.45f;
            for (int i = 0; i < 7; i++) { float u = i / 6f; Spawn(Kind.Coin, x0 + dir * (u * u) * 2.2f, y + 0.4f + i * 0.65f); }
            if (withWall) { float g = t.wallGap + extraGap; WallAt(y + 3.2f, GapX(g), g); }
            return H;
        }

        float GateChunk(float y)
        {
            Spawn(Kind.Gate, 0, y + 1.2f);
            CoinRing(0, y + 1.6f, 1.1f, 12);
            return 3.4f;
        }
    }
}
