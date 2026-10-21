using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Oynanabilir kahramanlar (model 9-11): Ay Savascisi, Ki Kahramani, Imparator. Primitive'lerden insan figurleri
// uretir, prefab olarak kaydeder, savas sahnesindeki FlightTestManager'a ve hangar sahnesine ekler.
// Menu: Tools/Create Heroes (tekrar calistirilabilir; mevcut kahramanlari yeniler).
// Poz: DIK dururlar (Superman gibi yatmazlar), buyuyle/ki ile ucarlar; govde "Body" pivotunda one egiktir
// (PlaneBuilder.HeroLean ile ayni aci: namlu ve omuz noktalari bu egimle hesaplanir). Motor/kanat yok.
// Koordinat (Body icinde): +Z on, +Y yukari; kokun merkezi karakterin gobegi.
public static class CreateHeroes
{
    private const string PrefabFolder = "Assets/Prefabs";
    private const string HangarPath = "Assets/Scenes/Hangar.unity";

    private static readonly string[] Names = { "AySavascisi", "KiKahramani", "Imparator" };
    private static readonly int[] ModelIndex = { 9, 10, 11 };

    [MenuItem("Tools/Create Heroes")]
    public static void CreateAll()
    {
        CreateGliderPlane.EnsureFolder("Assets", "Materials");
        CreateGliderPlane.EnsureFolder("Assets", "Prefabs");

        BuildMoon();
        BuildKi();
        BuildEmperor();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        AddToScene();
        RegisterInHangar();
        Debug.Log("Kahramanlar hazir: Ay Savascisi, Ki Kahramani, Imparator (model 9-11).");
    }

    // ================================================================== AY SAVASCISI (dik, 15 derece one egik)

