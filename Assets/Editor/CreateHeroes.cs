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

    // Kullanicinin modeli (Assets/_Art): yalniz Imparator icin var; digerleri gelince ayni yolla takilir
    private const string EmperorModel = "Assets/_Art/Models/Emperor.fbx";
    private const string MoonModel = "Assets/_Art/Models/Quenn.fbx";
    private const string KiModel = "Assets/_Art/Models/KiHero.fbx";

    /// <summary>Yalniz kahraman prefab'larini yeniden kurar (sahne / hangar kayitlari ayni prefab'lari gosterir).</summary>
    [MenuItem("Tools/Create Heroes (yalniz prefab'lar)")]
    public static void RebuildPrefabs()
    {
        BuildMoon();
        BuildKi();
        BuildEmperor();
        AssetDatabase.SaveAssets();
    }

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

    // ================================================================== AY SAVASCISI (dik, 6 derece one egik)

    private static void BuildMoon()
    {
        Material star = Unlit("Hero_Star", new Color(1f, 0.95f, 0.5f));
        Material glow = Lit("Hero_Wing", new Color(0.8f, 0.9f, 1f));
        Material white = Unlit("Hero_StarWhite", new Color(1f, 1f, 1f));
        Material dark = CreateGliderPlane.GetMaterial("Plane_Dark", new Color(0.12f, 0.12f, 0.12f));

        var root = new GameObject("AySavascisi");
        GameObject body = Body(root, PlaneBuilder.HeroLean[0]);

        // Kullanicinin modeli (Quenn.fbx): FBX yatik ucus pozunda gelir (kokte -90 X); kok donusu sifirlaninca dik durur,
        // +Z'ye bakar. 2 m -> 1,5 olcekle eski boyuna (3 m). Degnek sag elde, ucu basin ustunde.
        Model(body, MoonModel, 1.5f, true);
        // Kullanici istegi: hafif sola donuk (namlu noktalari PlaneBuilder'da ayni aciyla)
        Transform moonModel = body.transform.Find("Model");
        if (moonModel != null) moonModel.localRotation = Quaternion.Euler(0f, -15f, 0f);
        Hitbox(body, "Torso", V(0, 0.05f, 0), V(0.75f, 0.85f, 0.65f));
        Hitbox(body, "Head", V(0, 0.95f, 0.05f), V(0.85f, 0.8f, 0.8f));

        // Degnek ucunda parlayan yildiz cekirdegi (ates ederken parlar, HeroAnimator "StarCore")
        Part(body, PrimitiveType.Sphere, "StarCore", V(0.62f, 1.44f, 0.25f), V(0.2f, 0.2f, 0.2f), white);

        // Kanat / motor yok: ayaklarinin altinda parlayan buyu halkasi
        Part(body, PrimitiveType.Cylinder, "MagicRing", V(0, -1.6f, 0), V(1.5f, 0.02f, 1.5f), star);
        Part(body, PrimitiveType.Cylinder, "MagicRingInner", V(0, -1.62f, 0), V(1.15f, 0.015f, 1.15f), glow);

        Part(body, PrimitiveType.Cube, "BellySkid", V(0, -1.68f, 0), V(0.3f, 0.08f, 0.8f), dark);
        body.transform.Find("BellySkid").GetComponent<Renderer>().enabled = false;

        Finish(root, "AySavascisi", 0.2f, 0f, -1.62f);
    }

    // ================================================================== KI KAHRAMANI (dik, 6 derece one egik)

    private static void BuildKi()
    {
        Material ki = Unlit("Hero_KiGold", new Color(1f, 0.92f, 0.45f));
        Material dark = CreateGliderPlane.GetMaterial("Plane_Dark", new Color(0.12f, 0.12f, 0.12f));

        var root = new GameObject("KiKahramani");
        GameObject body = Body(root, PlaneBuilder.HeroLean[1]);

        // Kullanicinin modeli (KiHero.fbx): Ay Savascisi gibi yatik gelir, kok donusu sifirlaninca dik; 1,5 olcek.
        // Sol yumruk one uzanik, sag yumruk belde geriye cekik.
        Model(body, KiModel, 1.5f, true);
        Hitbox(body, "Torso", V(0, 0.05f, 0), V(0.8f, 0.85f, 0.7f));
        Hitbox(body, "Head", V(0, 1.0f, 0.05f), V(0.9f, 0.85f, 0.85f));

        // Yumruklarda ki kureleri: ki ateşi buradan cikar, ates ederken parlar (HeroAnimator "FistL / FistR")
        Part(body, PrimitiveType.Sphere, "FistL", V(-0.69f, 0f, 1.02f), V(0.2f, 0.2f, 0.2f), ki);
        Part(body, PrimitiveType.Sphere, "FistR", V(0.69f, -0.18f, -0.25f), V(0.2f, 0.2f, 0.2f), ki);

        Part(body, PrimitiveType.Cube, "BellySkid", V(0, -1.65f, 0), V(0.3f, 0.08f, 0.8f), dark);
        body.transform.Find("BellySkid").GetComponent<Renderer>().enabled = false;

        Finish(root, "KiKahramani", 0.15f, 0f, -1.6f);
    }

    // ================================================================== IMPARATOR (dik, 10 derece one egik)

    private static void BuildEmperor()
    {
        Material dark = Unlit("Hero_DarkEnergy", new Color(0.6f, 0.25f, 1f));
        Material black = CreateGliderPlane.GetMaterial("Plane_Dark", new Color(0.12f, 0.12f, 0.12f));

        var root = new GameObject("Imparator");
        GameObject body = Body(root, PlaneBuilder.HeroLean[2]);

        // Kullanicinin modeli: kukuletali, pelerinli imparator (+Z'ye bakar, 2 m boy, merkezi gobek). 1,2 olcekle
        // eski boyuna (2,4 m) gelir; eller one uzanik (sol ~(-0,45; 0,3; 0,7), sag ~(0,54; 0,27; 0,51) model biriminde).
        Model(body, EmperorModel, 1.2f, false);

        // Carpisma hacimleri (gorunmez): govde ve kukuleta
        Hitbox(body, "Robe", V(0, -0.05f, 0), V(0.9f, 1.1f, 0.9f));
        Hitbox(body, "Hood", V(0, 0.85f, 0.1f), V(0.62f, 0.6f, 0.62f));

        // Parmak uclarinda karanlik enerji kureleri: yildirim kaynagi, ates ederken parlar (HeroAnimator "Hand")
        Part(body, PrimitiveType.Sphere, "Hand", V(-0.54f, 0.36f, 0.86f), V(0.14f, 0.14f, 0.14f), dark);
        Part(body, PrimitiveType.Sphere, "Hand", V(0.62f, 0.33f, 0.66f), V(0.14f, 0.14f, 0.14f), dark);

        // Ayak altinda karanlik enerji halkasi
        Part(body, PrimitiveType.Cylinder, "Ring", V(0, -1.15f, 0), V(1.5f, 0.03f, 1.5f), dark);
        Part(body, PrimitiveType.Cylinder, "RingInner", V(0, -1.17f, 0), V(1.1f, 0.02f, 1.1f), dark);

        Part(body, PrimitiveType.Cube, "BellySkid", V(0, -1.25f, 0), V(0.3f, 0.08f, 0.8f), black);
        body.transform.Find("BellySkid").GetComponent<Renderer>().enabled = false;

        Finish(root, "Imparator", 0.75f, 0f, -1.05f);
    }

    // Kullanicinin FBX modelini govdeye takar (prefab baglantili: model guncellenince prefab da guncellenir).
    // resetRotation: FBX kokundeki donusu (Blender'in -90 X'i) sifirlar.
    private static void Model(GameObject body, string path, float scale, bool resetRotation)
    {
        var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (fbx == null) { Debug.LogWarning("Kahraman modeli yok: " + path); return; }
        var model = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
        model.name = "Model";
        model.transform.SetParent(body.transform, false);
        if (resetRotation) model.transform.localRotation = Quaternion.identity;
        model.transform.localScale = Vector3.one * scale;
        foreach (var r in model.GetComponentsInChildren<Renderer>(true))
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    // Gorunmez carpisma hacmi: Finish isme gore collider ekler (Robe / Torso = kapsul, Hood / Head = kure)
    private static void Hitbox(GameObject parent, string name, Vector3 pos, Vector3 scale)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        go.transform.localPosition = pos;
        go.transform.localScale = scale;
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
