using System.Collections.Generic;
using UnityEngine;

public enum BossType { Jet, Shark, Crab, Octopus, Zeppelin, Dragon, Demon, Angel, KiWarrior, MagicalGirl, Eagle, Golem }

// Boss govdelerini Unity primitive'lerinden kodla uretir (model dosyasi gerekmez).
// Her bossun parcalari BossBrain tarafindan isimle bulunup canlandirilir; isimleri degistirirsen
// BossBrain.*.cs'deki Find cagrilarini da guncelle.
// Koordinat: +Z on, +Y yukari, +X sag; olcek metre.
public static partial class BossBuilder
{
    private static Material lit, glow;
    private static readonly Dictionary<long, Material> cache = new Dictionary<long, Material>();

    public static GameObject Build(BossType type, Material litBase, Material glowBase)
    {
        lit = litBase;
        glow = glowBase;

        var rootGo = new GameObject("Boss_" + type);
        Transform root = rootGo.transform;

        switch (type)
        {
            case BossType.Jet: BuildJet(root); break;
            case BossType.Shark: BuildShark(root); break;
            case BossType.Crab: BuildCrab(root); break;
            case BossType.Octopus: BuildOctopus(root); break;
            case BossType.Zeppelin: BuildZeppelin(root); break;
            case BossType.Demon: BuildDemon(root); break;
            case BossType.Angel: BuildAngel(root); break;
            case BossType.KiWarrior: BuildKiWarrior(root); break;
            case BossType.MagicalGirl: BuildMagicalGirl(root); break;
            case BossType.Eagle: BuildEagle(root); break;
            case BossType.Golem: BuildGolem(root); break;
            default: BuildDragon(root); break;
        }

        var rb = rootGo.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
        return rootGo;
    }

    // ------------------------------------------------------------ yardimcilar

    private static Color C(float r, float g, float b) { return new Color(r, g, b, 1f); }

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
        if (!collider) Object.Destroy(go.GetComponent<Collider>());   // sus/aura parcalari mermi yutmasin
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

    private static Vector3 V(float x, float y, float z) { return new Vector3(x, y, z); }

    // ==================================================================== JET (Sahin-X)

    private static void BuildJet(Transform root)
    {
        Color hull = C(0.36f, 0.42f, 0.56f), dark = C(0.16f, 0.18f, 0.24f), red = C(0.8f, 0.12f, 0.1f);
        Color flame = C(1f, 0.55f, 0.1f), eye = C(1f, 0.85f, 0.2f);

        Part(root, PrimitiveType.Capsule, "Fuselage", V(0, 0, 0), V(3.4f, 9f, 3.4f), hull, V(90, 0, 0));
        Part(root, PrimitiveType.Sphere, "Nose", V(0, 0, 9.5f), V(3.4f, 3.2f, 8f), hull);
        Part(root, PrimitiveType.Sphere, "Cockpit", V(0, 1.9f, 3.5f), V(2.3f, 1.8f, 5f), eye, V(0, 0, 0), true);

        // Suprulmus kanatlar
        Part(root, PrimitiveType.Cube, "WingL", V(-10f, -0.3f, -2.5f), V(16f, 0.6f, 7f), hull, V(0, 24, -4));
        Part(root, PrimitiveType.Cube, "WingR", V(10f, -0.3f, -2.5f), V(16f, 0.6f, 7f), hull, V(0, -24, 4));
        Part(root, PrimitiveType.Cube, "StripeL", V(-12f, 0.05f, -4f), V(8f, 0.7f, 1.6f), red, V(0, 24, -4));
        Part(root, PrimitiveType.Cube, "StripeR", V(12f, 0.05f, -4f), V(8f, 0.7f, 1.6f), red, V(0, -24, 4));

        // Kanat ucu topcu kapsulleri
        Part(root, PrimitiveType.Capsule, "PodL", V(-17.5f, -0.6f, -5f), V(1.3f, 3.4f, 1.3f), dark, V(90, 0, 0));
        Part(root, PrimitiveType.Capsule, "PodR", V(17.5f, -0.6f, -5f), V(1.3f, 3.4f, 1.3f), dark, V(90, 0, 0));

        // Kuyruk
        Part(root, PrimitiveType.Cube, "FinL", V(-2.6f, 3.4f, -8.5f), V(0.5f, 5.5f, 5f), red, V(0, 0, 14));
        Part(root, PrimitiveType.Cube, "FinR", V(2.6f, 3.4f, -8.5f), V(0.5f, 5.5f, 5f), red, V(0, 0, -14));
        Part(root, PrimitiveType.Cube, "TailplaneL", V(-5f, 0.2f, -9.5f), V(7f, 0.4f, 3.4f), hull, V(0, 14, 0));
        Part(root, PrimitiveType.Cube, "TailplaneR", V(5f, 0.2f, -9.5f), V(7f, 0.4f, 3.4f), hull, V(0, -14, 0));

        // Hava alicilar ve motorlar
        Part(root, PrimitiveType.Cube, "IntakeL", V(-2.5f, -1.2f, 3f), V(1.7f, 1.8f, 4.5f), dark);
        Part(root, PrimitiveType.Cube, "IntakeR", V(2.5f, -1.2f, 3f), V(1.7f, 1.8f, 4.5f), dark);
        Part(root, PrimitiveType.Cylinder, "EngineL", V(-2.2f, 0, -9.5f), V(2.3f, 2.4f, 2.3f), dark, V(90, 0, 0));
        Part(root, PrimitiveType.Cylinder, "EngineR", V(2.2f, 0, -9.5f), V(2.3f, 2.4f, 2.3f), dark, V(90, 0, 0));
        Part(root, PrimitiveType.Sphere, "FlameL", V(-2.2f, 0, -12.2f), V(2f, 2f, 3.4f), flame, default, true);
        Part(root, PrimitiveType.Sphere, "FlameR", V(2.2f, 0, -12.2f), V(2f, 2f, 3.4f), flame, default, true);

        Pivot(root, "MuzzleNose", V(0, 0, 14f));
        Pivot(root, "MuzzlePodL", V(-17.5f, -0.6f, -1f));
        Pivot(root, "MuzzlePodR", V(17.5f, -0.6f, -1f));
    }

