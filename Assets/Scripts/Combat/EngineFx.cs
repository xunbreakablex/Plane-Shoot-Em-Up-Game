using System.Collections.Generic;
using UnityEngine;

// Motor efekti: gaza gore buyuyen egzoz alevi + duman izi (oyuncu ve dusman ucaklari). Kahramanlarda motor yok:
// yerine ayak altinda buyu parildamasi (Ay Savascisi pembe, Ki Kahramani sari, Imparator mor), o da gazla yogunlasir.
// Kaynaklar modelden bulunur: jetlerde "Flame" nesneleri (alev mesh'i de gazla uzar), pervanelilerde kaporta yanlari
// (egzoz borulari), kaportasiz pervanelide (planor) pervane arkasi.
// Gaz kaynagi bir fonksiyon: 0-1; negatif = motor sustu (dusen enkaz, cakilmis ucak) ve yayim durur.
public class EngineFx : MonoBehaviour
{
    private static readonly Color[] HeroColors =
    {
        new Color(1f, 0.45f, 0.85f),   // Ay Savascisi: pembe
        new Color(1f, 0.88f, 0.3f),    // Ki Kahramani: sari
        new Color(0.65f, 0.3f, 1f),    // Imparator: mor
    };

    private static Material softMaterial;

    /// <summary>Iz temasi: tema dusmanlari (iblis kizil alev + kara duman, melek altin, dusmus melek mor parilti).</summary>
    public enum Style { Normal, Hell, Holy, Fallen }

    private Style style;

    private System.Func<float> throttle;
    private readonly List<ParticleSystem> fires = new List<ParticleSystem>();
    private readonly List<ParticleSystem> smokes = new List<ParticleSystem>();
    private readonly List<ParticleSystem> sparkles = new List<ParticleSystem>();
    private readonly List<Transform> flameMeshes = new List<Transform>();
    private readonly List<Vector3> flameBase = new List<Vector3>();
    private float fireScale = 1f, smokeScale = 1f;
    private float shown = 0.3f;   // yumusatilmis gaz (alev aninda sicramasin)

    /// <summary>Ucak kokune bir kez eklenir; varsa mevcut olani dondurur.</summary>
    public static EngineFx Attach(GameObject root, int model, System.Func<float> throttle, Style style = Style.Normal)
    {
        var fx = root.GetComponent<EngineFx>();
        if (fx != null) { fx.throttle = throttle; return fx; }
        fx = root.AddComponent<EngineFx>();
        fx.style = style;
        fx.Build(model, throttle);
        return fx;
    }

    private void Build(int model, System.Func<float> thr)
    {
        throttle = thr;
        Transform root = transform;

        if (PlaneBuilder.IsHero(model))
        {
            Color c = HeroColors[Mathf.Clamp(model - PlaneBuilder.HeroStart, 0, HeroColors.Length - 1)];
            sparkles.Add(MakeSparkle(new Vector3(0f, -0.6f, -0.2f), c));
            return;
        }

        // Jet: alev mesh'lerinin arka ucundan
        foreach (var t in GetComponentsInChildren<Transform>(true))
        {
            if (t.name != "Flame") continue;
            flameMeshes.Add(t);
            flameBase.Add(t.localScale);
            Vector3 back = root.InverseTransformPoint(t.position - root.forward * (t.lossyScale.z * 0.4f));
            fires.Add(MakeFire(back, Mathf.Clamp(t.lossyScale.x / Mathf.Max(root.lossyScale.x, 0.01f), 0.2f, 0.6f) * 1.6f));
            smokes.Add(MakeSmoke(back));
        }
        if (fires.Count > 0) return;

        // Pervaneli: kaporta yanlarindaki egzoz borulari (kisa alev, bol duman)
        Propeller prop = GetComponentInChildren<Propeller>(true);
        if (prop == null) return;
        Transform cowl = null;
        foreach (var t in GetComponentsInChildren<Transform>(true))
            if (t.name == "Cowling") { cowl = t; break; }

        Vector3 c0;
        float side;
        if (cowl != null)
        {
            c0 = root.InverseTransformPoint(cowl.position) + new Vector3(0f, -0.08f, -0.25f);
            side = cowl.lossyScale.x * 0.5f / Mathf.Max(root.lossyScale.x, 0.01f) + 0.04f;
        }
        else
        {
            c0 = root.InverseTransformPoint(prop.transform.position) + new Vector3(0f, -0.05f, -0.4f);
            side = 0.2f;
        }
        fireScale = 0.9f;
        smokeScale = 1.3f;
        for (int s = -1; s <= 1; s += 2)
        {
            Vector3 p = c0 + new Vector3(side * s, 0f, 0f);
            fires.Add(MakeFire(p, 0.45f));
            smokes.Add(MakeSmoke(p));
        }

        // Melek / dusmus melek: govdenin arkasindan parilti izi (kahramanlardaki gibi)
        if (style == Style.Holy || style == Style.Fallen)
            sparkles.Add(MakeSparkle(new Vector3(0f, 0f, -0.5f), style == Style.Holy ? new Color(1f, 0.85f, 0.35f) : new Color(0.65f, 0.3f, 1f)));
    }

