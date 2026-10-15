using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

// Hangar (ucak ozellestirme) sahnesini olusturur, Build Settings'e ilk sirada ekler ve
// editorde Play'in her zaman hangardan baslamasini saglar.
public static class SetupHangar
{
    private const string HangarPath = "Assets/Scenes/Hangar.unity";
    private const string FlightPath = "Assets/Scenes/SampleScene.unity";

    [MenuItem("Tools/Setup Hangar")]
    public static void Setup()
    {
        // Sira = AircraftCatalog model indeksi. Ilk uc zorunlu; yeni ucaklar (Tools/Create New Aircraft) yoksa bos kalir.
        var models = new[]
        {
            AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/WW1Biplane.prefab"),
            AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/WW2Fighter.prefab"),
            AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/GliderPlane.prefab"),
            AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Atmaca.prefab"),
            AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Kartal.prefab"),
            AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/YavruKopekbaligi.prefab"),
        };
        for (int i = 0; i < 3; i++)
        {
            if (models[i] == null)
            {
                Debug.LogError("Ucak prefab'lari eksik. Once Tools/Setup Flight Test calistir.");
                return;
            }
        }

        // Acik sahneyi kaydet (yeni sahne acilirken dialog cikmasin)
        EditorSceneManager.SaveOpenScenes();

        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        Material floorMat = CreateGliderPlane.GetMaterial("Hangar_Floor", new Color(0.16f, 0.18f, 0.22f));
        Material ringMat = CreateGliderPlane.GetMaterial("Hangar_Ring", new Color(0.95f, 0.7f, 0.15f));
        Material wallMat = CreateGliderPlane.GetMaterial("Hangar_Wall", new Color(0.09f, 0.11f, 0.15f));

        // Kamera
        Camera cam = Camera.main;
        if (cam != null)
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.06f, 0.08f, 0.12f);
            cam.fieldOfView = 38f;
            cam.transform.position = new Vector3(0f, 3.5f, -11.5f);
            cam.transform.LookAt(new Vector3(0f, 1.2f, 0f));
        }

        // Isik
        Light sun = Object.FindFirstObjectByType<Light>();
        if (sun != null)
        {
            sun.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
            sun.color = new Color(1f, 0.96f, 0.9f);
            sun.intensity = 1.15f;
            sun.shadows = LightShadows.Soft;
        }
        MakeLight("Rim Light Blue", new Vector3(-9f, 5f, 6f), new Color(0.35f, 0.55f, 1f), 2.2f, 24f);
        MakeLight("Rim Light Warm", new Vector3(9f, 4f, 6f), new Color(1f, 0.6f, 0.3f), 1.6f, 24f);

        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.32f, 0.36f, 0.44f);
        RenderSettings.fog = false;

        // Zemin ve platform
        var floor = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        floor.name = "Platform";
        floor.transform.position = new Vector3(0f, -0.1f, 0f);
        floor.transform.localScale = new Vector3(11f, 0.1f, 11f);
        floor.GetComponent<Renderer>().sharedMaterial = floorMat;

        var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        ring.name = "PlatformRing";
        ring.transform.position = new Vector3(0f, -0.12f, 0f);
        ring.transform.localScale = new Vector3(11.5f, 0.1f, 11.5f);
        ring.GetComponent<Renderer>().sharedMaterial = ringMat;

        var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ground.name = "Ground";
        ground.transform.position = new Vector3(0f, -0.6f, 0f);
        ground.transform.localScale = new Vector3(200f, 1f, 200f);
        ground.GetComponent<Renderer>().sharedMaterial = wallMat;

        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = "BackWall";
        wall.transform.position = new Vector3(0f, 9f, 22f);
        wall.transform.localScale = new Vector3(80f, 24f, 1f);
        wall.GetComponent<Renderer>().sharedMaterial = wallMat;

        // Onizleme koku + kontrolcu
        var previewRoot = new GameObject("PreviewRoot");

        var hangar = new GameObject("Hangar");
        var controller = hangar.AddComponent<HangarController>();
        controller.modelPrefabs = models;
        controller.previewRoot = previewRoot.transform;
        controller.cam = cam;

        // Kaydet, Build Settings, Play baslangic sahnesi
        Directory.CreateDirectory(Path.GetDirectoryName(HangarPath));
        EditorSceneManager.SaveScene(scene, HangarPath);

        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene(HangarPath, true),
            new EditorBuildSettingsScene(FlightPath, true),
        };

        EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(HangarPath);

        // Calisma sahnesine geri don
        EditorSceneManager.OpenScene(FlightPath, OpenSceneMode.Single);

        Debug.Log("Hangar hazir: Build Settings ilk sahne = Hangar. Play her zaman hangardan baslar.");
    }

    private static void MakeLight(string name, Vector3 pos, Color color, float intensity, float range)
    {
        var go = new GameObject(name);
        go.transform.position = pos;
        var l = go.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = color;
        l.intensity = intensity * 25f;   // URP'de nokta isiklari icin guc birimi buyuk
        l.range = range;
    }
}
