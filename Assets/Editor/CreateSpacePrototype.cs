using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

// Uzay prototipi sahnesini uretir: SampleScene'in kopyasi; arazi, pist, binalar ve bulutlar kaldirilir,
// yerine dogrusal meteor koridoru (SpaceRail), yildizli gokyuzu ve uzay isigi kurulur.
// Savas sistemi (CombatManager, ucaklar, HUD, magaza) aynen kalir. Her calistirmada sahne bastan uretilir.
public static class CreateSpacePrototype
{
    private const string SourceScene = "Assets/Scenes/SampleScene.unity";
    private const string ScenePath = "Assets/Scenes/SpacePrototype.unity";
    private const string HangarPath = "Assets/Scenes/Hangar.unity";
    private const string Folder = "Assets/Space";
    private const string SettingsPath = "Assets/Resources/GameSettings.asset";

    private static readonly string[] RemoveFromWorld =
    {
        "Terrain", "Water", "Runway", "RunwayStripe", "Hangar", "Tower", "Buildings", "Clouds"
    };

    [MenuItem("Tools/Create Space Prototype")]
    public static void Create()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("Once Play modundan cik.");
            return;
        }

        EditorSceneManager.SaveOpenScenes();
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets", "Space");

        Material meteorMat = EnsureMeteorMaterial();
        Material skyMat = EnsureSkybox();

        var settings = AssetDatabase.LoadAssetAtPath<GameSettings>(SettingsPath);
        float altitude = settings != null ? settings.space.corridorAltitude : 800f;

        // Kaynak sahnenin kopyasi
        AssetDatabase.DeleteAsset(ScenePath);
        if (!AssetDatabase.CopyAsset(SourceScene, ScenePath))
        {
            Debug.LogError("SampleScene kopyalanamadi.");
            return;
        }
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        // Yer dunyasi kaldirilir
        GameObject world = GameObject.Find("FlightTestWorld");
        if (world != null)
        {
            var remove = new HashSet<string>(RemoveFromWorld);
            for (int i = world.transform.childCount - 1; i >= 0; i--)
            {
                Transform c = world.transform.GetChild(i);
                if (remove.Contains(c.name)) Object.DestroyImmediate(c.gameObject);
            }
        }

        // Koridor: baslangic noktasi + ileri yon (+Z)
        Vector3 start = new Vector3(0f, altitude, 0f);
        var railGo = new GameObject("SpaceRail");
        railGo.transform.SetPositionAndRotation(start, Quaternion.identity);
        var rail = railGo.AddComponent<SpaceRail>();
        rail.meteorMaterial = meteorMat;

        // Ucaklar koridorun basinda dogar
        var flight = Object.FindFirstObjectByType<FlightTestManager>(FindObjectsInactive.Include);
        if (flight != null && flight.planes != null)
        {
            foreach (PlaneController p in flight.planes)
            {
                if (p == null) continue;
                p.transform.SetPositionAndRotation(start, Quaternion.identity);
                p.SetSpawn(start, Quaternion.identity);
                EditorUtility.SetDirty(p);
            }
        }

        // Gokyuzu ve isik
        RenderSettings.skybox = skyMat;
        RenderSettings.fog = false;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.15f, 0.17f, 0.25f);

        Light sun = Object.FindFirstObjectByType<Light>();
        if (sun != null)
        {
            sun.transform.rotation = Quaternion.Euler(28f, -40f, 0f);
            sun.color = new Color(1f, 0.95f, 0.88f);
            sun.intensity = 1.35f;
            sun.shadows = LightShadows.Soft;
        }

        Camera cam = Camera.main;
        if (cam != null)
        {
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.farClipPlane = Mathf.Max(cam.farClipPlane, 4000f);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        AddToBuildSettings(ScenePath);

        // Prototipi hemen denemek icin: hangardaki SAVASA BASLA uzay sahnesini acsin
        if (settings != null)
        {
            settings.useSpacePrototype = true;
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssetIfDirty(settings);
        }

        EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(HangarPath);
        Debug.Log("Uzay prototipi hazir: " + ScenePath + ". Play -> hangar -> SAVASA BASLA. Klasik sahneye donmek: GameSettings.useSpacePrototype kapat.");
    }

    private static void AddToBuildSettings(string path)
    {
        var list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        foreach (var s in list)
            if (s.path == path) return;
        list.Add(new EditorBuildSettingsScene(path, true));
        EditorBuildSettings.scenes = list.ToArray();
    }

    // ------------------------------------------------------------------ malzemeler

    private static Material EnsureMeteorMaterial()
    {
        string path = Folder + "/Meteor.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat != null) return mat;

        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        mat = new Material(lit) { enableInstancing = true };
        Color c = new Color(0.36f, 0.32f, 0.3f);
        mat.SetColor("_BaseColor", c);
        mat.color = c;
        mat.SetFloat("_Smoothness", 0.15f);
        mat.SetFloat("_Metallic", 0f);
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    private static Material EnsureSkybox()
    {
        string texPath = Folder + "/SpaceSky.png";
        string matPath = Folder + "/SpaceSky.mat";

        if (!File.Exists(texPath))
        {
            File.WriteAllBytes(texPath, MakeSkyTexture().EncodeToPNG());
            AssetDatabase.ImportAsset(texPath);
            var imp = (TextureImporter)AssetImporter.GetAtPath(texPath);
            imp.mipmapEnabled = false;
            imp.wrapModeU = TextureWrapMode.Repeat;
            imp.wrapModeV = TextureWrapMode.Clamp;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.maxTextureSize = 4096;
            imp.SaveAndReimport();
        }

        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
        var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Skybox/Panoramic"));
            AssetDatabase.CreateAsset(mat, matPath);
        }

        mat.SetTexture("_MainTex", tex);
        mat.SetFloat("_Exposure", 1f);
        mat.SetFloat("_Mapping", 1f);
        mat.SetFloat("_ImageType", 0f);
        mat.EnableKeyword("_MAPPING_LATITUDE_LONGITUDE_LAYOUT");
        mat.DisableKeyword("_MAPPING_6_FRAMES_LAYOUT");
        EditorUtility.SetDirty(mat);
        return mat;
    }

    // Esdikdortgen (lat-long) yildiz haritasi: koyu mavi zemin, iki renkli nebula, ~9000 yildiz
    private static Texture2D MakeSkyTexture()
    {
        const int W = 2048, H = 1024;
        var px = new Color[W * H];

        for (int y = 0; y < H; y++)
        {
            float phi = ((y + 0.5f) / H - 0.5f) * Mathf.PI;
            float cp = Mathf.Cos(phi), sp = Mathf.Sin(phi);
            for (int x = 0; x < W; x++)
            {
                float theta = (x + 0.5f) / W * Mathf.PI * 2f;
                Vector3 d = new Vector3(cp * Mathf.Cos(theta), sp, cp * Mathf.Sin(theta));

                float n1 = Fbm(d, 1.7f, 0f);
                float n2 = Fbm(d, 2.6f, 57f);

                Color c = new Color(0.008f, 0.011f, 0.024f);
                c += new Color(0.34f, 0.11f, 0.42f) * (Mathf.SmoothStep(0.5f, 0.66f, n1) * 0.5f);
                c += new Color(0.06f, 0.27f, 0.42f) * (Mathf.SmoothStep(0.53f, 0.68f, n2) * 0.42f);
                px[y * W + x] = c;
            }
        }

        var rnd = new System.Random(4242);
        for (int i = 0; i < 9000; i++)
        {
            // Kure uzerinde duzgun dagilim
            float u = (float)rnd.NextDouble();
            float v = Mathf.Acos(2f * (float)rnd.NextDouble() - 1f) / Mathf.PI;
            int sx = Mathf.Clamp((int)(u * W), 0, W - 1);
            int sy = Mathf.Clamp((int)(v * H), 0, H - 1);

            float b = Mathf.Pow((float)rnd.NextDouble(), 5f) * 0.9f + 0.12f;
            double tint = rnd.NextDouble();
            Color sc = tint < 0.15 ? new Color(0.7f, 0.8f, 1f) : (tint < 0.25 ? new Color(1f, 0.9f, 0.7f) : Color.white);

            Add(px, W, H, sx, sy, sc * b);
            if (b > 0.55f)
            {
                // Parlak yildiz: kucuk hale
                float g = b * 0.35f;
                Add(px, W, H, sx + 1, sy, sc * g);
                Add(px, W, H, sx - 1, sy, sc * g);
                Add(px, W, H, sx, sy + 1, sc * g);
                Add(px, W, H, sx, sy - 1, sc * g);
            }
        }

        var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
        tex.SetPixels(px);
        tex.Apply();
        return tex;
    }

    private static void Add(Color[] px, int w, int h, int x, int y, Color c)
    {
        if (y < 0 || y >= h) return;
        x = (x % w + w) % w;
        int i = y * w + x;
        px[i] = new Color(Mathf.Min(1f, px[i].r + c.r), Mathf.Min(1f, px[i].g + c.g), Mathf.Min(1f, px[i].b + c.b));
    }

    // Dikis yeri olmayan (kure uzerinde) yaklasik 3B gurultu: uc duzlemde Perlin ortalamasi, 3 oktav
    private static float Fbm(Vector3 d, float scale, float offset)
    {
        float sum = 0f, amp = 0.5f, norm = 0f;
        for (int o = 0; o < 3; o++)
        {
            float s = scale * (1 << o);
            float n = (Mathf.PerlinNoise(d.x * s + offset, d.y * s + 11f)
                       + Mathf.PerlinNoise(d.y * s + 31f, d.z * s + offset)
                       + Mathf.PerlinNoise(d.z * s + offset + 71f, d.x * s + 5f)) / 3f;
            sum += n * amp;
            norm += amp;
            amp *= 0.5f;
        }
        return sum / norm;
    }
}