    // ==================================================================== KOPEKBALIGI

    private static void BuildShark(Transform root)
    {
        Color body = C(0.30f, 0.38f, 0.50f), belly = C(0.74f, 0.80f, 0.86f), dark = C(0.13f, 0.16f, 0.21f);
        Color fin = C(0.22f, 0.28f, 0.40f), eye = C(1f, 0.15f, 0.1f), tooth = C(0.93f, 0.93f, 0.88f);
        Color accent = C(1f, 0.6f, 0.1f);

        // Govde: kalinlasip incelen yuvarlak dilimler
        Part(root, PrimitiveType.Sphere, "Seg0", V(0, 0.5f, 16f), V(8f, 7.5f, 13f), body);
        Part(root, PrimitiveType.Sphere, "Seg1", V(0, 0.2f, 6f), V(10.5f, 9.5f, 15f), body);
        Part(root, PrimitiveType.Sphere, "Seg2", V(0, 0, -5f), V(10f, 9f, 15f), body);
        Part(root, PrimitiveType.Sphere, "Seg3", V(0, 0, -15f), V(7.5f, 7f, 13f), body);
        Part(root, PrimitiveType.Sphere, "Seg4", V(0, 0, -23f), V(4.6f, 4.6f, 9f), body);
        Part(root, PrimitiveType.Sphere, "Belly", V(0, -2.4f, 6f), V(8.6f, 5f, 26f), belly);
        Part(root, PrimitiveType.Sphere, "Snout", V(0, 0.4f, 24f), V(5.6f, 4.2f, 9f), body);

        // Alt cene (acilir)
        Transform jaw = Pivot(root, "Jaw", V(0, -2.6f, 12.5f));
        Part(jaw, PrimitiveType.Cube, "JawBone", V(0, -0.9f, 6.5f), V(5.4f, 1.4f, 13f), dark);
        for (int i = 0; i < 5; i++)
        {
            Part(jaw, PrimitiveType.Cube, "ToothLoL" + i, V(-2.2f, 0.2f, 2.5f + i * 2.4f), V(0.55f, 1.4f, 0.7f), tooth);
            Part(jaw, PrimitiveType.Cube, "ToothLoR" + i, V(2.2f, 0.2f, 2.5f + i * 2.4f), V(0.55f, 1.4f, 0.7f), tooth);
        }
        for (int i = 0; i < 5; i++)
        {
            Part(root, PrimitiveType.Cube, "ToothUpL" + i, V(-2.3f, -2.3f, 15f + i * 2.3f), V(0.55f, 1.4f, 0.7f), tooth);
            Part(root, PrimitiveType.Cube, "ToothUpR" + i, V(2.3f, -2.3f, 15f + i * 2.3f), V(0.55f, 1.4f, 0.7f), tooth);
        }

        Part(root, PrimitiveType.Sphere, "EyeL", V(-4.3f, 2.5f, 18.5f), V(1.9f, 1.9f, 1.9f), eye, default, true);
        Part(root, PrimitiveType.Sphere, "EyeR", V(4.3f, 2.5f, 18.5f), V(1.9f, 1.9f, 1.9f), eye, default, true);

        // Yuzgecler
        Part(root, PrimitiveType.Cube, "Dorsal", V(0, 8f, -1f), V(0.9f, 9.5f, 11f), fin, V(-18, 0, 0));
        Part(root, PrimitiveType.Cube, "FinL", V(-11.5f, -2.6f, 6f), V(15f, 0.7f, 7.5f), fin, V(0, -18, 18));
        Part(root, PrimitiveType.Cube, "FinR", V(11.5f, -2.6f, 6f), V(15f, 0.7f, 7.5f), fin, V(0, 18, -18));
        for (int i = 0; i < 4; i++)
            Part(root, PrimitiveType.Cube, "Plate" + i, V(0, 5f - i * 0.3f, 12f - i * 7f), V(6.5f, 0.7f, 3.6f), dark);
        for (int i = 0; i < 3; i++)
        {
            Part(root, PrimitiveType.Cube, "GillL" + i, V(-5.1f, 0.4f, 10f - i * 1.8f), V(0.5f, 4.5f, 0.6f), dark);
            Part(root, PrimitiveType.Cube, "GillR" + i, V(5.1f, 0.4f, 10f - i * 1.8f), V(0.5f, 4.5f, 0.6f), dark);
        }

        // Kuyruk (sallanir)
        Transform tail = Pivot(root, "Tail", V(0, 0, -26f));
        Part(tail, PrimitiveType.Cube, "TailFinUp", V(0, 4f, -4f), V(0.9f, 11f, 8f), fin, V(22, 0, 0));
        Part(tail, PrimitiveType.Cube, "TailFinLo", V(0, -3.6f, -3f), V(0.9f, 7.5f, 6f), fin, V(-30, 0, 0));
        Part(tail, PrimitiveType.Sphere, "Thruster", V(0, 0, -1.5f), V(2.4f, 2.4f, 3f), accent, default, true);

        Pivot(root, "MuzzleMouth", V(0, -1.4f, 27f));
    }

