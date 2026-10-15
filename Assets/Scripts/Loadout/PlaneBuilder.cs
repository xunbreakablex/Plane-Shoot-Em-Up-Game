using UnityEngine;

// Secilen donanima gore ucagin gorunumunu olusturur (hem hangar onizlemesinde hem oyunda).
// Not: kanat uzunlugu ve pervane degisimi taze kopya uzerinde bir kez uygulanmak uzere yazildi.
public static class PlaneBuilder
{
    public const string Prefix = "Cust_";

    // Modele gore sabitler: 0 = WW1 cift kanat, 1 = WW2 avci, 2 = planor, 3 = Atmaca, 4 = Kartal, 5 = Yavru Kopekbaligi
    private const int Models = 6;
    private static readonly float[] WingHalfSpan = { 3.2f, 4.0f, 3.5f, 2.7f, 3.3f, 2.5f };
    private static readonly float[] WingY = { -0.35f, -0.15f, 0.1f, -0.05f, 0f, -0.15f };
    private static readonly float[] PropRadius = { 0.85f, 1.05f, 0.7f, 0.5f, 0.5f, 0.5f };
    // Ic silah x'i: jetlerde hava girisi / govde disinda (govde yaricapi 0,4-0,5; giris 0,7'ye kadar)
    private static readonly float[] InnerGunX = { 1.1f, 1.1f, 1.1f, 0.95f, 0.85f, 0.75f };
    private static readonly Vector3[] DorsalMount =
    {
        new Vector3(0f, 0.42f, -0.95f), new Vector3(0f, 0.42f, -0.95f), new Vector3(0f, 0.32f, -0.7f),
        new Vector3(0f, 0.5f, -0.9f), new Vector3(0f, 0.52f, -1.0f), new Vector3(0f, 0.62f, -0.35f)
    };
    private static readonly Vector3[] VentralMount =
    {
        new Vector3(0f, -0.34f, 0.35f), new Vector3(0f, -0.4f, 0.35f), new Vector3(0f, -0.28f, 0.25f),
        new Vector3(0f, -0.3f, 0.3f), new Vector3(0f, -0.36f, 0.3f), new Vector3(0f, -0.4f, 0.2f)
    };

    // Namlu ucunun (mermi cikis noktasi) z konumu; her ucagin kanat/hava girisi on kenarinin onunde.
    private static readonly float[] InnerTipZ = { 1.7f, 1.7f, 1.7f, 2.2f, 2.3f, 1.9f };
    private static readonly float[] OuterTipZ = { 1.25f, 1.25f, 1.25f, 1.2f, 1.4f, 1.5f };
    private const float BarrelLength = 1f;
    private const float TurretTipZ = 0.8f;     // taret namlusunun ucu (kafa ekseninde)

    // Silah NAMLU UCU (mermi buradan cikar): 0,1 = govde yani (ic); 2,3 = kanat ortasi (dis)
    public static Vector3 WeaponOffset(int model, int slot, float span)
    {
        model = Mathf.Clamp(model, 0, Models - 1);
        bool left = slot % 2 == 0;
        float side = left ? -1f : 1f;
        bool outer = slot >= 2;
        float x = outer ? WingHalfSpan[model] * 0.6f * span : InnerGunX[model];
        float z = outer ? OuterTipZ[model] : InnerTipZ[model];
        return new Vector3(side * x, WingY[model] + (outer ? 0f : 0.05f), z);
    }

    // Fuze rampasi: kanat altinda, slot 0 sol / slot 1 sag
    public static Vector3 MissileOffset(int model, int slot, float span)
    {
        model = Mathf.Clamp(model, 0, Models - 1);
        float side = slot == 0 ? -1f : 1f;
        return new Vector3(side * WingHalfSpan[model] * 0.45f * span, WingY[model] - 0.28f, 0.45f);
    }

    public static Vector3 TurretOffset(int model, int slot)
    {
        model = Mathf.Clamp(model, 0, Models - 1);
        return slot == 0 ? DorsalMount[model] : VentralMount[model];
    }

