using UnityEngine;

// Prosedurel pervane motoru sesi: harmonikli bir dongu klibi, perde gaz ve hiza gore degisir.
[RequireComponent(typeof(PlaneController))]
public class EngineSound : MonoBehaviour
{
    [Range(0f, 1f)] public float volume = 0.35f;

    private static AudioClip sharedClip;
    private AudioSource source;
    private PlaneController plane;

    private void Awake()
    {
        plane = GetComponent<PlaneController>();
        source = gameObject.AddComponent<AudioSource>();
        source.clip = GetClip();
        source.loop = true;
        source.spatialBlend = 0f;
        source.volume = 0f;
        source.playOnAwake = false;
    }

    private void OnEnable()
    {
        if (source != null && !source.isPlaying) source.Play();
    }

    private void Update()
    {
        if (source == null || plane == null) return;

        float target = plane.IsCrashed ? 0f : volume * Mathf.Lerp(0.5f, 1f, plane.Throttle);
        source.volume = Mathf.Lerp(source.volume, target, 5f * Time.deltaTime);

        float pitch = Mathf.Lerp(0.6f, 1.6f, plane.Throttle) * (1f + Mathf.Clamp01(plane.Speed / 120f) * 0.15f);
        source.pitch = Mathf.Lerp(source.pitch, pitch, 4f * Time.deltaTime);
    }

    private static AudioClip GetClip()
    {
        if (sharedClip != null) return sharedClip;

        const int rate = 44100;
        const float baseHz = 70f;
        const int cycles = 35;                       // tam dongu icin tam periyot sayisi
        int samples = Mathf.RoundToInt(rate * cycles / baseHz);
        var data = new float[samples];
        var rng = new System.Random(5);

        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / rate;
            float v = 0f;
            for (int h = 1; h <= 8; h++)
                v += Mathf.Sin(2f * Mathf.PI * baseHz * h * t) / h;
            // Piston darbesi hissi: dusuk frekansli genlik titresimi
            float pulse = 0.75f + 0.25f * Mathf.Sin(2f * Mathf.PI * baseHz * 0.5f * t);
            float noise = ((float)rng.NextDouble() * 2f - 1f) * 0.15f;
            data[i] = Mathf.Clamp((v * 0.35f * pulse) + noise * 0.3f, -1f, 1f);
        }

        sharedClip = AudioClip.Create("EngineLoop", samples, 1, rate, false);
        sharedClip.SetData(data, 0);
        return sharedClip;
    }
}