    private static void BuildMoon()
    {
        Material white = Lit("Hero_White", new Color(0.97f, 0.96f, 0.98f));
        Material pink = Lit("Hero_Pink", new Color(1f, 0.55f, 0.75f));
        Material skin = Lit("Hero_Skin", new Color(0.99f, 0.84f, 0.74f));
        Material hair = Lit("Hero_Hair", new Color(1f, 0.82f, 0.35f));
        Material blue = Lit("Hero_Blue", new Color(0.4f, 0.65f, 0.98f));
        Material red = Lit("Hero_Red", new Color(0.9f, 0.2f, 0.3f));
        Material gold = Lit("Hero_Gold", new Color(1f, 0.85f, 0.3f));
        Material glow = Lit("Hero_Wing", new Color(0.8f, 0.9f, 1f));
        Material star = Unlit("Hero_Star", new Color(1f, 0.95f, 0.5f));
        Material eye = Unlit("Hero_EyeBlue", new Color(0.5f, 0.3f, 0.95f));
        Material dark = CreateGliderPlane.GetMaterial("Plane_Dark", new Color(0.12f, 0.12f, 0.12f));

        var root = new GameObject("AySavascisi");
        GameObject body = Body(root, PlaneBuilder.HeroLean[0]);

        // Govde (dik), etek, kurdeleler, bros
        Part(body, PrimitiveType.Capsule, "Torso", V(0, 0.3f, 0), V(0.55f, 0.5f, 0.5f), white);
        Part(body, PrimitiveType.Sphere, "Skirt", V(0, -0.25f, 0), V(0.95f, 0.5f, 0.9f), pink);
        Part(body, PrimitiveType.Cube, "BowL", V(-0.25f, 0.65f, 0.22f), V(0.3f, 0.22f, 0.1f), pink, V(0, 0, 20));
        Part(body, PrimitiveType.Cube, "BowR", V(0.25f, 0.65f, 0.22f), V(0.3f, 0.22f, 0.1f), pink, V(0, 0, -20));
        Part(body, PrimitiveType.Sphere, "Brooch", V(0, 0.7f, 0.28f), V(0.14f, 0.14f, 0.1f), star);
        Part(body, PrimitiveType.Cube, "Collar", V(0, 0.78f, 0.1f), V(0.6f, 0.08f, 0.45f), blue);

        // Kafa, sac, gozler, topuzlar ve asagi sarkan uzun kuyruklar
        Part(body, PrimitiveType.Sphere, "Head", V(0, 1.15f, 0.05f), V(0.5f, 0.52f, 0.5f), skin);
        Part(body, PrimitiveType.Sphere, "HairBack", V(0, 1.22f, -0.05f), V(0.58f, 0.56f, 0.5f), hair);
        Part(body, PrimitiveType.Sphere, "Bangs", V(0, 1.38f, 0.2f), V(0.5f, 0.22f, 0.3f), hair);
        Part(body, PrimitiveType.Sphere, "EyeL", V(-0.11f, 1.17f, 0.28f), V(0.1f, 0.13f, 0.05f), eye);
        Part(body, PrimitiveType.Sphere, "EyeR", V(0.11f, 1.17f, 0.28f), V(0.1f, 0.13f, 0.05f), eye);
        for (int s = -1; s <= 1; s += 2)
        {
            Part(body, PrimitiveType.Sphere, "Bun", V(s * 0.3f, 1.45f, -0.05f), V(0.2f, 0.2f, 0.2f), hair);
            Part(body, PrimitiveType.Capsule, "Tail", V(s * 0.42f, 0.75f, -0.15f), V(0.12f, 0.75f, 0.12f), hair, V(0, 0, s * -6));
        }

        // Kollar one uzanir; sag elde degnek, ucunda yildiz
        Part(body, PrimitiveType.Capsule, "ArmL", V(-0.33f, 0.6f, 0.35f), V(0.14f, 0.42f, 0.14f), skin, V(90, 0, 0));
        Part(body, PrimitiveType.Capsule, "ArmR", V(0.33f, 0.6f, 0.4f), V(0.14f, 0.42f, 0.14f), skin, V(90, 0, 0));
        Part(body, PrimitiveType.Sphere, "HandL", V(-0.33f, 0.6f, 0.82f), V(0.16f, 0.16f, 0.16f), white);
        Part(body, PrimitiveType.Sphere, "HandR", V(0.33f, 0.6f, 0.85f), V(0.16f, 0.16f, 0.16f), white);
        Part(body, PrimitiveType.Cylinder, "Wand", V(0.33f, 0.6f, 1.35f), V(0.05f, 0.5f, 0.05f), gold, V(90, 0, 0));
        Part(body, PrimitiveType.Cube, "StarA", V(0.33f, 0.6f, 1.9f), V(0.08f, 0.5f, 0.08f), star);
        Part(body, PrimitiveType.Cube, "StarB", V(0.33f, 0.6f, 1.9f), V(0.08f, 0.5f, 0.08f), star, V(0, 0, 90));
        Part(body, PrimitiveType.Sphere, "StarCore", V(0.33f, 0.6f, 1.9f), V(0.18f, 0.18f, 0.18f), white);

        // Bacaklar ve cizmeler
        Part(body, PrimitiveType.Capsule, "LegL", V(-0.14f, -0.95f, 0), V(0.17f, 0.45f, 0.17f), skin);
        Part(body, PrimitiveType.Capsule, "LegR", V(0.14f, -0.95f, 0), V(0.17f, 0.45f, 0.17f), skin);
        Part(body, PrimitiveType.Cube, "BootL", V(-0.14f, -1.5f, 0.05f), V(0.2f, 0.18f, 0.32f), red);
        Part(body, PrimitiveType.Cube, "BootR", V(0.14f, -1.5f, 0.05f), V(0.2f, 0.18f, 0.32f), red);

        // Kanat / motor yok: ayaklarinin altinda parlayan buyu halkasi
        Part(body, PrimitiveType.Cylinder, "MagicRing", V(0, -1.6f, 0), V(1.5f, 0.02f, 1.5f), star);
        Part(body, PrimitiveType.Cylinder, "MagicRingInner", V(0, -1.62f, 0), V(1.15f, 0.015f, 1.15f), glow);

        Part(body, PrimitiveType.Cube, "BellySkid", V(0, -1.68f, 0), V(0.3f, 0.08f, 0.8f), dark);

        Finish(root, "AySavascisi", 0.2f, 0f, -1.62f);
    }

    // ================================================================== KI KAHRAMANI (dik, 12 derece one egik)

