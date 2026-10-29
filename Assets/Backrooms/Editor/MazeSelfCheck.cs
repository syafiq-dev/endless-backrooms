using UnityEditor;
using UnityEngine;

public static class MazeSelfCheck
{
    [MenuItem("Backrooms/Run Maze Self-Check")]
    static void Run()
    {
        for (int seed = 0; seed < 200; seed++)
        {
            var m = Maze.Generate(12, 9, seed, 0.15f, out var start, out var exit);
            var dist = Maze.Distances(m, start);
            for (int x = 0; x < 12; x++)
                for (int z = 0; z < 9; z++)
                {
                    if (dist[x, z] < 0) { Debug.LogError($"seed {seed}: cell {x},{z} unreachable"); return; }
                    // Fittings must line up with their neighbours, or you'd walk into a wall.
                    if (x < 11 && ((m[x, z] & Maze.E) != 0) != ((m[x + 1, z] & Maze.W) != 0)) { Debug.LogError($"seed {seed}: E/W mismatch at {x},{z}"); return; }
                    if (z < 8 && ((m[x, z] & Maze.N) != 0) != ((m[x, z + 1] & Maze.S) != 0)) { Debug.LogError($"seed {seed}: N/S mismatch at {x},{z}"); return; }
                }
            if (start == exit) { Debug.LogError($"seed {seed}: exit == start"); return; }
        }
        Debug.Log("Maze self-check passed (200 seeds).");
    }
}