    public static void Apply(GameObject root, int model, PlaneLoadout loadout, bool forGame)
    {
        LoadoutStats stats = LoadoutMath.Compute(loadout);

        Clear(root.transform);
        Paint(root, loadout.paint);
        Blades(root, model, stats.blades);
        Span(root, stats.span);
        BuildWeapons(root, model, loadout, stats.span, forGame);
        BuildMissiles(root, model, loadout, stats.span);
        BuildTurrets(root, model, loadout, forGame);

        // Oyunda kalkan gorunmez; carpma noktasinda renkli alan belirir. Hangarda yalniz
        // cok silik bir kure kalkan kapsamini gosterir.
        if (!forGame) BuildShield(root, stats);
    }

    private static void BuildMissiles(GameObject root, int model, PlaneLoadout l, float span)
    {
        Material metal = FindMaterial(root, "WW2_Metal", "Plane_Dark", "WW2_Black");
        Material red = FindMaterial(root, "WW2_Red", "Plane_Red", "WW1_Red");

        for (int i = 0; i < l.missiles.Length; i++)
        {
            int type = l.missiles[i];
            if (type <= 0) continue;

            bool heavy = PartCatalog.Missiles[type].damage > 3f;
            float thick = heavy ? 0.26f : 0.18f;

            var holder = new GameObject(Prefix + "Missile" + i);
            holder.transform.SetParent(root.transform, false);
            holder.transform.localPosition = MissileOffset(model, i, span);

            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            body.name = Prefix + "MissileBody";
            Object.Destroy(body.GetComponent<Collider>());
            body.transform.SetParent(holder.transform, false);
            body.transform.localEulerAngles = new Vector3(90f, 0f, 0f);
            body.transform.localScale = new Vector3(thick, 0.65f, thick);
            if (metal != null) body.GetComponent<Renderer>().sharedMaterial = metal;

            GameObject nose = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            nose.name = Prefix + "MissileNose";
            Object.Destroy(nose.GetComponent<Collider>());
            nose.transform.SetParent(holder.transform, false);
            nose.transform.localPosition = new Vector3(0f, 0f, 0.65f);
            nose.transform.localScale = new Vector3(thick, thick, thick * 1.5f);
            if (red != null) nose.GetComponent<Renderer>().sharedMaterial = red;
        }
    }

    // -------------------------------------------------------------- pieces

    private static void Clear(Transform root)
    {
        for (int i = root.childCount - 1; i >= 0; i--)
        {
            Transform c = root.GetChild(i);
            if (c.name.StartsWith(Prefix)) Object.Destroy(c.gameObject);
        }
    }

    private static void Paint(GameObject root, int paintIndex)
    {
        PaintScheme scheme = PartCatalog.Paints[Mathf.Clamp(paintIndex, 0, PartCatalog.Paints.Length - 1)];
        if (scheme.original) return;

        foreach (var r in root.GetComponentsInChildren<Renderer>(true))
        {
            if (r is TrailRenderer || r is ParticleSystemRenderer) continue;
            if (r.transform.name.StartsWith(Prefix)) continue;

            Material[] shared = r.sharedMaterials;
            Material[] mats = r.materials;   // kopya: asset materyallerine dokunmaz
            bool changed = false;

            for (int i = 0; i < mats.Length && i < shared.Length; i++)
            {
                if (shared[i] == null) continue;
                switch (shared[i].name)
                {
                    case "WW1_Body":
                    case "WW2_Olive":
                    case "Plane_Body":
                    case "Atmaca_Body":
                    case "Kartal_Body":
                    case "Shark_Body":
                        mats[i].color = scheme.primary;
                        changed = true;
                        break;
                    case "WW1_Fabric":
                    case "WW2_Belly":
                    case "Atmaca_Belly":
                    case "Kartal_Belly":
                    case "Shark_Belly":
                        mats[i].color = scheme.secondary;
                        changed = true;
                        break;
                    case "WW1_Red":
                    case "WW2_Red":
                    case "Plane_Red":
                    case "Atmaca_Accent":
                    case "Kartal_Accent":
                    case "Shark_Accent":
                        mats[i].color = scheme.accent;
                        changed = true;
                        break;
                }
            }

            if (changed) r.materials = mats;
        }
    }

