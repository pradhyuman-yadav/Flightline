using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Flightline
{
    public enum Up { Tank, Magnet, Shield, Coin, HeadStart }

    public class UpgradeDef
    {
        public string name, desc, icon;
        public int max, baseCost;
        public float growth;
        public Func<int, string> effect;
    }

    // An airframe. Upgrade levels are shared across the fleet, but each plane caps how many of them count,
    // so every stat has an upper bound and a better plane is the way past it.
    public class PlaneDef
    {
        public string name, blurb;
        public int price, headStep;           // headStep: head-start miles per level
        public float speed, burn, coin;       // multipliers on zone speed and fuel burn; base coin value
        public float magnet, shield;          // base power-up seconds
        public float size = 1f, propY = -1f;  // visual scale; propeller hub in sprite space (-1 = none)
        public bool jet; public Vector2 engine; // contrail source (sprite space, mirrored) for jets
        public int[] cap;                     // max effective level per Up
    }

    public class AchDef
    {
        public string id, name, desc;
        public int reward;
        public Func<Prog.RunStats, SaveData, bool> test;
    }

    public struct Zone
    {
        public string name, code, gate, line;
        public int start;
        public bool night;
    }

    public static class Prog
    {
        public static string N(long n) => n.ToString("N0", CultureInfo.InvariantCulture);

        // ---------- Fleet ----------
        // BUSH PLANE (index 5) is the original airframe: its stats reproduce the game exactly as it was.
        public static readonly PlaneDef[] Planes =
        {
            new PlaneDef { name = "PAPER DART",      blurb = "Folded from a boarding pass. Slow, short range.",    price = 0,     speed = 0.75f, burn = 1.6f,  coin = 0.8f,  magnet = 4.0f, shield = 5.0f, headStep = 250, size = 0.85f, cap = new[] { 3, 3, 3, 3, 1 } },
            new PlaneDef { name = "BALSA GLIDER",    blurb = "Light wood, long wings. Rides the air a little longer.", price = 750, speed = 0.8f, burn = 1.45f, coin = 0.85f, magnet = 4.2f, shield = 5.2f, headStep = 275, size = 0.95f, cap = new[] { 4, 4, 4, 4, 2 } },
            new PlaneDef { name = "RC TRAINER",      blurb = "Electric motor, spinning prop. Your first engine.",  price = 2000,  speed = 0.85f, burn = 1.3f,  coin = 0.9f,  magnet = 4.4f, shield = 5.4f, headStep = 300, size = 0.88f, propY = 0.86f, cap = new[] { 5, 5, 5, 5, 3 } },
            new PlaneDef { name = "BIPLANE",         blurb = "Two wings each side. Fabric, wire and nerve.",       price = 4500,  speed = 0.9f,  burn = 1.2f,  coin = 0.92f, magnet = 4.6f, shield = 5.6f, headStep = 325, size = 0.95f, propY = 0.86f, cap = new[] { 6, 6, 6, 6, 4 } },
            new PlaneDef { name = "PROP FIGHTER",    blurb = "Wartime piston fighter. Elliptical wings, big prop.", price = 9000, speed = 0.95f, burn = 1.1f,  coin = 0.96f, magnet = 4.8f, shield = 5.8f, headStep = 350, propY = 0.9f,  cap = new[] { 8, 8, 8, 8, 6 } },
            new PlaneDef { name = "BUSH PLANE",      blurb = "High wing, honest and tough. Goes anywhere.",        price = 16000, speed = 1.0f,  burn = 1.0f,  coin = 1.0f,  magnet = 5.0f, shield = 6.0f, headStep = 400, propY = 0.86f, cap = new[] { 10, 10, 10, 10, 8 } },
            new PlaneDef { name = "BIZ JET",         blurb = "Swept wings, tail engines, leather seats.",          price = 28000, speed = 1.08f, burn = 0.92f, coin = 1.1f,  magnet = 5.4f, shield = 6.4f, headStep = 450, jet = true, engine = new Vector2(0.2f, -0.66f), cap = new[] { 10, 10, 10, 10, 8 } },
            new PlaneDef { name = "JUMBO JET",       blurb = "Four engines, two decks, enormous tanks.",           price = 45000, speed = 1.12f, burn = 0.8f,  coin = 1.2f,  magnet = 5.8f, shield = 6.8f, headStep = 500, size = 1.15f, jet = true, engine = new Vector2(0.4f, -0.1f), cap = new[] { 10, 10, 10, 10, 8 } },
            new PlaneDef { name = "STEALTH FIGHTER", blurb = "Faceted, twin-tailed, very fast. Top of the fleet.", price = 75000, speed = 1.22f, burn = 0.85f, coin = 1.3f,  magnet = 6.2f, shield = 7.2f, headStep = 600, size = 1.05f, jet = true, engine = new Vector2(0.12f, -0.78f), cap = new[] { 10, 10, 10, 10, 8 } },
        };

        public static PlaneDef Plane => Planes[Mathf.Clamp(Save.D.plane, 0, Planes.Length - 1)];
        public static bool Owns(int i) => (Save.D.planesOwned & (1 << i)) != 0;
        public static bool CanBuyPlane(int i) => !Owns(i) && (i == 0 || Owns(i - 1)); // bought in order
        public static int Cap(Up u, PlaneDef p = null) => Mathf.Min((p ?? Plane).cap[(int)u], Upgrades[(int)u].max);

        // ---------- Upgrades (Hangar) ----------
        public static readonly UpgradeDef[] Upgrades =
        {
            new UpgradeDef { name = "FUEL TANK",  icon = "fuel",   desc = "Burn less fuel",      max = 10, baseCost = 60,  growth = 1.5f,  effect = l => l == 0 ? "0%" : $"-{l * 5}%" },
            new UpgradeDef { name = "MAGNET",     icon = "magnet", desc = "Longer coin magnet",  max = 10, baseCost = 80,  growth = 1.5f,  effect = l => $"{MagnetTime(l):0.0}s" },
            new UpgradeDef { name = "SHIELD",     icon = "shield", desc = "Longer shield",       max = 10, baseCost = 80,  growth = 1.5f,  effect = l => $"{ShieldTime(l):0.0}s" },
            new UpgradeDef { name = "COIN VALUE", icon = "coin",   desc = "Coins worth more",    max = 10, baseCost = 120, growth = 1.6f,  effect = l => $"x{CoinMult(l):0.00}" },
            new UpgradeDef { name = "HEAD START", icon = "boost",  desc = "Afterburner launch",  max = 8,  baseCost = 150, growth = 1.65f, effect = l => l == 0 ? "OFF" : $"{N(HeadStartMiles(l))} mi" },
        };

        // effective level: what you bought, bounded by what the current airframe can use
        public static int Lvl(Up u) => Mathf.Min(Save.D.upgrades[(int)u], Cap(u));
        public static int Cost(int i, int lvl) => Mathf.RoundToInt(Upgrades[i].baseCost * Mathf.Pow(Upgrades[i].growth, lvl) / 5f) * 5;
        public static float BurnRate(int l, PlaneDef p = null) => (1f / 15f) * (1f - 0.05f * l) * (p ?? Plane).burn;
        public static float MagnetTime(int l, PlaneDef p = null) => (p ?? Plane).magnet + 0.8f * l;
        public static float ShieldTime(int l, PlaneDef p = null) => (p ?? Plane).shield + 0.8f * l;
        public static float CoinMult(int l, PlaneDef p = null) => (p ?? Plane).coin + 0.15f * l;
        public static int HeadStartMiles(int l, PlaneDef p = null) => l * (p ?? Plane).headStep;

        // ---------- Pilot rank (meta level) ----------
        public static int XpToNext(int level) => Mathf.RoundToInt(120f + 80f * Mathf.Pow(level - 1, 1.35f));

        public static string Rank(int level) =>
            level >= 25 ? "CHIEF PILOT" : level >= 15 ? "SENIOR CAPTAIN" : level >= 10 ? "CAPTAIN" :
            level >= 5 ? "FIRST OFFICER" : level >= 3 ? "STUDENT PILOT" : "CADET";

        public static int LevelReward(int level) => 40 * level;

        public static void AddXp(int amount, List<int> levelUps)
        {
            var d = Save.D; d.xp += amount;
            while (d.xp >= XpToNext(d.level))
            {
                d.xp -= XpToNext(d.level);
                d.level++;
                d.coins += LevelReward(d.level);
                levelUps?.Add(d.level);
            }
        }

        // ---------- Zones (in-flight levels) ----------
        public static readonly Zone[] Zones =
        {
            new Zone { name = "CLOUD NINE",  code = "CNE", gate = "A1", start = 0,     line = "Clear skies ahead." },
            new Zone { name = "JET STREAM",  code = "JST", gate = "B2", start = 2000,  line = "Tailwind. Speed is picking up." },
            new Zone { name = "STORM FRONT", code = "STF", gate = "C3", start = 5000,  line = "Turbulence ahead." },
            new Zone { name = "AURORA",      code = "AUR", gate = "D4", start = 9000,  line = "Night flight. Keep your eyes open.", night = true },
            new Zone { name = "HIGH CIRRUS", code = "HCR", gate = "E5", start = 14000, line = "Thin air. Stay sharp." },
            new Zone { name = "THE EDGE",    code = "EDG", gate = "F6", start = 20000, line = "Few flights make it this far.", night = true },
        };

        // First time you reach a zone pays a one-off bonus.
        public static int ArrivalReward(int zone) => zone <= 0 ? 0 : 100 + 100 * zone;

        public static int Affordable
        {
            get
            {
                int n = 0; var d = Save.D;
                for (int i = 0; i < Upgrades.Length; i++) if (d.upgrades[i] < Cap((Up)i) && d.coins >= Cost(i, d.upgrades[i])) n++;
                for (int i = 0; i < Planes.Length; i++) if (CanBuyPlane(i)) { if (d.coins >= Planes[i].price) n++; break; }
                return n;
            }
        }

        public static int ZoneIndex(float miles)
        {
            int z = 0;
            for (int i = 0; i < Zones.Length; i++) if (miles >= Zones[i].start) z = i;
            return z;
        }

        // ---------- Achievements (Logbook) ----------
        public class RunStats
        {
            public float miles;
            public int score, coins, nearMiss, shieldBlocks, fumes, powerups, zone;
        }

        static AchDef A(string id, string name, string desc, int reward, Func<RunStats, SaveData, bool> t) =>
            new AchDef { id = id, name = name, desc = desc, reward = reward, test = t };

        static bool AnyMaxed(SaveData s)
        {
            for (int i = 0; i < Upgrades.Length; i++) if (s.upgrades[i] >= Upgrades[i].max) return true;
            return false;
        }

        static int Bought(SaveData s) { int n = 0; foreach (var u in s.upgrades) n += u; return n; }

        public static readonly AchDef[] Achs =
        {
            A("first",   "FIRST FLIGHT",     "Complete your first flight.",              50,  (r, s) => s.runs >= 1),
            A("m1k",     "SHORT HAUL",       "Fly 1,000 mi in one flight.",              50,  (r, s) => r.miles >= 1000),
            A("m5k",     "MEDIUM HAUL",      "Fly 5,000 mi in one flight.",              150, (r, s) => r.miles >= 5000),
            A("m10k",    "LONG HAUL",        "Fly 10,000 mi in one flight.",             300, (r, s) => r.miles >= 10000),
            A("m25k",    "ULTRA LONG HAUL",  "Fly 25,000 mi in one flight.",             800, (r, s) => r.miles >= 25000),
            A("c100",    "POCKET CHANGE",    "Grab 100 coins in one flight.",            100, (r, s) => r.coins >= 100),
            A("c300",    "COIN RUN",         "Grab 300 coins in one flight.",            300, (r, s) => r.coins >= 300),
            A("near10",  "CLOSE CALLS",      "Get 10 close calls in one flight.",        120, (r, s) => r.nearMiss >= 10),
            A("near40",  "NERVES OF STEEL",  "Get 40 close calls in one flight.",        400, (r, s) => r.nearMiss >= 40),
            A("storm",   "STORM CHASER",     "Reach STORM FRONT.",                       150, (r, s) => r.zone >= 2),
            A("aurora",  "NIGHT FLIGHT",     "Reach AURORA.",                            250, (r, s) => r.zone >= 3),
            A("edge",    "THE EDGE",         "Reach THE EDGE.",                          600, (r, s) => r.zone >= 5),
            A("fumes",   "RUNNING ON FUMES", "Refuel with under 10% fuel left.",         100, (r, s) => r.fumes > 0),
            A("shield3", "UNTOUCHABLE",      "Block 3 hits with shields in one flight.", 200, (r, s) => r.shieldBlocks >= 3),
            A("power5",  "FULLY BOOSTED",    "Grab 5 power-ups in one flight.",          150, (r, s) => r.powerups >= 5),
            A("runs25",  "FREQUENT FLYER",   "Complete 25 flights.",                     250, (r, s) => s.runs >= 25),
            A("runs100", "PLATINUM STATUS",  "Complete 100 flights.",                    750, (r, s) => s.runs >= 100),
            A("bank",    "SAVINGS ACCOUNT",  "Collect 5,000 coins in total.",            300, (r, s) => s.totalCoins >= 5000),
            A("mech",    "MECHANIC",         "Buy your first upgrade.",                  50,  (r, s) => Bought(s) >= 1),
            A("maxed",   "FULLY LOADED",     "Max out any upgrade.",                     500, (r, s) => AnyMaxed(s)),
            A("cap",     "CAPTAIN",          "Reach pilot level 10.",                    500, (r, s) => s.level >= 10),
            A("elite",   "ELITE STATUS",     "Fly 100,000 mi in total.",                 1000,(r, s) => s.totalMiles >= 100000),
        };

        // Shared empty result: callers only iterate / read Count, never store or mutate it.
        static readonly List<AchDef> NoAchs = new List<AchDef>();
        static readonly RunStats EmptyRun = new RunStats();

        // Allocation-free unless something unlocks (called every 0.5 s mid-flight).
        public static List<AchDef> Check(RunStats r)
        {
            var d = Save.D; List<AchDef> res = null;
            if (r == null) r = EmptyRun;
            foreach (var a in Achs)
            {
                if (d.ach.Contains(a.id) || !a.test(r, d)) continue;
                d.ach.Add(a.id); d.coins += a.reward; (res ??= new List<AchDef>()).Add(a);
            }
            return res ?? NoAchs;
        }
    }
}
