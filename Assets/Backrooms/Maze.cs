using System.Collections.Generic;

// Each cell is a 1x1 "pipe fitting": a 4-bit mask of open sides.
// 1 open side = dead end, 2 = straight or corner, 3 = T, 4 = cross.
public static class Maze
{
    public const int N = 1, E = 2, S = 4, W = 8; // N = +z, E = +x
    static readonly int[] DX = { 0, 1, 0, -1 }, DZ = { 1, 0, -1, 0 };
    static readonly int[] Bit = { N, E, S, W }, Opp = { S, W, N, E };

    public static int[,] Generate(int w, int h, int seed, float loopChance, out (int x, int z) start, out (int x, int z) exit)
    {
        var rng = new System.Random(seed);
        var m = new int[w, h];
        var visited = new bool[w, h];
        var stack = new Stack<(int x, int z)>();
        var dirs = new List<int>(4);

        // Recursive backtracker: every cell reachable, long winding corridors.
        start = (rng.Next(w), rng.Next(h));
        visited[start.x, start.z] = true;
        stack.Push(start);
        while (stack.Count > 0)
        {
            var (x, z) = stack.Peek();
            dirs.Clear();
            for (int d = 0; d < 4; d++)
            {
                int nx = x + DX[d], nz = z + DZ[d];
                if (nx >= 0 && nz >= 0 && nx < w && nz < h && !visited[nx, nz]) dirs.Add(d);
            }
            if (dirs.Count == 0) { stack.Pop(); continue; }
            int dir = dirs[rng.Next(dirs.Count)];
            Carve(m, x, z, dir);
            visited[x + DX[dir], z + DZ[dir]] = true;
            stack.Push((x + DX[dir], z + DZ[dir]));
        }

        // Knock out a few extra walls so it loops back on itself (more T's and crosses, easier to get lost).
        for (int x = 0; x < w; x++)
            for (int z = 0; z < h; z++)
                for (int d = 0; d < 2; d++) // N and E only, so each wall is considered once
                    if (x + DX[d] < w && z + DZ[d] < h && (m[x, z] & Bit[d]) == 0 && rng.NextDouble() < loopChance)
                        Carve(m, x, z, d);

        // Open halls: knock out every wall inside a random 2-4 x 2-4 block, so room sizes vary.
        for (int i = 0; i < w * h / 20; i++)
        {
            int hw = rng.Next(2, 5), hd = rng.Next(2, 5);
            if (hw > w || hd > h) continue;
            int hx = rng.Next(w - hw + 1), hz = rng.Next(h - hd + 1);
            for (int x = hx; x < hx + hw; x++)
                for (int z = hz; z < hz + hd; z++)
                {
                    if (x + 1 < hx + hw) Carve(m, x, z, 1); // E
                    if (z + 1 < hz + hd) Carve(m, x, z, 0); // N
                }
        }

        // Exit = the cell farthest (by walking) from the start.
        var dist = Distances(m, start);
        exit = start;
        for (int x = 0; x < w; x++)
            for (int z = 0; z < h; z++)
                if (dist[x, z] > dist[exit.x, exit.z] && m[x, z] != 15) exit = (x, z); // needs a wall for the exit door
        return m;
    }

    static void Carve(int[,] m, int x, int z, int d)
    {
        m[x, z] |= Bit[d];
        m[x + DX[d], z + DZ[d]] |= Opp[d];
    }

    // BFS walking distance from a cell; -1 = unreachable.
    public static int[,] Distances(int[,] m, (int x, int z) from)
    {
        int w = m.GetLength(0), h = m.GetLength(1);
        var dist = new int[w, h];
        for (int x = 0; x < w; x++) for (int z = 0; z < h; z++) dist[x, z] = -1;
        var q = new Queue<(int x, int z)>();
        dist[from.x, from.z] = 0;
        q.Enqueue(from);
        while (q.Count > 0)
        {
            var (x, z) = q.Dequeue();
            for (int d = 0; d < 4; d++)
            {
                if ((m[x, z] & Bit[d]) == 0) continue;
                int nx = x + DX[d], nz = z + DZ[d];
                if (dist[nx, nz] >= 0) continue;
                dist[nx, nz] = dist[x, z] + 1;
                q.Enqueue((nx, nz));
            }
        }
        return dist;
    }
}
