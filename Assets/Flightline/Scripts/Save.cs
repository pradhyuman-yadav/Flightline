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
    }

    public static class Save
    {
        const string Key = "flightline.save.v1";
        static SaveData d;
        public static SaveData D { get { if (d == null) Load(); return d; } }

        public static void Load()
        {
            var s = PlayerPrefs.GetString(Key, "");
            try { d = string.IsNullOrEmpty(s) ? new SaveData() : JsonUtility.FromJson<SaveData>(s); }
            catch { d = new SaveData(); }
            if (d == null) d = new SaveData();
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
        }

        public static bool Suspend; // editor autopilot tests never write to disk
        public static void Write()
        {
            if (Suspend) return;
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(D));
            PlayerPrefs.Save();
        }

        public static void Reset() { d = new SaveData(); Write(); }
    }
}