    private static void Blades(GameObject root, int model, int count)
    {
        Propeller prop = root.GetComponentInChildren<Propeller>(true);
        if (prop == null) return;

        Material bladeMat = null;
        foreach (var r in prop.GetComponentsInChildren<Renderer>(true))
        {
            if (r.name.Contains("Blade"))
            {
                if (bladeMat == null) bladeMat = r.sharedMaterial;
                r.enabled = false;           // eski kanatlari gizle
            }
        }

        float radius = PropRadius[Mathf.Clamp(model, 0, Models - 1)];
        for (int i = 0; i < count; i++)
        {
            var arm = new GameObject(Prefix + "BladeArm" + i);
            arm.transform.SetParent(prop.transform, false);
            arm.transform.localEulerAngles = new Vector3(0f, 0f, i * 360f / count);

            GameObject blade = GameObject.CreatePrimitive(PrimitiveType.Cube);
            blade.name = Prefix + "Blade";
            Object.Destroy(blade.GetComponent<Collider>());
            blade.transform.SetParent(arm.transform, false);
            blade.transform.localPosition = new Vector3(0f, radius * 0.5f, 0f);
            blade.transform.localScale = new Vector3(0.14f, radius, 0.03f);
            if (bladeMat != null) blade.GetComponent<Renderer>().sharedMaterial = bladeMat;
        }
    }

