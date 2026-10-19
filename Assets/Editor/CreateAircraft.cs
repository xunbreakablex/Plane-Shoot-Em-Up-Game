using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Yeni ucaklar: Atmaca (hafif jet), Kartal (agir jet), Yavru Kopekbaligi. Modelleri primitive'lerden uretir,
// prefab olarak kaydeder, collider/iz ekler ve acik sahnedeki FlightTestManager'a ekler.
// Menu: Tools/Create New Aircraft  (tekrar calistirilabilir; mevcut olanlari yeniler)
public static class CreateAircraft
{
    private const string PrefabFolder = "Assets/Prefabs";

    private static readonly string[] Names = { "Atmaca", "Kartal", "YavruKopekbaligi", "UzayJeti", "Roket" };
    private static readonly int[] ModelIndex = { 3, 4, 5, 6, 7 };

    [MenuItem("Tools/Create New Aircraft")]
    public static void CreateAll()
    {
        CreateGliderPlane.EnsureFolder("Assets", "Materials");
        CreateGliderPlane.EnsureFolder("Assets", "Prefabs");

        BuildAtmaca();
        BuildKartal();
        BuildShark();
        BuildSpaceJet();
        BuildRocket();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        AddToScene();
        Debug.Log("Yeni ucaklar hazir: Atmaca, Kartal, Yavru Kopekbaligi. Hangar icin Tools/Setup Hangar, savas icin Tools/Setup Combat calistir.");
    }

    // ================================================================== modeller

    private static void BuildAtmaca()
    {
        Material body = CreateGliderPlane.GetMaterial("Atmaca_Body", new Color(0.55f, 0.62f, 0.74f));
        Material belly = CreateGliderPlane.GetMaterial("Atmaca_Belly", new Color(0.80f, 0.82f, 0.86f));
        Material accent = CreateGliderPlane.GetMaterial("Atmaca_Accent", new Color(0.92f, 0.22f, 0.14f));
        Material dark = CreateGliderPlane.GetMaterial("Plane_Dark", new Color(0.12f, 0.12f, 0.12f));
        Material glass = CreateGliderPlane.GetMaterial("Plane_Glass", new Color(0.35f, 0.65f, 0.95f));
        Material flame = Unlit("Jet_Flame", new Color(1f, 0.6f, 0.15f));

        var root = new GameObject("Atmaca");

        Part(root, PrimitiveType.Capsule, "Fuselage", V(0, 0, 0), V(0.75f, 2.8f, 0.75f), body, V(90, 0, 0));
        Part(root, PrimitiveType.Sphere, "Nose", V(0, 0, 2.9f), V(0.55f, 0.5f, 1.7f), body);
        Part(root, PrimitiveType.Sphere, "Canopy", V(0, 0.38f, 0.9f), V(0.5f, 0.42f, 1.4f), glass);
        Part(root, PrimitiveType.Cube, "IntakeL", V(-0.5f, -0.15f, 0.7f), V(0.3f, 0.42f, 1.1f), dark);
        Part(root, PrimitiveType.Cube, "IntakeR", V(0.5f, -0.15f, 0.7f), V(0.3f, 0.42f, 1.1f), dark);

        // Suprulmus kanatlar (uc kisim geriye)
        Part(root, PrimitiveType.Cube, "WingL", V(-1.58f, -0.05f, -0.96f), V(2.8f, 0.07f, 1.5f), body, V(0, -28, 0));
        Part(root, PrimitiveType.Cube, "WingR", V(1.58f, -0.05f, -0.96f), V(2.8f, 0.07f, 1.5f), body, V(0, 28, 0));
        Part(root, PrimitiveType.Cube, "WingTipL", V(-2.6f, -0.05f, -1.5f), V(0.35f, 0.08f, 0.95f), accent, V(0, -28, 0));
        Part(root, PrimitiveType.Cube, "WingTipR", V(2.6f, -0.05f, -1.5f), V(0.35f, 0.08f, 0.95f), accent, V(0, 28, 0));

        // Kuyruk: iki yana yatik dikey stabilize + yatay kanatciklar
        Part(root, PrimitiveType.Cube, "Fin", V(-0.45f, 0.58f, -2.3f), V(0.07f, 1.0f, 0.95f), accent, V(0, 0, 12));
        Part(root, PrimitiveType.Cube, "Fin", V(0.45f, 0.58f, -2.3f), V(0.07f, 1.0f, 0.95f), accent, V(0, 0, -12));
        Part(root, PrimitiveType.Cube, "TailPlane", V(-0.85f, 0.02f, -2.5f), V(1.3f, 0.05f, 0.7f), belly, V(0, -20, 0));
        Part(root, PrimitiveType.Cube, "TailPlane", V(0.85f, 0.02f, -2.5f), V(1.3f, 0.05f, 0.7f), belly, V(0, 20, 0));

        // Motor lüle ve alev
        Part(root, PrimitiveType.Cylinder, "Nozzle", V(0, 0, -2.95f), V(0.5f, 0.3f, 0.5f), dark, V(90, 0, 0));
        Part(root, PrimitiveType.Sphere, "Flame", V(0, 0, -3.4f), V(0.34f, 0.34f, 0.95f), flame);

        // Kucuk inis takimi
        Part(root, PrimitiveType.Cylinder, "Wheel", V(0, -0.55f, 1.0f), V(0.28f, 0.04f, 0.28f), dark, V(0, 0, 90));
        Part(root, PrimitiveType.Cylinder, "Wheel", V(-0.55f, -0.5f, -0.6f), V(0.3f, 0.04f, 0.3f), dark, V(0, 0, 90));
        Part(root, PrimitiveType.Cylinder, "Wheel", V(0.55f, -0.5f, -0.6f), V(0.3f, 0.04f, 0.3f), dark, V(0, 0, 90));

        Finish(root, "Atmaca", 2.6f, -1.5f, 0f);
    }

