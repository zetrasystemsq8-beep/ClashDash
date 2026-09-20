using UnityEngine;

namespace Zetra.ClashDash
{
    /// <summary>
    /// Per-run collectible tracking. Coins picked up are held here and only banked into the profile when the
    /// arena is completed (so restarting a run cannot be used to farm them).
    /// </summary>
    public static class RunStats
    {
        public static int CoinsCollected { get; private set; }
        public static int CoinsBanked { get; private set; }

        public static event System.Action Changed;

        public static void ResetRun()
        {
            CoinsCollected = 0;
            CoinsBanked = 0;
            if (Changed != null) Changed();
        }

        public static void AddCoins(int amount)
        {
            if (amount <= 0) return;
            CoinsCollected += amount;
            if (Changed != null) Changed();
        }

        /// <summary>Adds the collected coins to the saved total. Safe to call once per run.</summary>
        public static int Bank()
        {
            int amount = CoinsCollected - CoinsBanked;
            if (amount <= 0) return 0;

            PlayerPrefs.SetInt("CD_TOTAL_COINS", PlayerPrefs.GetInt("CD_TOTAL_COINS", 0) + amount);
            PlayerPrefs.Save();
            CoinsBanked = CoinsCollected;
            return amount;
        }
    }
}
