using UnityEngine;
using UnityEngine.Events;

namespace Zetra.ClashDash
{
    /// <summary>
    /// Vibrates the phone when the player takes a hit. Toggle it from the main menu (HAPTICS button).
    /// Does nothing in the Editor or on devices without a vibration motor.
    /// </summary>
    public class HapticsFx : MonoBehaviour
    {
        private const string Key = "CD_HAPTICS";
        private const float MinInterval = 1.2f;

        public static bool Enabled
        {
            get { return PlayerPrefs.GetInt(Key, 1) == 1; }
            set
            {
                PlayerPrefs.SetInt(Key, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        private PlayerController player;
        private UnityAction onHit;
        private float nextTime;

        public void Bind(PlayerController playerController)
        {
            player = playerController;
            if (player == null) return;

            onHit = Vibrate;
            player.onHit.AddListener(onHit);
        }

        private void Vibrate()
        {
            if (!Enabled || Application.isEditor) return;
            if (Time.unscaledTime < nextTime) return;

            nextTime = Time.unscaledTime + MinInterval;
#if UNITY_ANDROID || UNITY_IOS
            Handheld.Vibrate();
#endif
        }

        private void OnDestroy()
        {
            if (player != null && onHit != null) player.onHit.RemoveListener(onHit);
        }
    }
}