    private static void BuildKartal()
    {
        Material body = CreateGliderPlane.GetMaterial("Kartal_Body", new Color(0.32f, 0.37f, 0.35f));
        Material belly = CreateGliderPlane.GetMaterial("Kartal_Belly", new Color(0.62f, 0.64f, 0.60f));
        Material accent = CreateGliderPlane.GetMaterial("Kartal_Accent", new Color(0.95f, 0.76f, 0.15f));
        Material dark = CreateGliderPlane.GetMaterial("Plane_Dark", new Color(0.12f, 0.12f, 0.12f));
        Material glass = CreateGliderPlane.GetMaterial("Plane_Glass", new Color(0.35f, 0.65f, 0.95f));
        Material flame = Unlit("Jet_Flame", new Color(1f, 0.6f, 0.15f));

        var root = new GameObject("Kartal");

        Part(root, PrimitiveType.Capsule, "Fuselage", V(0, 0, 0), V(1.0f, 2.7f, 0.95f), body, V(90, 0, 0));
        Part(root, PrimitiveType.Sphere, "Nose", V(0, -0.02f, 2.8f), V(0.7f, 0.62f, 1.9f), body);
        Part(root, PrimitiveType.Sphere, "Canopy", V(0, 0.45f, 0.9f), V(0.62f, 0.5f, 1.8f), glass);
        Part(root, PrimitiveType.Cube, "Spine", V(0, 0.42f, -0.9f), V(0.45f, 0.25f, 2.0f), belly);

        // Genis, hafif suprulmus kanat
        Part(root, PrimitiveType.Cube, "WingL", V(-1.95f, -0.02f, -0.8f), V(3.2f, 0.09f, 2.0f), body, V(0, -20, 0));
        Part(root, PrimitiveType.Cube, "WingR", V(1.95f, -0.02f, -0.8f), V(3.2f, 0.09f, 2.0f), body, V(0, 20, 0));
        Part(root, PrimitiveType.Cube, "WingTipL", V(-3.15f, -0.02f, -1.25f), V(0.4f, 0.1f, 1.3f), accent, V(0, -20, 0));
        Part(root, PrimitiveType.Cube, "WingTipR", V(3.15f, -0.02f, -1.25f), V(0.4f, 0.1f, 1.3f), accent, V(0, 20, 0));

        // Cift motor
        for (int s = -1; s <= 1; s += 2)
        {
            Part(root, PrimitiveType.Cylinder, "EnginePod", V(0.8f * s, -0.12f, -1.3f), V(0.6f, 1.2f, 0.6f), dark, V(90, 0, 0));
            Part(root, PrimitiveType.Sphere, "Flame", V(0.8f * s, -0.12f, -2.65f), V(0.38f, 0.38f, 1.0f), flame);
            Part(root, PrimitiveType.Cube, "Fin", V(0.95f * s, 0.72f, -2.2f), V(0.08f, 1.3f, 1.2f), accent, V(0, 0, 10 * -s));
            Part(root, PrimitiveType.Cube, "TailPlane", V(1.15f * s, 0.0f, -2.5f), V(1.5f, 0.06f, 0.8f), belly);
        }

        // Inis takimi
        Part(root, PrimitiveType.Cylinder, "Wheel", V(0, -0.65f, 1.2f), V(0.32f, 0.05f, 0.32f), dark, V(0, 0, 90));
        Part(root, PrimitiveType.Cylinder, "Wheel", V(-0.7f, -0.6f, -0.6f), V(0.36f, 0.05f, 0.36f), dark, V(0, 0, 90));
        Part(root, PrimitiveType.Cylinder, "Wheel", V(0.7f, -0.6f, -0.6f), V(0.36f, 0.05f, 0.36f), dark, V(0, 0, 90));

        Finish(root, "Kartal", 3.2f, -1.6f, 0f);
    }

