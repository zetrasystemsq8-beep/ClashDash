using UnityEngine;
using UnityEngine.SceneManagement;

namespace Zetra.ClashDash
{
    /// <summary>
    /// Runs automatically. In every arena it replaces the placeholder capsule character with the animated runner
    /// and adds three holographic rival racers with a live position display.
    /// </summary>
    public static class RacingBootstrap
    {
        private static readonly string[] OldPartNames =
        {
            "Body", "Visor", "Core", "Pack", "PackGlow", "Belt", "BootL", "BootR"
        };

        private static GameManager attachedTo;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            attachedTo = null;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            GameManager gm = GameManager.Instance;
            if (gm == null || gm == attachedTo) return;

            PlayerController player = ClashDashBootstrap.FindOne<PlayerController>();
            if (player == null) return;

            attachedTo = gm;

            Transform visual = player.transform.Find("Visual");
            Material bodyMaterial = null;
            Material glowMaterial = null;

            if (visual != null)
            {
                bodyMaterial = MaterialOf(visual.Find("Body"));
                glowMaterial = MaterialOf(visual.Find("Visor"));

                for (int i = 0; i < OldPartNames.Length; i++)
                {
                    Transform part = visual.Find(OldPartNames[i]);
                    if (part != null) part.gameObject.SetActive(false);
                }

                RunnerAvatar.Create(visual, "RunnerAvatar", bodyMaterial, glowMaterial, player);
            }

            FinishGate gate = ClashDashBootstrap.FindOne<FinishGate>();
            if (gate != null)
            {
                GameObject rivals = new GameObject("ClashDashRivals");
                rivals.AddComponent<RivalManager>().Bind(gm, player, gate.transform, ArenaCatalog.Current(), bodyMaterial, glowMaterial);
            }
        }

        private static Material MaterialOf(Transform t)
        {
            if (t == null) return null;
            Renderer r = t.GetComponent<Renderer>();
            return r != null ? r.sharedMaterial : null;
        }
    }
}
