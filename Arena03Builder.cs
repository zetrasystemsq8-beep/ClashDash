using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using static Zetra.ClashDash.EditorTools.ArenaBuildKit;

namespace Zetra.ClashDash.EditorTools
{
    /// <summary>
    /// Menu: ZETRA > CLASHDASH > Build Arena 03 - THE TRIBUNAL
    /// The Season Zero finale at twilight: spinner-guarded narrow beams, pendulum + laser + crusher combinations,
    /// fast crumbling tiles under meteors, a laser/spinner/pendulum gauntlet and a boosted final leap.
    /// Saved as Assets/Scenes/Arena03_TheTribunal.unity.
    /// </summary>
    public static class Arena03Builder
    {
        private const float FinishZ = 489f;

        [MenuItem("ZETRA/CLASHDASH/Build Arena 03 - THE TRIBUNAL")]
        public static void Build()
        {
            ArenaInfo info = ArenaCatalog.Get(3);
            Assemble(info, CreateTheme(), BuildCourse, BuildSigns, new Vector3(0f, 0f, 2f), new Vector3(0f, 0f, FinishZ), 20260922);
        }

        private static ArenaTheme CreateTheme()
        {
            ArenaTheme t = new ArenaTheme();
            t.Prefix = "CDT_";
            t.SkyTint = new Color(0.32f, 0.28f, 0.72f);
            t.SkyGround = new Color(0.7f, 0.65f, 0.95f);
            t.Exposure = 1.1f;
            t.FogColor = new Color(0.62f, 0.58f, 0.92f);
            t.FogDensity = 0.0026f;
            t.SunColor = new Color(0.85f, 0.82f, 1f);
            t.SunIntensity = 1.05f;
            t.SunEuler = new Vector3(58f, 24f, 0f);
            t.AmbientSky = new Color(0.55f, 0.5f, 0.95f);
            t.AmbientEquator = new Color(0.62f, 0.58f, 0.88f);
            t.AmbientGround = new Color(0.3f, 0.25f, 0.52f);
            t.Deck = new Color(0.83f, 0.83f, 0.96f);
            t.DeckDark = new Color(0.24f, 0.22f, 0.42f);
            t.Tower = new Color(0.88f, 0.88f, 1f);
            t.Cloud = new Color(0.9f, 0.88f, 1f);
            t.Safe = new Color(0.55f, 0.8f, 1f);
            t.Secondary = new Color(0.8f, 0.55f, 1f);
            t.Gold = new Color(1f, 0.82f, 0.4f);
            t.Hazard = new Color(1f, 0.15f, 0.4f);
            return t;
        }

        // ------------------------------------------------------------------ course

