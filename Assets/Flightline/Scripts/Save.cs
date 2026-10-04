using System;
using System.Collections.Generic;
using UnityEngine;

namespace Flightline
{
    [Serializable]
    public class SaveData
    {
        public int coins;
        public int bestScore;
        public int bestMiles;
        public long totalMiles;
        public long totalCoins;
        public int runs;
        public int xp;
        public int level = 1;
        public int maxZone;
        public int flightNo = 200;
        public int[] upgrades = new int[5];
        public List<string> ach = new List<string>();
        public List<int> recentMiles = new List<int>();
        // daily missions
        public string missionDay = "";
        public List<Mission> missions = new List<Mission>();
        public bool rerollUsed;
        public int missionVer;
        public bool bonusClaimed;
        public int streak;
        public string lastStreakDay = "";
        public bool sound = true;
        public bool haptics = true;
        // review prompt
        public int reviewAsks;
        public long lastReviewAsk;
        public bool rated;            // opened the store page from inside the game
        public int rateCardShown;
        public long rateCardLast;
        public string homePort = "";
    }

    public static class Save
    {
        const string Key = "flightline.save.v1";
        const string BakKey = Key + ".bak";
        const string CorruptKey = Key + ".corrupt";
        static SaveData d;
        public static SaveData D { get { if (d == null) Load(); return d; } }

        public static void Load()
        {
            var s = PlayerPrefs.GetString(Key, "");
            d = Parse(s);
            if (d != null) { if (!string.IsNullOrEmpty(s)) PlayerPrefs.SetString(BakKey, s); } // remember last good save
            else
            {
                // corrupt/partial main save: keep the raw text for recovery and fall back to the last good copy
                if (!string.IsNullOrEmpty(s)) { PlayerPrefs.SetString(CorruptKey, s); Debug.LogWarning("Flightline: save data unreadable, restoring backup."); }
                d = Parse(PlayerPrefs.GetString(BakKey, "")) ?? new SaveData();
            }
            if (d.upgrades == null || d.upgrades.Length < 5)
            {
                var u = new int[5];
                if (d.upgrades != null) Array.Copy(d.upgrades, u, Mathf.Min(5, d.upgrades.Length));
                d.upgrades = u;
            }
            if (d.ach == null) d.ach = new List<string>();
            if (d.recentMiles == null) d.recentMiles = new List<int>();
            if (d.missions == null) d.missions = new List<Mission>();
            if (d.level < 1) d.level = 1;
            if (d.missionDay == null) d.missionDay = "";
            if (d.lastStreakDay == null) d.lastStreakDay = "";
            if (d.homePort == null) d.homePort = "";
        }

        static SaveData Parse(string s)
        {
            if (string.IsNullOrEmpty(s)) return null;
            try { return JsonUtility.FromJson<SaveData>(s); }
            catch { return null; }
        }

        public static bool Suspend; // editor autopilot tests never write to disk

        // Statics survive play-mode restarts when domain reload is off; never carry a stale test flag or cache over.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Suspend = false; d = null; }

        public static void Write()
        {
#if UNITY_EDITOR
            if (Suspend) return; // test-only flag; release builds always save
#endif
            try
            {
                PlayerPrefs.SetString(Key, JsonUtility.ToJson(D));
                PlayerPrefs.Save();
            }
            catch (Exception e) { Debug.LogWarning("Flightline: save failed: " + e.Message); }
        }

        public static void Reset() { d = new SaveData(); Write(); }
    }
}
