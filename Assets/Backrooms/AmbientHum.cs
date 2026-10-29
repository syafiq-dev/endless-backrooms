using UnityEngine;

// Fluorescent-light hum plus low room tone, generated live so there are no audio files to source.
// Sits next to the AudioListener and adds itself to the final mix.
// shortcut: synthetic and mono, swap for recorded loops (AudioSource + clip) when you have them.
public class AmbientHum : MonoBehaviour
{
    [Range(0, 1)] public float volume = 0.3f;

    readonly System.Random rng = new System.Random();
    double phase, step;
    float brown, wobble;

    void Awake() => step = 2 * Mathf.PI * 120.0 / AudioSettings.outputSampleRate; // 120 Hz: mains hum doubled by the ballast

    void OnAudioFilterRead(float[] data, int channels)
    {
        for (int i = 0; i < data.Length; i += channels)
        {
            phase += step;
            if (phase > 2 * Mathf.PI) phase -= 2 * Mathf.PI;
            float sin = (float)System.Math.Sin(phase);
            float hum = sin * 0.5f + (float)System.Math.Sin(phase * 2) * 0.25f + (float)System.Math.Sin(phase * 3) * 0.15f
                      + Mathf.Sign(sin) * 0.04f; // a little square-wave edge = buzz
            brown = (brown + (float)(rng.NextDouble() * 2 - 1) * 0.02f) * 0.998f; // brown noise: dull air-conditioning rumble
            wobble += 0.00002f;
            float s = (hum * 0.3f * (0.85f + 0.15f * Mathf.Sin(wobble)) + brown * 0.6f) * volume;
            for (int c = 0; c < channels; c++) data[i + c] += s;
        }
    }
}
