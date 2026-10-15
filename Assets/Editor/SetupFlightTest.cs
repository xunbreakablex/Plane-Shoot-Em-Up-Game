using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class SetupFlightTest
{
    private const string WorldName = "FlightTestWorld";
    private const string TerrainFolder = "Assets/Terrain";

    private const int Res = 1025;
    private const float Size = 6000f;
    private const float TerrainHeight = 320f;
    private const float AirfieldHeight = 10f;
    private const float WaterLevel = 4f;
    private const float RunwayLength = 900f;

    private static readonly Vector3 AirSpawn = new Vector3(0f, 280f, -900f);

    [MenuItem("Tools/Setup Flight Test")]
    public static void Setup()
    {
        CreateGliderPlane.EnsureFolder("Assets", "Materials");
        CreateGliderPlane.EnsureFolder("Assets", "Prefabs");
        CreateGliderPlane.EnsureFolder("Assets", "Terrain");

        // Savas efekt/mermi asset'leri (arazi verisi olusmadan once: asset import'lari terrain'i etkilemesin)
        SetupCombat.EnsureAssets();

        // Prefab'lari fizik/efekt icin hazirla
        PreparePrefab("Assets/Prefabs/WW1Biplane.prefab", 3.4f, false);
        PreparePrefab("Assets/Prefabs/WW2Fighter.prefab", 4.0f, false);
        PreparePrefab("Assets/Prefabs/GliderPlane.prefab", 3.5f, true);

        // Onceki test dunyasini ve sahnedeki demo ucaklari temizle
        DestroyRootByName(WorldName);
        DestroyRootByName("GliderPlane");
        DestroyRootByName("WW2Fighter");
        DestroyRootByName("WW1Biplane");

        var world = new GameObject(WorldName);

        Terrain terrain = BuildTerrain(world.transform);
        BuildWater(world.transform);
        BuildRunway(world.transform, out Vector3 runwayStart);
        BuildBuildings(world.transform, terrain);
        BuildClouds(world.transform);

        PlaneController[] planes = BuildPlanes(world.transform, runwayStart);
        if (planes == null) return;

        Camera cam = Camera.main;
        FollowCamera follow = null;
        if (cam != null)
        {
            cam.farClipPlane = 9000f;
            follow = cam.GetComponent<FollowCamera>();
            if (follow == null) follow = cam.gameObject.AddComponent<FollowCamera>();
            follow.target = planes[0].transform;
        }
        else
        {
            Debug.LogWarning("Main Camera bulunamadi, takip kamerasi eklenemedi.");
        }

        var mgrGo = new GameObject("FlightTestManager");
        mgrGo.transform.SetParent(world.transform, false);
        var mgr = mgrGo.AddComponent<FlightTestManager>();
        mgr.planes = planes;
        mgr.followCamera = follow;

        // Savas: dusmanlar, mouse ile nisan/ates, seri oldurme yazilari
        SetupCombat.Attach(world.transform, mgr, cam);

        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = new Color(0.70f, 0.80f, 0.90f);
        RenderSettings.fogStartDistance = 1500f;
        RenderSettings.fogEndDistance = 6000f;

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(world.scene);
        EditorSceneManager.SaveOpenScenes();
        Selection.activeGameObject = planes[0].gameObject;

        Debug.Log("Flight test hazir. Play'e bas.");
    }

    // ------------------------------------------------------------ prefabs

    private static void PreparePrefab(string path, float wingSpan, bool addGlider)
    {
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            foreach (var c in root.GetComponentsInChildren<Collider>(true))
                Object.DestroyImmediate(c);

            if (addGlider)
            {
                RemoveGliderGear(root.transform);   // tekrar calistirmada kopya birikmesin
                AddGliderGear(root.transform);
            }

            string[] boxes = { "Wing", "WingL", "WingR", "UpperWing", "LowerWing", "TailPlane", "Fin", "TailSkid" };
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                string n = t.name;
                if (System.Array.IndexOf(boxes, n) >= 0)
                    t.gameObject.AddComponent<BoxCollider>();
                else if (n == "Fuselage")
                    t.gameObject.AddComponent<CapsuleCollider>();
                else if (n == "Wheel" || n == "TailWheel")
                    t.gameObject.AddComponent<SphereCollider>().radius = 0.5f;
            }

            // Cift kanat: kuyruk kizagi yere yakin dursun ki ucak ~8 derece burun yukari dursun
            var skid = root.transform.Find("TailSkid");
            if (skid != null)
            {
                skid.localPosition = new Vector3(0f, -0.7f, -2.9f);
                skid.localEulerAngles = Vector3.zero;
                if (root.transform.Find("TailStrut") == null)
                {
                    var wood = CreateGliderPlane.GetMaterial("WW1_Wood", new Color(0.55f, 0.36f, 0.18f));
                    var strut = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    strut.name = "TailStrut";
                    Object.DestroyImmediate(strut.GetComponent<Collider>());
                    strut.transform.SetParent(root.transform, false);
                    strut.transform.localPosition = new Vector3(0f, -0.33f, -2.88f);
                    strut.transform.localScale = new Vector3(0.04f, 0.7f, 0.04f);
                    strut.GetComponent<Renderer>().sharedMaterial = wood;
                }
            }

            // Kanat ucu izleri (varsa korunur; sahnedeki PlaneController referanslari bozulmasin)
            Material trailMat = GetTrailMaterial();
            EnsureTrail(root.transform, "TrailL", new Vector3(-wingSpan, 0.1f, -0.3f), trailMat);
            EnsureTrail(root.transform, "TrailR", new Vector3(wingSpan, 0.1f, -0.3f), trailMat);

            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void RemoveGliderGear(Transform root)
    {
        for (int i = root.childCount - 1; i >= 0; i--)
        {
            string n = root.GetChild(i).name;
            if (n == "Wheel" || n == "TailWheel" || n == "GearStrut")
                Object.DestroyImmediate(root.GetChild(i).gameObject);
        }
    }

    private static void EnsureTrail(Transform root, string name, Vector3 localPos, Material mat)
    {
        Transform existing = root.Find(name);
        if (existing != null && existing.GetComponent<TrailRenderer>() != null)
        {
            existing.localPosition = localPos;
            return;
        }
        AddTrail(root, name, localPos, mat);
    }

    // Sahneyi bozmadan sadece prefab'lari (collider/izler/gear) yeniden hazirlar
    [MenuItem("Tools/Repair Plane Prefabs")]
    public static void RepairPrefabs()
    {
        PreparePrefab("Assets/Prefabs/WW1Biplane.prefab", 3.4f, false);
        PreparePrefab("Assets/Prefabs/WW2Fighter.prefab", 4.0f, false);
        PreparePrefab("Assets/Prefabs/GliderPlane.prefab", 3.5f, true);

        // Sahnedeki ucaklarin iz referanslarini tazele
        foreach (var pc in Object.FindObjectsByType<PlaneController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            pc.trails = pc.GetComponentsInChildren<TrailRenderer>(true);
            EditorUtility.SetDirty(pc);
        }

        AssetDatabase.SaveAssets();
        var scene = EditorSceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveOpenScenes();
        Debug.Log("Ucak prefab'lari onarildi.");
    }

    private static void AddGliderGear(Transform root)
    {
        Material black = CreateGliderPlane.GetMaterial("WW2_Black", new Color(0.06f, 0.06f, 0.06f));
        MakeGearPart(root, "Wheel", new Vector3(0f, -0.42f, 0.45f), new Vector3(0.32f, 0.05f, 0.32f), black, true);
        MakeGearPart(root, "TailWheel", new Vector3(0f, -0.08f, -1.4f), new Vector3(0.14f, 0.03f, 0.14f), black, true);
        MakeGearPart(root, "GearStrut", new Vector3(0f, -0.28f, 0.45f), new Vector3(0.05f, 0.3f, 0.05f), black, false);
    }

    private static void MakeGearPart(Transform parent, string name, Vector3 pos, Vector3 scale, Material mat, bool wheel)
    {
        var go = GameObject.CreatePrimitive(wheel ? PrimitiveType.Cylinder : PrimitiveType.Cube);
        go.name = name;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = scale;
        if (wheel) go.transform.localEulerAngles = new Vector3(0, 0, 90);
        go.GetComponent<Renderer>().sharedMaterial = mat;
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
        tr.sharedMaterial = mat;
        tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0.7f, 0f), new GradientAlphaKey(0f, 1f) });
        tr.colorGradient = g;
    }

    private static Material GetTrailMaterial()
    {
        const string path = "Assets/Materials/Trail.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat != null) return mat;
        mat = new Material(Shader.Find("Sprites/Default"));
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    private static PlaneController[] BuildPlanes(Transform parent, Vector3 runwayStart)
    {
        var root = new GameObject("Planes");
        root.transform.SetParent(parent, false);

        // Sira AircraftCatalog ile ayni. Yeni ucaklar (Tools/Create New Aircraft) yoksa atlanir,
        // sonradan ayni aracla eklenebilir.
        var defs = new (string prefab, PlaneController.PlanePreset preset)[]
        {
            ("Assets/Prefabs/WW1Biplane.prefab", PlaneController.PlanePreset.Biplane),
            ("Assets/Prefabs/WW2Fighter.prefab", PlaneController.PlanePreset.Fighter),
            ("Assets/Prefabs/GliderPlane.prefab", PlaneController.PlanePreset.Glider),
            ("Assets/Prefabs/Atmaca.prefab", PlaneController.PlanePreset.LightJet),
            ("Assets/Prefabs/Kartal.prefab", PlaneController.PlanePreset.HeavyJet),
            ("Assets/Prefabs/YavruKopekbaligi.prefab", PlaneController.PlanePreset.MiniShark),
        };

        var list = new List<PlaneController>();
        for (int i = 0; i < defs.Length; i++)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(defs[i].prefab);
            if (prefab == null)
            {
                if (i < 3)
                {
                    Debug.LogError("Prefab bulunamadi: " + defs[i].prefab);
                    return null;
                }
                continue;   // yeni ucak henuz uretilmemis
            }

            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.transform);
            go.transform.SetPositionAndRotation(AirSpawn, Quaternion.identity);

            var body = go.GetComponent<Rigidbody>();
            if (body == null) body = go.AddComponent<Rigidbody>();
            body.mass = 1f;

            var pc = go.GetComponent<PlaneController>();
            if (pc == null) pc = go.AddComponent<PlaneController>();
            pc.ApplyPreset(defs[i].preset);
            pc.SetSpawn(AirSpawn, Quaternion.identity);
            pc.SetRunway(runwayStart, Quaternion.identity);
            pc.trails = go.GetComponentsInChildren<TrailRenderer>(true);

            if (go.GetComponent<EngineSound>() == null) go.AddComponent<EngineSound>();

            list.Add(pc);
        }

        for (int i = 1; i < list.Count; i++) list[i].gameObject.SetActive(false);
        return list.ToArray();
    }

    // ------------------------------------------------------------ terrain

    private static Terrain BuildTerrain(Transform parent)
    {
        // Dikkat: TerrainData olusturulduktan sonra baska asset import edilirse Unity veriyi diskten
        // yeniden yukleyip bellekteki degisiklikleri siler. Bu yuzden tum asset'ler once hazirlanir.
        var grass = MakeLayer("Grass", new Color(0.22f, 0.40f, 0.14f), new Color(0.32f, 0.50f, 0.20f), 1);
        var rock = MakeLayer("Rock", new Color(0.38f, 0.37f, 0.35f), new Color(0.55f, 0.53f, 0.50f), 2);
        var sand = MakeLayer("Sand", new Color(0.70f, 0.64f, 0.45f), new Color(0.80f, 0.74f, 0.55f), 3);
        var treePrefab = BuildTreePrefab();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        string dataPath = TerrainFolder + "/FlightTerrain.asset";
        AssetDatabase.DeleteAsset(dataPath);

        var td = new TerrainData { heightmapResolution = Res };
        td.size = new Vector3(Size, TerrainHeight, Size);
        AssetDatabase.CreateAsset(td, dataPath);

        // Yukseklik haritasi: fbm + havaalani cevresinde duzluk
        var h = new float[Res, Res];
        float flat = AirfieldHeight / TerrainHeight;
        for (int y = 0; y < Res; y++)
        {
            float ny = y / (Res - 1f);
            float wz = (ny - 0.5f) * Size;
            for (int x = 0; x < Res; x++)
            {
                float nx = x / (Res - 1f);
                float wx = (nx - 0.5f) * Size;

                float n = 0.55f * Mathf.PerlinNoise(nx * 3f + 10f, ny * 3f + 10f)
                        + 0.30f * Mathf.PerlinNoise(nx * 7f + 50f, ny * 7f + 50f)
                        + 0.15f * Mathf.PerlinNoise(nx * 17f + 90f, ny * 17f + 90f);
                float e = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.30f, 0.80f, n)) * 0.95f;

                float zs = wz / 1.7f;
                float d = Mathf.Sqrt(wx * wx + zs * zs);
                float t = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(650f, 1500f, d));
                h[y, x] = Mathf.Lerp(flat, e, t);
            }
        }
        float hMax = 0f;
        foreach (float v in h) if (v > hMax) hMax = v;
        td.SetHeights(0, 0, h);
        Debug.Log($"[Terrain] input max={hMax:0.000} res={td.heightmapResolution} readback={td.GetInterpolatedHeight(0.5f, 0.5f):0.0}");

        // Katmanlar
        td.terrainLayers = new[] { grass, rock, sand };

        int ares = 512;
        td.alphamapResolution = ares;
        var a = new float[ares, ares, 3];
        for (int y = 0; y < ares; y++)
        {
            float ny = y / (ares - 1f);
            for (int x = 0; x < ares; x++)
            {
                float nx = x / (ares - 1f);
                float hm = td.GetInterpolatedHeight(nx, ny);
                float steep = td.GetSteepness(nx, ny);

                float rk = Mathf.Max(Mathf.Clamp01((steep - 26f) / 12f), Mathf.Clamp01((hm - 210f) / 50f));
                float sd = Mathf.Clamp01((8f - hm) / 4f) * (1f - rk);
                float gr = Mathf.Clamp01(1f - rk - sd);

                a[y, x, 0] = gr;
                a[y, x, 1] = rk;
                a[y, x, 2] = sd;
            }
        }
        td.SetAlphamaps(0, 0, a);

        // Agaclar
        td.treePrototypes = new[] { new TreePrototype { prefab = treePrefab } };
        var trees = new List<TreeInstance>();
        var rng = new System.Random(11);
        for (int i = 0; i < 40000 && trees.Count < 7000; i++)
        {
            float nx = 0.02f + (float)rng.NextDouble() * 0.96f;
            float ny = 0.02f + (float)rng.NextDouble() * 0.96f;
            float wx = (nx - 0.5f) * Size;
            float wz = (ny - 0.5f) * Size;
            float d = Mathf.Sqrt(wx * wx + (wz / 1.7f) * (wz / 1.7f));
            if (d < 750f) continue;

            float hm = td.GetInterpolatedHeight(nx, ny);
            if (hm < 9f || hm > 200f) continue;
            if (td.GetSteepness(nx, ny) > 28f) continue;
            if (Mathf.PerlinNoise(nx * 9f + 3f, ny * 9f + 7f) < 0.42f) continue;

            trees.Add(new TreeInstance
            {
                position = new Vector3(nx, hm / TerrainHeight, ny),
                prototypeIndex = 0,
                widthScale = 0.8f + (float)rng.NextDouble() * 0.7f,
                heightScale = 0.8f + (float)rng.NextDouble() * 0.9f,
                rotation = (float)rng.NextDouble() * Mathf.PI * 2f,
                color = Color.white,
                lightmapColor = Color.white
            });
        }
        td.SetTreeInstances(trees.ToArray(), true);
        EditorUtility.SetDirty(td);
        AssetDatabase.SaveAssets();   // sonraki asset islemlerinden once diske yaz
        Debug.Log($"[Terrain] height(0.5)={td.GetInterpolatedHeight(0.5f, 0.5f):0.0} trees={td.treeInstanceCount}");

        var go = Terrain.CreateTerrainGameObject(td);
        go.name = "Terrain";
        go.transform.SetParent(parent, false);
        go.transform.position = new Vector3(-Size * 0.5f, 0f, -Size * 0.5f);

        var terrain = go.GetComponent<Terrain>();
        terrain.heightmapPixelError = 6f;
        terrain.basemapDistance = 2500f;
        terrain.treeDistance = 1800f;
        terrain.treeBillboardDistance = 450f;
        terrain.drawInstanced = true;
        return terrain;
    }

    private static GameObject BuildTreePrefab()
    {
        const string path = "Assets/Prefabs/Tree.prefab";
        Material trunk = CreateGliderPlane.GetMaterial("Tree_Trunk", new Color(0.35f, 0.24f, 0.14f));
        Material leaf = CreateGliderPlane.GetMaterial("Tree_Leaf", new Color(0.13f, 0.32f, 0.12f));

        // Terrain agaclari kok objede MeshRenderer ister: govde + tac tek mesh'te (2 submesh)
        var cyl = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        var sph = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        var combine = new CombineInstance[2];
        combine[0].mesh = cyl.GetComponent<MeshFilter>().sharedMesh;
        combine[0].transform = Matrix4x4.TRS(new Vector3(0, 2f, 0), Quaternion.identity, new Vector3(0.6f, 2f, 0.6f));
        combine[1].mesh = sph.GetComponent<MeshFilter>().sharedMesh;
        combine[1].transform = Matrix4x4.TRS(new Vector3(0, 6f, 0), Quaternion.identity, new Vector3(4f, 6f, 4f));
        Object.DestroyImmediate(cyl);
        Object.DestroyImmediate(sph);

        var mesh = new Mesh { name = "TreeMesh" };
        mesh.CombineMeshes(combine, false, true);

        const string meshPath = "Assets/Prefabs/TreeMesh.asset";
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
        if (existing != null)
        {
            EditorUtility.CopySerialized(mesh, existing);
            Object.DestroyImmediate(mesh);
            mesh = existing;
        }
        else
        {
            AssetDatabase.CreateAsset(mesh, meshPath);
        }

        // Terrain agaci: LODGroup'lu kok + cocuk mesh (Unity bu yapiyi gecerli sayiyor)
        var root = new GameObject("Tree");
        var meshGo = new GameObject("LOD0");
        meshGo.transform.SetParent(root.transform, false);
        meshGo.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = meshGo.AddComponent<MeshRenderer>();
        mr.sharedMaterials = new[] { trunk, leaf };

        var lodGroup = root.AddComponent<LODGroup>();
        lodGroup.SetLODs(new[] { new LOD(0.005f, new Renderer[] { mr }) });
        lodGroup.RecalculateBounds();

        var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return prefab;
    }

    private static TerrainLayer MakeLayer(string name, Color a, Color b, int seed)
    {
        string texPath = $"{TerrainFolder}/{name}.png";
        string layerPath = $"{TerrainFolder}/{name}.terrainlayer";

        var tex = new Texture2D(128, 128, TextureFormat.RGBA32, false);
        for (int y = 0; y < 128; y++)
        for (int x = 0; x < 128; x++)
        {
            float n = 0.6f * Seamless(x, y, 128, 4f, seed) + 0.4f * Seamless(x, y, 128, 16f, seed + 9);
            tex.SetPixel(x, y, Color.Lerp(a, b, n));
        }
        File.WriteAllBytes(texPath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(texPath);

        AssetDatabase.DeleteAsset(layerPath);
        var layer = new TerrainLayer
        {
            diffuseTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath),
            tileSize = new Vector2(40f, 40f)
        };
        AssetDatabase.CreateAsset(layer, layerPath);
        return layer;
    }

    // Dikis izi vermeyen (tile'lanabilir) Perlin
    private static float Seamless(int x, int y, int size, float freq, int seed)
    {
        float u = x / (float)size;
        float v = y / (float)size;
        float s = freq / size * size; // freq = tile basina periyot sayisi
        float px = u * freq + seed * 13.7f;
        float py = v * freq + seed * 7.3f;

        float n00 = Mathf.PerlinNoise(px, py);
        float n10 = Mathf.PerlinNoise(px - freq, py);
        float n01 = Mathf.PerlinNoise(px, py - freq);
        float n11 = Mathf.PerlinNoise(px - freq, py - freq);
        return n00 * (1 - u) * (1 - v) + n10 * u * (1 - v) + n01 * (1 - u) * v + n11 * u * v + 0f * s;
    }

    // ------------------------------------------------------------- scenery

    private static void BuildWater(Transform parent)
    {
        var water = GameObject.CreatePrimitive(PrimitiveType.Cube);
        water.name = "Water";
        water.transform.SetParent(parent, false);
        water.transform.position = new Vector3(0f, WaterLevel - 0.5f, 0f);
        water.transform.localScale = new Vector3(Size * 1.6f, 1f, Size * 1.6f);

        Material mat = CreateGliderPlane.GetMaterial("Flight_Water", new Color(0.10f, 0.32f, 0.50f));
        mat.SetFloat("_Smoothness", 0.95f);
        water.GetComponent<Renderer>().sharedMaterial = mat;
    }

    private static void BuildRunway(Transform parent, out Vector3 runwayStart)
    {
        Material asphalt = CreateGliderPlane.GetMaterial("Flight_Asphalt", new Color(0.18f, 0.18f, 0.2f));
        Material paint = CreateGliderPlane.GetMaterial("Flight_Paint", new Color(0.9f, 0.9f, 0.9f));
        Material concrete = CreateGliderPlane.GetMaterial("Flight_Concrete", new Color(0.62f, 0.62f, 0.64f));

        float top = AirfieldHeight + 0.12f;

        var runway = GameObject.CreatePrimitive(PrimitiveType.Cube);
        runway.name = "Runway";
        runway.transform.SetParent(parent, false);
        runway.transform.position = new Vector3(0f, AirfieldHeight + 0.06f, 0f);
        runway.transform.localScale = new Vector3(30f, 0.12f, RunwayLength);
        runway.GetComponent<Renderer>().sharedMaterial = asphalt;

        for (int i = -10; i <= 10; i++)
        {
            var stripe = GameObject.CreatePrimitive(PrimitiveType.Cube);
            stripe.name = "RunwayStripe";
            stripe.transform.SetParent(parent, false);
            stripe.transform.position = new Vector3(0f, top + 0.01f, i * 40f);
            stripe.transform.localScale = new Vector3(1f, 0.02f, 18f);
            stripe.GetComponent<Renderer>().sharedMaterial = paint;
            Object.DestroyImmediate(stripe.GetComponent<Collider>());
        }

        // Hangar ve kule
        var hangar = GameObject.CreatePrimitive(PrimitiveType.Cube);
        hangar.name = "Hangar";
        hangar.transform.SetParent(parent, false);
        hangar.transform.position = new Vector3(70f, AirfieldHeight + 7f, -250f);
        hangar.transform.localScale = new Vector3(36f, 14f, 24f);
        hangar.GetComponent<Renderer>().sharedMaterial = concrete;

        var tower = GameObject.CreatePrimitive(PrimitiveType.Cube);
        tower.name = "Tower";
        tower.transform.SetParent(parent, false);
        tower.transform.position = new Vector3(60f, AirfieldHeight + 14f, 60f);
        tower.transform.localScale = new Vector3(6f, 28f, 6f);
        tower.GetComponent<Renderer>().sharedMaterial = concrete;

        runwayStart = new Vector3(0f, top, -RunwayLength * 0.5f + 40f);
    }

    private static void BuildBuildings(Transform parent, Terrain terrain)
    {
        var rng = new System.Random(7);
        Material[] mats =
        {
            CreateGliderPlane.GetMaterial("Flight_BuildingA", new Color(0.6f, 0.6f, 0.62f)),
            CreateGliderPlane.GetMaterial("Flight_BuildingB", new Color(0.55f, 0.45f, 0.38f)),
            CreateGliderPlane.GetMaterial("Flight_BuildingC", new Color(0.35f, 0.38f, 0.45f)),
        };

        var holder = new GameObject("Buildings");
        holder.transform.SetParent(parent, false);

        var td = terrain.terrainData;
        int clusters = 0;
        for (int attempt = 0; attempt < 600 && clusters < 7; attempt++)
        {
            float angle = (float)(rng.NextDouble() * Mathf.PI * 2f);
            float dist = 1200f + (float)rng.NextDouble() * 1600f;
            Vector3 c = new Vector3(Mathf.Cos(angle) * dist, 0f, Mathf.Sin(angle) * dist);

            float nx = c.x / Size + 0.5f, ny = c.z / Size + 0.5f;
            float hm = td.GetInterpolatedHeight(nx, ny);
            if (hm < 12f || hm > 70f || td.GetSteepness(nx, ny) > 7f) continue;

            clusters++;
            for (int b = 0; b < 20; b++)
            {
                Vector3 p = c + new Vector3(((float)rng.NextDouble() - 0.5f) * 260f, 0f,
                                            ((float)rng.NextDouble() - 0.5f) * 260f);
                float bh = 14f + (float)rng.NextDouble() * 70f;
                float bw = 14f + (float)rng.NextDouble() * 22f;
                float gy = terrain.SampleHeight(p) + terrain.transform.position.y;
                if (gy < WaterLevel + 1f) continue;

                var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
                box.name = "Building";
                box.transform.SetParent(holder.transform, false);
                box.transform.position = new Vector3(p.x, gy + bh * 0.5f - 1f, p.z);
                box.transform.localScale = new Vector3(bw, bh, bw);
                box.GetComponent<Renderer>().sharedMaterial = mats[rng.Next(mats.Length)];
            }
        }
    }

    private static void BuildClouds(Transform parent)
    {
        var rng = new System.Random(21);
        Material cloud = CreateGliderPlane.GetMaterial("Flight_Cloud", new Color(1f, 1f, 1f));

        var holder = new GameObject("Clouds");
        holder.transform.SetParent(parent, false);

        for (int c = 0; c < 80; c++)
        {
            float angle = (float)(rng.NextDouble() * Mathf.PI * 2f);
            float dist = 250f + (float)rng.NextDouble() * 3600f;
            float y = 350f + (float)rng.NextDouble() * 500f;
            Vector3 center = new Vector3(Mathf.Cos(angle) * dist, y, Mathf.Sin(angle) * dist);

            int puffs = 3 + rng.Next(4);
            for (int p = 0; p < puffs; p++)
            {
                var puff = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                puff.name = "CloudPuff";
                puff.transform.SetParent(holder.transform, false);
                float s = 40f + (float)rng.NextDouble() * 70f;
                puff.transform.position = center + new Vector3(
                    ((float)rng.NextDouble() - 0.5f) * 100f,
                    ((float)rng.NextDouble() - 0.5f) * 18f,
                    ((float)rng.NextDouble() - 0.5f) * 70f);
                puff.transform.localScale = new Vector3(s * 1.6f, s * 0.5f, s);
                puff.GetComponent<Renderer>().sharedMaterial = cloud;
                Object.DestroyImmediate(puff.GetComponent<Collider>());
            }
        }
    }

    // ---------------------------------------------------------------- utils

    private static void Part(Transform parent, PrimitiveType type, string name, Vector3 pos, Vector3 scale, Material mat)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = mat;
    }

    private static void DestroyRootByName(string name)
    {
        foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            // Kok silinince alt objeleri de yok olur, bu yuzden null kontrolu sart
            if (go != null && go.name == name && go.transform.parent == null)
                Object.DestroyImmediate(go);
        }
    }
}
