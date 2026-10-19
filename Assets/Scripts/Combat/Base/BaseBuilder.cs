using System.Collections.Generic;
using UnityEngine;

// Dusman ussunu Unity primitive'lerinden kodla uretir (model dosyasi gerekmez): zemin (kara pedi ya da ada),
// pist + parktaki ucaklar, hangarlar, yakit depolari, radar, komuta merkezi, uc aksavarlar ve toplar.
// Yerlesim yerel koordinattadir: +Z = ussun oyuncuya bakan ucu (oyuncu -Z tarafindan yaklasir), +X sag.
// Yeni yapi eklerken: bir fonksiyon yaz, Build icinden cagir; yer birimleri NewUnit ile kaydolur.
public static class BaseBuilder
{
    public sealed class Result
    {
        public GameObject root;
        public readonly List<EnemyPlane> units = new List<EnemyPlane>();
        public EnemyPlane core;
    }

    private static Material lit, glow;
    private static readonly Dictionary<long, Material> cache = new Dictionary<long, Material>();

    private static CombatManager mgr;
    private static Result res;
    private static int stageIdx;
    private static float hpMul;

    // ------------------------------------------------------------ yerlesim tablolari (yerel x, z)

    private static readonly Vector2[] FlakSpots =
    {
        new Vector2(-210f, -190f), new Vector2(210f, -190f), new Vector2(0f, -250f), new Vector2(-250f, 40f),
        new Vector2(250f, 40f), new Vector2(-170f, 240f), new Vector2(170f, 240f), new Vector2(0f, 275f),
        new Vector2(-120f, -120f), new Vector2(120f, -120f), new Vector2(-260f, 160f), new Vector2(260f, 160f),
    };

    private static readonly Vector2[] CannonSpots =
    {
        new Vector2(-70f, -170f), new Vector2(70f, -170f), new Vector2(0f, 150f), new Vector2(-190f, -60f),
    };

    private static readonly Vector2[] HangarSpots =
    {
        new Vector2(150f, -20f), new Vector2(150f, 70f), new Vector2(150f, 160f), new Vector2(150f, -110f),
    };

    private static readonly Vector2[] TankSpots =
    {
        new Vector2(-100f, 185f), new Vector2(-55f, 205f), new Vector2(-10f, 215f), new Vector2(35f, 205f),
    };

    // Parktaki ucaklar: pistin iki yaninda
    private static readonly Vector3[] PlaneSpots =   // x, z, yaw
    {
        new Vector3(-163f, -70f, 0f), new Vector3(-227f, -10f, 180f), new Vector3(-163f, 50f, 0f),
        new Vector3(-227f, 110f, 180f), new Vector3(-163f, 170f, 0f), new Vector3(-227f, 230f, 180f),
    };

    // ------------------------------------------------------------------ giris

    /// <summary>Ussu kurar. center: dunya konumu (y = zemin yuzeyi); approach: oyuncunun geldigi yon (yatay).</summary>
    public static Result Build(CombatManager manager, BaseDef def, int stage, Vector3 center, Vector3 approach,
        GameObject[] planeModels, Material litBase, Material glowBase)
    {
        mgr = manager;
        lit = litBase;
        glow = glowBase != null ? glowBase : litBase;
        cache.Clear();
        stageIdx = stage;
        hpMul = GameSettings.Current.baseHpMultiplier;
        res = new Result();

        var rootGo = new GameObject("EnemyBase_" + def.name);
        Transform root = rootGo.transform;
        root.position = center;
        approach.y = 0f;
        if (approach.sqrMagnitude < 0.01f) approach = Vector3.forward;
        root.rotation = Quaternion.LookRotation(approach.normalized, Vector3.up);
        res.root = rootGo;

        if (def.island) BuildIsland(root, def);
        else BuildPad(root, def);

        BuildRunway(root);

        // Parktaki ucaklar
        for (int i = 0; i < def.planes && i < PlaneSpots.Length; i++)
            ParkedPlane(root, planeModels, PlaneSpots[i], i);

        for (int i = 0; i < def.hangars && i < HangarSpots.Length; i++)
            Hangar(root, HangarSpots[i], i);

        for (int i = 0; i < def.tanks && i < TankSpots.Length; i++)
            FuelTank(root, TankSpots[i], i);

        Radar(root, new Vector2(-70f, -40f));
        Core(root, new Vector2(0f, 60f));

        for (int i = 0; i < def.cannons && i < CannonSpots.Length; i++)
            Cannon(root, CannonSpots[i]);

        for (int i = 0; i < def.flak && i < FlakSpots.Length; i++)
            Flak(root, FlakSpots[i]);

        return res;
    }