    private static void BuildShark()
    {
        Material body = CreateGliderPlane.GetMaterial("Shark_Body", new Color(0.28f, 0.42f, 0.58f));
        Material belly = CreateGliderPlane.GetMaterial("Shark_Belly", new Color(0.82f, 0.86f, 0.90f));
        Material accent = CreateGliderPlane.GetMaterial("Shark_Accent", new Color(0.95f, 0.30f, 0.15f));
        Material dark = CreateGliderPlane.GetMaterial("Plane_Dark", new Color(0.12f, 0.12f, 0.12f));
        Material white = CreateGliderPlane.GetMaterial("WW2_White", new Color(0.95f, 0.95f, 0.95f));
        Material eye = Unlit("Shark_Eye", new Color(1f, 0.2f, 0.1f));
        Material flame = Unlit("Jet_Flame", new Color(1f, 0.6f, 0.15f));

        var root = new GameObject("YavruKopekbaligi");

        Part(root, PrimitiveType.Capsule, "Fuselage", V(0, 0, 0), V(0.95f, 2.0f, 0.95f), body, V(90, 0, 0));
        Part(root, PrimitiveType.Capsule, "Belly", V(0, -0.2f, 0.25f), V(0.8f, 1.6f, 0.62f), belly, V(90, 0, 0));
        Part(root, PrimitiveType.Sphere, "Snout", V(0, 0.02f, 2.0f), V(0.7f, 0.62f, 1.3f), body);
        Part(root, PrimitiveType.Sphere, "Rear", V(0, 0, -1.95f), V(0.5f, 0.5f, 1.4f), body);

        // Gozler, agiz ve dis
        Part(root, PrimitiveType.Sphere, "EyeL", V(-0.4f, 0.22f, 1.6f), V(0.22f, 0.22f, 0.22f), eye);
        Part(root, PrimitiveType.Sphere, "EyeR", V(0.4f, 0.22f, 1.6f), V(0.22f, 0.22f, 0.22f), eye);
        Part(root, PrimitiveType.Cube, "Mouth", V(0, -0.27f, 1.9f), V(0.55f, 0.05f, 0.9f), dark);
        for (int i = 0; i < 4; i++)
        {
            Part(root, PrimitiveType.Cube, "Tooth", V(-0.2f, -0.24f, 1.55f + i * 0.2f), V(0.05f, 0.1f, 0.06f), white);
            Part(root, PrimitiveType.Cube, "Tooth", V(0.2f, -0.24f, 1.55f + i * 0.2f), V(0.05f, 0.1f, 0.06f), white);
        }

        // Sirt yuzgeci ve kuyruk
        Part(root, PrimitiveType.Cube, "Dorsal", V(0, 0.72f, -0.15f), V(0.07f, 0.8f, 1.1f), body, V(-20, 0, 0));
        Part(root, PrimitiveType.Cube, "Fin", V(0, 0.5f, -2.35f), V(0.08f, 1.3f, 0.9f), accent, V(20, 0, 0));
        Part(root, PrimitiveType.Cube, "TailPlane", V(0, 0.0f, -2.2f), V(1.3f, 0.05f, 0.6f), belly);

        // Gogus yuzgecleri = kanatlar (geriye suprulmus, ucu asagi)
        Part(root, PrimitiveType.Cube, "WingL", V(-1.35f, -0.28f, 0.1f), V(2.2f, 0.06f, 0.9f), body, V(0, -22, 16));
        Part(root, PrimitiveType.Cube, "WingR", V(1.35f, -0.28f, 0.1f), V(2.2f, 0.06f, 0.9f), body, V(0, 22, -16));

        // Itici alev ve karin kizagi
        Part(root, PrimitiveType.Sphere, "Flame", V(0, 0, -2.75f), V(0.32f, 0.32f, 0.9f), flame);
        Part(root, PrimitiveType.Cube, "BellySkid", V(0, -0.5f, 0.3f), V(0.18f, 0.1f, 1.4f), dark);

        Finish(root, "YavruKopekbaligi", 2.4f, -0.5f, -0.3f);
    }