    // ==================================================================== YENGEC

    private static void BuildCrab(Transform root)
    {
        Color shell = C(0.72f, 0.28f, 0.14f), dark = C(0.32f, 0.12f, 0.08f), claw = C(0.85f, 0.4f, 0.18f);
        Color eye = C(1f, 0.85f, 0.15f), metal = C(0.35f, 0.36f, 0.4f), flame = C(1f, 0.5f, 0.15f);

        Part(root, PrimitiveType.Sphere, "Body", V(0, 0, 0), V(25f, 9.5f, 18f), shell);
        Part(root, PrimitiveType.Sphere, "Shell", V(0, 3.2f, -1.5f), V(19f, 6.5f, 13f), dark);
        Part(root, PrimitiveType.Cube, "FacePlate", V(0, 0.4f, 8.2f), V(11f, 5.5f, 2.2f), metal);
        for (int i = 0; i < 4; i++)
            Part(root, PrimitiveType.Cube, "ShellSpike" + i, V(-6f + i * 4f, 6.6f, -1f), V(1.2f, 2.4f, 1.2f), metal, V(0, 0, (i - 1.5f) * 10f));

        // Goz saplari
        for (int s = -1; s <= 1; s += 2)
        {
            string side = s < 0 ? "L" : "R";
            Part(root, PrimitiveType.Capsule, "Stalk" + side, V(s * 5f, 6.4f, 8f), V(0.9f, 3.2f, 0.9f), metal);
            Part(root, PrimitiveType.Sphere, "Eye" + side, V(s * 5f, 10f, 8f), V(2.6f, 2.6f, 2.6f), eye, default, true);
        }

        // Kollar ve kiskaclar
        for (int s = -1; s <= 1; s += 2)
        {
            string side = s < 0 ? "L" : "R";
            float yaw = s > 0 ? -48f : 228f;     // +X ekseni one-disa baksin
            Transform arm = Pivot(root, "Arm" + side, V(s * 11.5f, 0, 5f), V(0, yaw, 0));
            Part(arm, PrimitiveType.Capsule, "Upper" + side, V(7f, 0, 0), V(2.8f, 7.5f, 2.8f), shell, V(0, 0, 90));
            Part(arm, PrimitiveType.Sphere, "Elbow" + side, V(0.5f, 0, 0), V(3.6f, 3.6f, 3.6f), metal);

            Transform clawPivot = Pivot(arm, "Claw" + side, V(14f, 0, 0));
            Part(clawPivot, PrimitiveType.Sphere, "Palm" + side, V(1.5f, 0, 0), V(5f, 4.4f, 4.2f), claw);

            Transform top = Pivot(clawPivot, "PincerTop" + side, V(3f, 0.9f, 0));
            Part(top, PrimitiveType.Cube, "TopBlade" + side, V(4.2f, 0.9f, 0), V(8.5f, 1.5f, 2.6f), claw);
            Transform bot = Pivot(clawPivot, "PincerBot" + side, V(3f, -0.9f, 0));
            Part(bot, PrimitiveType.Cube, "BotBlade" + side, V(4.2f, -0.9f, 0), V(8.5f, 1.5f, 2.6f), claw);

            Pivot(clawPivot, "MuzzleClaw" + side, V(9f, 0, 0));
        }

        // Bacaklar
        for (int s = -1; s <= 1; s += 2)
        {
            string side = s < 0 ? "L" : "R";
            for (int i = 0; i < 3; i++)
            {
                Transform leg = Pivot(root, "Leg" + side + i, V(s * 9.5f, -3f, -3f - i * 4f), V(0, 0, s * -55f));
                Part(leg, PrimitiveType.Capsule, "LegBone" + side + i, V(0, -3.2f, 0), V(1.3f, 3.6f, 1.3f), metal);
            }
        }

        Part(root, PrimitiveType.Sphere, "ThrusterL", V(-4f, 0.5f, -9f), V(3.4f, 3.4f, 3.4f), flame, default, true);
        Part(root, PrimitiveType.Sphere, "ThrusterR", V(4f, 0.5f, -9f), V(3.4f, 3.4f, 3.4f), flame, default, true);
        Pivot(root, "MuzzleBody", V(0, 0.4f, 10f));
    }

