using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Flightline
{
    public enum MType { Coins, CloseCalls, Flights, Powerups, Canisters, ShieldBlocks, MilesTotal, MilesRun, ScoreRun, CoinsRun, ReachZone, NearRun }

    [Serializable]
    public class Mission { public int type, target, progress, reward; public bool done, claimed; }

    // Three daily missions, picked from the local date. Progress across flights; rewards pay out on completion.
    public static class Missions
    {
        // Targets by tier (tier rises every 4 pilot levels).
        static readonly int[][] Targets =
        {
            new[] { 80, 150, 250, 400 },          // Coins
            new[] { 8, 15, 25, 40 },              // CloseCalls
            new[] { 3, 4, 5, 6 },                 // Flights
            new[] { 3, 5, 8, 12 },                // Powerups
            new[] { 4, 8, 12, 18 },               // Canisters
            new[] { 1, 2, 3, 5 },                 // ShieldBlocks
            new[] { 5000, 10000, 18000, 30000 },  // MilesTotal
            new[] { 1500, 3000, 5000, 9000 },     // MilesRun
            new[] { 3000, 7000, 14000, 25000 },   // ScoreRun
            new[] { 40, 80, 130, 200 },           // CoinsRun
            null,                                 // ReachZone (uses zone index)
            new[] { 5, 10, 18, 28 },              // NearRun
        };
        static readonly string[] Text =
        {
            "Grab {0} coins", "{0} close calls", "Fly {0} flights", "Grab {0} power-ups", "Grab {0} fuel cans",
            "Block {0} hits with a shield", "Fly {0} mi in total", "Fly {0} mi in one flight", "Score {0} in one flight",
            "{0} coins in one flight", "Reach {0}", "{0} close calls in one flight"
        };
        static readonly string[] Icons = { "coin", "boost", "plane", "shield", "fuel", "shield", "plane", "arrowU", "trophy", "coin", "flag", "boost" };
        public static string Icon(Mission m) => Icons[m.type];
        static readonly float[] Weight = { 1f, 1.1f, 0.8f, 1f, 1f, 1.3f, 1f, 1.1f, 1.1f, 1f, 1.3f, 1.2f };
        static readonly int[] TierReward = { 60, 100, 150, 220 };
        static readonly MType[] Cumulative = { MType.Coins, MType.CloseCalls, MType.Flights, MType.Powerups, MType.Canisters, MType.ShieldBlocks, MType.MilesTotal };
        static readonly MType[] PerRun = { MType.MilesRun, MType.ScoreRun, MType.CoinsRun, MType.ReachZone, MType.NearRun };

        public static string Today => DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        static string Yesterday => DateTime.Now.AddDays(-1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        static int Hash(string s) { unchecked { int h = (int)2166136261; foreach (char c in s) { h ^= c; h *= 16777619; } return h & 0x7fffffff; } }

        public static List<Mission> List { get { EnsureToday(); return Save.D.missions; } }

        // Returns true when a new day's missions were generated.
        public static bool EnsureToday()
        {
            var d = Save.D;
            if (d.missionVer < 2) { if (d.missions != null) foreach (var m in d.missions) if (m.done) m.claimed = true; d.missionVer = 2; } // older saves paid out on completion
            bool valid = Valid(d.missions);
            if (valid && d.missionDay == Today) return false;
            if (valid && Rewound(d.missionDay)) return false; // clock moved back: keep current set, no fresh rewards
            d.missionDay = Today; d.rerollUsed = false; d.bonusClaimed = false;
            var rng = new System.Random(Hash(Today));
            d.missions = new List<Mission>
            {
                Make(Cumulative[rng.Next(Cumulative.Length)], rng),
                Make(PerRun[rng.Next(PerRun.Length)], rng)
            };
            d.missions.Add(Make(PickNew(rng), rng));
            Save.Write();
            return true;
        }

        static bool Valid(List<Mission> list)
        {
            if (list == null || list.Count != 3) return false;
            foreach (var m in list)
            {
                if (m == null || m.type < 0 || m.type >= Text.Length) return false;
                if ((MType)m.type == MType.ReachZone && (m.target < 0 || m.target >= Prog.Zones.Length)) return false;
            }
            return true;
        }

        // Saved mission day is later than today (device clock rewound). Ignored if absurdly far ahead so a once-bogus clock can recover.
        static bool Rewound(string day)
        {
            if (string.IsNullOrEmpty(day) || !DateTime.TryParseExact(day, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var saved)) return false;
            double ahead = (saved - DateTime.Today).TotalDays;
            return ahead > 0 && ahead <= 30;
        }

        static MType PickNew(System.Random rng)
        {
            var d = Save.D; MType t; int guard = 0;
            do { t = (MType)rng.Next(12); } while (d.missions.Exists(m => m.type == (int)t) && guard++ < 60);
            return t;
        }

        static Mission Make(MType t, System.Random rng)
        {
            var d = Save.D;
            int tier = Mathf.Clamp((d.level - 1) / 4, 0, 3);
            if (tier > 0 && rng.Next(3) == 0) tier--;
            int target = t == MType.ReachZone ? Mathf.Clamp(d.maxZone + 1, 1, Prog.Zones.Length - 1) : Targets[(int)t][tier];
            int reward = Mathf.RoundToInt(TierReward[tier] * Weight[(int)t] / 5f) * 5;
            return new Mission { type = (int)t, target = target, reward = reward };
        }

        public static string Describe(Mission m) =>
            (MType)m.type == MType.ReachZone ? string.Format(Text[m.type], Prog.Zones[m.target].name) : string.Format(Text[m.type], Prog.N(m.target));

        public static string ProgressText(Mission m) =>
            (MType)m.type == MType.ReachZone
                ? $"BEST {Prog.Zones[Mathf.Clamp(m.progress, 0, Prog.Zones.Length - 1)].code} / {Prog.Zones[m.target].code}"
                : $"{Prog.N(m.progress)} / {Prog.N(m.target)}";

        public static void Add(MType t, int amount)
        {
            if (amount <= 0) return;
            var list = Save.D.missions; if (list == null) return;
            foreach (var m in list) if (m.type == (int)t && !m.done) { m.progress += amount; Check(m); }
        }

        public static void Max(MType t, int value)
        {
            var list = Save.D.missions; if (list == null) return;
            foreach (var m in list) if (m.type == (int)t && !m.done && value > m.progress) { m.progress = value; Check(m); }
        }

        static void Check(Mission m)
        {
            if (m.done || m.progress < m.target) return;
            var d = Save.D;
            m.done = true; m.progress = m.target;
            if (GameUI.I) GameUI.I.Toast("MISSION COMPLETE", $"{Describe(m)}. Claim +{Prog.N(m.reward)} in Missions.");
            if (Sfx.I) Sfx.I.Chime();
            Save.Write();
        }

        public static int Bonus(int streak) => 250 + 50 * Mathf.Clamp(streak - 1, 0, 10);

        // Streak counts consecutive days with all three missions done.
        public static int Streak { get { var d = Save.D; return d.lastStreakDay == Today || d.lastStreakDay == Yesterday ? d.streak : 0; } }
        public static int NextBonus => Save.D.bonusClaimed ? Bonus(Save.D.streak) : Bonus(Streak + 1);

        // ---- claiming: rewards are collected by tapping, so finishing a mission pulls you back to the menu
        public static int Claim(int i)
        {
            var d = Save.D; if (d.missions == null || i < 0 || i >= d.missions.Count) return 0;
            var m = d.missions[i];
            if (m == null || !m.done || m.claimed) return 0;
            m.claimed = true; d.coins += m.reward; Save.Write();
            return m.reward;
        }

        public static bool BonusReady { get { var d = Save.D; return !d.bonusClaimed && d.missions != null && d.missions.Count == 3 && d.missions.TrueForAll(x => x.claimed); } }

        public static int ClaimBonus()
        {
            if (!BonusReady) return 0;
            var d = Save.D; d.bonusClaimed = true;
            d.streak = d.lastStreakDay == Yesterday ? d.streak + 1 : 1;
            d.lastStreakDay = Today;
            int b = Bonus(d.streak); d.coins += b; Save.Write();
            return b;
        }

        public static int Claimable { get { int n = 0; foreach (var m in List) if (m.done && !m.claimed) n++; return n + (BonusReady ? 1 : 0); } }

        public static bool CanSwap(int i) { var d = Save.D; return !d.rerollUsed && d.missions != null && i >= 0 && i < d.missions.Count && !d.missions[i].done; }

        public static void Swap(int i)
        {
            if (!CanSwap(i)) return;
            var d = Save.D; var rng = new System.Random(Hash(Today + "swap" + i));
            d.missions[i] = Make(PickNew(rng), rng);
            d.rerollUsed = true; Save.Write();
        }

        public static int DoneCount { get { int n = 0; foreach (var m in List) if (m.done) n++; return n; } }

        public static string ResetsIn
        {
            get { var ts = DateTime.Today.AddDays(1) - DateTime.Now; return $"{(int)ts.TotalHours:00}:{ts.Minutes:00}"; }
        }
    }
}
