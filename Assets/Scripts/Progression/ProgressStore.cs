using UnityEngine;

namespace Zetra.ClashDash
{
    /// <summary>
    /// Player profile stored in PlayerPrefs. Uses the same keys GameManager already writes on finish
    /// (total XP, total coins, best time per arena), and adds level, unlocks and settings on top.
    /// </summary>
    public static class ProgressStore
    {
        public const string Arena01 = "ARENA_01";

        private const string KeyXp = "CD_TOTAL_XP";
        private const string KeyCoins = "CD_TOTAL_COINS";
        private const string KeyBestPrefix = "CD_BEST_";
        private const string KeyDonePrefix = "CD_DONE_";
        private const string KeyRuns = "CD_RUNS";
        private const string KeySound = "CD_SOUND";

        public static int TotalXp => PlayerPrefs.GetInt(KeyXp, 0);
        public static int TotalCoins => PlayerPrefs.GetInt(KeyCoins, 0);
        public static int RunsCompleted => PlayerPrefs.GetInt(KeyRuns, 0);
        public static int Level => LevelFromXp(TotalXp);
        public static float LevelProgress01 => LevelProgress(TotalXp);

        /// <summary>Total XP needed to reach a level (level 1 = 0, 2 = 300, 3 = 900, 4 = 1800 ...).</summary>
        public static int XpForLevel(int level)
        {
            level = Mathf.Max(1, level);
            return 300 * (level - 1) * level / 2;
        }

        public static int LevelFromXp(int xp)
        {
            int level = 1;
            while (XpForLevel(level + 1) <= xp) level++;
            return level;
        }

        public static float LevelProgress(int xp)
        {
            int level = LevelFromXp(xp);
            int from = XpForLevel(level);
            int to = XpForLevel(level + 1);
            return Mathf.Clamp01((xp - from) / (float)Mathf.Max(1, to - from));
        }

        public static float BestTime(string arenaId)
        {
            return PlayerPrefs.GetFloat(KeyBestPrefix + arenaId, 0f);
        }

        public static bool IsArenaCompleted(string arenaId)
        {
            return PlayerPrefs.GetInt(KeyDonePrefix + arenaId, 0) == 1;
        }

        public static void MarkArenaCompleted(string arenaId)
        {
            PlayerPrefs.SetInt(KeyDonePrefix + arenaId, 1);
            PlayerPrefs.Save();
        }

        public static void AddRunCompleted()
        {
            PlayerPrefs.SetInt(KeyRuns, RunsCompleted + 1);
            PlayerPrefs.Save();
        }

        /// <summary>Arena 1 is always open; each later arena opens when the previous one is completed.</summary>
        public static bool IsArenaUnlocked(int arenaNumber)
        {
            if (arenaNumber <= 1) return true;
            return IsArenaCompleted("ARENA_" + (arenaNumber - 1).ToString("00"));
        }

        public static bool SoundEnabled
        {
            get { return PlayerPrefs.GetInt(KeySound, 1) == 1; }
            set
            {
                PlayerPrefs.SetInt(KeySound, value ? 1 : 0);
                PlayerPrefs.Save();
                AudioListener.volume = value ? 1f : 0f;
            }
        }

        public static string FormatTime(float seconds)
        {
            seconds = Mathf.Max(0f, seconds);
            int minutes = (int)(seconds / 60f);
            float rest = seconds - minutes * 60f;
            return minutes.ToString("00") + ":" + rest.ToString("00.00", System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>Debug helper: wipes the CLASHDASH profile.</summary>
        public static void ResetAll()
        {
            PlayerPrefs.DeleteKey(KeyXp);
            PlayerPrefs.DeleteKey(KeyCoins);
            PlayerPrefs.DeleteKey(KeyRuns);
            for (int i = 1; i <= 20; i++)
            {
                string id = "ARENA_" + i.ToString("00");
                PlayerPrefs.DeleteKey(KeyBestPrefix + id);
                PlayerPrefs.DeleteKey(KeyDonePrefix + id);
            }
            PlayerPrefs.Save();
        }
    }
}