    // Uzay jeti: ucgen kanatli, cam kubbeli, parlayan hatlari olan koyu krom jet (yerde kayak, tekerlek yok)
    private static void BuildSpaceJet()
    {
        Material body = CreateGliderPlane.GetMaterial("SpaceJet_Body", new Color(0.20f, 0.22f, 0.30f));
        Material belly = CreateGliderPlane.GetMaterial("SpaceJet_Belly", new Color(0.55f, 0.58f, 0.68f));
        Material accent = CreateGliderPlane.GetMaterial("SpaceJet_Accent", new Color(0.15f, 0.85f, 0.95f));
        Material dark = CreateGliderPlane.GetMaterial("Plane_Dark", new Color(0.12f, 0.12f, 0.12f));
        Material glass = CreateGliderPlane.GetMaterial("Plane_Glass", new Color(0.35f, 0.65f, 0.95f));
        Material glow = Unlit("SpaceJet_Glow", new Color(0.3f, 0.95f, 1f));
        Material flame = Unlit("SpaceJet_Flame", new Color(0.5f, 0.6f, 1f));

        var root = new GameObject("UzayJeti");

        Part(root, PrimitiveType.Capsule, "Fuselage", V(0, 0, 0), V(0.8f, 2.9f, 0.55f), body, V(90, 0, 0));
        Part(root, PrimitiveType.Sphere, "Nose", V(0, -0.02f, 3.0f), V(0.42f, 0.32f, 2.2f), body);
        Part(root, PrimitiveType.Sphere, "Canopy", V(0, 0.28f, 0.9f), V(0.45f, 0.36f, 1.6f), glass);

        // Ucgen kanatlar + parlayan on kenar seritleri
        Part(root, PrimitiveType.Cube, "WingL", V(-1.5f, -0.04f, -0.9f), V(2.8f, 0.05f, 2.4f), body, V(0, -35, 0));
        Part(root, PrimitiveType.Cube, "WingR", V(1.5f, -0.04f, -0.9f), V(2.8f, 0.05f, 2.4f), body, V(0, 35, 0));
        Part(root, PrimitiveType.Cube, "WingTipL", V(-2.55f, -0.04f, -1.9f), V(0.4f, 0.07f, 1.0f), accent, V(0, -35, 0));
        Part(root, PrimitiveType.Cube, "WingTipR", V(2.55f, -0.04f, -1.9f), V(0.4f, 0.07f, 1.0f), accent, V(0, 35, 0));
        Part(root, PrimitiveType.Cube, "GlowL", V(-1.15f, -0.01f, 0.1f), V(2.0f, 0.04f, 0.07f), glow, V(0, 35, 0));
        Part(root, PrimitiveType.Cube, "GlowR", V(1.15f, -0.01f, 0.1f), V(2.0f, 0.04f, 0.07f), glow, V(0, -35, 0));

        // On kanatciklar
        Part(root, PrimitiveType.Cube, "TailPlane", V(-0.55f, 0.02f, 1.7f), V(0.9f, 0.04f, 0.5f), belly, V(0, 18, 0));
        Part(root, PrimitiveType.Cube, "TailPlane", V(0.55f, 0.02f, 1.7f), V(0.9f, 0.04f, 0.5f), belly, V(0, -18, 0));

        // V kuyruk (disa yatik)
        Part(root, PrimitiveType.Cube, "Fin", V(-0.5f, 0.38f, -2.3f), V(0.05f, 0.7f, 0.7f), accent, V(0, 0, 28));
        Part(root, PrimitiveType.Cube, "Fin", V(0.5f, 0.38f, -2.3f), V(0.05f, 0.7f, 0.7f), accent, V(0, 0, -28));

        // Ikiz itici: parlayan cember + mavi alev
        for (int s = -1; s <= 1; s += 2)
        {
            Part(root, PrimitiveType.Cylinder, "Nozzle", V(0.32f * s, 0, -2.9f), V(0.42f, 0.28f, 0.42f), dark, V(90, 0, 0));
            Part(root, PrimitiveType.Cylinder, "Ring", V(0.32f * s, 0, -3.2f), V(0.5f, 0.03f, 0.5f), glow, V(90, 0, 0));
            Part(root, PrimitiveType.Sphere, "Flame", V(0.32f * s, 0, -3.7f), V(0.28f, 0.28f, 1.0f), flame);
        }

        Part(root, PrimitiveType.Cube, "BellySkid", V(0, -0.34f, 0.2f), V(0.16f, 0.08f, 1.6f), dark);

        Finish(root, "UzayJeti", 2.3f, -3.0f, 0f);
    }

