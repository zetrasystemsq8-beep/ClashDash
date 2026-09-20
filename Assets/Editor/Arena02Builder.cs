using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using static Zetra.ClashDash.EditorTools.ArenaBuildKit;

namespace Zetra.ClashDash.EditorTools
{
    /// <summary>
    /// Menu: ZETRA > CLASHDASH > Build Arena 02 - THE CRUCIBLE
    /// A harder, longer arena at fiery dusk: boost-pad speed traps, pendulums, boost-powered gap leaps, crumbling
    /// tiles under meteors, laser gates and a crusher finale. Saved as Assets/Scenes/Arena02_TheCrucible.unity.
    /// </summary>
    public static class Arena02Builder
    {
        private const float FinishZ = 502f;

        [MenuItem("ZETRA/CLASHDASH/Build Arena 02 - THE CRUCIBLE")]
        public static void Build()
        {
            ArenaInfo info = ArenaCatalog.Get(2);
            Assemble(info, CreateTheme(), BuildCourse, BuildSigns, new Vector3(0f, 0f, 2f), new Vector3(0f, 0f, FinishZ), 20260921);
        }

        private static ArenaTheme CreateTheme()
        {
            ArenaTheme t = new ArenaTheme();
            t.Prefix = "CDC_";
            t.SkyTint = new Color(0.78f, 0.45f, 0.72f);
            t.SkyGround = new Color(1f, 0.82f, 0.72f);
            t.Exposure = 1.25f;
            t.FogColor = new Color(1f, 0.72f, 0.66f);
            t.FogDensity = 0.0022f;
            t.SunColor = new Color(1f, 0.76f, 0.52f);
            t.SunIntensity = 1.3f;
            t.SunEuler = new Vector3(30f, -48f, 0f);
            t.AmbientSky = new Color(1f, 0.74f, 0.72f);
            t.AmbientEquator = new Color(0.88f, 0.66f, 0.72f);
            t.AmbientGround = new Color(0.45f, 0.3f, 0.42f);
            t.Deck = new Color(0.97f, 0.9f, 0.84f);
            t.DeckDark = new Color(0.36f, 0.26f, 0.36f);
            t.Tower = new Color(1f, 0.93f, 0.9f);
            t.Cloud = new Color(1f, 0.9f, 0.86f);
            t.Safe = new Color(0.1f, 0.9f, 0.85f);
            t.Secondary = new Color(0.85f, 0.4f, 0.95f);
            t.Gold = new Color(1f, 0.8f, 0.35f);
            t.Hazard = new Color(1f, 0.32f, 0.08f);
            return t;
        }

        // ------------------------------------------------------------------ course

