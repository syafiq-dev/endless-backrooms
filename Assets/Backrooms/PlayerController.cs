using UnityEngine;
using UnityEngine.InputSystem;

// Phone: drag on the left half to walk, drag on the right half to look.
// Editor/desktop: WASD to walk, hold right mouse button to look.
[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    public float speed = 3f, mouseLook = 0.15f, touchLook = 0.15f;
    [Range(0, 2)] public float sway = 1f; // handheld camcorder wobble; 0 = tripod-steady

    CharacterController cc;
    Transform cam;
    float pitch, camY, bob, walk;

    void Awake()
    {
        cc = GetComponent<CharacterController>();
        cam = GetComponentInChildren<Camera>().transform;
        camY = cam.localPosition.y;
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
            foreach (var t in ts.touches)
            {
                if (!t.press.isPressed) continue;
                var startPos = t.startPosition.ReadValue();
                if (startPos.x < Screen.width * 0.5f)
                    move += Vector2.ClampMagnitude((t.position.ReadValue() - startPos) / (Screen.height * 0.12f), 1f);
                else
                    look += t.delta.ReadValue() * touchLook;
            }

        move = Vector2.ClampMagnitude(move, 1f);
        transform.Rotate(0, look.x, 0);
        pitch = Mathf.Clamp(pitch - look.y, -80f, 80f);
        // Handheld camcorder: a slow drifting wobble and tilt all the time, plus a step rhythm while walking.
        walk = Mathf.Lerp(walk, move.magnitude, Mathf.Min(1, Time.deltaTime * 5));
        bob += move.magnitude * Time.deltaTime * 9;
        float t = Time.time, amp = sway * (1 + walk * 1.5f);
        float Drift(float a, float b, float c) => Mathf.Sin(t * a) * 0.5f + Mathf.Sin(t * b + 1.7f) * 0.3f + Mathf.Sin(t * c + 4.1f) * 0.2f;
        cam.localEulerAngles = new Vector3(pitch + Drift(0.29f, 0.71f, 1.53f) * 0.35f * amp, Drift(0.37f, 0.83f, 1.9f) * 0.45f * amp,
                                           Drift(0.23f, 0.61f, 1.31f) * 0.7f * amp + Mathf.Sin(bob * 0.5f) * 0.6f * walk * sway);
        cam.localPosition = new Vector3(Mathf.Sin(bob * 0.5f) * 0.025f * walk * sway, camY + Mathf.Sin(bob) * 0.03f * walk * sway, 0);
        cc.SimpleMove(transform.TransformDirection(new Vector3(move.x, 0, move.y)) * speed); // SimpleMove adds gravity
    }
}