    private void Update()
    {
        float t = throttle != null ? throttle() : 0.4f;
        bool off = t < 0f;
        shown = Mathf.MoveTowards(shown, Mathf.Clamp01(t), 2.5f * Time.deltaTime);

        foreach (var ps in fires)
        {
            var em = ps.emission;
            em.rateOverTimeMultiplier = off ? 0f : 40f + 110f * shown;
            var main = ps.main;
            main.startSizeMultiplier = fireScale * (0.55f + 0.9f * shown);
            main.startSpeedMultiplier = 8f + 14f * shown;
        }
        foreach (var ps in smokes)
        {
            var em = ps.emission;
            em.rateOverTimeMultiplier = off ? 0f : smokeScale * (5f + 18f * shown);
        }
        foreach (var ps in sparkles)
        {
            var em = ps.emission;
            em.rateOverTimeMultiplier = off ? 0f : 30f + 90f * shown;
            var main = ps.main;
            main.startSpeedMultiplier = 4f + 6f * shown;
        }
        for (int i = 0; i < flameMeshes.Count; i++)
        {
            Transform f = flameMeshes[i];
            if (f == null) continue;
            Vector3 b = flameBase[i];
            float k = off ? 0f : 0.85f + 0.3f * shown;
            f.localScale = new Vector3(b.x * k, b.y * k, b.z * (off ? 0f : 0.55f + 0.9f * shown));
        }
    }

    // ---------------------------------------------------------------- parcacik kurulumu

    private ParticleSystem NewSystem(string name, Vector3 localPos, int max)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);   // koni geriye (+Z'si ucagin arkasi)
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = true;
        main.playOnAwake = true;
        main.maxParticles = max;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;   // buyuk dusman ucaginda alev de buyuk
        main.startColor = Color.white;
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = SoftMaterial();
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        return ps;
    }

    private ParticleSystem MakeFire(Vector3 localPos, float size)
    {
        var ps = NewSystem("EngineFire", localPos, 60);
        var main = ps.main;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;   // alev egzoza yapisik, kisa
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.07f, 0.13f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 1.2f);  // Update carpanla 8-22 m/s
        main.startSize = new ParticleSystem.MinMaxCurve(size * 0.7f, size);

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 7f;
        shape.radius = 0.05f;

        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.35f));

        var col = ps.colorOverLifetime;
        col.enabled = true;
        Color a = new Color(1f, 0.97f, 0.75f), b = new Color(1f, 0.6f, 0.15f), c = new Color(0.9f, 0.2f, 0.05f);
        if (style == Style.Hell) { a = new Color(1f, 0.7f, 0.3f); b = new Color(0.95f, 0.2f, 0.05f); c = new Color(0.45f, 0.02f, 0.02f); }
        else if (style == Style.Holy) { a = Color.white; b = new Color(1f, 0.9f, 0.55f); c = new Color(1f, 0.75f, 0.3f); }
        else if (style == Style.Fallen) { a = new Color(0.9f, 0.7f, 1f); b = new Color(0.6f, 0.25f, 0.95f); c = new Color(0.25f, 0.05f, 0.4f); }
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(a, 0f), new GradientColorKey(b, 0.35f), new GradientColorKey(c, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.85f, 0.5f), new GradientAlphaKey(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(g);
        ps.Play();
        return ps;
    }

    private ParticleSystem MakeSmoke(Vector3 localPos)
    {
        var ps = NewSystem("EngineSmoke", localPos, 90);
        var main = ps.main;
        main.simulationSpace = ParticleSystemSimulationSpace.World;   // geride iz birakir
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.1f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 4f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.45f, 0.8f);

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 10f;
        shape.radius = 0.08f;

        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 2.4f));

        var col = ps.colorOverLifetime;
        col.enabled = true;
        Color s0 = new Color(0.55f, 0.53f, 0.5f), s1 = new Color(0.75f, 0.75f, 0.75f);
        float alpha = 0.28f;
        if (style == Style.Hell || style == Style.Fallen) { s0 = new Color(0.08f, 0.05f, 0.05f); s1 = new Color(0.2f, 0.15f, 0.15f); alpha = 0.45f; }
        else if (style == Style.Holy) { s0 = new Color(1f, 0.97f, 0.9f); s1 = Color.white; alpha = 0.22f; }
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(s0, 0f), new GradientColorKey(s1, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(alpha, 0.12f), new GradientAlphaKey(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(g);
        ps.Play();
        return ps;
    }

    private ParticleSystem MakeSparkle(Vector3 localPos, Color c)
    {
        var ps = NewSystem("MagicSparkle", localPos, 160);
        var main = ps.main;
        // Kahramana bagli: govdenin cevresinden dogup geriye akan kisa iz (dunya uzayinda 100 m/s'de aninda geride kaliyordu)
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.75f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.4f);   // Update carpanla 2-14 m/s geriye
        main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.28f);

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 25f;
        shape.radius = 0.55f;

        // Pirildama: buyuyup kuculur
        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        var curve = new AnimationCurve(new Keyframe(0f, 0.2f), new Keyframe(0.2f, 1f), new Keyframe(0.55f, 0.45f), new Keyframe(0.75f, 0.9f), new Keyframe(1f, 0f));
        sol.size = new ParticleSystem.MinMaxCurve(1f, curve);

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.Lerp(c, Color.white, 0.7f), 0f), new GradientColorKey(c, 0.35f), new GradientColorKey(c * 0.8f, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.9f, 0.6f), new GradientAlphaKey(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(g);
        ps.Play();
        return ps;
    }

    // Yumusak daire dokulu malzeme (bir kez uretilir; varlik dosyasi gerekmez)
    private static Material SoftMaterial()
    {
        if (softMaterial != null) return softMaterial;
        const int n = 64;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "EngineFxSoft" };
        var px = new Color[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                px[y * n + x] = new Color(1f, 1f, 1f, a * a);
            }
        tex.SetPixels(px);
        tex.Apply();
        softMaterial = new Material(Shader.Find("Sprites/Default")) { name = "EngineFxSoft", mainTexture = tex };
        return softMaterial;
    }
}