        private static Transform BuildCourse(Transform root, List<Checkpoint> checkpoints)
        {
            // ---- Start ----
            Deck(root, "Deck_Start", 0f, 6f, 14f, 20f, 0f);
            Transform start = Group("StartPoint", root).transform;
            start.position = new Vector3(0f, 0.1f, 2f);

            // ---- 1. IGNITION (16 -> 72): boost pad into fast sliding walls ----
            Deck(root, "Deck_Ignition", 0f, 44f, 12f, 56f, 0f);
            BuildBoostPad(root, "Boost_Start", new Vector3(0f, 0f, 20f), 12f, 5f, 12f, 1f);

            float[] wallZ = { 34f, 44f, 54f, 64f };
            float[] wallPhase = { 0f, 0.5f, 0.25f, 0.75f };
            for (int i = 0; i < wallZ.Length; i++)
            {
                SlideWall(root, "Wall_" + (i + 1), new Vector3(-2.5f, 1.5f, wallZ[i]), new Vector3(7f, 3f, 0.8f),
                          new Vector3(5f, 0f, 0f), 2.4f, wallPhase[i]);
            }
            BuildCoinLine(root, new Vector3(-3f, 1.3f, 26f), new Vector3(3f, 1.3f, 29f), 3);
            BuildCoin(root, new Vector3(4f, 1.3f, 39f));
            BuildCoin(root, new Vector3(-4f, 1.3f, 49f));
            BuildCoin(root, new Vector3(4f, 1.3f, 59f));
            checkpoints.Add(MakeCheckpoint(root, 1, new Vector3(0f, 0f, 69f), 12f));

            // ---- 2. FURNACE (72 -> 140): pendulum hammers ----
            Deck(root, "Deck_Furnace", 0f, 106f, 14f, 68f, 0f);
            float[] pendulumZ = { 88f, 104f, 120f };
            float[] pendulumPhase = { 0f, 0.33f, 0.66f };
            for (int i = 0; i < pendulumZ.Length; i++)
            {
                BuildPendulum(root, "Pendulum_" + (i + 1), new Vector3(0f, 10.2f, pendulumZ[i]), 8.6f, 58f, 3f,
                              pendulumPhase[i], 7.6f, 0f);
            }
            BuildCoinLine(root, new Vector3(5.8f, 1.3f, 80f), new Vector3(5.8f, 1.3f, 128f), 6);
            BuildCoinLine(root, new Vector3(-5.8f, 1.3f, 96f), new Vector3(-5.8f, 1.3f, 128f), 4);
            checkpoints.Add(MakeCheckpoint(root, 2, new Vector3(0f, 0f, 136f), 14f));

            // ---- 3. LEAP (140 -> 286): boost pads carry the player over 10 m gaps ----
            Deck(root, "Deck_LeapA", 0f, 152f, 12f, 24f, 0f);
            BuildBoostPad(root, "Boost_LeapA", new Vector3(0f, 0f, 160.5f), 12f, 5f, 22f, 1.4f);

            Deck(root, "Deck_LeapB", 0f, 190f, 12f, 32f, 0f);
            BuildBoostPad(root, "Boost_LeapB", new Vector3(0f, 0f, 201.5f), 12f, 5f, 22f, 1.4f);
            BuildMeteorZone(root, "Meteors_LeapB", new Vector3(0f, 0f, 190f), new Vector2(10f, 20f), 3);

            Deck(root, "Deck_LeapC", 0f, 232f, 10f, 32f, 0f);
            BuildBoostPad(root, "Boost_LeapC", new Vector3(0f, 0f, 243.5f), 10f, 5f, 22f, 1.4f);

            Deck(root, "Deck_LeapD", 0f, 272f, 12f, 28f, 0f);

            float[] gapMid = { 169f, 211f, 253f };
            for (int i = 0; i < gapMid.Length; i++)
            {
                BuildCoinLine(root, new Vector3(0f, 2.2f, gapMid[i] - 2f), new Vector3(0f, 2.2f, gapMid[i] + 2f), 3);
            }
            checkpoints.Add(MakeCheckpoint(root, 3, new Vector3(0f, 0f, 268f), 12f));

            // ---- 4. COLLAPSE (286 -> 375): crumbling tiles ----
            string[] rows =
            {
                ".X.", "XX.", "X..", "XX.", ".XX", "..X", "SSS", ".XX", "XX.", ".X.", "XXX", ".X.", "X.X", ".X."
            };
            float[] columns = { -5.5f, 0f, 5.5f };
            const float rowStart = 292f;
            const float rowPitch = 5.5f;
            for (int r = 0; r < rows.Length; r++)
            {
                float z = rowStart + r * rowPitch;
                if (rows[r] == "SSS")
                {
                    Deck(root, "Deck_CollapseMid", 0f, z, 12f, 6f, 0f);
                    checkpoints.Add(MakeCheckpoint(root, 4, new Vector3(0f, 0f, z), 12f));
                    continue;
                }

                for (int c = 0; c < 3; c++)
                {
                    if (rows[r][c] != 'X') continue;
                    BuildFallingTile(root, "Tile_" + r + "_" + c, new Vector3(columns[c], 0f, z), new Vector2(4f, 4f));
                    if (r == 2 || r == 4 || r == 8 || r == 11) BuildCoin(root, new Vector3(columns[c], 1.3f, z));
                }
            }
            float collapseEnd = rowStart + (rows.Length - 1) * rowPitch;
            float endZ = collapseEnd + 8.5f;
            Deck(root, "Deck_CollapseEnd", 0f, endZ, 12f, 6f, 0f);
            checkpoints.Add(MakeCheckpoint(root, 5, new Vector3(0f, 0f, endZ), 12f));

            // ---- 5. LASER HALL (375 -> 430): a wave of laser gates ----
            Deck(root, "Deck_Laser", 0f, 402.5f, 10f, 55f, 0f);
            float[] laserZ = { 386f, 398f, 410f, 422f };
            for (int i = 0; i < laserZ.Length; i++)
            {
                BuildPulseGate(root, "Laser_" + (i + 1), new Vector3(0f, 0f, laserZ[i]), 9.4f, 3.2f, 1.3f, 1.9f,
                               Mathf.Repeat(-0.42f * i, 1f));
                BuildCoin(root, new Vector3(0f, 1.3f, laserZ[i] + 6f));
            }
            checkpoints.Add(MakeCheckpoint(root, 6, new Vector3(0f, 0f, 427f), 10f));

            // ---- 6. FINAL BLAZE (430 -> 512): crushers, then a last boost leap to the finish ----
            Deck(root, "Deck_Blaze", 0f, 456f, 10f, 52f, 0f);
            float[] slamZ = { 442f, 454f, 466f };
            float[] slamPhase = { 0f, 0.33f, 0.66f };
            for (int i = 0; i < slamZ.Length; i++)
            {
                SlamBlock(root, "Slam_" + (i + 1), new Vector3(0f, 7.8f, slamZ[i]), new Vector3(10f, 1.6f, 2.6f), 6f, slamPhase[i]);
                SlamFrame(root, slamZ[i], 10f);
            }
            BuildBoostPad(root, "Boost_Final", new Vector3(0f, 0f, 478.5f), 10f, 5f, 22f, 1.4f);
            BuildCoinLine(root, new Vector3(0f, 2.2f, 485f), new Vector3(0f, 2.2f, 489f), 3);

            Deck(root, "Deck_Finish", 0f, FinishZ, 14f, 20f, 0f);
            BuildFinishGate(root, new Vector3(0f, 0f, FinishZ));

            return start;
        }

