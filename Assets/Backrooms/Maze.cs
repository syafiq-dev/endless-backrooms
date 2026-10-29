using System.Collections.Generic;

// Each cell is a 1x1 "pipe fitting": a 4-bit mask of open sides.
// 1 open side = dead end, 2 = straight or corner, 3 = T, 4 = cross.
public static class Maze
{
    public const int N = 1, E = 2, S = 4, W = 8; // N = +z, E = +x
    static readonly int[] DX = { 0, 1, 0, -1 }, DZ = { 1, 0, -1, 0 };
    static readonly int[] Bit = { N, E, S, W }, Opp = { S, W, N, E };

    public static int[,] Generate(int w, int h, int seed, float loopChance, out (int x, int z) start, out (int x, int z) exit, out Features f)
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

        // Long halls: a couple of dead-straight runs that go on far longer than they should. The first ends in a funnel.
        f = new Features { longHall = new bool[w, h], arch = new bool[w, h], funnel = new int[w, h] };
        for (int i = 0; i < System.Math.Max(1, w * h / 250); i++)
        {
            bool alongX = rng.NextDouble() < 0.5;
            int len = System.Math.Min(10 + rng.Next(7), (alongX ? w : h) - 1);
            int a0 = rng.Next((alongX ? w : h) - len), b = rng.Next(alongX ? h : w);
            for (int k = 0; k <= len; k++)
            {
                var (x, z) = alongX ? (a0 + k, b) : (b, a0 + k);
                f.longHall[x, z] = true;
                if (i == 0 && k > len - 3) f.funnel[x, z] = k - len + 3; // 3 rooms that close in
                if (k < len) Carve(m, x, z, alongX ? 1 : 0);
            }
        }

        // Archway halls: open 3-5 x 3-5 blocks that BackroomsGame fills with + pillars joined by low beams.
        for (int i = 0; i < w * h / 150; i++)
        {
            int hw = rng.Next(3, 6), hd = rng.Next(3, 6);
            if (hw > w || hd > h) continue;
            int hx = rng.Next(w - hw + 1), hz = rng.Next(h - hd + 1);
            for (int x = hx; x < hx + hw; x++)
                for (int z = hz; z < hz + hd; z++)
                {
                    f.arch[x, z] = true; f.longHall[x, z] = false; f.funnel[x, z] = 0;
                    if (x + 1 < hx + hw) Carve(m, x, z, 1);
                    if (z + 1 < hz + hd) Carve(m, x, z, 0);
                }
        }

        // The funnel's rooms are walled off at the sides so it stays one straight squeeze, unless that would strand rooms.
        var cells = new List<(int x, int z)>();
        for (int x = 0; x < w; x++) for (int z = 0; z < h; z++) if (f.funnel[x, z] > 0) cells.Add((x, z));
        if (cells.Count > 1)
        {
            var cut = cells[0].z == cells[1].z ? new[] { 0, 2 } : new[] { 1, 3 }; // close N/S on an east-west run, E/W on a north-south one
            var closedOff = new List<(int x, int z, int d)>();
            foreach (var (x, z) in cells)
                foreach (int d in cut)
                    if ((m[x, z] & Bit[d]) != 0) { m[x, z] &= ~Bit[d]; m[x + DX[d], z + DZ[d]] &= ~Opp[d]; closedOff.Add((x, z, d)); }
            var reach = Distances(m, start);
            bool stranded = false;
            foreach (var v in reach) if (v < 0) stranded = true;
            if (stranded) foreach (var (x, z, d) in closedOff) Carve(m, x, z, d);
        }

        // Exit = the cell farthest (by walking) from the start.
        var dist = Distances(m, start);
        exit = start;
        for (int x = 0; x < w; x++)
            for (int z = 0; z < h; z++)
                if (dist[x, z] > dist[exit.x, exit.z] && m[x, z] != 15 && !f.arch[x, z]) exit = (x, z); // needs a wall for the exit door
        return m;
    }

    // Special areas, decided with the maze. funnel: 1..3 along a long hall's last rooms (0 = none).
    public struct Features { public bool[,] longHall, arch; public int[,] funnel; }

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
