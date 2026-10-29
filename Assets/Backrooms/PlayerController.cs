using UnityEngine;
using UnityEngine.InputSystem;

// Phone: drag on the left half to walk, drag on the right half to look.
// Editor/desktop: WASD to walk, hold right mouse button to look.
[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    public float speed = 3f, mouseLook = 0.15f, touchLook = 0.15f;
    public float lookSpring = 90f, lookDamp = 15f; // camera chases your aim on a spring (slight overshoot) instead of snapping, like a handheld camcorder
    [Range(0, 2)] public float sway = 1f; // handheld camcorder wobble; 0 = tripod-steady

    CharacterController cc;
    Transform cam;
    AudioSource steps;
    AudioClip thud, squelch;
    float pitch, camY, bob, walk, aimYaw, yaw, yawSpin, pitchView, pitchSpin;

    void Awake()
    {
        cc = GetComponent<CharacterController>();
        cam = GetComponentInChildren<Camera>().transform;
        camY = cam.localPosition.y;
        aimYaw = yaw = transform.eulerAngles.y;
        steps = gameObject.AddComponent<AudioSource>();
        thud = MakeStep(0.12f, false);
        squelch = MakeStep(0.24f, true);
    }

    // Footstep sounds, generated like AmbientHum: a soft carpet thud (smoothed brown noise), and a wet squelch
    // (noise ringing through a falling resonance) for soaked carpet (BackroomsGame.Wetness).
    static AudioClip MakeStep(float len, bool wet)
    {
        int sr = AudioSettings.outputSampleRate, n = (int)(sr * len);
        var d = new float[n];
        float b = 0, lp = 0, bp = 0, bv = 0;
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / n, env = Mathf.Min(1, i / (sr * 0.012f)) * Mathf.Exp(-t * 6);
            b = (b + Random.Range(-1f, 1f) * 0.2f) * 0.9f;
            if (!wet) { lp += (b - lp) * 0.08f; d[i] = lp * env * 4; continue; }
            float w = 2 * Mathf.PI * Mathf.Lerp(1400, 500, t) / sr; // resonance sweeping down
            bv += (b - bp) * w - bv * 0.08f; bp += bv * w;
            d[i] = bp * env * 2.5f;
        }
        var clip = AudioClip.Create(wet ? "Squelch" : "Step", n, 1, sr, false);
        clip.SetData(d, 0);
        return clip;
    }

    void Update()
    {
        Vector2 move = Vector2.zero, look = Vector2.zero;

        var kb = Keyboard.current;
        if (kb != null)
            move += new Vector2((kb.dKey.isPressed ? 1 : 0) - (kb.aKey.isPressed ? 1 : 0),
                                (kb.wKey.isPressed ? 1 : 0) - (kb.sKey.isPressed ? 1 : 0));

        var mouse = Mouse.current;
        if (mouse != null && mouse.rightButton.isPressed) look += mouse.delta.ReadValue() * mouseLook;

        var ts = Touchscreen.current;
        if (ts != null)
            foreach (var touch in ts.touches)
            {
                if (!touch.press.isPressed) continue;
                var startPos = touch.startPosition.ReadValue();
                if (startPos.x < Screen.width * 0.5f)
                    move += Vector2.ClampMagnitude((touch.position.ReadValue() - startPos) / (Screen.height * 0.12f), 1f);
                else
                    look += touch.delta.ReadValue() * touchLook;
            }

        move = Vector2.ClampMagnitude(move, 1f);
        aimYaw += look.x;
        pitch = Mathf.Clamp(pitch - look.y, -80f, 80f);
        if (Mathf.Abs(Mathf.DeltaAngle(transform.eulerAngles.y, yaw)) > 0.01f) aimYaw = yaw = transform.eulerAngles.y; // something else turned us (new run): snap
        float dt = Mathf.Min(Time.deltaTime, 0.05f);
        yawSpin += (lookSpring * (aimYaw - yaw) - lookDamp * yawSpin) * dt; yaw += yawSpin * dt;
        pitchSpin += (lookSpring * (pitch - pitchView) - lookDamp * pitchSpin) * dt; pitchView += pitchSpin * dt;
        transform.rotation = Quaternion.Euler(0, yaw, 0);
        // Handheld camcorder: a slow drifting wobble and tilt all the time, plus a step rhythm while walking.
        walk = Mathf.Lerp(walk, move.magnitude, Mathf.Min(1, Time.deltaTime * 5));
        int step = Mathf.FloorToInt(bob / Mathf.PI);
        bob += move.magnitude * Time.deltaTime * 9;
        if (Mathf.FloorToInt(bob / Mathf.PI) != step) // a footfall at every dip of the bob
        {
            steps.pitch = Random.Range(0.9f, 1.1f);
            steps.PlayOneShot(thud, Random.Range(0.5f, 0.7f));
            float wet = BackroomsGame.Wetness(transform.position.x, transform.position.z);
            if (wet > 0.3f) steps.PlayOneShot(squelch, 0.35f * wet);
        }
        float t = Time.time, amp = sway * (1 + walk * 1.5f);
        float Drift(float a, float b, float c) => Mathf.Sin(t * a) * 0.5f + Mathf.Sin(t * b + 1.7f) * 0.3f + Mathf.Sin(t * c + 4.1f) * 0.2f;
        cam.localEulerAngles = new Vector3(pitchView + Drift(0.29f, 0.71f, 1.53f) * 0.35f * amp, Drift(0.37f, 0.83f, 1.9f) * 0.45f * amp,
                                           Drift(0.23f, 0.61f, 1.31f) * 0.7f * amp + Mathf.Sin(bob * 0.5f) * 0.6f * walk * sway - Mathf.Clamp(yawSpin * 0.015f, -3.5f, 3.5f)); // leans into fast turns
        cam.localPosition = new Vector3(Mathf.Sin(bob * 0.5f) * 0.025f * walk * sway, camY + Mathf.Sin(bob) * 0.03f * walk * sway, 0);
        cc.SimpleMove(transform.TransformDirection(new Vector3(move.x, 0, move.y)) * speed); // SimpleMove adds gravity
    }
}