        private static void SlamFrame(Transform root, float z, float deckWidth)
        {
            float x = deckWidth * 0.5f + 0.9f;
            Prim(PrimitiveType.Cylinder, "PillarL", root, new Vector3(-x, 4.5f, z), new Vector3(0.9f, 4.5f, 0.9f), deckDark, true);
            Prim(PrimitiveType.Cylinder, "PillarR", root, new Vector3(x, 4.5f, z), new Vector3(0.9f, 4.5f, 0.9f), deckDark, true);
            Prim(PrimitiveType.Cube, "Lintel", root, new Vector3(0f, 9.4f, z), new Vector3(deckWidth + 2.8f, 0.6f, 1.4f), deckDark, false);
            Prim(PrimitiveType.Cube, "LintelGlow", root, new Vector3(0f, 9.1f, z), new Vector3(deckWidth + 2.4f, 0.12f, 1.5f), glowMagenta, false, false);
        }

        // ------------------------------------------------------------------ signage / easter eggs

        private static void BuildSigns(Transform root)
        {
            Sign(root, "Sign_Title", "THE CRUCIBLE", new Vector3(0f, 22f, 44f), Vector3.zero, 22f, GoldText, 130, true);
            Sign(root, "Sign_Arena", "ARENA 02", new Vector3(0f, 11f, 34f), Vector3.zero, 12f, CyanText, 100, true);
            Sign(root, "Sign_Season", "SEASON ZERO", new Vector3(0f, 7.6f, 34f), Vector3.zero, 8f, VioletText, 70, true);
            Sign(root, "Decal_CONNECT", "CONNECT", new Vector3(0f, 0.05f, 9f), new Vector3(90f, 0f, 0f), 8f, CyanText, 120, false);

            Sign(root, "Sec_Ignition", "IGNITION", new Vector3(0f, 9f, 20f), Vector3.zero, 10f, GoldText, 100, true);
            Sign(root, "Sec_Furnace", "FURNACE", new Vector3(0f, 14f, 76f), Vector3.zero, 10f, GoldText, 100, true);
            Sign(root, "Sec_Leap", "LEAP", new Vector3(0f, 9f, 144f), Vector3.zero, 7f, GoldText, 100, true);
            Sign(root, "Sec_Collapse", "COLLAPSE", new Vector3(0f, 9f, 284f), Vector3.zero, 10f, GoldText, 100, true);
            Sign(root, "Sec_Laser", "LASER HALL", new Vector3(0f, 9f, 376f), Vector3.zero, 12f, GoldText, 100, true);
            Sign(root, "Sec_Blaze", "FINAL BLAZE", new Vector3(0f, 14f, 432f), Vector3.zero, 12f, GoldText, 100, true);

            Sign(root, "Hint_Boost", "STEP ON THE PADS", new Vector3(0f, 6.5f, 150f), Vector3.zero, 9f, CyanText, 70, true);
            Sign(root, "Holo_Nigergram", "Nigergram", new Vector3(-17f, 8f, 60f), Vector3.zero, 8f, VioletText, 90, true);
            Sign(root, "Holo_NaijaLearn", "NaijaLearn", new Vector3(17f, 8f, 120f), Vector3.zero, 8f, GoldText, 90, true);
            Sign(root, "Holo_ZetraMail", "ZetraMail\n7 unread", new Vector3(-17f, 9f, 200f), Vector3.zero, 8f, CyanText, 80, true);
            Sign(root, "Holo_Store", "ZETRA STORE", new Vector3(17f, 9f, 240f), Vector3.zero, 9f, CyanText, 90, true);
            Sign(root, "Holo_NAI", "NAI\nWATCHING", new Vector3(-17f, 9f, 330f), Vector3.zero, 7f, VioletText, 80, true);
            Sign(root, "Holo_Crucible", "CRUCIBLE", new Vector3(17f, 10f, 400f), Vector3.zero, 8f, MagentaText, 90, true);

            Sign(root, "Sign_Finish", "FINISH", new Vector3(0f, 19.5f, FinishZ), Vector3.zero, 9f, Color.white, 130, true);
            Sign(root, "Sign_Next", "TRIBUNAL AHEAD", new Vector3(0f, 26f, FinishZ + 8f), Vector3.zero, 14f, MagentaText, 100, true);
            Sign(root, "Decal_Names", "TOLUWANI / TOFUMI / FOLAKEMI / MARVELLOUS", new Vector3(0f, 0.05f, FinishZ + 4f), new Vector3(90f, 0f, 0f), 11f, GoldText, 46, false);
            Sign(root, "Decal_Signature", "CONNECT", new Vector3(0f, 0.05f, FinishZ + 8f), new Vector3(90f, 0f, 0f), 4f, CyanText, 90, false);
        }
    }
}
