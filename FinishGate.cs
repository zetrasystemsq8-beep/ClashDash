using UnityEngine;
using UnityEngine.Events;

namespace Zetra.ClashDash
{
    /// <summary>
    /// Trigger volume that ends the run when the player crosses it.
    /// </summary>
    public class FinishGate : MonoBehaviour
    {
        public UnityEvent onCrossed = new UnityEvent();

        private bool triggered;

        private void OnTriggerEnter(Collider other)
        {
            if (triggered) return;
            if (other.GetComponent<PlayerController>() == null) return;

            GameManager manager = GameManager.Instance;
            if (manager == null || manager.State != GameState.Running) return;

            triggered = true;
            manager.CompleteArena();
            onCrossed.Invoke();
        }
    }
}
