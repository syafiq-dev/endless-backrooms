using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// Drop on an empty GameObject in an empty scene. Builds the maze, the player and the camera.
// Reaching the exit door starts a new run with a fresh maze.
public class BackroomsGame : MonoBehaviour
{
    public int width = 24, depth = 24; // ~20 min runs; growPerRun > 0 adds rooms a side each run, up to maxSize
    public int growPerRun = 0, maxSize = 32;
    public float cellSize = 6f, wallHeight = 3.4f; // tall office ceiling, like the reference footage
    [Range(0, 1)] public float loopChance = 0.3f; // higher = more open rooms, lower = tighter corridors
    public Color fogColor = new Color(0.50f, 0.48f, 0.20f);
    public float fogDensity = 0.035f;
    [Range(1, 5)] public int look = 2; // shader preset ShaderV1..V5 (Resources/Presets); each run picks one at random, L or the LOOK button cycles
    public static int Look { get; private set; } = 2; // read by VhsCamera
    public static float LookChangedAt = -10;
    public bool debugMode = false; // shows (DEBUG MODE) on the VHS stamp and each room's variant number on its floor

    Material mat;
    GameObject level, player;
    Mesh mesh;
    Vector3 exitPos;
    int run;

    void Start()
    {
        Application.targetFrameRate = 60;
        Look = look;
        mat = new Material(Shader.Find("Backrooms/Surface V" + look)); // lives in Resources/, so it ships in builds
        mat.SetTexture("_WallTex", Resources.Load<Texture2D>("backroom_wallpaper"));
        mat.SetTexture("_CarpetTex", Resources.Load<Texture2D>("carpet"));
        Shader.SetGlobalFloat("_CellSize", cellSize);
        Shader.SetGlobalFloat("_WallHeight", wallHeight);
        Shader.SetGlobalFloat("_FogDensity", fogDensity);
        Shader.SetGlobalColor("_FogColor", fogColor);
        player = CreatePlayer();
        NewRun();
    }

    void Update()
    {
        Shader.SetGlobalFloat("_Debug", debugMode ? 1 : 0); // read by the surface shader and VhsCamera
        if (Keyboard.current != null && Keyboard.current.lKey.wasPressedThisFrame) NextLook();
        if (look != Look)
        {
            Look = look; LookChangedAt = Time.time; // VhsCamera swaps its shader and pops up the name
            mat.shader = Shader.Find("Backrooms/Surface V" + look);
        }
        var d = player.transform.position - exitPos;
        if (new Vector2(d.x, d.z).sqrMagnitude < 1.2f * 1.2f) NewRun();
    }

    void NewRun()
    {
        if (level) { Destroy(level); Destroy(mesh); }
        look = Random.Range(1, 6); // a random look every run (Update swaps it and pops up the name)
        Shader.SetGlobalFloat("_Seed", Random.Range(0f, 100f)); // moves the low-power zones each run
        int grow = growPerRun * run++;
        var m = Maze.Generate(Mathf.Min(width + grow, maxSize), Mathf.Min(depth + grow, maxSize), Random.Range(int.MinValue, int.MaxValue), loopChance, out var start, out var exit);

        level = new GameObject("Level");
        mesh = BuildMesh(m, exit);
        level.AddComponent<MeshFilter>().sharedMesh = mesh;
        level.AddComponent<MeshRenderer>().sharedMaterial = mat;
        level.AddComponent<MeshCollider>().sharedMesh = mesh;

        var cc = player.GetComponent<CharacterController>();
        cc.enabled = false; // CharacterController ignores direct position writes while enabled
        player.transform.position = CellCenter(start);
        cc.enabled = true;
    }

    public void NextLook() => look = look % 5 + 1;

    Vector3 CellCenter((int x, int z) c) => new Vector3((c.x + 0.5f) * cellSize, 0, (c.z + 0.5f) * cellSize);

