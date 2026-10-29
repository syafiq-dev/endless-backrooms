using UnityEngine;

// Renders the game camera into a low-res "tape" frame, then shows it full screen through the VHS shader.
// Works in plain URP with no renderer-feature setup: a second camera looks at one quad on a spare layer.
// Low res is part of the look and also keeps the fragment cost down on phones.
[RequireComponent(typeof(Camera))]
public class VhsCamera : MonoBehaviour
{
    public int lines = 360; // vertical resolution of the tape; 240-480 looks right
    public bool dateStamp = true;

    const int Layer = 31; // unnamed layers work by index; only the screen quad lives here

    Camera cam;
    RenderTexture rt;
    Material mat;
    Transform quad;
    GUIStyle style, debugStyle, lookStyle;
    int look;

    void Start()
    {
        cam = GetComponent<Camera>();
        cam.cullingMask &= ~(1 << Layer);
        look = BackroomsGame.Look;
        mat = new Material(Shader.Find("Backrooms/VHS V" + look)); // in Resources/, so it ships in builds

        var displayGo = new GameObject("VHS Display");
        displayGo.transform.position = new Vector3(0, -1000, 0); // far from the level
        var display = displayGo.AddComponent<Camera>();
        display.orthographic = true;
        display.orthographicSize = 1;
        display.cullingMask = 1 << Layer;
        display.clearFlags = CameraClearFlags.SolidColor;
        display.backgroundColor = Color.black;
        display.nearClipPlane = 0.1f;
        display.farClipPlane = 10f;
        display.depth = cam.depth + 1;

        var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Destroy(q.GetComponent<Collider>());
        q.layer = Layer;
        q.GetComponent<MeshRenderer>().sharedMaterial = mat;
        quad = q.transform;
        quad.position = displayGo.transform.position + Vector3.forward;
    }

    void LateUpdate()
    {
        if (look != BackroomsGame.Look) { look = BackroomsGame.Look; mat.shader = Shader.Find("Backrooms/VHS V" + look); }
        float aspect = (float)Screen.width / Screen.height;
        int h = lines, w = Mathf.RoundToInt(lines * aspect);
        if (rt == null || rt.width != w || rt.height != h)
        {
            if (rt) { cam.targetTexture = null; rt.Release(); }
            rt = new RenderTexture(w, h, 24) { filterMode = FilterMode.Bilinear };
            cam.targetTexture = rt;
            mat.mainTexture = rt;
            mat.SetVector("_SrcSize", new Vector2(w, h));
        }
        quad.localScale = new Vector3(2 * aspect, 2, 1);
    }

    // Camcorder overlay: PLAY in the corner, time and date bottom-left (year pinned to 1996 for the found-footage feel).
    void OnGUI()
    {
        // LOOK button bottom-right cycles the shader presets; the new name pops up mid-screen for a moment.
        float bw = Screen.height * 0.22f, bh = Screen.height * 0.08f;
        if (GUI.Button(new Rect(Screen.width - bw - Screen.height * 0.04f, Screen.height - bh - Screen.height * 0.04f, bw, bh), "LOOK"))
            FindAnyObjectByType<BackroomsGame>()?.NextLook();
        // NIGHT button next to it (or N): night vision.
        if (GUI.Button(new Rect(Screen.width - 2 * bw - Screen.height * 0.07f, Screen.height - bh - Screen.height * 0.04f, bw, bh), BackroomsGame.NightVision ? "NIGHT ON" : "NIGHT"))
            BackroomsGame.ToggleNightVision();
        // Key item prompt (also E on a keyboard) and the KEY marker once you have it.
        float sw = Screen.height * 0.32f, sh = Screen.height * 0.1f;
        if (BackroomsGame.CanSearch && GUI.Button(new Rect((Screen.width - sw) / 2, Screen.height * 0.7f, sw, sh), "SEARCH"))
            FindAnyObjectByType<BackroomsGame>()?.Search();
        if (Time.time - BackroomsGame.PopupAt < 1.5f) // look names, LOCKED, GOT KEY
        {
            lookStyle ??= new GUIStyle(GUI.skin.label) { fontSize = Screen.height / 10, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(1f, 1f, 0.92f, 0.95f) } };
            GUI.Label(new Rect(0, 0, Screen.width, Screen.height), BackroomsGame.PopupText, lookStyle);
        }
        if (!dateStamp) return;
        if (style == null)
        {
            style = new GUIStyle(GUI.skin.label) { fontSize = Screen.height / 8, fontStyle = FontStyle.Bold }; // big camcorder text
            style.normal.textColor = new Color(1f, 1f, 0.92f, 0.9f);
        }
        float m = Screen.height * 0.05f;
        GUI.Label(new Rect(m, m, Screen.width, style.fontSize * 1.5f), "PLAY", style);
        if (BackroomsGame.HasKey) GUI.Label(new Rect(Screen.width - m - style.fontSize * 3, m, style.fontSize * 3, style.fontSize * 1.5f), "KEY", style);
        var now = System.DateTime.Now;
        string stamp = now.ToString("tt h:mm:ss") + "\n" + now.ToString("MMM. dd").ToUpper() + " 1996";
        GUI.Label(new Rect(m, Screen.height - m - style.fontSize * 2.6f, Screen.width, style.fontSize * 3), stamp, style);
        if (Shader.GetGlobalFloat("_Debug") > 0) // set by BackroomsGame.debugMode; sits above the stamp
        {
            debugStyle ??= new GUIStyle(style) { normal = { textColor = new Color(0.3f, 1f, 0.35f, 0.95f) } };
            GUI.Label(new Rect(m, Screen.height - m - style.fontSize * 3.9f, Screen.width, style.fontSize * 1.5f), "(DEBUG MODE)", debugStyle);
        }
    }

    void OnDestroy()
    {
        if (rt) rt.Release();
    }
}
