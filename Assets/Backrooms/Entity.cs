using UnityEngine;

// The entity (same as the browser build). A slam somewhere out of sight once you've explored 30% of the rooms; at 35% it
// appears a few rooms behind you and follows the maze toward you, lit or dark, a bit faster than walking but slower than
// running. Get loseRooms ahead and it loses you and sinks away (it comes back after a while). If it reaches you, the tape ends.
// BackroomsGame adds this when entityOn is ticked and calls NewRun for every maze.
public class Entity : MonoBehaviour
{
    public float slamAt = 0.30f, wakeAt = 0.35f, speed = 3.4f, reach = 0.9f, cooldown = 25f;
    public int loseRooms = 6;
    public static float Explored; // share of rooms you've been in this run, shown in debug mode

    static readonly int[] DX = { 0, 1, 0, -1 }, DZ = { 1, 0, -1, 0 };
    BackroomsGame game;
    Transform player;
    int[,] m;
    bool[,] seen;
    int count;
    bool on, slammed;
    float cool, fade, cell;
    Transform body;
    Mesh mesh;
    AudioSource drone, sfx;
    AudioClip slam;

    public void Init(BackroomsGame g, Transform p, Material mat, float cellSize)
    {
        game = g; player = p; cell = cellSize;
        var go = new GameObject("Entity");
        go.AddComponent<MeshFilter>().sharedMesh = mesh = new Mesh { name = "Entity" };
        go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        body = go.transform;
        go.SetActive(false);
        drone = p.gameObject.AddComponent<AudioSource>();
        drone.clip = MakeDrone(); drone.loop = true; drone.volume = 0; drone.Play();
        sfx = p.gameObject.AddComponent<AudioSource>();
        slam = MakeSlam();
    }

    public void NewRun(int[,] maze)
    {
        m = maze; seen = new bool[m.GetLength(0), m.GetLength(1)]; count = 0; Explored = 0; slammed = false; on = false; cool = 0;
        if (body) body.gameObject.SetActive(false);
    }

    void Update()
    {
        if (m == null) return;
        Shader.SetGlobalVector("_EntPos", on ? new Vector4(body.position.x, 1.5f + body.position.y, body.position.z, 1 - Mathf.Min(1, fade / 1.5f)) : Vector4.zero); // its black aura
        int W = m.GetLength(0), D = m.GetLength(1);
        var pp = player.position;
        (int x, int z) pc = (Mathf.Clamp(Mathf.FloorToInt(pp.x / cell), 0, W - 1), Mathf.Clamp(Mathf.FloorToInt(pp.z / cell), 0, D - 1));
        if (!seen[pc.x, pc.z]) { seen[pc.x, pc.z] = true; count++; }
        float explored = Explored = (float)count / (W * D);
        if (BackroomsGame.Easy) return; // still counted, for the debug readout
        if (explored >= slamAt && !slammed) { slammed = true; sfx.PlayOneShot(slam, 0.9f); } // the warning
        cool -= Time.deltaTime;
        var d = Maze.Distances(m, pc);
        if (!on)
        {
            drone.volume = Mathf.MoveTowards(drone.volume, 0, Time.deltaTime);
            if (explored < wakeAt || cool > 0) return;
            // spawn 3-5 rooms away, preferably behind you
            var fwd = player.forward; float score = -1e9f; (int x, int z)? best = null;
            for (int x = 0; x < W; x++)
                for (int z = 0; z < D; z++)
                {
                    if (d[x, z] < 3 || d[x, z] > 5) continue;
                    float sc = -(((x + 0.5f) * cell - pp.x) * fwd.x + ((z + 0.5f) * cell - pp.z) * fwd.z) + Random.value * 4;
                    if (sc > score) { score = sc; best = (x, z); }
                }
            if (best is not (int bx, int bz)) return;
            on = true; fade = 0;
            body.position = new Vector3((bx + 0.5f) * cell, 0, (bz + 0.5f) * cell);
            body.gameObject.SetActive(true);
        }
        // follow the maze: head for the centre of the next room on the way to you, then straight at you
        var ep = body.position;
        (int x, int z) ec = (Mathf.Clamp(Mathf.FloorToInt(ep.x / cell), 0, W - 1), Mathf.Clamp(Mathf.FloorToInt(ep.z / cell), 0, D - 1));
        var target = new Vector3(pp.x, 0, pp.z);
        if (ec != pc)
        {
            for (int k = 0; k < 4; k++)
            {
                if ((m[ec.x, ec.z] & (1 << k)) == 0) continue;
                int nx = ec.x + DX[k], nz = ec.z + DZ[k];
                if (d[nx, nz] != d[ec.x, ec.z] - 1) continue;
                target = new Vector3((nx + 0.5f) * cell, 0, (nz + 0.5f) * cell);
                // line up with the doorway before cutting across, so it never clips a wall corner
                if (DX[k] != 0) ep.z = Mathf.MoveTowards(ep.z, (ec.z + 0.5f) * cell, Time.deltaTime * 2);
                else ep.x = Mathf.MoveTowards(ep.x, (ec.x + 0.5f) * cell, Time.deltaTime * 2);
                break;
            }
        }
        if (d[ec.x, ec.z] > loseRooms) // you got away: it sinks out of sight
        {
            fade += Time.deltaTime;
            if (fade > 1.5f) { on = false; cool = cooldown; body.gameObject.SetActive(false); }
        }
        else
        {
            fade = Mathf.Max(0, fade - Time.deltaTime);
            ep = Vector3.MoveTowards(new Vector3(ep.x, 0, ep.z), target, speed * Time.deltaTime);
        }
        var toPlayer = new Vector3(pp.x - ep.x, 0, pp.z - ep.z);
        body.position = new Vector3(ep.x, -fade * 0.6f, ep.z);
        Pose(mesh, Time.time);
        if (toPlayer.sqrMagnitude > 0.001f) body.rotation = Quaternion.LookRotation(toPlayer);
        drone.volume = Mathf.Min(0.9f, 6 / (toPlayer.magnitude + 2)) * (1 - Mathf.Min(1, fade)); // a low growl that swells as it closes in
        if (on && toPlayer.magnitude < reach) { on = false; body.gameObject.SetActive(false); game.Caught(); }
    }

