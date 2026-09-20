using UnityEngine;

namespace Zetra.ClashDash
{
    /// <summary>
    /// Timing hazard: an energy wall that is dangerous while lit, flickers as a warning just before it switches on,
    /// and is harmless and invisible while off. The hazardObject (trigger collider + Hazard) is toggled with it.
    /// </summary>
    public class PulseGate : MonoBehaviour
    {
        [SerializeField] private GameObject hazardObject;
        [SerializeField] private Renderer[] beams;
        [SerializeField] private float onTime = 1.4f;
        [SerializeField] private float offTime = 1.8f;
        [SerializeField] private float warnTime = 0.6f;
        [SerializeField, Range(0f, 1f)] private float phase;

        private bool lastOn;
        private bool lastVisible;
        private bool initialised;

        private void Update()
        {
            float cycle = Mathf.Max(0.2f, onTime + offTime);
            float t = Mathf.Repeat(Time.time + phase * cycle, cycle);

            bool on = t < onTime;
            bool warning = !on && t > cycle - warnTime;
            bool visible = on || (warning && (((int)(Time.time * 16f)) & 1) == 0);

            if (!initialised || on != lastOn)
            {
                lastOn = on;
                if (hazardObject != null) hazardObject.SetActive(on);
            }

            if (!initialised || visible != lastVisible)
            {
                lastVisible = visible;
                if (beams != null)
                {
                    for (int i = 0; i < beams.Length; i++)
                    {
                        if (beams[i] != null) beams[i].enabled = visible;
                    }
                }
            }

            initialised = true;
        }

        public void Bind(GameObject hazard, Renderer[] beamRenderers, float on, float off, float warn, float phase01)
        {
            hazardObject = hazard;
            beams = beamRenderers;
            onTime = on;
            offTime = off;
            warnTime = warn;
            phase = Mathf.Repeat(phase01, 1f);
        }
    }
}