    private static void BuildKi()
    {
        Material gi = Lit("Hero_Gi", new Color(0.9f, 0.45f, 0.08f));
        Material blue = Lit("Hero_GiBlue", new Color(0.12f, 0.2f, 0.62f));
        Material skin = Lit("Hero_SkinTan", new Color(0.93f, 0.74f, 0.58f));
        Material hair = Unlit("Hero_HairGold", new Color(1f, 0.9f, 0.3f));
        Material ki = Unlit("Hero_Ki", new Color(0.5f, 0.85f, 1f));
        Material white = Unlit("Hero_EyeWhite", new Color(0.95f, 0.95f, 0.95f));
        Material dark = CreateGliderPlane.GetMaterial("Plane_Dark", new Color(0.12f, 0.12f, 0.12f));

        var root = new GameObject("KiKahramani");
        GameObject body = Body(root, PlaneBuilder.HeroLean[1]);

        Part(body, PrimitiveType.Capsule, "Torso", V(0, 0.3f, 0), V(0.62f, 0.5f, 0.55f), gi);
        Part(body, PrimitiveType.Cube, "Belt", V(0, -0.2f, 0), V(0.66f, 0.14f, 0.58f), blue);
        Part(body, PrimitiveType.Sphere, "Head", V(0, 1.15f, 0.05f), V(0.5f, 0.52f, 0.5f), skin);
        Part(body, PrimitiveType.Sphere, "EyeL", V(-0.11f, 1.17f, 0.28f), V(0.1f, 0.08f, 0.05f), white);
        Part(body, PrimitiveType.Sphere, "EyeR", V(0.11f, 1.17f, 0.28f), V(0.1f, 0.08f, 0.05f), white);
        Part(body, PrimitiveType.Cube, "Brow", V(0, 1.27f, 0.27f), V(0.34f, 0.04f, 0.05f), dark);

        // Sivri sac: kafanin ustunden yukari/geri dilimler
        for (int i = 0; i < 6; i++)
        {
            float a = -50f + i * 20f;
            Part(body, PrimitiveType.Cube, "Hair", V(Mathf.Sin(a * Mathf.Deg2Rad) * 0.22f, 1.45f + 0.1f * Mathf.Cos(a * Mathf.Deg2Rad), -0.05f - i % 2 * 0.1f),
                V(0.12f, 0.5f, 0.12f), hair, V(-25, 0, -a * 0.7f));
        }

        // Kollar one uzanir, yumruklar ki kuresi
        Part(body, PrimitiveType.Capsule, "ArmL", V(-0.33f, 0.6f, 0.4f), V(0.16f, 0.45f, 0.16f), skin, V(90, 0, 0));
        Part(body, PrimitiveType.Capsule, "ArmR", V(0.33f, 0.6f, 0.4f), V(0.16f, 0.45f, 0.16f), skin, V(90, 0, 0));
        Part(body, PrimitiveType.Cube, "BandL", V(-0.33f, 0.6f, 0.75f), V(0.2f, 0.2f, 0.12f), blue);
        Part(body, PrimitiveType.Cube, "BandR", V(0.33f, 0.6f, 0.75f), V(0.2f, 0.2f, 0.12f), blue);
        Part(body, PrimitiveType.Sphere, "FistL", V(-0.33f, 0.6f, 0.98f), V(0.26f, 0.26f, 0.26f), ki);
        Part(body, PrimitiveType.Sphere, "FistR", V(0.33f, 0.6f, 0.98f), V(0.26f, 0.26f, 0.26f), ki);

        // Bacaklar ve cizmeler
        Part(body, PrimitiveType.Capsule, "LegL", V(-0.15f, -0.95f, 0), V(0.2f, 0.45f, 0.2f), gi);
        Part(body, PrimitiveType.Capsule, "LegR", V(0.15f, -0.95f, 0), V(0.2f, 0.45f, 0.2f), gi);
        Part(body, PrimitiveType.Cube, "BootL", V(-0.15f, -1.5f, 0.05f), V(0.22f, 0.2f, 0.34f), blue);
        Part(body, PrimitiveType.Cube, "BootR", V(0.15f, -1.5f, 0.05f), V(0.22f, 0.2f, 0.34f), blue);

        // Aura alevleri: govdenin cevresinde dik parlak dilimler
        for (int i = 0; i < 8; i++)
        {
            float a = Mathf.PI * 2f * i / 8f;
            Part(body, PrimitiveType.Cube, "Aura", V(Mathf.Cos(a) * 0.65f, 0.2f, Mathf.Sin(a) * 0.6f), V(0.08f, 1.5f, 0.08f), ki, V(0, 0, Mathf.Cos(a) * 12f));
        }

        Part(body, PrimitiveType.Cube, "BellySkid", V(0, -1.65f, 0), V(0.3f, 0.08f, 0.8f), dark);

        Finish(root, "KiKahramani", 0.15f, 0f, -1.6f);
    }

    // ================================================================== IMPARATOR (dik, 22 derece one egik)

