using UnityEngine;
using UnityEngine.InputSystem;

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
    float holdTime; // hidden debug switch on the start screen
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
        if (!BackroomsGame.Started) // start screen: title, then NORMAL or EASY (no entity)
        {
            lookStyle ??= new GUIStyle(GUI.skin.label) { fontSize = Screen.height / 10, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(1f, 1f, 0.92f, 0.95f) } };
            GUI.Label(new Rect(0, Screen.height * 0.2f, Screen.width, Screen.height * 0.2f), "ENDLESS BACKROOMS", lookStyle);
            float w = Screen.height * 0.4f, h = Screen.height * 0.12f, y = Screen.height * 0.55f;
            if (GUI.Button(new Rect(Screen.width / 2f - w - 10, y, w, h), "NORMAL")) BackroomsGame.Begin(false);
            if (GUI.Button(new Rect(Screen.width / 2f + 10, y, w, h), "EASY")) BackroomsGame.Begin(true);
            // Hidden debug switch: hold the bottom middle of the start screen for 3 s.
            var ptr = Pointer.current;
            var at = ptr != null ? ptr.position.ReadValue() : Vector2.zero;
            bool held = ptr != null && ptr.press.isPressed && new Rect(Screen.width * 0.35f, Screen.height * 0.86f, Screen.width * 0.3f, Screen.height * 0.14f).Contains(new Vector2(at.x, Screen.height - at.y));
            if (Event.current.type == EventType.Repaint)
            {
                holdTime = held ? holdTime + Time.unscaledDeltaTime : 0;
                if (holdTime > 3) { holdTime = -1e9f; FindAnyObjectByType<BackroomsGame>()?.ToggleDebug(); } // once per hold
            }
            return;
        }
        // LOOK button bottom-right cycles the shader presets; the new name pops up mid-screen for a moment.
        float bw = Screen.height * 0.22f, bh = Screen.height * 0.08f;
        if (GUI.Button(new Rect(Screen.width - bw - Screen.height * 0.04f, Screen.height - bh - Screen.height * 0.04f, bw, bh), "LOOK"))
            FindAnyObjectByType<BackroomsGame>()?.NextLook();
        // NIGHT (or N): night vision. A big square button mid-right, easy to hit with the right thumb.
        float nw = Screen.height * 0.16f;
        if (GUI.Button(new Rect(Screen.width - nw - Screen.height * 0.04f, (Screen.height - nw) / 2, nw, nw), BackroomsGame.NightVision ? "NIGHT ON" : "NIGHT"))
            BackroomsGame.ToggleNightVision();
        // RUN: tap to toggle running (phones), bigger, bottom-right above LOOK.
        float rw = Screen.height * 0.24f;
        if (GUI.Button(new Rect(Screen.width - rw - Screen.height * 0.04f, Screen.height - bh - rw - Screen.height * 0.08f, rw, rw), PlayerController.RunHeld ? "RUN ON" : "RUN"))
            PlayerController.RunHeld = !PlayerController.RunHeld;
        // Running energy: a thin vertical bar just left of RUN, red while you're winded.
        var bar = new Rect(Screen.width - rw - Screen.height * 0.04f - 14, Screen.height - bh - rw - Screen.height * 0.08f, 8, rw);
        GUI.color = new Color(1, 1, 0.92f, 0.35f); GUI.DrawTexture(bar, Texture2D.whiteTexture);
        GUI.color = PlayerController.Winded ? new Color(0.82f, 0.29f, 0.23f) : new Color(1, 1, 0.92f);
        GUI.DrawTexture(new Rect(bar.x, bar.y + bar.height * (1 - PlayerController.Energy), bar.width, bar.height * PlayerController.Energy), Texture2D.whiteTexture);
        GUI.color = Color.white;
        // Key item prompt (also E on a keyboard) and the KEY marker once you have it.
        float sw = Screen.height * 0.32f, sh = Screen.height * 0.1f;
        bool hidden = PlayerController.Hidden >= 0;
        if (PlayerController.HideDir == 0 && (hidden || BackroomsGame.NearSpot >= 0 || BackroomsGame.CanSearch) && GUI.Button(new Rect((Screen.width - sw) / 2, Screen.height * 0.7f, sw, sh), hidden ? "LEAVE" : BackroomsGame.NearSpot >= 0 ? "HIDE" : "SEARCH"))
            FindAnyObjectByType<BackroomsGame>()?.Act();
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
        string tape = (BackroomsGame.HasKey ? "KEY  " : "") + "TAPE " + BackroomsGame.Tape; // each run is another tape
        GUI.Label(new Rect(Screen.width - m - style.fontSize * 6, m, style.fontSize * 6, style.fontSize * 1.5f), tape, new GUIStyle(style) { alignment = TextAnchor.UpperRight });
        var now = System.DateTime.Now;
        string stamp = now.ToString("tt h:mm:ss") + "\n" + now.ToString("MMM. dd").ToUpper() + " 1996";
        GUI.Label(new Rect(m, Screen.height - m - style.fontSize * 2.6f, Screen.width, style.fontSize * 3), stamp, style);
        if (Shader.GetGlobalFloat("_Debug") > 0) // set by BackroomsGame.debugMode; sits above the stamp
        {
            debugStyle ??= new GUIStyle(style) { normal = { textColor = new Color(0.3f, 1f, 0.35f, 0.95f) } };
            debugStyle.fontSize = style.fontSize / 2; // half the stamp's size
            GUI.Label(new Rect(m, Screen.height - m - style.fontSize * 3.3f, Screen.width, style.fontSize), "(DEBUG MODE) EXPLORED " + Mathf.FloorToInt(Entity.Explored * 100) + "%", debugStyle); // slam at 20, lights stutter at 23, entity at 25
        }
    }

    void OnDestroy()
    {
        if (rt) rt.Release();
    }
}
