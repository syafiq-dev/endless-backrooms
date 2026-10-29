using UnityEngine;

// The entity (same as the browser build). A slam somewhere out of sight once you've explored 30% of the rooms; at 35% it
// appears a few rooms behind you and follows the maze toward you, lit or dark, a bit faster than walking but slower than
// running. Get loseRooms ahead and it loses you and sinks away (it comes back after a while). If it reaches you, the tape ends.
// BackroomsGame adds this when entityOn is ticked and calls NewRun for every maze.
public class Entity : MonoBehaviour
{
    public float slamAt = 0.30f, wakeAt = 0.35f, speed = 3.4f, reach = 0.9f, cooldown = 25f;
    public int loseRooms = 6;

    static readonly int[] DX = { 0, 1, 0, -1 }, DZ = { 1, 0, -1, 0 };
    BackroomsGame game;
    Transform player;
    int[,] m;
    bool[,] seen;
    int count;
    bool on, slammed;
    float cool, fade, cell;
    Transform body;
    AudioSource drone, sfx;
    AudioClip slam;

    public void Init(BackroomsGame g, Transform p, Material mat, float cellSize)
    {
        game = g; player = p; cell = cellSize;
        var go = new GameObject("Entity");
        go.AddComponent<MeshFilter>().sharedMesh = BuildBody();
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
        m = maze; seen = new bool[m.GetLength(0), m.GetLength(1)]; count = 0; slammed = false; on = false; cool = 0;
        if (body) body.gameObject.SetActive(false);
    }

    void Update()
    {
        if (m == null || BackroomsGame.Peaceful) return;
        int W = m.GetLength(0), D = m.GetLength(1);
        var pp = player.position;
        (int x, int z) pc = (Mathf.Clamp(Mathf.FloorToInt(pp.x / cell), 0, W - 1), Mathf.Clamp(Mathf.FloorToInt(pp.z / cell), 0, D - 1));
        if (!seen[pc.x, pc.z]) { seen[pc.x, pc.z] = true; count++; }
        float explored = (float)count / (W * D);
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
        if (toPlayer.sqrMagnitude > 0.001f) body.rotation = Quaternion.LookRotation(toPlayer);
        drone.volume = Mathf.Min(0.9f, 6 / (toPlayer.magnitude + 2)) * (1 - Mathf.Min(1, fade)); // a low growl that swells as it closes in
        if (on && toPlayer.magnitude < reach) { on = false; body.gameObject.SetActive(false); game.Caught(); }
    }

    // Tall and thin, long arms, small head; +z faces you. Painted with the dark prop colour (uv0.x = 256 + 4).
    static Mesh BuildBody()
    {
        Vector3[][] boxes = {
            new[] { new Vector3(-0.12f, 0.55f, 0), new Vector3(0.06f, 0.55f, 0.07f) }, new[] { new Vector3(0.12f, 0.55f, 0), new Vector3(0.06f, 0.55f, 0.07f) },
            new[] { new Vector3(0, 1.45f, 0), new Vector3(0.2f, 0.38f, 0.1f) },
            new[] { new Vector3(-0.27f, 1.25f, 0.05f), new Vector3(0.04f, 0.55f, 0.05f) }, new[] { new Vector3(0.27f, 1.25f, 0.05f), new Vector3(0.04f, 0.55f, 0.05f) },
            new[] { new Vector3(0, 2.0f, 0.02f), new Vector3(0.1f, 0.14f, 0.11f) } };
        var v = new System.Collections.Generic.List<Vector3>(); var n = new System.Collections.Generic.List<Vector3>();
        var uv = new System.Collections.Generic.List<Vector2>(); var t = new System.Collections.Generic.List<int>();
        foreach (var b in boxes)
            for (int f = 0; f < 6; f++)
            {
                int ax = f >> 1, u = (ax + 1) % 3, w = (ax + 2) % 3; float sg = (f & 1) == 1 ? 1 : -1;
                var nn = Vector3.zero; nn[ax] = sg;
                int i0 = v.Count;
                foreach (var (a, c) in new[] { (-1f, -1f), (1f, -1f), (1f, 1f), (-1f, 1f) })
                {
                    var p = b[0]; p[ax] += sg * b[1][ax]; p[u] += a * b[1][u]; p[w] += c * b[1][w];
                    v.Add(p); n.Add(nn); uv.Add(new Vector2(256 + Props.Dark, 0));
                }
                // both windings, so every face shows whichever way the box was laid out
                t.AddRange(new[] { i0, i0 + 1, i0 + 2, i0, i0 + 2, i0 + 3, i0, i0 + 2, i0 + 1, i0, i0 + 3, i0 + 2 });
            }
        var mesh = new Mesh { name = "Entity" };
        mesh.SetVertices(v); mesh.SetNormals(n); mesh.SetUVs(0, uv); mesh.SetTriangles(t, 0); mesh.RecalculateBounds();
        return mesh;
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
