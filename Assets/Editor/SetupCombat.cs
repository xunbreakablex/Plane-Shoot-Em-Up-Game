using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Savas sistemi icin efekt/mermi prefab'larini uretir ve sahneye CombatManager + PlayerGun ekler.
public static class SetupCombat
{
    private const string FxFolder = "Assets/Combat";
    private const string PrefabFolder = "Assets/Prefabs/Combat";

    private const string BulletPlayer = PrefabFolder + "/Bullet_Player.prefab";
    private const string BulletEnemy = PrefabFolder + "/Bullet_Enemy.prefab";
    private const string Explosion = PrefabFolder + "/Explosion.prefab";
    private const string HitSpark = PrefabFolder + "/HitSpark.prefab";
    private const string SmokeTrail = PrefabFolder + "/SmokeTrail.prefab";
    private const string MissilePrefab = PrefabFolder + "/Missile.prefab";
    private const string ShieldHitPrefab = PrefabFolder + "/ShieldHit.prefab";
    private const string PowerUpPrefab = PrefabFolder + "/PowerUp.prefab";

    [MenuItem("Tools/Setup Combat")]
    public static void SetupMenu()
    {
        var flight = Object.FindFirstObjectByType<FlightTestManager>();
        if (flight == null)
        {
            Debug.LogError("FlightTestManager bulunamadi. Once Tools/Setup Flight Test calistir.");
            return;
        }

        EnsureAssets();

        foreach (var old in Object.FindObjectsByType<CombatManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (old != null) Object.DestroyImmediate(old.gameObject);

        Attach(flight.transform.parent, flight, Camera.main);

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(flight.gameObject.scene);
        EditorSceneManager.SaveOpenScenes();
        Debug.Log("Combat hazir. Play'e bas: fare ile nisan, sol tik ates.");
    }

    // ---------------------------------------------------------------- scene

    public static void Attach(Transform parent, FlightTestManager flight, Camera cam)
    {
        var go = new GameObject("CombatManager");
        go.transform.SetParent(parent, false);

        var gun = go.AddComponent<PlayerGun>();
        gun.flight = flight;
        gun.cam = cam;
        gun.bulletPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BulletPlayer).GetComponent<Bullet>();
        gun.missilePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(MissilePrefab).GetComponent<Missile>();

        // Hangar donanimi (gorunum/performans/taretler) ve ESC duraklat menusu
        var applier = go.AddComponent<LoadoutApplier>();
        applier.flight = flight;
        go.AddComponent<ShopUI>();
        go.AddComponent<StageClearUI>();
        go.AddComponent<RunResultUI>();
        go.AddComponent<PauseMenu>();

        var cm = go.AddComponent<CombatManager>();
        cm.flight = flight;
        cm.cam = cam;
        cm.gun = gun;
        cm.playerBulletPrefab = gun.bulletPrefab;
        // Sira = AircraftCatalog model indeksi (dusman olarak yalnizca ilk 3 kullanilir)
        cm.modelPrefabs = new[]
        {
            AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/WW1Biplane.prefab"),
            AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/WW2Fighter.prefab"),
            AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/GliderPlane.prefab"),
            AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Atmaca.prefab"),
            AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Kartal.prefab"),
            AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/YavruKopekbaligi.prefab"),
        };
        cm.enemyBulletPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BulletEnemy).GetComponent<Bullet>();
        cm.explosionPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Explosion);
        cm.hitSparkPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(HitSpark);
        cm.smokeTrailPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SmokeTrail);
        cm.shieldHitPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ShieldHitPrefab);
        cm.powerUpPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PowerUpPrefab).GetComponent<PowerUp>();
        cm.fxMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Fx_Soft.mat");
        cm.bossLit = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Boss_Lit.mat");
        cm.bossGlow = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Boss_Glow.mat");
    }

    // --------------------------------------------------------------- assets

    public static void EnsureAssets()
    {
        CreateFolder("Assets", "Combat");
        CreateFolder("Assets", "Materials");
        CreateFolder("Assets", "Prefabs");
        CreateFolder("Assets/Prefabs", "Combat");

        Texture2D soft = EnsureSoftCircle();
        Material fx = EnsureMaterial("Fx_Soft", "Sprites/Default", Color.white, soft);
        Material bulletP = EnsureMaterial("Bullet_Player", "Universal Render Pipeline/Unlit", new Color(1f, 0.92f, 0.3f), null);
        Material bulletE = EnsureMaterial("Bullet_Enemy", "Universal Render Pipeline/Unlit", new Color(1f, 0.25f, 0.15f), null);

        Material shieldMat = EnsureMaterial("Fx_Shield", "Sprites/Default", new Color(0.35f, 0.85f, 1f, 0.9f), soft);
        Material missileBody = EnsureMaterial("Missile_Body", "Universal Render Pipeline/Lit", new Color(0.82f, 0.84f, 0.86f), null);
        Material missileNose = EnsureMaterial("Missile_Nose", "Universal Render Pipeline/Lit", new Color(0.85f, 0.12f, 0.1f), null);

        // Guclendirmeler (P / S / M / B / +): her tur kendi renginde parlar
        var pickupMats = new Material[PowerUp.KindColors.Length];
        for (int i = 0; i < pickupMats.Length; i++)
            pickupMats[i] = EnsureMaterial("Pickup_" + PowerUp.Names[i], "Universal Render Pipeline/Unlit", PowerUp.KindColors[i], null);

        // Boss govdeleri BossBuilder tarafindan bu iki malzemeden turetilen renklerle kodla uretilir
        EnsureMaterial("Boss_Lit", "Universal Render Pipeline/Lit", Color.white, null);
        EnsureMaterial("Boss_Glow", "Universal Render Pipeline/Unlit", Color.white, null);

        BuildBullet(BulletPlayer, bulletP);
        BuildBullet(BulletEnemy, bulletE);
        BuildPowerUp(fx, pickupMats);
        BuildExplosion(fx);
        BuildHitSpark(fx);
        BuildSmokeTrail(fx);
        BuildMissile(missileBody, missileNose, fx);
        BuildShieldHit(shieldMat, fx);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static void CreateFolder(string parent, string child)
    {
        if (!AssetDatabase.IsValidFolder($"{parent}/{child}"))
            AssetDatabase.CreateFolder(parent, child);
    }

    private static Texture2D EnsureSoftCircle()
    {
        string path = FxFolder + "/SoftCircle.png";
        var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (existing != null) return existing;

        const int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx = (x + 0.5f) / size * 2f - 1f;
            float dy = (y + 0.5f) / size * 2f - 1f;
            float r = Mathf.Sqrt(dx * dx + dy * dy);
            float a = Mathf.Pow(Mathf.Clamp01(1f - r), 1.4f);
            tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
        }
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path);

        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    private static Material EnsureMaterial(string name, string shaderName, Color color, Texture2D tex)
    {
        string path = $"Assets/Materials/{name}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat != null) return mat;

        Shader shader = Shader.Find(shaderName) ?? Shader.Find("Sprites/Default");
        mat = new Material(shader) { color = color };
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        if (tex != null) mat.mainTexture = tex;
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    // -------------------------------------------------------------- prefabs

    private static void BuildBullet(string path, Material mat)
    {
        var root = new GameObject(Path.GetFileNameWithoutExtension(path));
        root.AddComponent<Bullet>();

        var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
        visual.name = "Tracer";
        Object.DestroyImmediate(visual.GetComponent<Collider>());
        visual.transform.SetParent(root.transform, false);
        visual.transform.localScale = new Vector3(0.18f, 0.18f, 6f);
        visual.GetComponent<Renderer>().sharedMaterial = mat;
        visual.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
    }

    // Guclendirme: koseden duran kup (elmas) + arkada daire seklinde parlama
    private static void BuildPowerUp(Material glowMat, Material[] kindMaterials)
    {
        var root = new GameObject("PowerUp");
        var gem = root.AddComponent<PowerUp>();

        var coreHolder = new GameObject("Core");
        coreHolder.transform.SetParent(root.transform, false);

        GameObject core = GameObject.CreatePrimitive(PrimitiveType.Cube);
        core.name = "Diamond";
        Object.DestroyImmediate(core.GetComponent<Collider>());
        core.transform.SetParent(coreHolder.transform, false);
        core.transform.localRotation = Quaternion.Euler(35.264f, 45f, 0f);
        core.GetComponent<Renderer>().sharedMaterial = kindMaterials[0];
        core.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        GameObject glow = GameObject.CreatePrimitive(PrimitiveType.Quad);
        glow.name = "Glow";
        Object.DestroyImmediate(glow.GetComponent<Collider>());
        glow.transform.SetParent(root.transform, false);
        glow.transform.localScale = Vector3.one * 7f;
        var gr = glow.GetComponent<Renderer>();
        gr.sharedMaterial = glowMat;
        gr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        gr.receiveShadows = false;

        gem.core = coreHolder.transform;
        gem.glow = glow.transform;
        gem.coreRenderer = core.GetComponent<Renderer>();
        gem.glowRenderer = gr;
        gem.kindMaterials = kindMaterials;

        PrefabUtility.SaveAsPrefabAsset(root, PowerUpPrefab);
        Object.DestroyImmediate(root);
    }

    private static void BuildExplosion(Material mat)
    {
        var root = new GameObject("Explosion");
        root.AddComponent<AutoDestroy>().lifetime = 9f;

        // Parlama
        var flash = NewSystem(root, "Flash", mat);
        Main(flash, 0.3f, false, 0.12f, 0.2f, 0f, 0f, 28f, 36f, new Color(1f, 0.95f, 0.7f, 1f), 0f, 1);
        Burst(flash, 1);
        FadeOut(flash, new Color(1f, 0.95f, 0.7f), new Color(1f, 0.6f, 0.2f));

        // Ates topu
        var fire = NewSystem(root, "Fireball", mat);
        Main(fire, 0.6f, false, 0.5f, 1.0f, 4f, 18f, 7f, 15f, new Color(1f, 0.7f, 0.2f, 1f), -0.08f, 60);
        Burst(fire, 34);
        Sphere(fire, 2.5f);
        FadeOut(fire, new Color(1f, 0.95f, 0.55f), new Color(1f, 0.4f, 0.08f));
        SizeCurve(fire, 0.5f, 1f, 0.5f);

        // Duman (yukari yukselir; kill yazisi bunun ustunde cikar)
        var smoke = NewSystem(root, "Smoke", mat);
        Main(smoke, 1f, false, 2.5f, 4.5f, 2f, 8f, 6f, 12f, new Color(0.18f, 0.18f, 0.18f, 0.8f), -0.12f, 40);
        Burst(smoke, 14);
        Sphere(smoke, 3f);
        FadeOut(smoke, new Color(0.22f, 0.22f, 0.22f), new Color(0.35f, 0.35f, 0.35f));
        SizeCurve(smoke, 0.35f, 0.8f, 1.2f);

        // Kivilcimlar
        var sparks = NewSystem(root, "Sparks", mat);
        Main(sparks, 1f, false, 0.6f, 1.3f, 30f, 70f, 0.5f, 0.9f, new Color(1f, 0.85f, 0.3f, 1f), 1.2f, 60);
        Burst(sparks, 45);
        Sphere(sparks, 1f);
        var sr = sparks.GetComponent<ParticleSystemRenderer>();
        sr.renderMode = ParticleSystemRenderMode.Stretch;
        sr.lengthScale = 3f;
        FadeOut(sparks, new Color(1f, 0.95f, 0.5f), new Color(1f, 0.4f, 0.1f));

        PrefabUtility.SaveAsPrefabAsset(root, Explosion);
        Object.DestroyImmediate(root);
    }

    private static void BuildHitSpark(Material mat)
    {
        var root = new GameObject("HitSpark");
        root.AddComponent<AutoDestroy>().lifetime = 2f;

        var sparks = NewSystem(root, "Sparks", mat);
        Main(sparks, 0.3f, false, 0.2f, 0.45f, 15f, 35f, 0.4f, 0.7f, new Color(1f, 0.85f, 0.3f, 1f), 0.5f, 20);
        Burst(sparks, 10);
        Sphere(sparks, 0.2f);
        var sr = sparks.GetComponent<ParticleSystemRenderer>();
        sr.renderMode = ParticleSystemRenderMode.Stretch;
        sr.lengthScale = 2.5f;
        FadeOut(sparks, new Color(1f, 0.95f, 0.5f), new Color(1f, 0.45f, 0.1f));

        var puff = NewSystem(root, "Puff", mat);
        Main(puff, 0.3f, false, 0.25f, 0.4f, 0f, 2f, 2.5f, 4f, new Color(1f, 0.8f, 0.4f, 0.9f), 0f, 4);
        Burst(puff, 2);
        FadeOut(puff, new Color(1f, 0.9f, 0.5f), new Color(0.6f, 0.4f, 0.2f));

        PrefabUtility.SaveAsPrefabAsset(root, HitSpark);
        Object.DestroyImmediate(root);
    }

    private static void BuildSmokeTrail(Material mat)
    {
        var root = new GameObject("SmokeTrail");
        var ps = root.AddComponent<ParticleSystem>();
        var r = root.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = mat;
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.duration = 1f;
        main.loop = true;
        main.playOnAwake = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.5f, 2.6f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 2f);
        main.startSize = new ParticleSystem.MinMaxCurve(2f, 4f);
        main.startColor = new Color(0.1f, 0.1f, 0.1f, 0.85f);
        main.gravityModifier = -0.05f;
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.maxParticles = 120;

        var em = ps.emission;
        em.rateOverTime = 28f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.4f;

        FadeOut(ps, new Color(0.15f, 0.15f, 0.15f), new Color(0.35f, 0.35f, 0.35f));
        SizeCurve(ps, 0.4f, 0.8f, 1.4f);

        PrefabUtility.SaveAsPrefabAsset(root, SmokeTrail);
        Object.DestroyImmediate(root);
    }

    private static void BuildMissile(Material bodyMat, Material noseMat, Material fx)
    {
        var root = new GameObject("Missile");
        root.AddComponent<Missile>();

        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        body.name = "Body";
        Object.DestroyImmediate(body.GetComponent<Collider>());
        body.transform.SetParent(root.transform, false);
        body.transform.localEulerAngles = new Vector3(90f, 0f, 0f);
        body.transform.localScale = new Vector3(0.22f, 0.7f, 0.22f);
        body.GetComponent<Renderer>().sharedMaterial = bodyMat;

        GameObject nose = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        nose.name = "Nose";
        Object.DestroyImmediate(nose.GetComponent<Collider>());
        nose.transform.SetParent(root.transform, false);
        nose.transform.localPosition = new Vector3(0f, 0f, 0.72f);
        nose.transform.localScale = new Vector3(0.22f, 0.22f, 0.45f);
        nose.GetComponent<Renderer>().sharedMaterial = noseMat;

        // Kuyruk kanatlari (+)
        for (int i = 0; i < 2; i++)
        {
            GameObject fin = GameObject.CreatePrimitive(PrimitiveType.Cube);
            fin.name = "Fin" + i;
            Object.DestroyImmediate(fin.GetComponent<Collider>());
            fin.transform.SetParent(root.transform, false);
            fin.transform.localPosition = new Vector3(0f, 0f, -0.55f);
            fin.transform.localScale = i == 0 ? new Vector3(0.8f, 0.03f, 0.3f) : new Vector3(0.03f, 0.8f, 0.3f);
            fin.GetComponent<Renderer>().sharedMaterial = noseMat;
        }

        // Duman izi
        var trail = new GameObject("Trail");
        trail.transform.SetParent(root.transform, false);
        trail.transform.localPosition = new Vector3(0f, 0f, -0.8f);
        var ps = trail.AddComponent<ParticleSystem>();
        trail.GetComponent<ParticleSystemRenderer>().sharedMaterial = fx;
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.loop = true;
        main.playOnAwake = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.3f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.7f, 1.1f);
        main.startColor = new Color(0.9f, 0.9f, 0.9f, 0.8f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.maxParticles = 250;
        var em = ps.emission;
        em.rateOverTime = 70f;
        FadeOut(ps, new Color(1f, 0.85f, 0.5f), new Color(0.75f, 0.75f, 0.75f));
        SizeCurve(ps, 0.5f, 1f, 1.8f);

        PrefabUtility.SaveAsPrefabAsset(root, MissilePrefab);
        Object.DestroyImmediate(root);
    }

    private static void BuildShieldHit(Material patchMat, Material fx)
    {
        var root = new GameObject("ShieldHit");
        root.AddComponent<AutoDestroy>().lifetime = 1.8f;

        // Carpma noktasinda renklenen kucuk alan
        GameObject patch = GameObject.CreatePrimitive(PrimitiveType.Quad);
        patch.name = "Patch";
        Object.DestroyImmediate(patch.GetComponent<Collider>());
        patch.transform.SetParent(root.transform, false);
        patch.GetComponent<Renderer>().sharedMaterial = patchMat;
        patch.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        patch.AddComponent<ShieldPatch>();

        // Mermi parcalari (turuncu-kirmizi, yercekimiyle duser)
        var shards = NewSystem(root, "BulletShards", fx);
        Main(shards, 0.3f, false, 0.25f, 0.55f, 10f, 26f, 0.25f, 0.45f, new Color(1f, 0.55f, 0.2f, 1f), 1.5f, 24);
        Burst(shards, 18);
        Sphere(shards, 0.15f);
        var sr = shards.GetComponent<ParticleSystemRenderer>();
        sr.renderMode = ParticleSystemRenderMode.Stretch;
        sr.lengthScale = 2.5f;
        FadeOut(shards, new Color(1f, 0.85f, 0.4f), new Color(1f, 0.25f, 0.1f));

        // Kalkan kivilcimlari (camgobegi)
        var sparks = NewSystem(root, "ShieldSparks", fx);
        Main(sparks, 0.3f, false, 0.2f, 0.45f, 4f, 12f, 0.3f, 0.5f, new Color(0.5f, 0.9f, 1f, 1f), 0f, 16);
        Burst(sparks, 10);
        Sphere(sparks, 0.3f);
        FadeOut(sparks, new Color(0.85f, 1f, 1f), new Color(0.3f, 0.7f, 1f));

        // Anlik parlama
        var flash = NewSystem(root, "Flash", fx);
        Main(flash, 0.2f, false, 0.1f, 0.16f, 0f, 0f, 2.2f, 3f, new Color(0.7f, 0.95f, 1f, 1f), 0f, 2);
        Burst(flash, 1);
        FadeOut(flash, new Color(0.9f, 1f, 1f), new Color(0.4f, 0.8f, 1f));

        PrefabUtility.SaveAsPrefabAsset(root, ShieldHitPrefab);
        Object.DestroyImmediate(root);
    }

    // ------------------------------------------------------ particle helpers

    private static ParticleSystem NewSystem(GameObject root, string name, Material mat)
    {
        var go = new GameObject(name);
        go.transform.SetParent(root.transform, false);
        var ps = go.AddComponent<ParticleSystem>();
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = mat;
        r.renderMode = ParticleSystemRenderMode.Billboard;
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        return ps;
    }

    private static void Main(ParticleSystem ps, float duration, bool loop, float lifeMin, float lifeMax,
        float speedMin, float speedMax, float sizeMin, float sizeMax, Color color, float gravity, int maxParticles)
    {
        var main = ps.main;
        main.duration = duration;
        main.loop = loop;
        main.playOnAwake = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(lifeMin, lifeMax);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speedMin, speedMax);
        main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
        main.startColor = color;
        main.gravityModifier = gravity;
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.maxParticles = maxParticles;

        var em = ps.emission;
        em.rateOverTime = 0f;
    }

    private static void Burst(ParticleSystem ps, int count)
    {
        var em = ps.emission;
        em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
    }

    private static void Sphere(ParticleSystem ps, float radius)
    {
        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = radius;
    }

    // Renk gecisi + omur sonunda saydamlasma
    private static void FadeOut(ParticleSystem ps, Color from, Color to)
    {
        var col = ps.colorOverLifetime;
        col.enabled = true;

        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(from, 0f), new GradientColorKey(to, 0.5f), new GradientColorKey(to, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.08f), new GradientAlphaKey(0.7f, 0.5f), new GradientAlphaKey(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(g);
    }

    private static void SizeCurve(ParticleSystem ps, float start, float mid, float end)
    {
        var size = ps.sizeOverLifetime;
        size.enabled = true;
        var curve = new AnimationCurve(
            new Keyframe(0f, start), new Keyframe(0.4f, mid), new Keyframe(1f, end));
        size.size = new ParticleSystem.MinMaxCurve(1f, curve);
    }
}