    /// <summary>
    /// Savas gemisi (deniz gecisi): govde + kopru + baca + ileri top kulesi + iki uçaksavar. Yerel +Z = pruva.
    /// pos: gemi kokunun dunya konumu (su cizgisi), heading: yatay yol yonu. units[0] gemi govdesidir (hedef).
    /// </summary>
    public static Result BuildShip(CombatManager manager, Material litBase, Material glowBase, Vector3 pos, Vector3 heading, int stage)
    {
        mgr = manager;
        lit = litBase;
        glow = glowBase != null ? glowBase : litBase;
        cache.Clear();
        stageIdx = stage;
        hpMul = GameSettings.Current.baseHpMultiplier;
        res = new Result();

        heading.y = 0f;
        if (heading.sqrMagnitude < 0.01f) heading = Vector3.forward;
        var rootGo = new GameObject("Warship");
        Transform root = rootGo.transform;
        root.SetPositionAndRotation(pos, Quaternion.LookRotation(heading.normalized, Vector3.up));
        res.root = rootGo;

        // Kok y = su cizgisi: govde 3 m suyun altinda, 5 m ustunde
        EnemyPlane hull = NewUnit(root, "Hull", V(0f, 1f, 0f), 0f, EnemyKind.GroundBuilding, "Savaş Gemisi", Hits(30), 30f, (int)PickupKind.Missile);
        Transform t = hull.transform;
        Color steel = C(0.36f, 0.4f, 0.45f), dark = C(0.15f, 0.17f, 0.2f), red = C(0.6f, 0.1f, 0.08f), pale = C(0.62f, 0.66f, 0.7f);

        Part(t, PrimitiveType.Cube, "Hull", V(0f, 0f, 0f), V(16f, 8f, 86f), steel);
        Part(t, PrimitiveType.Cube, "Bow", V(0f, 0f, 47f), V(10f, 8f, 12f), steel);
        Part(t, PrimitiveType.Cube, "BowTip", V(0f, 0.5f, 55f), V(4f, 7f, 6f), steel);
        Part(t, PrimitiveType.Cube, "Deck", V(0f, 4.2f, 0f), V(14f, 0.6f, 80f), dark);
        Part(t, PrimitiveType.Cube, "Stripe", V(0f, -1.5f, 0f), V(16.2f, 1.2f, 86.2f), red, default, false, false);
        Part(t, PrimitiveType.Cube, "Bridge", V(0f, 10f, -10f), V(9f, 11f, 16f), pale);
        Part(t, PrimitiveType.Cylinder, "Funnel", V(0f, 18f, -20f), V(4.5f, 4f, 4.5f), red);
        Part(t, PrimitiveType.Cylinder, "Mast", V(0f, 22f, -8f), V(0.8f, 6f, 0.8f), dark);

        // Ileri top kulesi (agir top davranisi)
        Transform head = Pivot(t, "Head", V(0f, 6.2f, 24f));
        Part(head, PrimitiveType.Cube, "Body", V(0f, 0.8f, 0f), V(7f, 3.4f, 8f), steel);
        Part(head, PrimitiveType.Cylinder, "BarrelL", V(-1.4f, 1f, 8f), V(1.1f, 4.5f, 1.1f), dark, V(90f, 0f, 0f));
        Part(head, PrimitiveType.Cylinder, "BarrelR", V(1.4f, 1f, 8f), V(1.1f, 4.5f, 1.1f), dark, V(90f, 0f, 0f));
        Transform muzzle = Pivot(head, "Muzzle", V(0f, 1f, 13.2f));
        hull.gameObject.AddComponent<GroundUnit>().Setup(GroundUnit.Role.Cannon, head, muzzle);
        FinishUnit(hull);

        // Iki uçaksavar: kopru onunde ve kiciga yakin, guverte ustunde (kok yerel y: guverte ~ 1 + 4.5)
        Flak(root, new Vector2(0f, -32f), 5.5f);
        Flak(root, new Vector2(0f, 8f), 5.5f);
        return res;
    }

    // ------------------------------------------------------------ yardimcilar

    private static Color C(float r, float g, float b) { return new Color(r, g, b, 1f); }
    private static Vector3 V(float x, float y, float z) { return new Vector3(x, y, z); }

    private static Material MatFor(Color c, bool emissive)
    {
        Color32 c32 = c;
        long key = ((long)c32.r << 24) | ((long)c32.g << 16) | ((long)c32.b << 8) | c32.a;
        if (emissive) key |= 1L << 40;

        if (cache.TryGetValue(key, out Material m) && m != null) return m;

        Material src = emissive ? glow : lit;
        m = new Material(src) { color = c };
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        cache[key] = m;
        return m;
    }

