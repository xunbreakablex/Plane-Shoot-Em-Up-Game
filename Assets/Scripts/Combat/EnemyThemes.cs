using System.Collections.Generic;
using UnityEngine;

public enum EnemyTheme { None, Demon, Heaven }

// Tema dusmanlari (2026-10-23): Cehennem Kapisi'nda iblis, Gokyuzu Tapinagi'nda melek ve dusmus melek ucaklari.
// Davranis mevcut turlerden (avci, suru, agir, nisanci, kamikaze) gelir; degisen yalniz gorunum: govde rengi, kanatlar
// (yarasa zari / tuy), boynuz ya da hale, parlayan gozler ve motor izi (EngineFx.Style). Parcalar collider'siz (isabet
// alani degismez) ve ucagin kendi olculerinden (yerel sinir kutusu) boyutlanir; kokun olcegiyle buyur.
public static class EnemyThemes
{
    public enum Look { None, Demon, Angel, Fallen }

    /// <summary>Asama temasi ve dusman turune gore gorunum. Cennette agir / nisanci / kamikaze dusmus melektir.</summary>
    public static Look Pick(EnemyTheme theme, EnemyKind kind)
    {
        switch (theme)
        {
            case EnemyTheme.Demon: return Look.Demon;
            case EnemyTheme.Heaven:
                return kind == EnemyKind.Heavy || kind == EnemyKind.Sniper || kind == EnemyKind.Kamikaze ? Look.Fallen : Look.Angel;
            default: return Look.None;
        }
    }

    /// <summary>Govde boyasi (CombatManager.Tint ile karisir).</summary>
    public static Color Tint(Look look, Color fallback)
    {
        switch (look)
        {
            case Look.Demon: return new Color(0.45f, 0.06f, 0.05f);
            case Look.Angel: return new Color(1f, 0.97f, 0.88f);
            case Look.Fallen: return new Color(0.16f, 0.1f, 0.22f);
            default: return fallback;
        }
    }

    public static EngineFx.Style Style(Look look)
    {
        switch (look)
        {
            case Look.Demon: return EngineFx.Style.Hell;
            case Look.Angel: return EngineFx.Style.Holy;
            case Look.Fallen: return EngineFx.Style.Fallen;
            default: return EngineFx.Style.Normal;
        }
    }

    public static void Decorate(GameObject root, Look look)
    {
        if (look == Look.None) return;
        Bounds b = LocalBounds(root.transform);
        Vector3 c = b.center, e = b.extents;
        float sz = Mathf.Max(0.5f, e.z / 3f);

        // Parcalar dogrudan koke: kanat kopunca o taraftaki tema kanadi da kopar (EnemyPlane.DetachParts)
        Transform h = root.transform;

        if (look == Look.Demon)
        {
            Material membrane = Lit(new Color(0.4f, 0.04f, 0.06f));
            Material bone = Lit(new Color(0.88f, 0.82f, 0.66f));
            Material eye = Unlit(new Color(1f, 0.45f, 0.1f));
            for (int s = -1; s <= 1; s += 2)
            {
                // Yarasa kanadi: arkaya ve yukari acilan uc zar "parmagi"
                for (int i = 0; i < 3; i++)
                {
                    Part(h, PrimitiveType.Cube, V(s * e.x * 0.5f, c.y + e.y * 0.35f + i * 0.12f * sz, c.z - i * 0.35f * sz),
                        V(e.x * (0.95f - i * 0.15f), 0.05f * sz, (1.6f - i * 0.3f) * sz), membrane,
                        new Vector3(0f, s * (-10f - i * 12f), s * (12f + i * 8f)));
                }
                // Boynuz ve kor gozler
                Part(h, PrimitiveType.Capsule, V(s * 0.22f * sz, c.y + e.y * 0.9f, c.z + e.z * 0.55f),
                    V(0.12f * sz, 0.4f * sz, 0.12f * sz), bone, new Vector3(-35f, 0f, s * -25f));
                Part(h, PrimitiveType.Sphere, V(s * 0.2f * sz, c.y + e.y * 0.35f, c.z + e.z * 0.92f),
                    V(0.16f * sz, 0.12f * sz, 0.1f * sz), eye);
            }
        }
        else
        {
            bool fallen = look == Look.Fallen;
            Material feather = Lit(fallen ? new Color(0.12f, 0.08f, 0.16f) : new Color(0.97f, 0.97f, 1f));
            Material tip = Lit(fallen ? new Color(0.45f, 0.2f, 0.65f) : new Color(1f, 0.85f, 0.4f));
            Material eye = Unlit(fallen ? new Color(0.75f, 0.35f, 1f) : new Color(0.6f, 0.9f, 1f));
            for (int s = -1; s <= 1; s += 2)
            {
                // Tuy kanat: yelpaze gibi acilan dort uzun tuy, uclari renkli
                for (int i = 0; i < 4; i++)
                {
                    float len = e.x * (0.7f - i * 0.08f);
                    var rot = new Vector3(0f, s * (-8f - i * 11f), s * (14f + i * 7f));
                    Vector3 p = V(s * (e.x * 0.35f + i * 0.1f * sz), c.y + e.y * 0.45f + i * 0.1f * sz, c.z + 0.2f * sz - i * 0.3f * sz);
                    Part(h, PrimitiveType.Sphere, p, V(len, 0.07f * sz, 0.32f * sz), feather, rot);
                    Vector3 tipPos = p + Quaternion.Euler(rot) * new Vector3(s * len * 0.42f, 0f, 0f);
                    Part(h, PrimitiveType.Sphere, tipPos, V(len * 0.25f, 0.08f * sz, 0.22f * sz), tip, rot);
                }
                Part(h, PrimitiveType.Sphere, V(s * 0.2f * sz, c.y + e.y * 0.35f, c.z + e.z * 0.92f),
                    V(0.14f * sz, 0.1f * sz, 0.08f * sz), eye);
            }
            // Hale: kokpitin ustunde altin halka; dusmus melekte kirik ve mor
            Halo(h, V(0f, c.y + e.y + 0.55f * sz, c.z + e.z * 0.15f), 0.6f * sz, 0.07f * sz,
                fallen ? new Color(0.6f, 0.25f, 0.95f, 0.9f) : new Color(1f, 0.85f, 0.35f, 1f), fallen ? 250f : 360f);
        }
    }

