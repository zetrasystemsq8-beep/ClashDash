using System.Collections.Generic;
using UnityEngine;

namespace Zetra.ClashDash
{
    /// <summary>
    /// Developer test helper (only attached in the Editor and development builds).
    /// Toggle with F3 / the back-quote key, or a four-finger tap on a phone.
    /// Shows FPS and run state, and offers shortcuts: next checkpoint, jump to finish, +1000 XP, reset save.
    /// </summary>
    public class DebugOverlay : MonoBehaviour
    {
        private GameManager gm;
        private PlayerController player;
        private Transform finishGate;
        private readonly List<Checkpoint> checkpoints = new List<Checkpoint>();

        private bool visible;
        private float fps;
        private float fpsTimer;
        private int fpsFrames;
        private float nextToggleTime;
        private int nextCheckpointIndex;
        private GUIStyle labelStyle;
        private GUIStyle buttonStyle;

        public void Bind(GameManager manager, PlayerController playerController, Transform gate)
        {
            gm = manager;
            player = playerController;
            finishGate = gate;

            Checkpoint[] found = FindCheckpoints();
            checkpoints.AddRange(found);
            checkpoints.Sort((a, b) => a.Index.CompareTo(b.Index));
        }

        private static Checkpoint[] FindCheckpoints()
        {
#if UNITY_2022_2_OR_NEWER
            return Object.FindObjectsByType<Checkpoint>(FindObjectsSortMode.None);
#else
            return Object.FindObjectsOfType<Checkpoint>();
#endif
        }

        private void Update()
        {
            fpsFrames++;
            fpsTimer += Time.unscaledDeltaTime;
            if (fpsTimer >= 0.5f)
            {
                fps = fpsFrames / fpsTimer;
                fpsFrames = 0;
                fpsTimer = 0f;
            }

            if (Time.unscaledTime >= nextToggleTime && ToggleRequested())
            {
                visible = !visible;
                nextToggleTime = Time.unscaledTime + 0.8f;
            }
        }

        private static bool ToggleRequested()
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.F3) || Input.GetKeyDown(KeyCode.BackQuote) || Input.touchCount >= 4;
#elif ENABLE_INPUT_SYSTEM
            UnityEngine.InputSystem.Keyboard kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && (kb.f3Key.wasPressedThisFrame || kb.backquoteKey.wasPressedThisFrame)) return true;

            UnityEngine.InputSystem.Touchscreen ts = UnityEngine.InputSystem.Touchscreen.current;
            if (ts != null)
            {
                int pressed = 0;
                for (int i = 0; i < ts.touches.Count; i++)
                {
                    if (ts.touches[i].press.isPressed) pressed++;
                }
                if (pressed >= 4) return true;
            }
            return false;
#else
            return false;
#endif
        }

        private void OnGUI()
        {
            if (!visible) return;

            float scale = Mathf.Max(1f, Screen.height / 540f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));

            if (labelStyle == null)
            {
                labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, richText = true };
                buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 14 };
            }

            float width = 240f;
            float x = Screen.width / scale - width - 10f;
            GUILayout.BeginArea(new Rect(x, 90f, width, 360f), GUI.skin.box);

            string state = gm != null ? gm.State.ToString() : "-";
            string pos = player != null ? player.transform.position.ToString("0.0") : "-";
            GUILayout.Label("<b>CLASHDASH DEBUG</b>", labelStyle);
            GUILayout.Label("FPS " + fps.ToString("0") + "   (" + (fps > 0f ? (1000f / fps).ToString("0.0") : "-") + " ms)   tier " + PerformanceGuard.Tier, labelStyle);
            GUILayout.Label("State " + state + "   T " + (gm != null ? gm.TotalTime.ToString("0.00") : "-"), labelStyle);
            GUILayout.Label("Pos " + pos, labelStyle);
            GUILayout.Label("CP " + (gm != null ? gm.CheckpointsReached : 0) + "   Mistakes " + (gm != null ? gm.Mistakes : 0) + "   Coins " + RunStats.CoinsCollected, labelStyle);
            GUILayout.Label("XP " + ProgressStore.TotalXp + "   Lvl " + ProgressStore.Level, labelStyle);

            if (GUILayout.Button("NEXT CHECKPOINT", buttonStyle)) TeleportToNextCheckpoint();
            if (GUILayout.Button("JUMP TO FINISH", buttonStyle)) TeleportToFinish();
            if (GUILayout.Button("+1000 XP", buttonStyle))
            {
                PlayerPrefs.SetInt("CD_TOTAL_XP", ProgressStore.TotalXp + 1000);
                PlayerPrefs.Save();
            }
            if (GUILayout.Button("RESET SAVE", buttonStyle)) ProgressStore.ResetAll();
            if (GUILayout.Button("HIDE", buttonStyle)) visible = false;

            GUILayout.EndArea();
        }

        private void TeleportToNextCheckpoint()
        {
            if (player == null || checkpoints.Count == 0) return;

            nextCheckpointIndex = Mathf.Clamp(nextCheckpointIndex, 0, checkpoints.Count - 1);
            Checkpoint cp = checkpoints[nextCheckpointIndex];
            Transform point = cp.RespawnPoint;
            player.TeleportTo(point.position + Vector3.up * 0.2f, point.rotation);
            nextCheckpointIndex = Mathf.Min(nextCheckpointIndex + 1, checkpoints.Count - 1);
        }

        private void TeleportToFinish()
        {
            if (player == null || finishGate == null) return;
            player.TeleportTo(finishGate.position + Vector3.up * 0.3f, Quaternion.identity);
        }
    }
}