    // ==================================================================== AHTAPOT

    public const int OctoTentacles = 8;
    public const int OctoSegments = 6;

    private static void BuildOctopus(Transform root)
    {
        Color skin = C(0.50f, 0.20f, 0.66f), dark = C(0.2f, 0.08f, 0.3f), eye = C(1f, 0.9f, 0.2f);
        Color tip = C(1f, 0.3f, 0.8f), metal = C(0.32f, 0.3f, 0.4f);

        Part(root, PrimitiveType.Sphere, "Head", V(0, 5f, 0), V(21f, 18f, 21f), skin);
        Part(root, PrimitiveType.Sphere, "Visor", V(0, 6.5f, 7.5f), V(15f, 8f, 10f), dark);
        Part(root, PrimitiveType.Sphere, "Mantle", V(0, 11f, -4f), V(12f, 10f, 14f), dark);
        Part(root, PrimitiveType.Sphere, "EyeL", V(-5.6f, 6.4f, 10f), V(4.4f, 4.4f, 4.4f), eye, default, true);
        Part(root, PrimitiveType.Sphere, "EyeR", V(5.6f, 6.4f, 10f), V(4.4f, 4.4f, 4.4f), eye, default, true);
        Part(root, PrimitiveType.Cube, "Beak", V(0, -3f, 9.5f), V(3.4f, 3f, 5f), metal, V(20, 0, 0));
        for (int i = 0; i < 3; i++)
            Part(root, PrimitiveType.Cube, "Spike" + i, V((i - 1) * 4.5f, 13.5f, -9f), V(1.2f, 4f, 1.2f), metal, V(-25, 0, (i - 1) * 12f));

        // Dokuncaklar: kure zinciri, BossBrain her karede konumlandirir
        for (int t = 0; t < OctoTentacles; t++)
        {
            for (int k = 0; k < OctoSegments; k++)
            {
                float radius = Mathf.Lerp(3.2f, 1.0f, k / (float)(OctoSegments - 1));
                bool last = k == OctoSegments - 1;
                Part(root, PrimitiveType.Sphere, $"T{t}_{k}", V(0, 0, 0), V(radius * 2f, radius * 2f, radius * 2f),
                    last ? tip : skin, default, last);
            }
        }

        Pivot(root, "MuzzleEyes", V(0, 6.4f, 12f));
    }