    // ------------------------------------------------------------------ yardimcilar

    private static Vector3 V(float x, float y, float z) { return new Vector3(x, y, z); }

    private static Bounds LocalBounds(Transform root)
    {
        bool any = false;
        var b = new Bounds();
        foreach (var r in root.GetComponentsInChildren<MeshRenderer>())
        {
            Bounds wb = r.bounds;
            Vector3 mn = wb.min, mx = wb.max;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = new Vector3((i & 1) == 0 ? mn.x : mx.x, (i & 2) == 0 ? mn.y : mx.y, (i & 4) == 0 ? mn.z : mx.z);
                Vector3 lp = root.InverseTransformPoint(corner);
                if (!any) { b = new Bounds(lp, Vector3.zero); any = true; }
                else b.Encapsulate(lp);
            }
        }
        return any ? b : new Bounds(Vector3.zero, new Vector3(6f, 2f, 6f));
    }

    private static void Part(Transform parent, PrimitiveType type, Vector3 pos, Vector3 scale, Material mat, Vector3 euler = default)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = "TemaParca";
        Object.Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localEulerAngles = euler;
        go.transform.localScale = scale;
        var r = go.GetComponent<Renderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    private static void Halo(Transform parent, Vector3 center, float radius, float width, Color color, float arcDeg)
    {
        var go = new GameObject("Hale");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = center;
        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = false;
        lr.loop = arcDeg >= 359f;
        int n = 24;
        lr.positionCount = lr.loop ? n : n + 1;
        for (int i = 0; i < lr.positionCount; i++)
        {
            float a = (lr.loop ? 360f * i / n : arcDeg * i / n + 20f) * Mathf.Deg2Rad;
            lr.SetPosition(i, new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius));
        }
        lr.widthMultiplier = width;
        lr.sharedMaterial = LineMaterial();
        lr.startColor = lr.endColor = color;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.numCornerVertices = 2;
    }

    private static readonly Dictionary<Color, Material> litCache = new Dictionary<Color, Material>();
    private static readonly Dictionary<Color, Material> unlitCache = new Dictionary<Color, Material>();
    private static Material lineMat;

    private static Material Lit(Color c) { return Cached(litCache, c, "Universal Render Pipeline/Lit"); }
    private static Material Unlit(Color c) { return Cached(unlitCache, c, "Universal Render Pipeline/Unlit"); }

    private static Material Cached(Dictionary<Color, Material> cache, Color c, string shader)
    {
        if (cache.TryGetValue(c, out Material m) && m != null) return m;
        Shader sh = Shader.Find(shader);
        if (sh == null) sh = Shader.Find("Sprites/Default");
        m = new Material(sh) { color = c, name = "Tema_" + ColorUtility.ToHtmlStringRGB(c) };
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        cache[c] = m;
        return m;
    }

    private static Material LineMaterial()
    {
        if (lineMat == null) lineMat = new Material(Shader.Find("Sprites/Default")) { name = "Tema_Hale" };
        return lineMat;
    }
}
