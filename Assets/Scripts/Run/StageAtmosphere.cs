using UnityEngine;

// Asama atmosferi: her asamanin kendi gokyuzu, sis ve isik havasi vardir (sabah, firtina, gun batimi, cehennem,
// gokyuzu tapinagi). Asama degisince yumusakca gecilir. Gokyuzu, Resources/GradientSky malzemesinin calisma zamani
// kopyasidir (proje varligina yazilmaz). Sonsuz modda asama numarasi 5'e gore tekrar eder.
public static class StageAtmosphere
{
    private struct Look
    {
        public Color top, horizon, bottom, sun;
        public float sunIntensity, sunPitch, sunGlow, fogStart, fogEnd;

        public static Look Lerp(Look a, Look b, float t)
        {
            return new Look
            {
                top = Color.Lerp(a.top, b.top, t),
                horizon = Color.Lerp(a.horizon, b.horizon, t),
                bottom = Color.Lerp(a.bottom, b.bottom, t),
                sun = Color.Lerp(a.sun, b.sun, t),
                sunIntensity = Mathf.Lerp(a.sunIntensity, b.sunIntensity, t),
                sunPitch = Mathf.Lerp(a.sunPitch, b.sunPitch, t),
                sunGlow = Mathf.Lerp(a.sunGlow, b.sunGlow, t),
                fogStart = Mathf.Lerp(a.fogStart, b.fogStart, t),
                fogEnd = Mathf.Lerp(a.fogEnd, b.fogEnd, t)
            };
        }
    }

    private const float TransitionSeconds = 3.5f;

    private static Look[] looks;
    private static Look from, to;
    private static float blend = 1f;
    private static bool captured;
    private static Material sky;
    private static Light sun;
    private static float nextGi;

    private static readonly int TopId = Shader.PropertyToID("_Top");
    private static readonly int HorizonId = Shader.PropertyToID("_Horizon");
    private static readonly int BottomId = Shader.PropertyToID("_Bottom");
    private static readonly int SunDirId = Shader.PropertyToID("_SunDir");
    private static readonly int SunColorId = Shader.PropertyToID("_SunColor");
    private static readonly int SunGlowId = Shader.PropertyToID("_SunGlow");

    private static void Capture()
    {
        if (captured) return;
        captured = true;

        sun = RenderSettings.sun != null ? RenderSettings.sun : Object.FindFirstObjectByType<Light>();
        Material template = Resources.Load<Material>("GradientSky");
        if (template != null)
        {
            sky = new Material(template);   // kopya: proje varligi degismesin
            RenderSettings.skybox = sky;
        }

        float basePitch = sun != null ? sun.transform.eulerAngles.x : 50f;
        float baseFogStart = RenderSettings.fogStartDistance, baseFogEnd = RenderSettings.fogEndDistance;

        looks = new[]
        {
            new Look                                                         // 1: sabah
            {
                top = new Color(0.27f, 0.5f, 0.88f), horizon = new Color(0.74f, 0.85f, 0.95f), bottom = new Color(0.45f, 0.52f, 0.45f),
                sun = new Color(1f, 0.97f, 0.88f), sunIntensity = sun != null ? sun.intensity : 1f, sunPitch = basePitch, sunGlow = 0.3f,
                fogStart = baseFogStart, fogEnd = baseFogEnd
            },
            new Look                                                         // 2: firtina
            {
                top = new Color(0.16f, 0.19f, 0.25f), horizon = new Color(0.46f, 0.5f, 0.55f), bottom = new Color(0.3f, 0.32f, 0.34f),
                sun = new Color(0.8f, 0.86f, 0.95f), sunIntensity = 0.75f, sunPitch = 42f, sunGlow = 0.05f,
                fogStart = 700f, fogEnd = 4300f
            },
            new Look                                                         // 3: gun batimi
            {
                top = new Color(0.2f, 0.27f, 0.52f), horizon = new Color(1f, 0.6f, 0.38f), bottom = new Color(0.45f, 0.3f, 0.28f),
                sun = new Color(1f, 0.68f, 0.42f), sunIntensity = 1.15f, sunPitch = 13f, sunGlow = 0.8f,
                fogStart = 1000f, fogEnd = 5200f
            },
            new Look                                                         // 4: cehennem
            {
                top = new Color(0.04f, 0.01f, 0.02f), horizon = new Color(0.36f, 0.1f, 0.07f), bottom = new Color(0.12f, 0.04f, 0.03f),
                sun = new Color(1f, 0.4f, 0.22f), sunIntensity = 1.1f, sunPitch = 9f, sunGlow = 0.6f,
                fogStart = 700f, fogEnd = 4200f
            },
            new Look                                                         // 5: gokyuzu tapinagi
            {
                top = new Color(0.5f, 0.68f, 1f), horizon = new Color(1f, 0.96f, 0.86f), bottom = new Color(0.88f, 0.88f, 0.92f),
                sun = new Color(1f, 0.96f, 0.82f), sunIntensity = 1.25f, sunPitch = 62f, sunGlow = 0.55f,
                fogStart = 1500f, fogEnd = 6500f
            },
        };

        from = to = looks[0];
    }

    /// <summary>Sahne yuklenince: onceki sahnenin gokyuzu/isik onbellegini birakir.</summary>
    public static void Reset()
    {
        captured = false;
        sky = null;
        sun = null;
        looks = null;
        blend = 1f;
    }

    /// <summary>Asamanin (0 tabanli) atmosferine gecer. instant: ilk kurulum, gecis olmadan.</summary>
    public static void Begin(int stageIndex, bool instant)
    {
        Capture();
        if (looks == null) return;

        Look target = looks[Mathf.Abs(stageIndex) % looks.Length];
        from = instant ? target : Current();
        to = target;
        blend = instant ? 1f : 0f;
        Apply(Current());
    }

    /// <summary>Her karede (gercek zaman): gecisi surdurur.</summary>
    public static void Tick(float dt)
    {
        if (!captured || blend >= 1f) return;
        blend = Mathf.Min(1f, blend + dt / TransitionSeconds);
        Apply(Current());
    }

    private static Look Current()
    {
        return Look.Lerp(from, to, Mathf.SmoothStep(0f, 1f, blend));
    }

    private static void Apply(Look l)
    {
        if (sun != null)
        {
            sun.color = l.sun;
            sun.intensity = l.sunIntensity;
            Vector3 e = sun.transform.eulerAngles;
            sun.transform.rotation = Quaternion.Euler(l.sunPitch, e.y, e.z);
        }

        if (sky != null)
        {
            sky.SetColor(TopId, l.top);
            sky.SetColor(HorizonId, l.horizon);
            sky.SetColor(BottomId, l.bottom);
            sky.SetColor(SunColorId, l.sun);
            sky.SetFloat(SunGlowId, l.sunGlow);
            if (sun != null) sky.SetVector(SunDirId, -sun.transform.forward);
        }

        // Sis ufuk rengiyle ayni: uzak arazi gokyuzune dikissiz karisir
        RenderSettings.fogColor = l.horizon;
        RenderSettings.fogStartDistance = l.fogStart;
        RenderSettings.fogEndDistance = l.fogEnd;

        // Ortam isigi gokyuzunden gelir: yeniden hesapla (gecis boyunca seyrek)
        if (Time.unscaledTime >= nextGi || blend >= 1f)
        {
            nextGi = Time.unscaledTime + 0.4f;
            DynamicGI.UpdateEnvironment();
        }
    }
}