    // ==================================================================== ZEPLIN

    public const int ZepTurrets = 4;

    private static void BuildZeppelin(Transform root)
    {
        Color env = C(0.46f, 0.50f, 0.34f), dark = C(0.2f, 0.22f, 0.18f), plate = C(0.34f, 0.36f, 0.28f);
        Color red = C(0.78f, 0.15f, 0.12f), glowC = C(1f, 0.7f, 0.2f), metal = C(0.3f, 0.32f, 0.36f);

        Part(root, PrimitiveType.Sphere, "Envelope", V(0, 6f, 0), V(24f, 19f, 72f), env);
        Part(root, PrimitiveType.Cube, "Gondola", V(0, -8.5f, 2f), V(9f, 5.5f, 30f), dark);
        Part(root, PrimitiveType.Cube, "Keel", V(0, -3f, 0), V(1.4f, 5f, 44f), metal);
        for (int i = 0; i < 6; i++)
            Part(root, PrimitiveType.Cube, "Band" + i, V(0, 6f, -26f + i * 10.5f), V(24.6f, 19.6f, 1.2f), plate);

        // Kuyruk yuzgecleri
        Part(root, PrimitiveType.Cube, "FinTop", V(0, 14f, -31f), V(0.9f, 11f, 11f), red);
        Part(root, PrimitiveType.Cube, "FinBot", V(0, -3f, -31f), V(0.9f, 11f, 11f), red);
        Part(root, PrimitiveType.Cube, "FinL", V(-12f, 6f, -31f), V(11f, 0.9f, 11f), red);
        Part(root, PrimitiveType.Cube, "FinR", V(12f, 6f, -31f), V(11f, 0.9f, 11f), red);

        // Motorlar
        for (int s = -1; s <= 1; s += 2)
        {
            string side = s < 0 ? "L" : "R";
            Part(root, PrimitiveType.Cylinder, "Engine" + side, V(s * 7f, -9f, -9f), V(3.2f, 2.4f, 3.2f), metal, V(90, 0, 0));
            Part(root, PrimitiveType.Sphere, "Prop" + side, V(s * 7f, -9f, -12.4f), V(5.5f, 5.5f, 0.8f), glowC, default, true);
        }

        // Burun topu / goz
        Part(root, PrimitiveType.Sphere, "Eye", V(0, 4f, 35f), V(5f, 5f, 5f), red, default, true);

        // Kuleler: kendi basina doner
        Vector3[] spots = { V(0, 15.2f, 14f), V(0, 15.2f, -12f), V(-8f, -11f, 8f), V(8f, -11f, 8f) };
        for (int i = 0; i < ZepTurrets; i++)
        {
            Transform turret = Pivot(root, "Turret" + i, spots[i]);
            Part(turret, PrimitiveType.Sphere, "Dome" + i, V(0, 0, 0), V(5.8f, 5.8f, 5.8f), metal);
            Part(turret, PrimitiveType.Capsule, "Barrel" + i, V(0, 0, 3.4f), V(0.8f, 2.4f, 0.8f), dark, V(90, 0, 0));
            Pivot(turret, "MuzzleTurret" + i, V(0, 0, 6f));
        }

        // Karin bombasi cikisi
        Pivot(root, "MuzzleBelly", V(0, -12f, 4f));
        Pivot(root, "MuzzleNose", V(0, 4f, 38f));
    }

    // ==================================================================== EJDERHA

    public const int DragonSegments = 16;

