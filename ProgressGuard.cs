using UnityEngine;
using UnityEngine.Events;

namespace Zetra.ClashDash
{
    /// <summary>
    /// The base bootstrap always marks ARENA 01 as completed when a run finishes. When a later arena is played
    /// directly (for example while testing in the Editor), this puts ARENA 01's flag back to what it was, so the
    /// unlock chain (01 -> 02 -> 03) only advances by playing the arenas in order.
    /// </summary>
    public class ProgressGuard : MonoBehaviour
    {
        private const string Arena01DoneKey = "CD_DONE_ARENA_01";

        private GameManager gm;
        private UnityAction onFinished;
        private bool arena01WasDone;
        private bool armed;
        private int framesLeft;

        public void Bind(GameManager manager, ArenaInfo arena)
        {
            if (manager == null || arena == null || arena.Number <= 1)
            {
                enabled = false;
                return;
            }

            gm = manager;
            arena01WasDone = ProgressStore.IsArenaCompleted(ProgressStore.Arena01);
            onFinished = () => { armed = true; framesLeft = 2; };
            gm.onFinished.AddListener(onFinished);
        }

        private void Update()
        {
            if (!armed) return;

            framesLeft--;
            if (framesLeft > 0) return;

            armed = false;
            if (!arena01WasDone)
            {
                PlayerPrefs.DeleteKey(Arena01DoneKey);
                PlayerPrefs.Save();
            }
        }

        private void OnDestroy()
        {
            if (gm != null && onFinished != null) gm.onFinished.RemoveListener(onFinished);
        }
    }
}