    // A stick man of black wire, crooked and alien: hunched, head jutting and tilted, one shoulder low, arms hanging past the
    // knees, the right knee bent the wrong way. Joints in metres, +z faces you; bones (joint, joint, radius), each drawn as two
    // strands twisted round each other. Same numbers as ENT_J / ENT_BONES in the browser build. Painted black (uv0.x = 256 + 7).
    static readonly Vector3[] Joints = { new Vector3(0, 1.05f, 0), new Vector3(0.03f, 1.5f, 0.12f), new Vector3(-0.02f, 1.85f, 0.3f), new Vector3(0.05f, 2.02f, 0.45f), new Vector3(0.17f, 2.1f, 0.66f), new Vector3(0.2f, 2.32f, 0.62f), new Vector3(-0.3f, 1.97f, 0.26f), new Vector3(0.26f, 1.78f, 0.32f), new Vector3(-0.46f, 1.38f, 0.08f), new Vector3(-0.4f, 0.72f, 0.36f), new Vector3(0.47f, 1.3f, 0.48f), new Vector3(0.38f, 0.8f, 0.74f), new Vector3(-0.13f, 1.05f, 0), new Vector3(0.12f, 1.0f, 0.02f), new Vector3(-0.22f, 0.55f, 0.24f), new Vector3(-0.16f, 0.04f, -0.06f), new Vector3(0.24f, 0.58f, -0.2f), new Vector3(0.18f, 0.06f, 0.06f), new Vector3(-0.46f, 0.38f, 0.42f), new Vector3(-0.36f, 0.4f, 0.48f), new Vector3(-0.4f, 0.34f, 0.52f), new Vector3(0.44f, 0.45f, 0.84f), new Vector3(0.33f, 0.48f, 0.88f), new Vector3(0.38f, 0.42f, 0.92f), new Vector3(-0.2f, 0.02f, 0.18f), new Vector3(0.22f, 0.02f, 0.32f), new Vector3(0.12f, 1.98f, 0.7f) };
    static readonly (int a, int b, float r)[] Bones = { (0, 1, 0.03f), (1, 2, 0.03f), (2, 3, 0.025f), (3, 4, 0.02f), (4, 5, 0.018f), (5, 3, 0.015f), (4, 26, 0.015f), (26, 3, 0.015f), (2, 6, 0.022f), (2, 7, 0.022f), (6, 7, 0.012f), (6, 1, 0.012f), (7, 1, 0.012f), (6, 8, 0.02f), (8, 9, 0.018f), (7, 10, 0.02f), (10, 11, 0.018f), (9, 18, 0.01f), (9, 19, 0.01f), (9, 20, 0.01f), (11, 21, 0.01f), (11, 22, 0.01f), (11, 23, 0.01f), (0, 12, 0.022f), (0, 13, 0.022f), (12, 13, 0.012f), (12, 14, 0.024f), (14, 15, 0.022f), (15, 24, 0.015f), (13, 16, 0.024f), (16, 17, 0.022f), (17, 25, 0.015f) };
    readonly System.Collections.Generic.List<Vector3> bv = new(), bn = new();
    readonly System.Collections.Generic.List<Vector2> buv = new();
    readonly System.Collections.Generic.List<int> bt = new();

