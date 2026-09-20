using UnityEngine;
using UnityEngine.SceneManagement;

namespace Zetra.ClashDash
{
    public sealed class ArenaInfo
    {
        public readonly int Number;
        public readonly string Id;
        public readonly string Title;
        public readonly string SceneName;
        public readonly float ParTime;

        public ArenaInfo(int number, string id, string title, string sceneName, float parTime)
        {
            Number = number;
            Id = id;
            Title = title;
            SceneName = sceneName;
            ParTime = parTime;
        }

        /// <summary>True when the scene is included in the build (or the editor's build settings).</summary>
        public bool IsInBuild => Application.CanStreamedLevelBeLoaded(SceneName);
    }

    /// <summary>
    /// Single source of truth for arenas. Add a new arena here (and build its scene) to have it appear in the
    /// menu, unlock chain and "next arena" flow.
    /// </summary>
    public static class ArenaCatalog
    {
        public static readonly ArenaInfo[] All =
        {
            new ArenaInfo(1, "ARENA_01", "ARENA 01 - THE TEST", "Arena01_TheTest", 95f),
            new ArenaInfo(2, "ARENA_02", "ARENA 02 - THE CRUCIBLE", "Arena02_TheCrucible", 125f),
            new ArenaInfo(3, "ARENA_03", "ARENA 03 - THE TRIBUNAL", "Arena03_TheTribunal", 150f)
        };

        public static ArenaInfo Get(int number)
        {
            for (int i = 0; i < All.Length; i++)
            {
                if (All[i].Number == number) return All[i];
            }
            return null;
        }

        /// <summary>Arena that matches the active scene (scene names may carry a " 1" suffix from unique naming).</summary>
        public static ArenaInfo Current()
        {
            string sceneName = SceneManager.GetActiveScene().name;
            for (int i = 0; i < All.Length; i++)
            {
                if (sceneName.StartsWith(All[i].SceneName)) return All[i];
            }
            return All[0];
        }

        public static ArenaInfo Next(ArenaInfo arena)
        {
            return arena == null ? null : Get(arena.Number + 1);
        }

        /// <summary>Mirrors the rank thresholds used by GameManager.</summary>
        public static string ComputeRank(float totalTime, int mistakes, float parTime)
        {
            if (mistakes <= 1 && totalTime <= parTime * 0.85f) return "S";
            if (totalTime <= parTime) return "A";
            if (totalTime <= parTime * 1.4f) return "B";
            return "C";
        }
    }
}