    private static void BuildDragon(Transform root)
    {
        Color red = C(0.66f, 0.07f, 0.07f), dark = C(0.16f, 0.12f, 0.12f), gold = C(0.92f, 0.72f, 0.2f);
        Color eye = C(1f, 0.55f, 0.1f), tooth = C(0.93f, 0.9f, 0.82f), mem = C(0.45f, 0.05f, 0.1f);

        // Kafa (root = kafa)
        Part(root, PrimitiveType.Cube, "Skull", V(0, 0, 3f), V(7f, 5.4f, 11f), red);
        Part(root, PrimitiveType.Cube, "Snout", V(0, -0.5f, 10.5f), V(4.8f, 3.2f, 7.5f), red);
        Part(root, PrimitiveType.Cube, "Crest", V(0, 3.2f, 2.5f), V(1.1f, 3.4f, 7f), dark);
        Part(root, PrimitiveType.Capsule, "HornL", V(-2.7f, 3.4f, 0.5f), V(1f, 3.8f, 1f), gold, V(-52, 0, 22));
        Part(root, PrimitiveType.Capsule, "HornR", V(2.7f, 3.4f, 0.5f), V(1f, 3.8f, 1f), gold, V(-52, 0, -22));
        Part(root, PrimitiveType.Sphere, "EyeL", V(-3.3f, 1.4f, 6.8f), V(1.7f, 1.7f, 1.7f), eye, default, true);
        Part(root, PrimitiveType.Sphere, "EyeR", V(3.3f, 1.4f, 6.8f), V(1.7f, 1.7f, 1.7f), eye, default, true);
        Part(root, PrimitiveType.Cube, "Nostril", V(0, 1f, 14f), V(1.6f, 0.8f, 1.2f), dark);

        Transform jaw = Pivot(root, "Jaw", V(0, -1.8f, 6f));
        Part(jaw, PrimitiveType.Cube, "JawBone", V(0, -0.5f, 5.2f), V(4.4f, 1.3f, 10.4f), dark);
        for (int i = 0; i < 4; i++)
        {
            Part(jaw, PrimitiveType.Cube, "FangL" + i, V(-1.8f, 0.3f, 2.5f + i * 2.4f), V(0.5f, 1.3f, 0.6f), tooth);
            Part(jaw, PrimitiveType.Cube, "FangR" + i, V(1.8f, 0.3f, 2.5f + i * 2.4f), V(0.5f, 1.3f, 0.6f), tooth);
        }

        // Kanatlar
        for (int s = -1; s <= 1; s += 2)
        {
            string side = s < 0 ? "L" : "R";
            Transform wing = Pivot(root, "Wing" + side, V(s * 3.5f, 1.8f, -4f));
            Part(wing, PrimitiveType.Cube, "WingArm" + side, V(s * 9f, 0, 0), V(18f, 0.7f, 2f), dark);
            Part(wing, PrimitiveType.Cube, "WingMem" + side, V(s * 10f, -0.1f, -4f), V(20f, 0.3f, 9f), mem, V(0, s * -8f, 0));
            Part(wing, PrimitiveType.Cube, "WingTip" + side, V(s * 19.5f, 0, -1f), V(3f, 0.6f, 4.5f), gold);
        }

        // Govde segmentleri: BossBrain iz uzerine dizer
        for (int k = 0; k < DragonSegments; k++)
        {
            float r = Mathf.Lerp(4.0f, 1.4f, k / (float)(DragonSegments - 1));
            Transform seg = Part(root, PrimitiveType.Sphere, "Seg" + k, V(0, 0, -6f * (k + 1)),
                V(r * 2f, r * 2f, r * 2.3f), k % 2 == 0 ? red : dark);
            Part(seg, PrimitiveType.Cube, "SpikeTop", V(0, 0.55f, 0), V(0.14f, 0.5f, 0.5f), gold);
            Part(seg, PrimitiveType.Cube, "SpikeL", V(-0.55f, 0, 0), V(0.5f, 0.14f, 0.4f), gold);
            Part(seg, PrimitiveType.Cube, "SpikeR", V(0.55f, 0, 0), V(0.5f, 0.14f, 0.4f), gold);
        }
        Part(root, PrimitiveType.Cube, "TailBlade", V(0, 0, -6f * (DragonSegments + 1.2f)), V(0.9f, 6f, 7f), gold);

        Pivot(root, "MuzzleMouth", V(0, -1.4f, 14.5f));
    }
}