    // Rebuilt every frame in its pose: the head jerks now and then, the hands sway.
    void Pose(Mesh mesh, float t)
    {
        var j = (Vector3[])Joints.Clone();
        float jerk = Mathf.Sin(Mathf.Floor(t * 3) * 78.233f) > 0.6f ? Mathf.Sin(Mathf.Floor(t * 3) * 12.9898f) * 0.08f : 0;
        foreach (int k in new[] { 4, 5, 26 }) j[k] += new Vector3(jerk, -Mathf.Abs(jerk) * 0.5f, 0);
        foreach (int k in new[] { 9, 18, 19, 20 }) j[k].z += Mathf.Sin(t * 1.3f) * 0.06f;
        foreach (int k in new[] { 11, 21, 22, 23 }) j[k].z += Mathf.Sin(t * 1.1f + 2) * 0.06f;
        bv.Clear(); bn.Clear(); buv.Clear(); bt.Clear();
        foreach (var (ja, jb, r) in Bones)
        {
            Vector3 A = j[ja], B = j[jb], d = (B - A).normalized;
            Vector3 p = Vector3.Cross(d, Mathf.Abs(d.y) > 0.9f ? Vector3.right : Vector3.up).normalized, q = Vector3.Cross(d, p);
            float w = r * 0.6f;
            foreach (float sg in new[] { 1f, -1f }) // two strands, swapping sides along the bone
            {
                Vector3 a = A + p * r * 0.7f * sg, b = B - p * r * 0.7f * sg;
                foreach (var (u, v) in new[] { (p, q), (q, -p), (-p, -q), (-q, p) }) // 4 sides of a thin square rod
                {
                    int i0 = bv.Count;
                    bv.Add(a + (u - v) * w); bv.Add(a + (u + v) * w); bv.Add(b + (u + v) * w); bv.Add(b + (u - v) * w);
                    for (int k = 0; k < 4; k++) { bn.Add(u); buv.Add(new Vector2(256 + Props.Black, 0)); }
                    // both windings, so every side shows whichever way the rod was laid out
                    bt.AddRange(new[] { i0, i0 + 1, i0 + 2, i0, i0 + 2, i0 + 3, i0, i0 + 2, i0 + 1, i0, i0 + 3, i0 + 2 });
                }
            }
        }
        mesh.Clear(); mesh.SetVertices(bv); mesh.SetNormals(bn); mesh.SetUVs(0, buv); mesh.SetTriangles(bt, 0); mesh.RecalculateBounds();
    }

    // Two detuned saws, low-passed: the drone.
    static AudioClip MakeDrone()
    {
        int sr = AudioSettings.outputSampleRate, len = sr * 4;
        var d = new float[len]; float lp = 0;
        for (int i = 0; i < len; i++)
        {
            float k = (float)i / sr, saw = (k * 41 % 1) + (k * 43.5f % 1) - 1;
            lp += (saw - lp) * 0.025f; d[i] = lp * 0.5f;
        }
        var clip = AudioClip.Create("Drone", len, 1, sr, false); clip.SetData(d, 0); return clip;
    }

    // Something heavy hitting a wall a few rooms away: a thump, a crack and a rattle, muffled.
    static AudioClip MakeSlam()
    {
        int sr = AudioSettings.outputSampleRate, len = (int)(sr * 1.2f);
        var d = new float[len]; float b = 0, lp = 0;
        for (int i = 0; i < len; i++)
        {
            float k = (float)i / sr;
            b = (b + Random.Range(-1f, 1f) * 0.3f) * 0.96f;
            float raw = Mathf.Sin(2 * Mathf.PI * 48 * k) * Mathf.Exp(-k * 9) * 1.2f + b * Mathf.Exp(-k * 14) * 1.5f + Random.Range(-1f, 1f) * Mathf.Exp(-k * 5) * 0.08f * (1 + Mathf.Sin(k * 90));
            lp += (raw - lp) * 0.12f; d[i] = lp;
        }
        var clip = AudioClip.Create("Slam", len, 1, sr, false); clip.SetData(d, 0); return clip;
    }
}
