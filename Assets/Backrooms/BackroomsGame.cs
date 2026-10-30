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
    public static string PopupText; public static float PopupAt = -10; // VhsCamera shows PopupText mid-screen for a moment
    public static bool CanSearch, HasKey; // key-item prompt and KEY marker, drawn by VhsCamera
    public struct HideSpot { public Vector3 cam, step; public float yaw; public int kind; public Transform door; } // yaw in degrees, facing out into the room
    public static readonly List<HideSpot> HideSpots = new List<HideSpot>();
    public static int NearSpot = -1; // the hiding spot you're standing at (VhsCamera shows HIDE)
    public static bool NightVision; // N or the NIGHT button (VhsCamera): infrared lamp + green NightShot picture, no battery
    public static int Tape; // run number, shown as TAPE n
    public static float Seed; // this run's _Seed, so Wetness() matches the shaders
    public static void ToggleNightVision() { NightVision = !NightVision; Popup(NightVision ? "NIGHT SHOT" : "NIGHT SHOT OFF"); }
    public static void Popup(string text) { PopupText = text; PopupAt = Time.time; }
    public bool entityOn = true; // hidden switch for the entity (Entity.cs); off = no entity
    public static bool Started, Easy; // start screen (VhsCamera): NORMAL or EASY (no entity)
    public static void Begin(bool easy) { Easy = easy; Started = true; Time.timeScale = 1; }
    [Range(0, 1)] public float dimChance = 1 / 30f; // super rare dim runs
    public static bool Dim;
    public bool debugMode = false; // shows (DEBUG MODE) and the share of rooms explored on the VHS stamp

    Material mat;
    GameObject level, player;
    Entity entity;
    Mesh mesh;
    Vector3 exitPos, keySpot;
    readonly List<Vector3> fakeDoors = new List<Vector3>();
    bool locked, atDoor, atFake;
    int run;

    void Start()
    {
        debugMode = PlayerPrefs.GetInt("debugMode", debugMode ? 1 : 0) == 1; // the start screen's hidden switch wins over the Inspector
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
        if (entityOn) { entity = gameObject.AddComponent<Entity>(); entity.Init(this, player.transform, mat, cellSize); }
        NewRun();
        Time.timeScale = 0; // wait on the start screen
    }

    void Update()
    {
        Shader.SetGlobalFloat("_Debug", debugMode ? 1 : 0); // read by VhsCamera
        if (Keyboard.current != null && Keyboard.current.lKey.wasPressedThisFrame) NextLook();
        if (Keyboard.current != null && Keyboard.current.nKey.wasPressedThisFrame) ToggleNightVision();
        Shader.SetGlobalFloat("_NV", NightVision ? 1 : 0);
        if (look != Look)
        {
            Look = look; Popup("SHADERV" + look); // VhsCamera swaps its shader
            mat.shader = Shader.Find("Backrooms/Surface V" + look);
        }
        var p = player.transform.position;
        var d = p - exitPos;
        bool near = new Vector2(d.x, d.z).sqrMagnitude < 1.2f * 1.2f;
        if (near && locked && !HasKey) { if (!atDoor) Popup("LOCKED"); } // locked runs: find the key item first
        else if (near) NewRun();
        atDoor = near;
        bool fakeNear = false; // dead-end doors never open
        foreach (var f in fakeDoors) if (new Vector2(p.x - f.x, p.z - f.z).sqrMagnitude < 1.2f * 1.2f) fakeNear = true;
        if (fakeNear && !atFake) Popup("LOCKED");
        atFake = fakeNear;
        var kd = p - keySpot;
        CanSearch = locked && !HasKey && new Vector2(kd.x, kd.z).sqrMagnitude < 1.7f * 1.7f;
        NearSpot = -1;
        if (PlayerController.Hidden < 0)
            for (int i = 0; i < HideSpots.Count; i++)
                if (new Vector2(p.x - HideSpots[i].step.x, p.z - HideSpots[i].step.z).sqrMagnitude < 1f) NearSpot = i;
        if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame) Act();
    }

    void NewRun()
    {
        if (level) { foreach (var mf in level.GetComponentsInChildren<MeshFilter>()) Destroy(mf.sharedMesh); Destroy(level); } // the maze and the locker doors
        look = Random.Range(1, 6); // a random look every run (Update swaps it and pops up the name)
        Seed = Random.Range(0f, 100f);
        Shader.SetGlobalFloat("_Seed", Seed);
        Dim = Random.value < dimChance; Shader.SetGlobalFloat("_Dim", Dim ? 1 : 0); // super rare: every light at 2-40% // moves the low-power zones, blackouts and wet patches each run
        int grow = growPerRun * run++;
        Tape = run;
        var m = Maze.Generate(Mathf.Min(width + grow, maxSize), Mathf.Min(depth + grow, maxSize), Random.Range(int.MinValue, int.MaxValue), loopChance, out var start, out var exit, out var features);

        // Half the runs the exit is locked: the key is in a key item in the room farthest from both the start and the exit.
        (int x, int z)? key = null;
        locked = Random.value < Props.KeyChance; HasKey = false; atDoor = false;
        if (locked)
        {
            var ds = Maze.Distances(m, start); var de = Maze.Distances(m, exit);
            int best = -1;
            for (int x = 0; x < m.GetLength(0); x++)
                for (int z = 0; z < m.GetLength(1); z++)
                {
                    int v = Mathf.Min(ds[x, z], de[x, z]);
                    if (m[x, z] != 15 && !features.arch[x, z] && v > best && (x, z) != start) { best = v; key = (x, z); } // needs a wall to stand against
                }
        }

        level = new GameObject("Level");
        mesh = BuildMesh(m, exit, key, features);
        level.AddComponent<MeshFilter>().sharedMesh = mesh;
        level.AddComponent<MeshRenderer>().sharedMaterial = mat;
        level.AddComponent<MeshCollider>().sharedMesh = mesh;

        if (entity) entity.NewRun(m);
        var cc = player.GetComponent<CharacterController>();
        PlayerController.Hidden = -1; PlayerController.HideDir = 0; PlayerController.Energy = 1; PlayerController.Winded = false;
        cc.enabled = false; // CharacterController ignores direct position writes while enabled
        player.transform.position = CellCenter(start);
        cc.enabled = true;
    }

    public void Caught() { Popup("TAPE ENDS"); NewRun(); } // the entity got you: the tape cuts and a new one starts

    public void NextLook() => look = look % 5 + 1;
    public void ToggleDebug() { debugMode = !debugMode; PlayerPrefs.SetInt("debugMode", debugMode ? 1 : 0); Popup(debugMode ? "DEBUG ON" : "DEBUG OFF"); } // remembered on this device
    // The action button (E): LEAVE a hiding spot, HIDE in the one you're at, or SEARCH the key item.
    public void Act()
    {
        var pc = player.GetComponent<PlayerController>();
        if (PlayerController.Hidden >= 0) pc.Leave();
        else if (NearSpot >= 0) pc.Hide(NearSpot);
        else if (CanSearch) { HasKey = true; CanSearch = false; Popup("GOT KEY"); }
    }

    // Same noise as the Surface shaders' wetness(), so footsteps (PlayerController) know where the carpet is wet.
    static float Hash(float x, float y)
    {
        var p3 = new Vector3(Frac(x * 0.1031f), Frac(y * 0.1031f), Frac(x * 0.1031f));
        float d = Vector3.Dot(p3, new Vector3(p3.y, p3.z, p3.x) + Vector3.one * 33.33f);
        p3 += Vector3.one * d;
        return Frac((p3.x + p3.y) * p3.z);
    }
    static float Frac(float v) => v - Mathf.Floor(v);
    static float VNoise(float x, float y)
    {
        float ix = Mathf.Floor(x), iy = Mathf.Floor(y), fx = x - ix, fy = y - iy, ux = fx * fx * (3 - 2 * fx), uy = fy * fy * (3 - 2 * fy);
        return Mathf.Lerp(Mathf.Lerp(Hash(ix, iy), Hash(ix + 1, iy), ux), Mathf.Lerp(Hash(ix, iy + 1), Hash(ix + 1, iy + 1), ux), uy);
    }
    public static float Wetness(float x, float z)
    {
        float rx = x * 0.8f + z * 0.6f, rz = z * 0.8f - x * 0.6f;
        float v = VNoise(rx * 0.09f + Seed * 1.3f + 70, rz * 0.09f + Seed * 1.3f + 70) + VNoise(rx * 0.9f + 3.3f, rz * 0.9f + 3.3f) * 0.12f - 0.06f;
        return Mathf.Clamp01((v - 0.92f) / 0.04f);
    }

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
        cam.nearClipPlane = 0.1f; // you never get closer to a wall than 25 cm; a farther near plane means finer depth and less flicker
        cam.fieldOfView = 78f; // a bit wider than default, so rooms read bigger
        cam.farClipPlane = 100f; // fog hides everything past ~90m anyway
        camGo.AddComponent<AudioListener>();
        camGo.AddComponent<AmbientHum>();
        camGo.AddComponent<VhsCamera>();

        p.AddComponent<PlayerController>();
        return p;
    }

    Mesh BuildMesh(int[,] m, (int x, int z) exit, (int x, int z)? key, Maze.Features f) // also sets exitPos, keySpot and fakeDoors
    {
        var verts = new List<Vector3>();
        var norms = new List<Vector3>();
        var masks = new List<Vector2>(); // open-side mask + 16 * room variant: corner shadows and the debug floor number
        var tris = new List<int>();

        int o = 0;
        void Box(float cx, float cz, float hx, float hz, float h, float y = 0) // y > 0: hangs from the ceiling (a beam), so it gets an underside
        {
            Quad(new Vector3(cx - hx, h, cz - hz), new Vector3(cx - hx, h, cz + hz), new Vector3(cx + hx, h, cz + hz), new Vector3(cx + hx, h, cz - hz), Vector3.up);
            if (y > 0) Quad(new Vector3(cx - hx, y, cz - hz), new Vector3(cx - hx, y, cz + hz), new Vector3(cx + hx, y, cz + hz), new Vector3(cx + hx, y, cz - hz), Vector3.down);
            Quad(new Vector3(cx - hx, y, cz + hz), new Vector3(cx + hx, y, cz + hz), new Vector3(cx + hx, h, cz + hz), new Vector3(cx - hx, h, cz + hz), Vector3.forward);
            Quad(new Vector3(cx - hx, y, cz - hz), new Vector3(cx + hx, y, cz - hz), new Vector3(cx + hx, h, cz - hz), new Vector3(cx - hx, h, cz - hz), Vector3.back);
            Quad(new Vector3(cx + hx, y, cz - hz), new Vector3(cx + hx, y, cz + hz), new Vector3(cx + hx, h, cz + hz), new Vector3(cx + hx, h, cz - hz), Vector3.right);
            Quad(new Vector3(cx - hx, y, cz - hz), new Vector3(cx - hx, y, cz + hz), new Vector3(cx - hx, h, cz + hz), new Vector3(cx - hx, h, cz - hz), Vector3.left);
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
            if (p.h.z == 0) { Quad(c[4], c[5], c[7], c[6], N(Vector3.forward)); return; } // no depth: one face, seen from the front only
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
    // Corner caps and archway beams are drawn this much thinner than they really are, so their faces sit just behind the wall or
    // pillar faces they line up with. Two faces in the same plane flicker (z-fighting).
    const float Seam = 0.003f;
        void Wall(Vector3 a, Vector3 b, Vector3 n)
        {
            a += n * WallHalf; b += n * WallHalf;
            Quad(a, b, b + Vector3.up * H, a + Vector3.up * H, n);
            Vector3 ta = a + n * Trim, tb = b + n * Trim, up = Vector3.up * TrimHeight;
            Quad(ta, tb, tb + up, ta + up, n);
            Quad(a + up, b + up, tb + up, ta + up, Vector3.up);
        }
        int W = m.GetLength(0), D = m.GetLength(1);
        int[] DX = { 0, 1, 0, -1 }, DZ = { 1, 0, -1, 0 };
        // Dead-end doors: about 3 a map, well away from the real exit, in rooms with a wall to set them in.
        var fake = new bool[W, D];
        for (int x = 0; x < W; x++)
            for (int z = 0; z < D; z++)
                fake[x, z] = Random.value < 5f / (W * D) && m[x, z] != 15 && !f.arch[x, z] && Mathf.Abs(x - exit.x) + Mathf.Abs(z - exit.z) > 6 && (x, z) != key;
        // Plan every room first, so a wrong-way room can copy the one next door.
        var variant = new int[W, D]; var turn = new int[W, D]; var twin = new int[W, D]; var prop = new bool[W, D];
        for (int x = 0; x < W; x++)
            for (int z = 0; z < D; z++)
            {
                // Straight runs often become narrow corridors (long halls always), lined up so the lights run in one file.
                // Otherwise mostly empty rooms; open crossings (halls) and archway halls always stay empty so they read as big spaces.
                int open = m[x, z];
                bool isExit = (x, z) == exit || (x, z) == key; // exit and key rooms stay empty
                prop[x, z] = !isExit && !fake[x, z] && !f.arch[x, z] && Random.value < propChance; // prop rooms are left empty so nothing blocks them
                if (prop[x, z] || isExit || fake[x, z] || f.arch[x, z]) { }
                else if ((open == (Maze.N | Maze.S) || open == (Maze.E | Maze.W)) && (f.longHall[x, z] || Random.value < 0.6f))
                {
                    variant[x, z] = RoomVariants.Corridor;
                    turn[x, z] = open == (Maze.N | Maze.S) ? 0 : 1;
                }
                else
                {
                    variant[x, z] = open == 15 || Random.value < 0.5f ? 0 : Random.Range(1, RoomVariants.Corridor);
                    turn[x, z] = Random.Range(0, 4);
                }
            }
        // Wrong-way rooms (~1 in 100): the room you walk into is the room you just left. Same shape, same layout, and the
        // shader borrows the other room's cell (uv0.x + 1024 * direction), so the same lights are dead and the same stains are on the carpet.
        bool Plain(int x, int z) => !prop[x, z] && !fake[x, z] && !f.arch[x, z] && (x, z) != exit && (x, z) != key;
        for (int x = 0; x < W; x++)
            for (int z = 0; z < D; z++)
            {
                if (Random.value > 0.01f || !Plain(x, z) || m[x, z] == 15) continue;
                for (int d = 0; d < 4; d++)
                {
                    int nx = x + DX[d], nz = z + DZ[d];
                    if ((m[x, z] & (1 << d)) == 0 || nx < 0 || nz < 0 || nx >= W || nz >= D || m[nx, nz] != m[x, z] || !Plain(nx, nz) || twin[nx, nz] > 0) continue;
                    variant[x, z] = variant[nx, nz]; turn[x, z] = turn[nx, nz]; twin[x, z] = d + 1;
                    break;
                }
            }
        const float Arm = 0.55f, Beam = 0.09f, BeamDrop = 0.45f; // archway pillars: + arms (half length, half thickness), and how far beams hang down
        for (int x = 0; x < W; x++)
            for (int z = 0; z < D; z++)
            {
                float x0 = x * s, x1 = x0 + s, z0 = z * s, z1 = z0 + s;
                int open = m[x, z];
                o = m[x, z] + 16 * variant[x, z] + 1024 * twin[x, z];
                if (f.longHall[x, z] && variant[x, z] == RoomVariants.Corridor)
                    for (int b = 0; b < 3; b++)
                    {
                        // Long halls: a low beam across the corridor every 2 m, so it reads as arches marching off into the distance.
                        float c = 2 * b, c1 = c + 1;
                        bool alongX = turn[x, z] == 1;
                        if (alongX) Box(x0 + c, z0 + s / 2, Beam, s / 4, H, H - BeamDrop); else Box(x0 + s / 2, z0 + c, s / 4, Beam, H, H - BeamDrop);
                        // Funnel: every 2 m the walls step in and the ceiling drops, down to a 1.2 m wide, 2.2 m high squeeze.
                        if (f.funnel[x, z] == 0) continue;
                        float t = ((f.funnel[x, z] - 1) * 3 + b + 1) / 9f, w = 1.35f - 0.75f * t, mid = (w + 1.35f) / 2, half = (1.35f - w) / 2;
                        foreach (int sd in new[] { -1, 1 })
                            if (alongX) Box(x0 + c1, z0 + s / 2 + sd * mid, 1, half, H); else Box(x0 + s / 2 + sd * mid, z0 + c1, half, 1, H);
                        if (alongX) Box(x0 + c1, z0 + s / 2, 1, 1.35f, H, H - 1.2f * t); else Box(x0 + s / 2, z0 + c1, 1.35f, 1, H, H - 1.2f * t);
                    }
                float k = s / 4; // variants are laid out for 4 m cells
                foreach (var b in RoomVariants.Boxes[variant[x, z]])
                {
                    var r = RoomVariants.Rotate(b, turn[x, z]);
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
                if (prop[x, z]) AddProp(new Vector3(x0 + s / 2, 0, z0 + s / 2), open, k);
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
        Shader.SetGlobalVector("_ExitPos", exitPos + en * 0.17f + Vector3.up * 2.45f); // the sign's red glow

        fakeDoors.Clear(); HideSpots.Clear();
        for (int x = 0; x < W; x++)
            for (int z = 0; z < D; z++)
            {
                if (!fake[x, z]) continue;
                var fw = ClosedSides(m[x, z]);
                var fn = fw[Random.Range(0, fw.Count)];
                var at = CellCenter((x, z)) - fn * (s / 2 - WallHalf);
                var ft = Matrix4x4.TRS(at, Quaternion.LookRotation(fn), Vector3.one);
                foreach (var part in Props.FakeDoor) PropPart(ft, part);
                fakeDoors.Add(at);
            }

        // Key item in the middle of one of the key room's walls.
        if (key is (int kx, int kz))
        {
            var kWalls = ClosedSides(m[kx, kz]);
            var kn = kWalls[Random.Range(0, kWalls.Count)];
            var keyItem = Props.KeyItems[Random.Range(0, Props.KeyItems.Length)];
            var at = CellCenter((kx, kz)) - kn * (s / 2 - WallHalf);
            var kt = Matrix4x4.TRS(at, Quaternion.LookRotation(kn), Vector3.one);
            foreach (var part in keyItem.parts) PropPart(kt, part);
            keySpot = at + kn * keyItem.depth;
        }

        // Hiding spots: a locker or a desk against a wall of some of the empty rooms.
        for (int x = 0; x < W; x++)
            for (int z = 0; z < D; z++)
            {
                if (variant[x, z] != 0 || !Plain(x, z) || twin[x, z] > 0 || f.longHall[x, z] || m[x, z] == 15 || Random.value > Props.HideChance) continue;
                var hWalls = ClosedSides(m[x, z]);
                var hn = hWalls[Random.Range(0, hWalls.Count)];
                int kind = Random.Range(0, Props.Hides.Length);
                var hide = Props.Hides[kind];
                var at = CellCenter((x, z)) - hn * (s / 2 - WallHalf);
                var ht = Matrix4x4.TRS(at, Quaternion.LookRotation(hn), Vector3.one);
                foreach (var part in hide.parts) PropPart(ht, part);
                Transform door = null;
                if (kind == 0) // the locker door gets its own mesh, turning on its hinge (PlayerController swings it)
                {
                    var keep = (verts, norms, masks, tris);
                    (verts, norms, masks, tris) = (new List<Vector3>(), new List<Vector3>(), new List<Vector2>(), new List<int>());
                    foreach (var part in Props.LockerDoor) PropPart(Matrix4x4.Translate(-Props.DoorHinge), part);
                    var pivot = new GameObject("Locker").transform;
                    pivot.SetParent(level.transform); pivot.SetPositionAndRotation(ht.MultiplyPoint3x4(Props.DoorHinge), Quaternion.LookRotation(hn));
                    door = new GameObject("Door").transform; door.SetParent(pivot, false);
                    door.gameObject.AddComponent<MeshFilter>().sharedMesh = Make("LockerDoor");
                    door.gameObject.AddComponent<MeshRenderer>().sharedMaterial = mat;
                    var col = door.gameObject.AddComponent<BoxCollider>(); col.center = new Vector3(0.3f, 0.95f, 0); col.size = new Vector3(0.6f, 1.9f, 0.02f);
                    (verts, norms, masks, tris) = keep;
                }
                HideSpots.Add(new HideSpot { cam = ht.MultiplyPoint3x4(hide.cam), step = at + hn * hide.step, yaw = Quaternion.LookRotation(hn).eulerAngles.y, kind = kind, door = door });
            }

        // Rare furniture. About half stand normally on the floor; the rest are wrong: tilted and sunk
        // into the carpet, hanging upside down from the ceiling, or half inside a wall.
        void AddProp(Vector3 centre, int open, float k)
        {
            int pick = Random.Range(0, Props.Items.Length);
            var item = Props.Items[pick];
            float sc = Random.value < Props.BigChance ? Random.Range(1.15f, 1.4f) : 1; // now and then slightly too big
            float a = Random.value * Mathf.PI * 2, r = Mathf.Max(Random.Range(1f, 1.8f) * k, Props.Footprint[pick] * sc * 1.3f + 0.8f); // off-centre, the spawn/exit spot stays clear
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
            var t = Matrix4x4.TRS(pos, rot, Vector3.one * sc);
            foreach (var part in item) PropPart(t, part);
        }

        // Grid corners: where any wall touches, a cap the wall's thickness closes the wall ends.
        // In open floor, now and then a freestanding square column.
        o = 15; // all open, so the shader doesn't shadow these faces
        // Archway halls: + pillars on a 2 m grid, joined by low beams. A point gets a pillar only when every room it
        // touches is part of the archway hall, so the edges of the hall stay clear.
        bool Pillar(int i, int j)
        {
            int xa = i % 3 == 0 ? i / 3 - 1 : i / 3, za = j % 3 == 0 ? j / 3 - 1 : j / 3; // the rooms this grid point touches
            for (int x = xa; x <= i / 3; x++)
                for (int z = za; z <= j / 3; z++)
                    if (x < 0 || z < 0 || x >= W || z >= D || !f.arch[x, z]) return false;
            return true;
        }
        for (int i = 0; i <= 3 * W; i++)
            for (int j = 0; j <= 3 * D; j++)
            {
                if (!Pillar(i, j)) continue;
                float px = 2 * i, pz = 2 * j;
                Box(px, pz, Arm, Beam, H); Box(px, pz, Beam, Arm, H);
                if (Pillar(i + 1, j)) Box(px + 1, pz, 1, Beam - Seam, H, H - BeamDrop);
                if (Pillar(i, j + 1)) Box(px, pz + 1, Beam - Seam, 1, H, H - BeamDrop);
            }

        bool Closed(int cx, int cz, int side) => cx >= 0 && cz >= 0 && cx < W && cz < D && (m[cx, cz] & side) == 0;
        for (int x = 0; x <= W; x++)
            for (int z = 0; z <= D; z++)
            {
                bool wall = Closed(x - 1, z - 1, Maze.N) || Closed(x - 1, z - 1, Maze.E) || Closed(x, z - 1, Maze.N) || Closed(x, z - 1, Maze.W)
                         || Closed(x - 1, z, Maze.S) || Closed(x - 1, z, Maze.E) || Closed(x, z, Maze.S) || Closed(x, z, Maze.W);
                bool inArch = x < W && z < D && f.arch[x, z] || x > 0 && z > 0 && f.arch[x - 1, z - 1]; // archway halls have their own pillars
                float half = wall ? WallHalf : inArch ? 0 : Random.value < 0.2f ? 0.35f : 0;
                if (half == 0) continue;
                Box(x * s, z * s, half - Seam, half - Seam, H);
                Box(x * s, z * s, half + Trim - Seam, half + Trim - Seam, TrimHeight);
            }

        return Make("Maze");

        Mesh Make(string name)
        {
            var mesh = new Mesh { name = name };
            if (verts.Count > 65535) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32; // big mazes
            mesh.SetVertices(verts);
            mesh.SetNormals(norms);
            mesh.SetUVs(0, masks);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
