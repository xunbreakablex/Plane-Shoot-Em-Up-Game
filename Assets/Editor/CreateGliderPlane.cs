using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class CreateGliderPlane
{
    private const string MaterialFolder = "Assets/Materials";
    private const string PrefabFolder = "Assets/Prefabs";

    [MenuItem("Tools/Create Glider Plane")]
    public static void Create()
    {
        EnsureFolder("Assets", "Materials");
        EnsureFolder("Assets", "Prefabs");

        Material white = GetMaterial("Plane_Body", new Color(0.92f, 0.92f, 0.92f));
        Material red = GetMaterial("Plane_Red", new Color(0.85f, 0.15f, 0.12f));
        Material glass = GetMaterial("Plane_Glass", new Color(0.35f, 0.65f, 0.95f));
        Material dark = GetMaterial("Plane_Dark", new Color(0.12f, 0.12f, 0.12f));

        // Forward = +Z
        var root = new GameObject("GliderPlane");

        // Govde (kapsul varsayilan olarak Y ekseninde, X'te 90 derece cevirip Z'ye yatiriyoruz)
        Part(root, PrimitiveType.Capsule, "Fuselage", new Vector3(0, 0, 0),
            new Vector3(0.5f, 1.5f, 0.5f), white, new Vector3(90, 0, 0));

        // Ana kanat (uzun, ince - planor)
        Part(root, PrimitiveType.Cube, "Wing", new Vector3(0, 0.1f, 0.2f),
            new Vector3(7f, 0.07f, 0.9f), white);

        // Kanat ucu seritleri
        Part(root, PrimitiveType.Cube, "WingTipL", new Vector3(-3.4f, 0.1f, 0.2f),
            new Vector3(0.3f, 0.08f, 0.92f), red);
        Part(root, PrimitiveType.Cube, "WingTipR", new Vector3(3.4f, 0.1f, 0.2f),
            new Vector3(0.3f, 0.08f, 0.92f), red);

        // Kuyruk: yatay stabilizer + dikey kuyruk
        Part(root, PrimitiveType.Cube, "TailPlane", new Vector3(0, 0.05f, -1.3f),
            new Vector3(2f, 0.05f, 0.5f), white);
        Part(root, PrimitiveType.Cube, "Fin", new Vector3(0, 0.4f, -1.3f),
            new Vector3(0.06f, 0.75f, 0.55f), red);

        // Kokpit camı
        Part(root, PrimitiveType.Sphere, "Canopy", new Vector3(0, 0.22f, 0.35f),
            new Vector3(0.38f, 0.3f, 0.7f), glass);

        // Burun (pervane govdesi)
        Part(root, PrimitiveType.Sphere, "Nose", new Vector3(0, 0, 1.45f),
            new Vector3(0.3f, 0.3f, 0.3f), dark);

        // Pervane: Propeller script'li pivot + 2 kanat
        var prop = new GameObject("Propeller");
        prop.transform.SetParent(root.transform, false);
        prop.transform.localPosition = new Vector3(0, 0, 1.62f);
        prop.AddComponent<Propeller>();
        Part(prop, PrimitiveType.Cube, "BladeA", Vector3.zero, new Vector3(1.4f, 0.12f, 0.03f), dark);
        Part(prop, PrimitiveType.Cube, "BladeB", Vector3.zero, new Vector3(0.12f, 1.4f, 0.03f), dark);

        // Tek collider: kolay carpisma icin
        var col = root.AddComponent<BoxCollider>();
        col.center = new Vector3(0, 0.1f, 0);
        col.size = new Vector3(7f, 0.6f, 3.4f);

        string path = PrefabFolder + "/GliderPlane.prefab";
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);

        // Sahneye bir ornek yerlestir
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        Undo.RegisterCreatedObjectUndo(instance, "Create Glider Plane");
        Selection.activeGameObject = instance;

        Debug.Log("GliderPlane prefab olusturuldu: " + path);
    }

    [MenuItem("Tools/Create WW2 Fighter")]
    public static void CreateWW2Fighter()
    {
        EnsureFolder("Assets", "Materials");
        EnsureFolder("Assets", "Prefabs");

        Material olive = GetMaterial("WW2_Olive", new Color(0.30f, 0.36f, 0.20f));
        Material belly = GetMaterial("WW2_Belly", new Color(0.62f, 0.68f, 0.72f));
        Material metal = GetMaterial("WW2_Metal", new Color(0.22f, 0.22f, 0.24f));
        Material red = GetMaterial("WW2_Red", new Color(0.80f, 0.12f, 0.10f));
        Material white = GetMaterial("WW2_White", new Color(0.95f, 0.95f, 0.95f));
        Material glass = GetMaterial("Plane_Glass", new Color(0.35f, 0.65f, 0.95f));
        Material black = GetMaterial("WW2_Black", new Color(0.06f, 0.06f, 0.06f));

        // Forward = +Z
        var root = new GameObject("WW2Fighter");

        // Govde + arka konik kisim
        Part(root, PrimitiveType.Capsule, "Fuselage", new Vector3(0, 0, 0),
            new Vector3(0.7f, 1.6f, 0.7f), olive, new Vector3(90, 0, 0));
        Part(root, PrimitiveType.Capsule, "Tail Boom", new Vector3(0, 0.05f, -2.1f),
            new Vector3(0.38f, 0.9f, 0.38f), olive, new Vector3(90, 0, 0));
        Part(root, PrimitiveType.Capsule, "Belly", new Vector3(0, -0.2f, 0.1f),
            new Vector3(0.55f, 1.2f, 0.4f), belly, new Vector3(90, 0, 0));

        // Motor kaputu ve pervane kovani
        Part(root, PrimitiveType.Cylinder, "Cowling", new Vector3(0, 0, 1.65f),
            new Vector3(0.76f, 0.3f, 0.76f), metal, new Vector3(90, 0, 0));
        Part(root, PrimitiveType.Sphere, "Spinner", new Vector3(0, 0, 2.0f),
            new Vector3(0.32f, 0.32f, 0.5f), red);

        // Kanatlar (hafif V / dihedral)
        Part(root, PrimitiveType.Cube, "WingL", new Vector3(-2.0f, -0.15f, 0.1f),
            new Vector3(4f, 0.1f, 1.5f), olive, new Vector3(0, 0, -4));
        Part(root, PrimitiveType.Cube, "WingR", new Vector3(2.0f, -0.15f, 0.1f),
            new Vector3(4f, 0.1f, 1.5f), olive, new Vector3(0, 0, 4));

        // Kanat ustu yuvarlak isaretler
        foreach (float side in new[] { -1f, 1f })
        {
            var tilt = new Vector3(0, 0, 4f * side);
            Part(root, PrimitiveType.Cylinder, "RoundelOuter", new Vector3(2.7f * side, -0.07f, 0.1f),
                new Vector3(0.7f, 0.01f, 0.7f), white, tilt);
            Part(root, PrimitiveType.Cylinder, "RoundelInner", new Vector3(2.7f * side, -0.065f, 0.1f),
                new Vector3(0.38f, 0.012f, 0.38f), red, tilt);
        }

        // Kuyruk
        Part(root, PrimitiveType.Cube, "TailPlane", new Vector3(0, 0.1f, -2.65f),
            new Vector3(2.6f, 0.06f, 0.75f), olive);
        Part(root, PrimitiveType.Cube, "Fin", new Vector3(0, 0.55f, -2.6f),
            new Vector3(0.07f, 0.95f, 0.85f), olive);
        Part(root, PrimitiveType.Cube, "Rudder", new Vector3(0, 0.55f, -3.05f),
            new Vector3(0.075f, 0.8f, 0.2f), red);

        // Balon kokpit
        Part(root, PrimitiveType.Sphere, "Canopy", new Vector3(0, 0.38f, -0.1f),
            new Vector3(0.5f, 0.45f, 1.2f), glass);

        // Sabit iniş takimi
        foreach (float side in new[] { -1f, 1f })
        {
            Part(root, PrimitiveType.Cube, "GearStrut", new Vector3(0.8f * side, -0.45f, 0.5f),
                new Vector3(0.07f, 0.6f, 0.07f), metal);
            Part(root, PrimitiveType.Cylinder, "Wheel", new Vector3(0.8f * side, -0.75f, 0.5f),
                new Vector3(0.42f, 0.06f, 0.42f), black, new Vector3(0, 0, 90));
        }
        Part(root, PrimitiveType.Cylinder, "TailWheel", new Vector3(0, -0.2f, -2.85f),
            new Vector3(0.18f, 0.03f, 0.18f), black, new Vector3(0, 0, 90));

        // Pervane: 3 kanatli, Propeller script'li pivot
        var prop = new GameObject("Propeller");
        prop.transform.SetParent(root.transform, false);
        prop.transform.localPosition = new Vector3(0, 0, 2.1f);
        prop.AddComponent<Propeller>();
        for (int i = 0; i < 3; i++)
        {
            var arm = new GameObject("Blade" + i);
            arm.transform.SetParent(prop.transform, false);
            arm.transform.localEulerAngles = new Vector3(0, 0, i * 120f);
            Part(arm, PrimitiveType.Cube, "Blade", new Vector3(0, 0.55f, 0),
                new Vector3(0.14f, 1.0f, 0.03f), black);
        }

        var col = root.AddComponent<BoxCollider>();
        col.center = new Vector3(0, -0.1f, -0.4f);
        col.size = new Vector3(8f, 1.5f, 6f);

        string path = PrefabFolder + "/WW2Fighter.prefab";
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.transform.position = new Vector3(0, 0, 12f);
        Undo.RegisterCreatedObjectUndo(instance, "Create WW2 Fighter");
        Selection.activeGameObject = instance;

        Debug.Log("WW2Fighter prefab olusturuldu: " + path);
    }

    [MenuItem("Tools/Create WW1 Biplane")]
    public static void CreateWW1Biplane()
    {
        EnsureFolder("Assets", "Materials");
        EnsureFolder("Assets", "Prefabs");

        Material body = GetMaterial("WW1_Body", new Color(0.20f, 0.25f, 0.14f));
        Material fabric = GetMaterial("WW1_Fabric", new Color(0.50f, 0.50f, 0.32f));
        Material wood = GetMaterial("WW1_Wood", new Color(0.55f, 0.36f, 0.18f));
        Material silver = GetMaterial("WW1_Silver", new Color(0.72f, 0.74f, 0.78f));
        Material black = GetMaterial("WW2_Black", new Color(0.06f, 0.06f, 0.06f));
        Material glass = GetMaterial("Plane_Glass", new Color(0.35f, 0.65f, 0.95f));
        Material blue = GetMaterial("WW1_Blue", new Color(0.08f, 0.18f, 0.60f));
        Material white = GetMaterial("WW2_White", new Color(0.95f, 0.95f, 0.95f));
        Material red = GetMaterial("WW2_Red", new Color(0.80f, 0.12f, 0.10f));
        Material skin = GetMaterial("WW1_Skin", new Color(0.85f, 0.65f, 0.50f));

        // Forward = +Z
        var root = new GameObject("WW1Biplane");

        // Govde ve kuyruk kismi
        Part(root, PrimitiveType.Capsule, "Fuselage", new Vector3(0, 0, -0.2f),
            new Vector3(0.6f, 1.4f, 0.6f), body, new Vector3(90, 0, 0));
        Part(root, PrimitiveType.Capsule, "TailBoom", new Vector3(0, 0.05f, -2.0f),
            new Vector3(0.3f, 0.9f, 0.3f), body, new Vector3(90, 0, 0));

        // Yuvarlak motor kaputu
        Part(root, PrimitiveType.Cylinder, "Cowling", new Vector3(0, 0, 1.35f),
            new Vector3(0.72f, 0.25f, 0.72f), silver, new Vector3(90, 0, 0));

        // Ust ve alt kanat
        Part(root, PrimitiveType.Cube, "UpperWing", new Vector3(0, 1.0f, 0.3f),
            new Vector3(7f, 0.06f, 1.2f), fabric);
        Part(root, PrimitiveType.Cube, "LowerWing", new Vector3(0, -0.35f, 0.3f),
            new Vector3(6.4f, 0.06f, 1.1f), fabric);

        // Kanatlar arasi ahsap payandalar
        foreach (float side in new[] { -1f, 1f })
        {
            Part(root, PrimitiveType.Cube, "InnerStrut", new Vector3(1.3f * side, 0.325f, 0.3f),
                new Vector3(0.06f, 1.4f, 0.06f), wood);
            Part(root, PrimitiveType.Cube, "OuterStrut", new Vector3(2.9f * side, 0.325f, 0.3f),
                new Vector3(0.06f, 1.4f, 0.06f), wood);
            // Govde ustunden ust kanada kabin payandalari
            Part(root, PrimitiveType.Cube, "CabaneStrut", new Vector3(0.3f * side, 0.65f, 0.4f),
                new Vector3(0.05f, 0.75f, 0.05f), wood);
        }

        // Kokartlar: ust kanatta (x2) ve govdede (x2)
        foreach (float side in new[] { -1f, 1f })
        {
            Roundel(root, new Vector3(2.3f * side, 1.04f, 0.3f), Vector3.zero, 0.8f, blue, white, red);
            Roundel(root, new Vector3(0.31f * side, 0f, -0.9f), new Vector3(0, 0, -90f * side), 0.5f, blue, white, red);
        }

        // Kuyruk
        Part(root, PrimitiveType.Cube, "TailPlane", new Vector3(0, 0.15f, -2.5f),
            new Vector3(2.4f, 0.05f, 0.7f), fabric);
        Part(root, PrimitiveType.Cube, "Fin", new Vector3(0, 0.55f, -2.4f),
            new Vector3(0.06f, 0.8f, 0.45f), body);
        Part(root, PrimitiveType.Cube, "RudderBlue", new Vector3(0, 0.55f, -2.68f),
            new Vector3(0.065f, 0.8f, 0.11f), blue);
        Part(root, PrimitiveType.Cube, "RudderWhite", new Vector3(0, 0.55f, -2.79f),
            new Vector3(0.065f, 0.8f, 0.11f), white);
        Part(root, PrimitiveType.Cube, "RudderRed", new Vector3(0, 0.55f, -2.9f),
            new Vector3(0.065f, 0.8f, 0.11f), red);
        Part(root, PrimitiveType.Cube, "TailSkid", new Vector3(0, -0.15f, -2.95f),
            new Vector3(0.05f, 0.05f, 0.35f), wood, new Vector3(-20, 0, 0));

        // Acik kokpit, cam ruzgarlik, pilot, makineli tufek
        Part(root, PrimitiveType.Cube, "Cockpit", new Vector3(0, 0.3f, 0f),
            new Vector3(0.38f, 0.08f, 0.6f), black);
        Part(root, PrimitiveType.Sphere, "PilotHead", new Vector3(0, 0.5f, -0.1f),
            new Vector3(0.22f, 0.22f, 0.22f), skin);
        Part(root, PrimitiveType.Cube, "Windscreen", new Vector3(0, 0.45f, 0.3f),
            new Vector3(0.4f, 0.2f, 0.03f), glass, new Vector3(-25, 0, 0));
        Part(root, PrimitiveType.Cylinder, "MachineGun", new Vector3(0, 0.38f, 0.75f),
            new Vector3(0.05f, 0.4f, 0.05f), black, new Vector3(90, 0, 0));

        // V iniş takimi, aks ve tekerlekler
        foreach (float side in new[] { -1f, 1f })
        {
            float angle = 25f * side; // ustu govdeye dogru
            Part(root, PrimitiveType.Cube, "GearStrutFront", new Vector3(0.4f * side, -0.6f, 0.7f),
                new Vector3(0.06f, 0.9f, 0.06f), wood, new Vector3(0, 0, angle));
            Part(root, PrimitiveType.Cube, "GearStrutRear", new Vector3(0.4f * side, -0.6f, 1.1f),
                new Vector3(0.06f, 0.9f, 0.06f), wood, new Vector3(0, 0, angle));
            Part(root, PrimitiveType.Cylinder, "Wheel", new Vector3(0.75f * side, -1.05f, 0.9f),
                new Vector3(0.55f, 0.07f, 0.55f), black, new Vector3(0, 0, 90));
        }
        Part(root, PrimitiveType.Cube, "Axle", new Vector3(0, -1.05f, 0.9f),
            new Vector3(1.5f, 0.04f, 0.04f), wood);

        // Pervane: ahsap, 2 kanatli
        var prop = new GameObject("Propeller");
        prop.transform.SetParent(root.transform, false);
        prop.transform.localPosition = new Vector3(0, 0, 1.65f);
        prop.AddComponent<Propeller>();
        Part(prop, PrimitiveType.Cube, "Blade", Vector3.zero,
            new Vector3(0.14f, 1.7f, 0.04f), wood);
        Part(prop, PrimitiveType.Sphere, "Hub", Vector3.zero,
            new Vector3(0.2f, 0.2f, 0.25f), silver);

        var col = root.AddComponent<BoxCollider>();
        col.center = new Vector3(0, 0.1f, -0.7f);
        col.size = new Vector3(7f, 2.4f, 5.6f);

        string path = PrefabFolder + "/WW1Biplane.prefab";
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.transform.position = new Vector3(14f, 0, 12f);
        Undo.RegisterCreatedObjectUndo(instance, "Create WW1 Biplane");
        Selection.activeGameObject = instance;

        Debug.Log("WW1Biplane prefab olusturuldu: " + path);
    }

    // Ic ice 3 disk: dis, orta, merkez
    private static void Roundel(GameObject parent, Vector3 pos, Vector3 euler, float size,
        Material outer, Material mid, Material center)
    {
        Part(parent, PrimitiveType.Cylinder, "RoundelOuter", pos,
            new Vector3(size, 0.01f, size), outer, euler);
        Part(parent, PrimitiveType.Cylinder, "RoundelMid", pos + RotatedUp(euler) * 0.003f,
            new Vector3(size * 0.68f, 0.01f, size * 0.68f), mid, euler);
        Part(parent, PrimitiveType.Cylinder, "RoundelCenter", pos + RotatedUp(euler) * 0.006f,
            new Vector3(size * 0.34f, 0.01f, size * 0.34f), center, euler);
    }

    private static Vector3 RotatedUp(Vector3 euler)
    {
        return Quaternion.Euler(euler) * Vector3.up;
    }

    private static GameObject Part(GameObject parent, PrimitiveType type, string name,
        Vector3 pos, Vector3 scale, Material mat, Vector3? euler = null)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent.transform, false);
        go.transform.localPosition = pos;
        go.transform.localEulerAngles = euler ?? Vector3.zero;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        return go;
    }

    internal static Material GetMaterial(string name, Color color)
    {
        string path = $"{MaterialFolder}/{name}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat != null) return mat;

        bool urp = GraphicsSettings.currentRenderPipeline != null;
        Shader shader = Shader.Find(urp ? "Universal Render Pipeline/Lit" : "Standard");
        mat = new Material(shader) { color = color };
        if (urp) mat.SetColor("_BaseColor", color);
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    internal static void EnsureFolder(string parent, string child)
    {
        if (!AssetDatabase.IsValidFolder($"{parent}/{child}"))
            AssetDatabase.CreateFolder(parent, child);
    }
}