    private static void Span(GameObject root, float span)
    {
        if (Mathf.Approximately(span, 1f)) return;

        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            switch (t.name)
            {
                case "Wing":
                case "UpperWing":
                case "LowerWing":
                    ScaleX(t, span, false);
                    break;
                case "WingL":
                case "WingR":
                    ScaleX(t, span, true);
                    break;
                case "WingTipL":
                case "WingTipR":
                case "InnerStrut":
                case "OuterStrut":
                case "RoundelOuter":
                case "RoundelInner":
                case "TrailL":
                case "TrailR":
                    Vector3 p = t.localPosition;
                    p.x *= span;
                    t.localPosition = p;
                    break;
            }
        }
    }

    private static void ScaleX(Transform t, float span, bool moveCenter)
    {
        Vector3 s = t.localScale;
        s.x *= span;
        t.localScale = s;

        if (moveCenter)
        {
            Vector3 p = t.localPosition;
            p.x *= span;   // ic kenar govdede kalir
            t.localPosition = p;
        }
    }

    private static Material FindMaterial(GameObject root, params string[] names)
    {
        foreach (var r in root.GetComponentsInChildren<Renderer>(true))
        {
            foreach (var m in r.sharedMaterials)
            {
                if (m == null) continue;
                foreach (var n in names)
                    if (m.name == n) return m;
            }
        }
        return null;
    }

    /// <summary>Oyunda her silahin namlu ucundaki agiz parlamasinin adi (PlayerGun bununla bulur).</summary>
    public static string GunFlashName(int slot) => Prefix + "GunFlash" + slot;

    private static void BuildWeapons(GameObject root, int model, PlaneLoadout l, float span, bool forGame)
    {
        Material metal = FindMaterial(root, "WW2_Metal", "Plane_Dark", "WW2_Black");

        for (int i = 0; i < l.weapons.Length; i++)
        {
            int type = l.weapons[i];
            if (type <= 0) continue;

            PartDef def = PartCatalog.Weapons[type];
            float thick = (def.damage > 1f ? 0.15f : (def.rate > 15f ? 0.1f : 0.075f)) * 1.5f;

            // Namlu ucu WeaponOffset'tedir: silindir merkezi uctan yarim boy geride
            GameObject barrel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            barrel.name = Prefix + "Gun" + i;
            Object.Destroy(barrel.GetComponent<Collider>());
            barrel.transform.SetParent(root.transform, false);
            barrel.transform.localPosition = WeaponOffset(model, i, span) - new Vector3(0f, 0f, BarrelLength * 0.5f);
            barrel.transform.localEulerAngles = new Vector3(90f, 0f, 0f);
            barrel.transform.localScale = new Vector3(thick, BarrelLength * 0.5f, thick);
            if (metal != null) barrel.GetComponent<Renderer>().sharedMaterial = metal;

            // Namlu noktasi + agiz parlamasi namlu ucunda (PlayerShot buradan ates eder); hangarda gerek yok
            if (forGame)
                MuzzleFlash.Create(root.transform, GunFlashName(i), WeaponOffset(model, i, span), def.damage > 1f ? 1.3f : 1f);
        }
    }

    private static void BuildTurrets(GameObject root, int model, PlaneLoadout l, bool forGame)
    {
        Material metal = FindMaterial(root, "WW2_Metal", "Plane_Dark", "WW2_Black");

        for (int i = 0; i < l.turrets.Length; i++)
        {
            int type = l.turrets[i];
            if (type <= 0) continue;

            PartDef def = PartCatalog.Turrets[type];
            bool heavy = def.damage > 1f;
            bool ventral = i == 1;

            var mount = new GameObject(Prefix + "Turret" + i);
            mount.transform.SetParent(root.transform, false);
            mount.transform.localPosition = TurretOffset(model, i);

            // Taban
            GameObject baseGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            baseGo.name = Prefix + "TurretBase";
            Object.Destroy(baseGo.GetComponent<Collider>());
            baseGo.transform.SetParent(mount.transform, false);
            baseGo.transform.localScale = new Vector3(0.5f, 0.08f, 0.5f);
            if (metal != null) baseGo.GetComponent<Renderer>().sharedMaterial = metal;

            // Donen kafa (hedefe bakar)
            var head = new GameObject(Prefix + "TurretHead");
            head.transform.SetParent(mount.transform, false);
            head.transform.localPosition = new Vector3(0f, ventral ? -0.12f : 0.12f, 0f);

            GameObject dome = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            dome.name = Prefix + "TurretDome";
            Object.Destroy(dome.GetComponent<Collider>());
            dome.transform.SetParent(head.transform, false);
            dome.transform.localScale = Vector3.one * (heavy ? 0.45f : 0.34f);
            if (metal != null) dome.GetComponent<Renderer>().sharedMaterial = metal;

            // Namlu: kafanin onunde, ucu TurretTipZ'de
            GameObject gun = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            gun.name = Prefix + "TurretGun";
            Object.Destroy(gun.GetComponent<Collider>());
            gun.transform.SetParent(head.transform, false);
            gun.transform.localPosition = new Vector3(0f, 0f, TurretTipZ * 0.5f);
            gun.transform.localEulerAngles = new Vector3(90f, 0f, 0f);
            gun.transform.localScale = new Vector3(heavy ? 0.12f : 0.07f, TurretTipZ * 0.5f, heavy ? 0.12f : 0.07f);
            if (metal != null) gun.GetComponent<Renderer>().sharedMaterial = metal;

            if (forGame)
            {
                var turret = mount.AddComponent<AutoTurret>();
                turret.def = def;
                turret.head = head.transform;
                turret.owner = root.transform;
                // Ana silahlarla ayni standart: namlu ucunda namlu noktasi + parlama (kafayla doner)
                turret.muzzle = MuzzleFlash.Create(head.transform, Prefix + "TurretFlash" + i,
                    new Vector3(0f, 0f, TurretTipZ), heavy ? 0.9f : 0.65f);
            }
        }
    }

    private static void BuildShield(GameObject root, LoadoutStats stats)
    {
        if (stats.shieldMax <= 0f) return;

        GameObject bubble = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        bubble.name = Prefix + "Shield";
        Object.Destroy(bubble.GetComponent<Collider>());
        bubble.transform.SetParent(root.transform, false);
        bubble.transform.localPosition = new Vector3(0f, 0f, 0.2f);
        bubble.transform.localScale = new Vector3(8f, 5.5f, 8f);

        var mat = new Material(Shader.Find("Sprites/Default"));
        mat.color = new Color(0.3f, 0.75f, 1f, 0.05f);
        var rend = bubble.GetComponent<Renderer>();
        rend.sharedMaterial = mat;
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }
}