    // Roket: uzun beyaz gövde, kirmizi burun, dort kanatcik, yan iticiler, buyuk nozul
    private static void BuildRocket()
    {
        Material body = CreateGliderPlane.GetMaterial("Rocket_Body", new Color(0.92f, 0.93f, 0.96f));
        Material belly = CreateGliderPlane.GetMaterial("Rocket_Belly", new Color(0.62f, 0.66f, 0.74f));
        Material accent = CreateGliderPlane.GetMaterial("Rocket_Accent", new Color(0.88f, 0.18f, 0.14f));
        Material dark = CreateGliderPlane.GetMaterial("Plane_Dark", new Color(0.12f, 0.12f, 0.12f));
        Material glass = CreateGliderPlane.GetMaterial("Plane_Glass", new Color(0.35f, 0.65f, 0.95f));
        Material flame = Unlit("Rocket_Flame", new Color(1f, 0.65f, 0.15f));

        var root = new GameObject("Roket");

        Part(root, PrimitiveType.Capsule, "Fuselage", V(0, 0, 0), V(1.1f, 2.8f, 1.1f), body, V(90, 0, 0));
        Part(root, PrimitiveType.Sphere, "Nose", V(0, 0, 2.7f), V(0.95f, 0.95f, 2.4f), accent);
        Part(root, PrimitiveType.Cylinder, "Band", V(0, 0, 1.0f), V(1.14f, 0.12f, 1.14f), accent, V(90, 0, 0));
        Part(root, PrimitiveType.Sphere, "Porthole", V(0, 0.46f, 0.3f), V(0.38f, 0.38f, 0.38f), glass);

        // Dort yonde sabit kanatcik: yatay ikisi kanat gorevi yapar, dikey ikisi kuyruk
        Part(root, PrimitiveType.Cube, "WingL", V(-1.2f, -0.1f, -1.2f), V(1.7f, 0.06f, 1.5f), accent, V(0, -18, 0));
        Part(root, PrimitiveType.Cube, "WingR", V(1.2f, -0.1f, -1.2f), V(1.7f, 0.06f, 1.5f), accent, V(0, 18, 0));
        Part(root, PrimitiveType.Cube, "Fin", V(0, 0.72f, -1.55f), V(0.07f, 0.95f, 0.85f), accent, V(18, 0, 0));
        Part(root, PrimitiveType.Cube, "TailPlane", V(0, -0.66f, -1.55f), V(0.07f, 0.75f, 0.8f), belly, V(-18, 0, 0));

        // Yan iticiler
        for (int s = -1; s <= 1; s += 2)
        {
            Part(root, PrimitiveType.Capsule, "Booster", V(0.85f * s, -0.05f, -0.4f), V(0.42f, 1.5f, 0.42f), belly, V(90, 0, 0));
            Part(root, PrimitiveType.Sphere, "BoosterCap", V(0.85f * s, -0.05f, 1.1f), V(0.4f, 0.4f, 0.8f), accent);
            Part(root, PrimitiveType.Sphere, "Flame", V(0.85f * s, -0.05f, -2.15f), V(0.3f, 0.3f, 1.0f), flame);
        }

        // Ana nozul ve alev
        Part(root, PrimitiveType.Cylinder, "Nozzle", V(0, 0, -2.0f), V(0.7f, 0.3f, 0.7f), dark, V(90, 0, 0));
        Part(root, PrimitiveType.Sphere, "Flame", V(0, 0, -2.9f), V(0.5f, 0.5f, 1.7f), flame);

        Part(root, PrimitiveType.Cube, "BellySkid", V(0, -0.58f, 0.3f), V(0.2f, 0.1f, 1.8f), dark);

        Finish(root, "Roket", 1.4f, -2.3f, 0f);
    }