    GameObject CreatePlayer()
    {
        var p = new GameObject("Player");
        var cc = p.AddComponent<CharacterController>();
        cc.height = 1.6f; cc.radius = 0.3f; cc.center = new Vector3(0, 0.8f, 0);

        var camGo = new GameObject("Camera");
        camGo.transform.SetParent(p.transform, false);
        camGo.transform.localPosition = new Vector3(0, 1.5f, 0); // handheld camcorder height
        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = fogColor;
        cam.nearClipPlane = 0.05f;
        cam.farClipPlane = 100f; // fog hides everything past ~90m anyway
        camGo.AddComponent<AudioListener>();
        camGo.AddComponent<AmbientHum>();
        camGo.AddComponent<VhsCamera>();

        p.AddComponent<PlayerController>();
        return p;
    }

    Mesh BuildMesh(int[,] m, (int x, int z) exit) // also sets exitPos
    {
        var verts = new List<Vector3>();
        var norms = new List<Vector3>();
        var masks = new List<Vector2>(); // open-side mask + 16 * room variant: corner shadows and the debug floor number
        var tris = new List<int>();

        int o = 0;
        void Box(float cx, float cz, float hx, float hz, float h)
        {
            Quad(new Vector3(cx - hx, h, cz - hz), new Vector3(cx - hx, h, cz + hz), new Vector3(cx + hx, h, cz + hz), new Vector3(cx + hx, h, cz - hz), Vector3.up);
            Quad(new Vector3(cx - hx, 0, cz + hz), new Vector3(cx + hx, 0, cz + hz), new Vector3(cx + hx, h, cz + hz), new Vector3(cx - hx, h, cz + hz), Vector3.forward);
            Quad(new Vector3(cx - hx, 0, cz - hz), new Vector3(cx + hx, 0, cz - hz), new Vector3(cx + hx, h, cz - hz), new Vector3(cx - hx, h, cz - hz), Vector3.back);
            Quad(new Vector3(cx + hx, 0, cz - hz), new Vector3(cx + hx, 0, cz + hz), new Vector3(cx + hx, h, cz + hz), new Vector3(cx + hx, h, cz - hz), Vector3.right);
            Quad(new Vector3(cx - hx, 0, cz - hz), new Vector3(cx - hx, 0, cz + hz), new Vector3(cx - hx, h, cz + hz), new Vector3(cx - hx, h, cz - hz), Vector3.left);
        }
        // One prop part: a box in prop space (Props.cs), placed by t.
        void PropPart(Matrix4x4 t, Props.Part p)
        {
            var pt = t * Matrix4x4.TRS(p.c, Quaternion.Euler(0, 0, p.rz), Vector3.one);
            var c = new Vector3[8]; // corner i: bit 0 = +x, bit 1 = +y, bit 2 = +z
            for (int i = 0; i < 8; i++)
                c[i] = pt.MultiplyPoint3x4(new Vector3((i & 1) == 0 ? -p.h.x : p.h.x, (i & 2) == 0 ? -p.h.y : p.h.y, (i & 4) == 0 ? -p.h.z : p.h.z));
            Vector3 N(Vector3 v) => pt.MultiplyVector(v).normalized;
            o = 256 + p.mat; // tells the shader to paint it a flat prop colour
            Quad(c[0], c[2], c[6], c[4], N(Vector3.left)); Quad(c[1], c[3], c[7], c[5], N(Vector3.right));
            Quad(c[0], c[1], c[5], c[4], N(Vector3.down)); Quad(c[2], c[3], c[7], c[6], N(Vector3.up));
            Quad(c[0], c[1], c[3], c[2], N(Vector3.back)); Quad(c[4], c[5], c[7], c[6], N(Vector3.forward));
        }
        void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 n)
        {
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), n) < 0) (b, d) = (d, b); // face the normal
            int i = verts.Count;
            verts.AddRange(new[] { a, b, c, d });
            norms.AddRange(new[] { n, n, n, n });
            masks.AddRange(new[] { new Vector2(o, 0), new Vector2(o, 0), new Vector2(o, 0), new Vector2(o, 0) });
            tris.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
        }

        float s = cellSize, H = wallHeight, propChance = 1 / Random.Range(Props.MinOdds, Props.MaxOdds); // new odds every run
        const float WallHalf = 0.2f, Trim = 0.02f, TrimHeight = 0.1f; // chunky 40 cm walls like the footage
        void Wall(Vector3 a, Vector3 b, Vector3 n)
        {
            a += n * WallHalf; b += n * WallHalf;
            Quad(a, b, b + Vector3.up * H, a + Vector3.up * H, n);
            Vector3 ta = a + n * Trim, tb = b + n * Trim, up = Vector3.up * TrimHeight;
            Quad(ta, tb, tb + up, ta + up, n);
            Quad(a + up, b + up, tb + up, ta + up, Vector3.up);
        }
        for (int x = 0; x < m.GetLength(0); x++)
            for (int z = 0; z < m.GetLength(1); z++)
            {
                float x0 = x * s, x1 = x0 + s, z0 = z * s, z1 = z0 + s;
                // Straight runs often become narrow corridors (lined up, so the lights run in one file).
                // Otherwise mostly empty rooms; open crossings (halls) always stay empty so they read as big spaces.
                int open = m[x, z], variant, turn;
                bool isExit = (x, z) == exit;
                bool prop = !isExit && Random.value < propChance; // prop and exit rooms are left empty so nothing blocks them
                if (prop || isExit) { variant = 0; turn = 0; }
                else if ((open == (Maze.N | Maze.S) || open == (Maze.E | Maze.W)) && Random.value < 0.6f)
                {
                    variant = RoomVariants.Corridor;
                    turn = open == (Maze.N | Maze.S) ? 0 : 1;
                }
                else
                {
                    variant = open == 15 || Random.value < 0.5f ? 0 : Random.Range(1, RoomVariants.Corridor);
                    turn = Random.Range(0, 4);
                }
                o = m[x, z] + 16 * variant;
                float k = s / 4; // variants are laid out for 4 m cells
                foreach (var b in RoomVariants.Boxes[variant])
                {
                    var r = RoomVariants.Rotate(b, turn);
                    Box(x0 + s / 2 + r.x * k, z0 + s / 2 + r.y * k, r.z * k, r.w * k, H);
                }
                Quad(new Vector3(x0, 0, z0), new Vector3(x0, 0, z1), new Vector3(x1, 0, z1), new Vector3(x1, 0, z0), Vector3.up);
                Quad(new Vector3(x0, H, z0), new Vector3(x0, H, z1), new Vector3(x1, H, z1), new Vector3(x1, H, z0), Vector3.down);
                // Walls: each side is inset by WallHalf, so a shared wall is 20 cm thick. Ends hide inside the corner pillars.
                // A 10 cm baseboard sticks out 2 cm along the bottom.
                if ((o & Maze.N) == 0) { Wall(new Vector3(x0, 0, z1), new Vector3(x1, 0, z1), Vector3.back); }
                if ((o & Maze.S) == 0) { Wall(new Vector3(x0, 0, z0), new Vector3(x1, 0, z0), Vector3.forward); }
                if ((o & Maze.E) == 0) { Wall(new Vector3(x1, 0, z0), new Vector3(x1, 0, z1), Vector3.left); }
                if ((o & Maze.W) == 0) { Wall(new Vector3(x0, 0, z0), new Vector3(x0, 0, z1), Vector3.right); }
                if (prop) AddProp(new Vector3(x0 + s / 2, 0, z0 + s / 2), open, k);
            }

        List<Vector3> ClosedSides(int open) // inward normals
        {
            var walls = new List<Vector3>();
            if ((open & Maze.N) == 0) walls.Add(Vector3.back);
            if ((open & Maze.S) == 0) walls.Add(Vector3.forward);
            if ((open & Maze.E) == 0) walls.Add(Vector3.left);
            if ((open & Maze.W) == 0) walls.Add(Vector3.right);
            return walls;
        }

        // Exit door in the middle of one of the exit room's walls (Maze.Generate never picks a room without one).
        var exitWalls = ClosedSides(m[exit.x, exit.z]);
        var en = exitWalls[Random.Range(0, exitWalls.Count)];
        exitPos = CellCenter(exit) - en * (s / 2 - WallHalf);
        var doorT = Matrix4x4.TRS(exitPos, Quaternion.LookRotation(en), Vector3.one);
        foreach (var part in Props.ExitDoor) PropPart(doorT, part);

        // Rare furniture. About half stand normally on the floor; the rest are wrong: tilted and sunk
        // into the carpet, hanging upside down from the ceiling, or half inside a wall.
        void AddProp(Vector3 centre, int open, float k)
        {
            var item = Props.Items[Random.Range(0, Props.Items.Length)];
            float a = Random.value * Mathf.PI * 2, r = Random.Range(1f, 1.8f) * k; // off-centre, the spawn/exit spot stays clear
            var pos = centre + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * r;
            var rot = Quaternion.Euler(0, Random.value * 360, 0);
            float mode = Random.value;
            var walls = ClosedSides(open);
            if (mode >= 0.85f && walls.Count == 0) mode = 0; // no wall to sink into
            if (mode < 0.5f) { }
            else if (mode < 0.7f)
            {
                rot *= Quaternion.Euler(Random.Range(15f, 40f) * (Random.value < 0.5f ? 1 : -1), 0, Random.Range(-15f, 15f));
                pos.y = -Random.Range(0.05f, 0.3f);
            }
            else if (mode < 0.85f) { pos.y = H; rot *= Quaternion.Euler(180, 0, 0); }
            else
            {
                var n = walls[Random.Range(0, walls.Count)];
                var along = new Vector3(n.z, 0, -n.x);
                pos = centre - n * (s / 2 - WallHalf) + along * Random.Range(-1.5f, 1.5f);
                pos.y = Random.value < 0.6f ? 0 : Random.Range(0.3f, 1.2f); // sometimes stuck partway up
            }
            var t = Matrix4x4.TRS(pos, rot, Vector3.one);
            foreach (var part in item) PropPart(t, part);
        }

        // Grid corners: where any wall touches, a cap the wall's thickness closes the wall ends.
        // In open floor, now and then a freestanding square column.
        o = 15; // all open, so the shader doesn't shadow these faces
        int W = m.GetLength(0), D = m.GetLength(1);
        bool Closed(int cx, int cz, int side) => cx >= 0 && cz >= 0 && cx < W && cz < D && (m[cx, cz] & side) == 0;
        for (int x = 0; x <= W; x++)
            for (int z = 0; z <= D; z++)
            {
                bool wall = Closed(x - 1, z - 1, Maze.N) || Closed(x - 1, z - 1, Maze.E) || Closed(x, z - 1, Maze.N) || Closed(x, z - 1, Maze.W)
                         || Closed(x - 1, z, Maze.S) || Closed(x - 1, z, Maze.E) || Closed(x, z, Maze.S) || Closed(x, z, Maze.W);
                float half = wall ? WallHalf : Random.value < 0.2f ? 0.35f : 0;
                if (half == 0) continue;
                Box(x * s, z * s, half, half, H);
                Box(x * s, z * s, half + Trim, half + Trim, TrimHeight);
            }

        var mesh = new Mesh { name = "Maze" };
        if (verts.Count > 65535) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32; // big mazes
        mesh.SetVertices(verts);
        mesh.SetNormals(norms);
        mesh.SetUVs(0, masks);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
        return mesh;
    }
}