    private static void BuildEmperor()
    {
        Material robe = Lit("Hero_Robe", new Color(0.1f, 0.08f, 0.12f));
        Material cape = Lit("Hero_Cape", new Color(0.3f, 0.05f, 0.08f));
        Material pale = Lit("Hero_Pale", new Color(0.85f, 0.82f, 0.75f));
        Material eye = Unlit("Hero_EyeRed", new Color(1f, 0.75f, 0.1f));
        Material dark = Unlit("Hero_DarkEnergy", new Color(0.6f, 0.25f, 1f));
        Material black = CreateGliderPlane.GetMaterial("Plane_Dark", new Color(0.12f, 0.12f, 0.12f));

        var root = new GameObject("Imparator");
        GameObject body = Body(root, PlaneBuilder.HeroLean[2]);

        // Dik suzulen cuppe: kapsul govde, kukuleta, icinde solgun yuz ve parlayan gozler
        Part(body, PrimitiveType.Capsule, "Robe", V(0, 0, 0), V(0.9f, 1.1f, 0.9f), robe);
        Part(body, PrimitiveType.Cylinder, "Hem", V(0, -0.95f, 0), V(1.1f, 0.12f, 1.1f), robe);
        Part(body, PrimitiveType.Sphere, "Hood", V(0, 1.05f, 0.05f), V(0.62f, 0.6f, 0.62f), robe);
        Part(body, PrimitiveType.Sphere, "Head", V(0, 1.0f, 0.22f), V(0.36f, 0.42f, 0.3f), pale);
        Part(body, PrimitiveType.Sphere, "EyeL", V(-0.08f, 1.05f, 0.38f), V(0.07f, 0.05f, 0.04f), eye);
        Part(body, PrimitiveType.Sphere, "EyeR", V(0.08f, 1.05f, 0.38f), V(0.07f, 0.05f, 0.04f), eye);
        Part(body, PrimitiveType.Cube, "Cape", V(0, 0.3f, -0.5f), V(1.1f, 1.9f, 0.08f), cape);

        // Kollar one uzanir, parmak uclari yildirim kaynagi
        for (int s = -1; s <= 1; s += 2)
        {
            Part(body, PrimitiveType.Capsule, "Sleeve", V(s * 0.42f, 0.68f, 0.35f), V(0.22f, 0.4f, 0.22f), robe, V(60, 0, s * -25));
            Part(body, PrimitiveType.Sphere, "Hand", V(s * 0.42f, 0.45f, 0.62f), V(0.17f, 0.15f, 0.2f), pale);
        }

        // Ayak altinda karanlik enerji halkasi
        Part(body, PrimitiveType.Cylinder, "Ring", V(0, -1.1f, 0), V(1.5f, 0.03f, 1.5f), dark);
        Part(body, PrimitiveType.Cylinder, "RingInner", V(0, -1.12f, 0), V(1.1f, 0.02f, 1.1f), dark);

        Part(body, PrimitiveType.Cube, "BellySkid", V(0, -1.2f, 0), V(0.3f, 0.08f, 0.8f), black);

        Finish(root, "Imparator", 0.75f, 0f, -1.05f);
    }

    // ================================================================== yardimcilar

    private static Vector3 V(float x, float y, float z) { return new Vector3(x, y, z); }

    // Govde pivotu: butun parcalar bunun altinda, one egik (PlaneBuilder.HeroLean ile ayni)
    private static GameObject Body(GameObject root, float lean)
    {
        var body = new GameObject("Body");
        body.transform.SetParent(root.transform, false);
        body.transform.localRotation = Quaternion.Euler(lean, 0f, 0f);
        return body;
    }

    private static Material Lit(string name, Color color) { return CreateGliderPlane.GetMaterial(name, color); }