    // ================================================================== yardimcilar

    private static Vector3 V(float x, float y, float z) { return new Vector3(x, y, z); }

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
        go.GetComponent<Renderer>().sharedMaterial = mat;
    }

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

    // Collider, iz efektleri ve prefab kaydi (SetupFlightTest.PreparePrefab ile ayni kurallar)
    private static void Finish(GameObject root, string prefabName, float trailX, float trailZ, float trailY)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            string n = t.name;
            if (n == "WingL" || n == "WingR" || n == "TailPlane" || n == "Fin" || n == "BellySkid")
                t.gameObject.AddComponent<BoxCollider>();
            else if (n == "Fuselage")
                t.gameObject.AddComponent<CapsuleCollider>();
            else if (n == "Wheel")
                t.gameObject.AddComponent<SphereCollider>().radius = 0.5f;
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

    // ================================================================== sahne

    // Acik sahnedeki FlightTestManager'a yeni ucaklari ekler (tekrar calistirilirsa eskileri degistirir)
    private static void AddToScene()
    {
        var mgr = Object.FindFirstObjectByType<FlightTestManager>();
        if (mgr == null || mgr.planes == null || mgr.planes.Length == 0)
        {
            Debug.LogWarning("FlightTestManager bulunamadi; prefab'lar uretildi ama sahneye eklenmedi (SampleScene'i ac).");
            return;
        }

        Transform parent = mgr.planes[0].transform.parent;

        // Ilk 3 ucagi koru, onceden eklenmis yeni ucaklari kaldir
        var keep = new List<PlaneController>();
        for (int i = 0; i < mgr.planes.Length; i++)
        {
            PlaneController p = mgr.planes[i];
            if (p == null) continue;
            if (i < 3) keep.Add(p);
            else Object.DestroyImmediate(p.gameObject);
        }
        foreach (var go in new List<GameObject>(CollectByName(parent, Names)))
            Object.DestroyImmediate(go);

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

    private static IEnumerable<GameObject> CollectByName(Transform parent, string[] names)
    {
        foreach (Transform child in parent)
        {
            string n = child.name.Replace("(Clone)", "").Trim();
            if (System.Array.IndexOf(names, n) >= 0) yield return child.gameObject;
        }
    }
}
