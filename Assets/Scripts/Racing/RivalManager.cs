using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Zetra.ClashDash
{
    /// <summary>
    /// Turns each arena into a race: three holographic rivals run the course at set paces (fast, par, slow).
    /// Shows "POSITION 2 / 4" while running and the final placing when the player finishes.
    /// </summary>
    public class RivalManager : MonoBehaviour
    {
        private readonly List<RivalRacer> rivals = new List<RivalRacer>();
        private GameManager gm;
        private PlayerController player;
        private Text positionLabel;
        private Text placingLabel;
        private float startZ;
        private float finishZ;
        private float nextRefresh;
        private int lastPosition = -1;

        public void Bind(GameManager manager, PlayerController playerController, Transform finishGate, ArenaInfo arena,
                         Material bodyMaterial, Material glowMaterial)
        {
            gm = manager;
            player = playerController;
            if (gm == null || player == null || finishGate == null) return;

            List<Vector3> path = BuildPath(finishGate);
            if (path == null) return;

            startZ = path[0].z;
            finishZ = path[path.Count - 1].z;

            float par = arena != null ? arena.ParTime : 95f;
            float[] factors = { 0.90f, 1.02f, 1.18f };
            float[] lanes = { -3.2f, 3.2f, -1.6f };
            string[] names = { "VEX", "ORION", "LUMA" };
            Color[] tints =
            {
                new Color(1f, 0.35f, 0.55f, 1f),
                new Color(1f, 0.8f, 0.3f, 1f),
                new Color(0.6f, 0.45f, 1f, 1f)
            };

            for (int i = 0; i < factors.Length; i++)
            {
                GameObject go = new GameObject("Rival_" + names[i]);
                go.transform.SetParent(transform, false);
                RivalRacer rival = go.AddComponent<RivalRacer>();
                rival.Bind(gm, path, par * factors[i], lanes[i], names[i], tints[i], bodyMaterial, glowMaterial);
                rivals.Add(rival);
            }

            BuildUi();
            gm.onFinished.AddListener(OnFinished);
        }

        private List<Vector3> BuildPath(Transform gate)
        {
            List<Vector3> points = new List<Vector3>();
            Vector3 start = player.transform.position;
            points.Add(new Vector3(0f, start.y, start.z));

            Checkpoint[] checkpoints;
#if UNITY_2022_2_OR_NEWER
            checkpoints = Object.FindObjectsByType<Checkpoint>(FindObjectsSortMode.None);
#else
            checkpoints = Object.FindObjectsOfType<Checkpoint>();
#endif
            List<Checkpoint> sorted = new List<Checkpoint>(checkpoints);
            sorted.Sort((a, b) => a.transform.position.z.CompareTo(b.transform.position.z));

            for (int i = 0; i < sorted.Count; i++)
            {
                Vector3 p = sorted[i].transform.position;
                if (p.z > points[points.Count - 1].z + 1f) points.Add(new Vector3(0f, p.y, p.z));
            }

            Vector3 finish = gate.position;
            if (finish.z <= points[points.Count - 1].z + 1f) return null;
            points.Add(new Vector3(0f, finish.y, finish.z));
            return points.Count >= 2 ? points : null;
        }

        private void BuildUi()
        {
            Canvas canvas = ClashUi.CreateCanvas("RaceUI", 12, transform);
            var raycaster = canvas.GetComponent<GraphicRaycaster>();
            if (raycaster != null) Destroy(raycaster);

            RectTransform positionRt = ClashUi.Rect("Position", canvas.transform, new Vector2(1f, 1f), new Vector2(1f, 1f),
                                                    new Vector2(1f, 1f), new Vector2(-40f, -170f), new Vector2(520f, 60f));
            positionLabel = ClashUi.Label(positionRt, string.Empty, 40, TextAnchor.MiddleRight, Color.white, true);

            RectTransform placingRt = ClashUi.Rect("Placing", canvas.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                                                   new Vector2(0.5f, 0.5f), new Vector2(0f, 430f), new Vector2(1500f, 110f));
            placingLabel = ClashUi.Label(placingRt, string.Empty, 84, TextAnchor.MiddleCenter, ClashUi.Gold, true);
        }

        private void Update()
        {
            if (gm == null || positionLabel == null) return;
            if (gm.State != GameState.Running) return;
            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + 0.2f;

            float span = Mathf.Max(1f, finishZ - startZ);
            float playerProgress = Mathf.Clamp01((player.transform.position.z - startZ) / span);

            int ahead = 0;
            for (int i = 0; i < rivals.Count; i++)
            {
                if (rivals[i].Finished || rivals[i].Progress01 > playerProgress) ahead++;
            }

            int position = ahead + 1;
            if (position != lastPosition)
            {
                lastPosition = position;
                positionLabel.text = "POSITION  " + position + " / " + (rivals.Count + 1);
            }
        }

        private void OnFinished()
        {
            if (positionLabel != null) positionLabel.text = string.Empty;

            float playerTime = gm.TotalTime;
            int place = 1;
            for (int i = 0; i < rivals.Count; i++)
            {
                if (rivals[i].PredictedFinishTime < playerTime) place++;
            }

            StartCoroutine(ShowPlacing(place, rivals.Count + 1));
        }

        private IEnumerator ShowPlacing(int place, int total)
        {
            yield return new WaitForSecondsRealtime(1.2f);
            if (placingLabel == null) yield break;

            placingLabel.color = place == 1 ? ClashUi.Gold : ClashUi.Cyan;
            placingLabel.text = "YOU FINISHED " + Ordinal(place) + " OF " + total;
        }

        private static string Ordinal(int n)
        {
            switch (n)
            {
                case 1: return "1ST";
                case 2: return "2ND";
                case 3: return "3RD";
                default: return n + "TH";
            }
        }

        private void OnDestroy()
        {
            if (gm != null) gm.onFinished.RemoveListener(OnFinished);
        }
    }
}