    private static Material Unlit(string name, Color color)
    {
        string path = $"Assets/Materials/{name}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat != null) return mat;

        mat = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { color = color };
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    private static void Part(GameObject parent, PrimitiveType type, string name, Vector3 pos, Vector3 scale,
        Material mat, Vector3 euler = default)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent.transform, false);
        go.transform.localPosition = pos;
        go.transform.localEulerAngles = euler;
        go.transform.localScale = scale;
        var r = go.GetComponent<Renderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    // Collider'lar (govde/kafa/etek), iz efektleri ve prefab kaydi
    private static void Finish(GameObject root, string prefabName, float trailX, float trailZ, float trailY)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            string n = t.name;
            if (n == "Torso" || n == "Robe")
                t.gameObject.AddComponent<CapsuleCollider>();
            else if (n == "Head" || n == "Skirt" || n == "Hood")
                t.gameObject.AddComponent<SphereCollider>();
            else if (n == "BellySkid")
                t.gameObject.AddComponent<BoxCollider>();
        }

        Material trail = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Trail.mat");
        AddTrail(root.transform, "TrailL", V(-trailX, trailY, trailZ), trail);
        AddTrail(root.transform, "TrailR", V(trailX, trailY, trailZ), trail);

        string path = $"{PrefabFolder}/{prefabName}.prefab";
        PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
    }

    private static void AddTrail(Transform root, string name, Vector3 localPos, Material mat)
    {
        var go = new GameObject(name);
        go.transform.SetParent(root, false);
        go.transform.localPosition = localPos;

        var tr = go.AddComponent<TrailRenderer>();
        tr.time = 1.4f;
        tr.startWidth = 0.18f;
        tr.endWidth = 0f;
        tr.minVertexDistance = 0.5f;
        tr.emitting = false;
        if (mat != null) tr.sharedMaterial = mat;
        tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0.7f, 0f), new GradientAlphaKey(0f, 1f) });
        tr.colorGradient = g;
    }

    // ================================================================== sahneler

    // Acik savas sahnesindeki FlightTestManager'a kahramanlari ekler (model indeksi sirasiyla 9, 10, 11)
    private static void AddToScene()
    {
        var mgr = Object.FindFirstObjectByType<FlightTestManager>();
        if (mgr == null || mgr.planes == null || mgr.planes.Length < 9)
        {
            Debug.LogWarning("FlightTestManager ya da 9 ucak bulunamadi; once Tools/Create New Aircraft calistir (SampleScene acik olmali).");
            return;
        }

        Transform parent = mgr.planes[0].transform.parent;
        var keep = new List<PlaneController>();
        for (int i = 0; i < mgr.planes.Length && i < 9; i++) keep.Add(mgr.planes[i]);
        for (int i = 9; i < mgr.planes.Length; i++) if (mgr.planes[i] != null) Object.DestroyImmediate(mgr.planes[i].gameObject);
        foreach (Transform child in new List<Transform>(parent.GetComponentsInChildren<Transform>(true)))
        {
            if (child == null || child.parent != parent) continue;
            string n = child.name.Replace("(Clone)", "").Trim();
            if (System.Array.IndexOf(Names, n) >= 0) Object.DestroyImmediate(child.gameObject);
        }

        PlaneController reference = keep[0];
        for (int i = 0; i < Names.Length; i++)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/{Names[i]}.prefab");
            if (prefab == null) continue;

            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.SetPositionAndRotation(reference.SpawnPosition, reference.SpawnRotation);

            var rb = go.GetComponent<Rigidbody>();
            if (rb == null) rb = go.AddComponent<Rigidbody>();
            rb.mass = 1f;

            var pc = go.GetComponent<PlaneController>();
            if (pc == null) pc = go.AddComponent<PlaneController>();
            pc.ApplyPreset(AircraftCatalog.All[ModelIndex[i]].preset);
            pc.CopyLocationsFrom(reference);
            pc.trails = go.GetComponentsInChildren<TrailRenderer>(true);

            if (go.GetComponent<EngineSound>() == null) go.AddComponent<EngineSound>();

            go.SetActive(false);
            keep.Add(pc);
        }

        mgr.planes = keep.ToArray();
        EditorUtility.SetDirty(mgr);
        EditorSceneManager.MarkSceneDirty(mgr.gameObject.scene);
        EditorSceneManager.SaveOpenScenes();
    }

    // Hangar sahnesindeki HangarController.modelPrefabs listesine kahraman prefab'larini (indeks 9-11) yazar
    private static void RegisterInHangar()
    {
        string current = EditorSceneManager.GetActiveScene().path;
        var scene = EditorSceneManager.OpenScene(HangarPath, OpenSceneMode.Single);
        var hc = Object.FindFirstObjectByType<HangarController>();
        if (hc == null)
        {
            Debug.LogWarning("Hangar sahnesinde HangarController yok; Tools/Setup Hangar calistir.");
        }
        else
        {
            var list = new List<GameObject>(hc.modelPrefabs ?? new GameObject[0]);
            while (list.Count < 12) list.Add(null);
            for (int i = 0; i < Names.Length; i++)
                list[ModelIndex[i]] = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/{Names[i]}.prefab");
            hc.modelPrefabs = list.ToArray();
            EditorUtility.SetDirty(hc);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        if (!string.IsNullOrEmpty(current)) EditorSceneManager.OpenScene(current, OpenSceneMode.Single);
    }
}