    private static Transform Part(Transform parent, PrimitiveType prim, string name, Vector3 pos, Vector3 scale,
        Color color, Vector3 euler = default, bool emissive = false, bool collider = true)
    {
        GameObject go = GameObject.CreatePrimitive(prim);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localEulerAngles = euler;
        go.transform.localScale = scale;

        var r = go.GetComponent<Renderer>();
        r.sharedMaterial = MatFor(color, emissive);
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        if (!collider) Object.Destroy(go.GetComponent<Collider>());
        return go.transform;
    }

    private static Transform Pivot(Transform parent, string name, Vector3 pos, Vector3 euler = default)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localEulerAngles = euler;
        return go.transform;
    }

    private static int Hits(int baseHits)
    {
        return Mathf.Max(1, Mathf.RoundToInt(baseHits * (1f + 0.55f * stageIdx) * hpMul));
    }

    private static void SetLayer(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform c in go.transform) SetLayer(c.gameObject, layer);
    }

    // Yer birimi kokunu olusturur; parcalar eklendikten sonra FinishUnit katmani ayarlar
    private static EnemyPlane NewUnit(Transform parent, string name, Vector3 localPos, float yaw, EnemyKind kind,
        string title, int hits, float radius, int drop)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localEulerAngles = new Vector3(0f, yaw, 0f);

        var rb = go.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        var e = go.AddComponent<EnemyPlane>();
        e.InitGround(mgr, hits, kind, title, radius, drop);
        res.units.Add(e);
        return e;
    }

    // Katman: dusman katmani (oyuncu ucagi icinden gecer, mermi/nisan vurur)
    private static void FinishUnit(EnemyPlane e)
    {
        SetLayer(e.gameObject, CombatManager.EnemyLayer);
    }

    // ------------------------------------------------------------------ zemin

    private static void BuildPad(Transform root, BaseDef def)
    {
        float r = def.radius;
        // Beton pedi: ust yuzey y = 0 (kok konumu zeminin en yuksek noktasina ayarlanir)
        Part(root, PrimitiveType.Cube, "Pad", V(0f, -2f, 0f), V(r * 2f + 120f, 4f, r * 2f), C(0.36f, 0.37f, 0.34f));
        Part(root, PrimitiveType.Cube, "PadRim", V(0f, -1.6f, 0f), V(r * 2f + 150f, 3f, r * 2f + 40f), C(0.27f, 0.29f, 0.24f));
    }

    private static void BuildIsland(Transform root, BaseDef def)
    {
        float r = def.radius;
        // Kumsal (genis, alcak) + ada (cim / kaya). Cylinder primitive: capi 1, yuksekligi 2 (olcek.y = yukseklik / 2)
        Color grass = def.ground;
        Color sand = C(0.78f, 0.7f, 0.5f);

        Transform beach = Part(root, PrimitiveType.Cylinder, "Beach", V(0f, -10.5f, 0f), V((r + 120f) * 2f, 7f, (r + 120f) * 2f), sand);
        Transform top = Part(root, PrimitiveType.Cylinder, "Island", V(0f, -5f, 0f), V(r * 2f, 5f, r * 2f), grass);

        // Silindir primitive'in kapsul collider'i yerine mesh collider
        foreach (var t in new[] { beach, top })
        {
            Object.Destroy(t.GetComponent<Collider>());
            t.gameObject.AddComponent<MeshCollider>();
        }
    }

    private static void BuildRunway(Transform root)
    {
        Color concrete = C(0.22f, 0.22f, 0.24f);
        Part(root, PrimitiveType.Cube, "Runway", V(-195f, 0.2f, 40f), V(38f, 0.5f, 380f), concrete);
        for (int i = 0; i < 9; i++)
            Part(root, PrimitiveType.Cube, "RunwayStripe", V(-195f, 0.55f, -130f + i * 40f), V(1.2f, 0.1f, 16f),
                C(0.9f, 0.9f, 0.85f), default, false, false);
    }

    // --------------------------------------------------------------- yapilar

    private static void Flak(Transform root, Vector2 p, float y = 0f)
    {
        EnemyPlane e = NewUnit(root, "Flak", V(p.x, y, p.y), 0f, EnemyKind.GroundTurret, "Uçaksavar", Hits(3), 5f, -1);
        Transform t = e.transform;
        Color olive = C(0.27f, 0.31f, 0.22f), dark = C(0.12f, 0.13f, 0.14f), metal = C(0.5f, 0.52f, 0.55f);

        Part(t, PrimitiveType.Cylinder, "Base", V(0f, 0.8f, 0f), V(7f, 0.8f, 7f), dark);
        Part(t, PrimitiveType.Cylinder, "Pedestal", V(0f, 2.4f, 0f), V(3.4f, 0.8f, 3.4f), olive);
        Transform head = Pivot(t, "Head", V(0f, 4f, 0f));
        Part(head, PrimitiveType.Cube, "Body", V(0f, 0f, 0f), V(3.8f, 2.2f, 4.2f), olive);
        Part(head, PrimitiveType.Cylinder, "BarrelL", V(-0.85f, 0.2f, 4.2f), V(0.5f, 3.2f, 0.5f), metal, V(90f, 0f, 0f));
        Part(head, PrimitiveType.Cylinder, "BarrelR", V(0.85f, 0.2f, 4.2f), V(0.5f, 3.2f, 0.5f), metal, V(90f, 0f, 0f));
        Transform muzzle = Pivot(head, "Muzzle", V(0f, 0.2f, 7.8f));

        e.gameObject.AddComponent<GroundUnit>().Setup(GroundUnit.Role.Flak, head, muzzle);
        FinishUnit(e);
    }

    private static void Cannon(Transform root, Vector2 p)
    {
        EnemyPlane e = NewUnit(root, "Cannon", V(p.x, 0f, p.y), 0f, EnemyKind.GroundTurret, "Ağır Top", Hits(9), 8f, -1);
        Transform t = e.transform;
        Color steel = C(0.33f, 0.35f, 0.38f), dark = C(0.1f, 0.1f, 0.12f), red = C(0.65f, 0.12f, 0.1f);

        Part(t, PrimitiveType.Cylinder, "Base", V(0f, 1.2f, 0f), V(13f, 1.2f, 13f), dark);
        Part(t, PrimitiveType.Sphere, "Dome", V(0f, 3.2f, 0f), V(9f, 6f, 9f), steel);
        Transform head = Pivot(t, "Head", V(0f, 5.6f, 0f));
        Part(head, PrimitiveType.Cube, "Body", V(0f, 0f, 0f), V(6.4f, 3.4f, 7f), steel);
        Part(head, PrimitiveType.Cylinder, "Barrel", V(0f, 0.2f, 7.2f), V(1.5f, 5.2f, 1.5f), dark, V(90f, 0f, 0f));
        Part(head, PrimitiveType.Cylinder, "Brake", V(0f, 0.2f, 12.1f), V(2.1f, 0.5f, 2.1f), red, V(90f, 0f, 0f));
        Transform muzzle = Pivot(head, "Muzzle", V(0f, 0.2f, 13.2f));

        e.gameObject.AddComponent<GroundUnit>().Setup(GroundUnit.Role.Cannon, head, muzzle);
        FinishUnit(e);
    }

    private static void Hangar(Transform root, Vector2 p, int index)
    {
        // Ilk hangar guc birakir
        int drop = index == 0 ? (int)PickupKind.Power : (index == 1 && stageIdx >= 1 ? (int)PickupKind.Shield : -1);
        EnemyPlane e = NewUnit(root, "Hangar", V(p.x, 0f, p.y), -90f, EnemyKind.GroundBuilding, "Hangar", Hits(12), 24f, drop);
        Transform t = e.transform;
        Color wall = C(0.5f, 0.5f, 0.46f), roof = C(0.3f, 0.34f, 0.3f), door = C(0.12f, 0.13f, 0.15f);

        Part(t, PrimitiveType.Cube, "Body", V(0f, 6.5f, 0f), V(30f, 13f, 44f), wall);
        Part(t, PrimitiveType.Cube, "Roof", V(0f, 14f, 0f), V(32f, 2.4f, 46f), roof);
        Part(t, PrimitiveType.Cube, "Door", V(0f, 5f, 22.2f), V(22f, 10f, 0.8f), door, default, false, false);

        e.gameObject.AddComponent<GroundUnit>().Setup(GroundUnit.Role.Building, null, null);
        FinishUnit(e);
    }

    private static void FuelTank(Transform root, Vector2 p, int index)
    {
        int drop = index == 0 ? (int)PickupKind.Repair : -1;
        EnemyPlane e = NewUnit(root, "FuelTank", V(p.x, 0f, p.y), 0f, EnemyKind.GroundBuilding, "Yakıt Deposu", Hits(6), 11f, drop);
        Transform t = e.transform;
        Color steel = C(0.62f, 0.64f, 0.66f), red = C(0.75f, 0.14f, 0.1f);

        Part(t, PrimitiveType.Cylinder, "Tank", V(0f, 5f, 0f), V(18f, 5f, 18f), steel);
        Part(t, PrimitiveType.Sphere, "Cap", V(0f, 10f, 0f), V(18f, 6f, 18f), steel);
        Part(t, PrimitiveType.Cylinder, "Band", V(0f, 4f, 0f), V(18.5f, 0.5f, 18.5f), red, default, false, false);

        e.gameObject.AddComponent<GroundUnit>().Setup(GroundUnit.Role.Tank, null, null);
        FinishUnit(e);
    }

    private static void Radar(Transform root, Vector2 p)
    {
        EnemyPlane e = NewUnit(root, "Radar", V(p.x, 0f, p.y), 0f, EnemyKind.GroundBuilding, "Radar", Hits(8), 9f, (int)PickupKind.Bomb);
        Transform t = e.transform;
        Color mast = C(0.55f, 0.57f, 0.6f), dish = C(0.85f, 0.86f, 0.88f), dark = C(0.14f, 0.15f, 0.17f);

        Part(t, PrimitiveType.Cylinder, "Foot", V(0f, 1.2f, 0f), V(8f, 1.2f, 8f), dark);
        Part(t, PrimitiveType.Cylinder, "Mast", V(0f, 8f, 0f), V(2.2f, 7f, 2.2f), mast);
        Transform head = Pivot(t, "Head", V(0f, 16f, 0f));
        Transform tilt = Pivot(head, "Tilt", V(0f, 0f, 0f), V(-28f, 0f, 0f));
        Part(tilt, PrimitiveType.Sphere, "Dish", V(0f, 0f, 0f), V(13f, 13f, 1.8f), dish);
        Part(tilt, PrimitiveType.Cylinder, "Feed", V(0f, 0f, 4.5f), V(0.5f, 2.2f, 0.5f), dark, V(90f, 0f, 0f), false, false);

        e.gameObject.AddComponent<GroundUnit>().Setup(GroundUnit.Role.Radar, head, null);
        FinishUnit(e);
    }

    private static void Core(Transform root, Vector2 p)
    {
        EnemyPlane e = NewUnit(root, "CommandCenter", V(p.x, 0f, p.y), 0f, EnemyKind.GroundCore, "Komuta Merkezi", Hits(110), 28f, -1);
        Transform t = e.transform;
        Color wall = C(0.52f, 0.48f, 0.42f), upper = C(0.4f, 0.37f, 0.34f), dark = C(0.15f, 0.15f, 0.17f);
        Color red = C(1f, 0.2f, 0.15f), glass = C(0.25f, 0.55f, 0.8f);

        Part(t, PrimitiveType.Cube, "Block", V(0f, 8f, 0f), V(40f, 16f, 40f), wall);
        Part(t, PrimitiveType.Cube, "Upper", V(0f, 21f, 0f), V(26f, 10f, 26f), upper);
        Part(t, PrimitiveType.Cube, "Windows", V(0f, 21.5f, -13.1f), V(20f, 3.4f, 0.6f), glass, default, true, false);
        Part(t, PrimitiveType.Cube, "Tower", V(15f, 17f, 15f), V(6f, 34f, 6f), dark);
        Part(t, PrimitiveType.Cylinder, "Antenna", V(-8f, 31f, 6f), V(0.7f, 6f, 0.7f), dark);
        Transform beacon = Part(t, PrimitiveType.Sphere, "Beacon", V(15f, 36.5f, 15f), V(4.5f, 4.5f, 4.5f), red, default, true, false);

        e.gameObject.AddComponent<GroundUnit>().Setup(GroundUnit.Role.Core, null, null, beacon);
        FinishUnit(e);
        res.core = e;
    }

    private static void ParkedPlane(Transform root, GameObject[] models, Vector3 spot, int index)
    {
        if (models == null || models.Length == 0) return;

        int m = Mathf.Min(index % 3, models.Length - 1);
        if (models[m] == null) return;

        int drop = index == 0 ? (int)PickupKind.Missile : -1;
        EnemyPlane e = NewUnit(root, "ParkedPlane", V(spot.x, 1.6f, spot.y), spot.z, EnemyKind.GroundBuilding, "Park Halindeki Uçak", Hits(3), 9f, drop);

        GameObject model = Object.Instantiate(models[m], e.transform);
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.identity;
        model.transform.localScale = Vector3.one * 1.3f;
        foreach (var trail in model.GetComponentsInChildren<TrailRenderer>()) trail.emitting = false;

        CombatManager.Tint(model, C(0.45f, 0.5f, 0.4f), false);
        e.gameObject.AddComponent<GroundUnit>().Setup(GroundUnit.Role.Building, null, null);
        FinishUnit(e);
    }
}
