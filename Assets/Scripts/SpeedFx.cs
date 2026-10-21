using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Hiz hissi: hizli ucarken ve hizlanirken ekranin kenarlarindan kameraya dogru akan cizgi partikulleri
// ("hiz cizgileri") + URP hareket bulanikligi. Kameraya FollowCamera takar; siddeti her kare Set ile gelir.
public class SpeedFx : MonoBehaviour
{
    private ParticleSystem lines;
    private ParticleSystem.EmissionModule emission;
    private Volume volume;
    private MotionBlur blur;
    private float level;

    public float Level => level;

    private void Awake()
    {
        BuildLines();
        BuildBlur();
    }

    /// <summary>speed01: hiz / referans hiz; accel01: hizlanma (0..1). Seviye yumusatilir.</summary>
    public void Set(float speed01, float accel01, float dt)
    {
        float want = Mathf.Clamp01(Mathf.Max(0f, (speed01 - 0.6f) / 0.4f) * 0.8f + accel01 * 0.7f);
        if (!UserSettings.SpeedFx) want = 0f;
        level = Mathf.Lerp(level, want, 1f - Mathf.Exp(-(want > level ? 6f : 2.2f) * dt));

        if (lines != null) emission.rateOverTime = level * 170f;
        if (volume != null) volume.weight = level;
        if (blur != null) blur.intensity.Override(0.15f + 0.45f * level);
    }

    private void BuildLines()
    {
        var go = new GameObject("SpeedLines");
        go.transform.SetParent(transform, false);
        lines = go.AddComponent<ParticleSystem>();
        lines.Stop();

        var main = lines.main;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.startLifetime = 0.5f;
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.17f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 1f, 1f, 0.9f), new Color(0.85f, 0.93f, 1f, 0.65f));
        main.maxParticles = 500;
        main.loop = true;
        main.playOnAwake = true;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;

        emission = lines.emission;
        emission.rateOverTime = 0f;

        // Kameranin 45 m onunde bir halka: ortasi bos kalir (nisan bolgesi temiz), cizgiler kenarlardan gelir
        var shape = lines.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 26f;
        shape.radiusThickness = 0.6f;
        shape.position = new Vector3(0f, 0f, 45f);

        var vel = lines.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.Local;
        vel.x = new ParticleSystem.MinMaxCurve(0f, 0f);
        vel.y = new ParticleSystem.MinMaxCurve(0f, 0f);
        vel.z = new ParticleSystem.MinMaxCurve(-170f, -120f);

        var col = lines.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
        col.color = g;

        var r = lines.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.Stretch;
        r.velocityScale = 0.04f;
        r.lengthScale = 1.5f;
        r.shadowCastingMode = ShadowCastingMode.Off;
        r.receiveShadows = false;
        r.sharedMaterial = new Material(Shader.Find("Sprites/Default"));

        lines.Play();
    }

    private void BuildBlur()
    {
        var go = new GameObject("SpeedBlur");
        go.transform.SetParent(transform, false);
        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        blur = profile.Add<MotionBlur>(true);
        blur.quality.Override(MotionBlurQuality.Low);
        blur.intensity.Override(0.3f);
        blur.clamp.Override(0.05f);
        volume = go.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 50f;
        volume.weight = 0f;
        volume.sharedProfile = profile;
    }
}