        private static Transform BuildCourse(Transform root, List<Checkpoint> checkpoints)
        {
            // ---- Start ----
            Deck(root, "Deck_Start", 0f, 6f, 14f, 20f, 0f);
            Transform start = Group("StartPoint", root).transform;
            start.position = new Vector3(0f, 0.1f, 2f);

            // ---- 1. INDICTMENT (16 -> 96): narrow beam with sweepers, then fast sliding walls ----
            Deck(root, "Beam_A", 0f, 44f, 4f, 56f, 0f);
            SpinBar(root, "Sweeper_1", new Vector3(0f, 0.95f, 34f), 6f, 130f, 0.6f);
            SpinBar(root, "Sweeper_2", new Vector3(0f, 0.95f, 50f), 6f, -150f, 0.6f);
            SpinBar(root, "Sweeper_3", new Vector3(0f, 0.95f, 64f), 6f, 170f, 0.6f);
            BuildCoin(root, new Vector3(0f, 1.3f, 42f));
            BuildCoin(root, new Vector3(0f, 1.3f, 57f));

            Deck(root, "Deck_Plaza1", 0f, 84f, 12f, 24f, 0f);
            checkpoints.Add(MakeCheckpoint(root, 1, new Vector3(0f, 0f, 76f), 12f));
            SlideWall(root, "Wall_1", new Vector3(-2.5f, 1.5f, 86f), new Vector3(7f, 3f, 0.8f), new Vector3(5f, 0f, 0f), 2f, 0f);
            SlideWall(root, "Wall_2", new Vector3(-2.5f, 1.5f, 92f), new Vector3(7f, 3f, 0.8f), new Vector3(5f, 0f, 0f), 2f, 0.5f);

            // ---- 2. WITNESS (96 -> 190): pendulums, lasers, crushers ----
            Deck(root, "Deck_Witness", 0f, 143f, 14f, 94f, 0f);
            BuildPendulum(root, "Pendulum_1", new Vector3(0f, 10.2f, 108f), 8.6f, 62f, 2.6f, 0f, 7.6f, 0f);
            BuildPendulum(root, "Pendulum_2", new Vector3(0f, 10.2f, 122f), 8.6f, 62f, 2.6f, 0.5f, 7.6f, 0f);
            BuildPulseGate(root, "Laser_1", new Vector3(0f, 0f, 136f), 13.4f, 3.2f, 1.1f, 1.6f, 0f);
            BuildPulseGate(root, "Laser_2", new Vector3(0f, 0f, 146f), 13.4f, 3.2f, 1.1f, 1.6f, 0.59f);
            float[] slamZ = { 160f, 172f };
            float[] slamPhase = { 0f, 0.5f };
            for (int i = 0; i < slamZ.Length; i++)
            {
                SlamBlock(root, "Slam_" + (i + 1), new Vector3(0f, 7.8f, slamZ[i]), new Vector3(14f, 1.6f, 2.6f), 6f, slamPhase[i]);
                SlamFrame(root, slamZ[i], 14f);
            }
            BuildCoin(root, new Vector3(0f, 1.3f, 115f));
            BuildCoin(root, new Vector3(0f, 1.3f, 141f));
            BuildCoin(root, new Vector3(0f, 1.3f, 166f));
            checkpoints.Add(MakeCheckpoint(root, 2, new Vector3(0f, 0f, 186f), 14f));

            // ---- 3. DELIBERATION (190 -> 295): fast crumbling tiles under meteors ----
            string[] rows =
            {
                ".X.", "XX.", ".X.", ".XX", "..X", ".X.", "XX.", "X..", "SSS", ".XX", "..X", ".X.", "XX.", ".X.", "..X", ".X."
            };
            float[] columns = { -5.5f, 0f, 5.5f };
            const float rowStart = 196f;
            const float rowPitch = 5.5f;
            for (int r = 0; r < rows.Length; r++)
            {
                float z = rowStart + r * rowPitch;
                if (rows[r] == "SSS")
                {
                    Deck(root, "Deck_DelibMid", 0f, z, 12f, 6f, 0f);
                    checkpoints.Add(MakeCheckpoint(root, 3, new Vector3(0f, 0f, z), 12f));
                    continue;
                }

                for (int c = 0; c < 3; c++)
                {
                    if (rows[r][c] != 'X') continue;
                    FallingTile tile = BuildFallingTile(root, "Tile_" + r + "_" + c, new Vector3(columns[c], 0f, z), new Vector2(4f, 4f));
                    tile.Configure(0.35f, 3f);
                    if (r == 2 || r == 6 || r == 10 || r == 13) BuildCoin(root, new Vector3(columns[c], 1.3f, z));
                }
            }
            float fieldEnd = rowStart + (rows.Length - 1) * rowPitch;
            BuildMeteorZone(root, "Meteors_Deliberation", new Vector3(0f, 0f, (rowStart + fieldEnd) * 0.5f), new Vector2(14f, fieldEnd - rowStart + 6f), 5);
            Deck(root, "Deck_DelibEnd", 0f, fieldEnd + 10.5f, 12f, 12f, 0f);
            checkpoints.Add(MakeCheckpoint(root, 4, new Vector3(0f, 0f, fieldEnd + 10.5f), 12f));

            // ---- 4. CROSS-EXAMINATION (295 -> 391): lasers, spinners and pendulums ----
            Deck(root, "Deck_Cross", 0f, 343f, 10f, 96f, 0f);
            float[] laserZ = { 310f, 322f, 334f, 346f };
            for (int i = 0; i < laserZ.Length; i++)
            {
                BuildPulseGate(root, "CrossLaser_" + (i + 1), new Vector3(0f, 0f, laserZ[i]), 9.4f, 3.2f, 1f, 1.4f,
                               Mathf.Repeat(-0.555f * i, 1f));
            }
            SpinBar(root, "CrossRotor_1", new Vector3(0f, 0.95f, 316f), 8f, 120f, 0.6f);
            SpinBar(root, "CrossRotor_2", new Vector3(0f, 0.95f, 340f), 8f, -140f, 0.6f);
            BuildPendulum(root, "CrossPendulum_1", new Vector3(0f, 10.2f, 362f), 8.6f, 60f, 2.4f, 0f, 5.6f, 0f);
            BuildPendulum(root, "CrossPendulum_2", new Vector3(0f, 10.2f, 376f), 8.6f, 60f, 2.4f, 0.5f, 5.6f, 0f);
            BuildCoin(root, new Vector3(0f, 1.3f, 328f));
            BuildCoin(root, new Vector3(0f, 1.3f, 352f));
            BuildCoin(root, new Vector3(0f, 1.3f, 369f));
            checkpoints.Add(MakeCheckpoint(root, 5, new Vector3(0f, 0f, 388f), 10f));

            // ---- 5. VERDICT (391 -> 497): boosted leap, crusher gauntlet, boosted final leap ----
            Deck(root, "Deck_Verdict1", 0f, 404f, 10f, 26f, 0f);
            BuildBoostPad(root, "Boost_V1", new Vector3(0f, 0f, 412.5f), 10f, 5f, 22f, 1.4f);
            BuildCoinLine(root, new Vector3(0f, 2.2f, 420.5f), new Vector3(0f, 2.2f, 424.5f), 3);

            Deck(root, "Deck_Verdict2", 0f, 449f, 10f, 42f, 0f);
            checkpoints.Add(MakeCheckpoint(root, 6, new Vector3(0f, 0f, 431f), 10f));
            float[] verdictZ = { 438f, 448f, 458f };
            float[] verdictPhase = { 0f, 0.33f, 0.66f };
            for (int i = 0; i < verdictZ.Length; i++)
            {
                SlamBlock(root, "VerdictSlam_" + (i + 1), new Vector3(0f, 7.8f, verdictZ[i]), new Vector3(10f, 1.6f, 2.6f), 6f, verdictPhase[i]);
                SlamFrame(root, verdictZ[i], 10f);
            }
            BuildBoostPad(root, "Boost_V2", new Vector3(0f, 0f, 466.5f), 10f, 5f, 22f, 1.4f);
            BuildCoinLine(root, new Vector3(0f, 2.2f, 473.5f), new Vector3(0f, 2.2f, 477.5f), 3);

            Deck(root, "Deck_Finish", 0f, FinishZ, 14f, 16f, 0f);
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
            Sign(root, "Sign_Title", "THE TRIBUNAL", new Vector3(0f, 22f, 44f), Vector3.zero, 22f, GoldText, 130, true);
            Sign(root, "Sign_Arena", "ARENA 03", new Vector3(0f, 11f, 34f), Vector3.zero, 12f, CyanText, 100, true);
            Sign(root, "Sign_Season", "SEASON ZERO", new Vector3(0f, 7.6f, 34f), Vector3.zero, 8f, VioletText, 70, true);
            Sign(root, "Decal_CONNECT", "CONNECT", new Vector3(0f, 0.05f, 9f), new Vector3(90f, 0f, 0f), 8f, CyanText, 120, false);

            Sign(root, "Sec_Indictment", "INDICTMENT", new Vector3(0f, 9f, 20f), Vector3.zero, 12f, GoldText, 100, true);
            Sign(root, "Sec_Witness", "WITNESS", new Vector3(0f, 15f, 100f), Vector3.zero, 9f, GoldText, 100, true);
            Sign(root, "Sec_Deliberation", "DELIBERATION", new Vector3(0f, 9f, 190f), Vector3.zero, 14f, GoldText, 100, true);
            Sign(root, "Sec_Cross", "CROSS-EXAMINATION", new Vector3(0f, 9f, 298f), Vector3.zero, 17f, GoldText, 90, true);
            Sign(root, "Sec_Verdict", "VERDICT", new Vector3(0f, 15f, 396f), Vector3.zero, 9f, GoldText, 100, true);

            Sign(root, "Holo_Tribunal", "TRIBUNAL", new Vector3(-17f, 8f, 70f), Vector3.zero, 8f, VioletText, 90, true);
            Sign(root, "Holo_Crucible", "CRUCIBLE\nCLEARED", new Vector3(17f, 8f, 150f), Vector3.zero, 8f, MagentaText, 80, true);
            Sign(root, "Holo_ZetraMail", "ZetraMail\n99+ unread", new Vector3(-17f, 9f, 230f), Vector3.zero, 8f, CyanText, 80, true);
            Sign(root, "Holo_Nigergram", "Nigergram", new Vector3(17f, 9f, 300f), Vector3.zero, 8f, VioletText, 90, true);
            Sign(root, "Holo_NaijaLearn", "NaijaLearn", new Vector3(-17f, 9f, 350f), Vector3.zero, 8f, GoldText, 90, true);
            Sign(root, "Holo_Store", "ZETRA STORE", new Vector3(17f, 9f, 410f), Vector3.zero, 9f, CyanText, 90, true);
            Sign(root, "Holo_NAI", "NAI\nJUDGING", new Vector3(-17f, 10f, 440f), Vector3.zero, 7f, VioletText, 80, true);

            Sign(root, "Sign_Finish", "FINISH", new Vector3(0f, 19.5f, FinishZ), Vector3.zero, 9f, Color.white, 130, true);
            Sign(root, "Sign_Complete", "SEASON ZERO COMPLETE", new Vector3(0f, 26f, FinishZ + 8f), Vector3.zero, 18f, GoldText, 100, true);
            Sign(root, "Decal_Names", "TOLUWANI / TOFUMI / FOLAKEMI / MARVELLOUS", new Vector3(0f, 0.05f, FinishZ + 4f), new Vector3(90f, 0f, 0f), 11f, GoldText, 46, false);
            Sign(root, "Decal_Signature", "CONNECT", new Vector3(0f, 0.05f, FinishZ + 7.5f), new Vector3(90f, 0f, 0f), 4f, CyanText, 90, false);
        }
    }
}
