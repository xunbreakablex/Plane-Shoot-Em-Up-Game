using System.Collections.Generic;
using UnityEngine;

// Kahraman karakterlerin bomba tusuyla kullandigi ozel yetenekler (bomba stogunu harcar):
//   MoonBeam        Ay Savascisi: degnekten dev pembe isin (3 sn), onundeki her seyi yakar
//   Kamehameha      Ki Kahramani: avuclardan dev mavi ki isini (3,2 sn)
//   LightningStorm  Imparator: iki elden 2,6 sn boyunca cevredeki dusmanlara yildirim yagmuru
// Isin oyuncuyla birlikte doner (nisan yonu), vurus her 0,1 sn'de isin icindeki dusmanlara; boss'a yuzde hasar.
public class HeroSpecial : MonoBehaviour
{
    public enum Kind { None, MoonBeam, Kamehameha, LightningStorm }

    private const float BeamLength = 1000f;
    private const float TickInterval = 0.1f;
    private const float StormInterval = 0.14f;
    private const float StormRange = 700f;
    private const int StormTargets = 6;

    private CombatManager mgr;
    private PlaneController plane;
    private PlayerGun gun;
    private Kind kind;
    private float start, duration, radius;
    private float nextTick;
    private Color core, glow;
    private Transform coreTube, glowTube;
    private Material coreMat, glowMat;
    private readonly List<LineRenderer> bolts = new List<LineRenderer>();
    private Transform orb;
    private AudioSource hum;
    private Material mat;
    private int model;

    public static HeroSpecial Begin(CombatManager mgr, PlaneController plane, PlayerGun gun, Kind kind)
    {
        var go = new GameObject("HeroSpecial_" + kind);
        var hs = go.AddComponent<HeroSpecial>();
        hs.mgr = mgr;
        hs.plane = plane;
        hs.gun = gun;
        hs.kind = kind;
        hs.start = Time.time;
        hs.model = mgr.flight != null ? mgr.flight.CurrentIndex : 0;
        hs.Setup();
        return hs;
    }

    private void Setup()
    {
        mat = new Material(mgr.fxMaterial != null ? mgr.fxMaterial : new Material(Shader.Find("Sprites/Default")));
        switch (kind)
        {
            case Kind.MoonBeam:
                duration = 3.0f; radius = 7f;
                core = new Color(1f, 0.95f, 1f, 1f); glow = new Color(1f, 0.45f, 0.85f, 0.75f);
                break;
            case Kind.Kamehameha:
                duration = 3.2f; radius = 9f;
                core = new Color(0.9f, 1f, 1f, 1f); glow = new Color(0.3f, 0.7f, 1f, 0.8f);
                break;
            default:
                duration = 2.6f; radius = 0f;
                core = new Color(0.9f, 0.85f, 1f, 1f); glow = new Color(0.55f, 0.25f, 1f, 0.5f);
                break;
        }

        if (kind != Kind.LightningStorm)
        {
            // Isin gercek bir tup: dis hale (yari saydam) + ic cekirdek (parlak); LineRenderer kameraya donuk serit
            // oldugu icin arkadan bakinca dev bir daire gibi gorunuyordu
            glowMat = new Material(mat) { color = glow };
            coreMat = new Material(mat) { color = core };
            glowTube = MakeTube("Glow", glowMat);
            coreTube = MakeTube("Core", coreMat);

            orb = GameObject.CreatePrimitive(PrimitiveType.Sphere).transform;
            Destroy(orb.GetComponent<Collider>());
            orb.name = "ChargeOrb";
            orb.SetParent(transform, false);
            var r = orb.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mat.color = glow;
            Sfx.PlayAt(Sfx.BeamCharge, plane.transform.position, 1f);
        }
        else
        {
            for (int i = 0; i < StormTargets * 2; i++) bolts.Add(MakeLine("Bolt" + i, 10));
        }

        hum = gameObject.AddComponent<AudioSource>();
        hum.clip = Sfx.BeamHum;
        hum.loop = true;
        hum.spatialBlend = 0f;
        hum.volume = 0.35f;
        hum.Play();
        FollowCamera.AddShake(0.8f, 0.5f);
        HeroVoice.Special((int)kind - 1, plane.transform.position);   // yetenek bagirisi
    }

    private static Mesh coneMesh;

    // Kapaksiz, ellerden genisleyen huni-tup (yaricap 1, boy 1, +Z boyunca): basta ince boyun, %8'de tam yaricap.
    // Silindir primitive'inin duz kapagi arkadan bakinca ekrani kaplayan bir kubbe gibi gorunuyordu.
    private static Mesh ConeMesh()
    {
        if (coneMesh != null) return coneMesh;
        const int sides = 28;
        float[] z = { 0f, 0.025f, 0.08f, 1f };
        float[] r = { 0.12f, 0.45f, 1f, 1f };
        var verts = new Vector3[sides * z.Length];
        var uv = new Vector2[verts.Length];
        for (int k = 0; k < z.Length; k++)
            for (int i = 0; i < sides; i++)
            {
                float a = Mathf.PI * 2f * i / sides;
                verts[k * sides + i] = new Vector3(Mathf.Cos(a) * r[k], Mathf.Sin(a) * r[k], z[k]);
                uv[k * sides + i] = new Vector2((float)i / sides, z[k]);
            }
        var tris = new int[(z.Length - 1) * sides * 6];
        int t = 0;
        for (int k = 0; k < z.Length - 1; k++)
            for (int i = 0; i < sides; i++)
            {
                int j = (i + 1) % sides;
                int a0 = k * sides + i, a1 = k * sides + j, b0 = (k + 1) * sides + i, b1 = (k + 1) * sides + j;
                tris[t++] = a0; tris[t++] = b0; tris[t++] = a1;
                tris[t++] = a1; tris[t++] = b0; tris[t++] = b1;
            }
        coneMesh = new Mesh { name = "HeroBeamCone", vertices = verts, uv = uv, triangles = tris };
        coneMesh.RecalculateBounds();
        coneMesh.bounds = new Bounds(new Vector3(0f, 0f, 0.5f), new Vector3(2f, 2f, 1f));
        return coneMesh;
    }

    private Transform MakeTube(string name, Material m)
    {
        var go = new GameObject(name);
        go.AddComponent<MeshFilter>().sharedMesh = ConeMesh();
        go.AddComponent<MeshRenderer>();
        go.transform.SetParent(transform, false);
        var r = go.GetComponent<Renderer>();
        r.sharedMaterial = m;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        go.SetActive(false);
        return go.transform;
    }

    private static void PlaceTube(Transform tube, Vector3 from, Vector3 dir, float length, float diameter)
    {
        if (tube == null) return;
        bool on = diameter > 0.05f;
        if (tube.gameObject.activeSelf != on) tube.gameObject.SetActive(on);
        if (!on) return;
        tube.position = from;
        tube.rotation = Quaternion.LookRotation(dir);
        tube.localScale = new Vector3(diameter * 0.5f, diameter * 0.5f, length);
    }

    private LineRenderer MakeLine(string name, int points)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var lr = go.AddComponent<LineRenderer>();
        lr.sharedMaterial = mat;
        lr.useWorldSpace = true;
        lr.positionCount = points;
        lr.numCapVertices = 4;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        lr.enabled = false;
        return lr;
    }

    private void Update()
    {
        if (plane == null || mgr == null || GameState.Paused) return;
        float t = Time.time - start;
        if (t >= duration || plane.IsCrashed)
        {
            Destroy(gameObject);
            return;
        }

        if (kind == Kind.LightningStorm) StormStep(t);
        else BeamStep(t);
    }

    // Kaynak: ellerin ortasi (govdenin biraz onu); yon: nisan noktasi (burundan en fazla 30 derece sapar)
    private Vector3 Origin => plane.transform.position + plane.transform.forward * 1.6f;

    private Vector3 AimDir()
    {
        Vector3 fwd = plane.transform.forward;
        if (gun == null) return fwd;
        Vector3 d = (gun.AimPoint - Origin).normalized;
        if (Vector3.Angle(fwd, d) > 30f) d = Vector3.RotateTowards(fwd, d, 30f * Mathf.Deg2Rad, 0f).normalized;
        return d;
    }

    private void BeamStep(float t)
    {
        float grow = Mathf.Clamp01(t / 0.35f);
        float fade = Mathf.Clamp01((duration - t) / 0.4f);
        float w = radius * 2f * grow * fade * (1f + 0.06f * Mathf.Sin(Time.time * 30f));
        Vector3 o = Origin;
        Vector3 d = AimDir();
        Vector3 end = o + d * BeamLength;

        PlaceTube(glowTube, o, d, BeamLength, w);
        PlaceTube(coreTube, o, d, BeamLength, w * 0.55f);
        if (glowMat != null) glowMat.color = new Color(glow.r, glow.g, glow.b, glow.a * (0.7f + 0.3f * Mathf.Sin(Time.time * 40f)));

        if (orb != null)
        {
            orb.position = o;
            orb.localScale = Vector3.one * (radius * 0.7f * grow * fade * (1f + 0.12f * Mathf.Sin(Time.time * 22f)));
        }
        FollowCamera.AddShake(0.25f * grow * fade, 0.1f);

        if (Time.time < nextTick || grow < 0.6f) return;
        nextTick = Time.time + TickInterval;

        // Isin icindeki her sey: boss'a yuzde, digerlerine sabit hasar
        foreach (var e in new List<EnemyPlane>(mgr.Enemies))
        {
            if (e == null || e.IsDead) continue;
            Vector3 rel = e.transform.position - o;
            float along = Vector3.Dot(rel, d);
            if (along < 0f || along > BeamLength) continue;
            float off = (rel - d * along).magnitude;
            if (off > radius + e.HitRadius) continue;
            int dmg = e.IsBoss ? Mathf.Max(3, Mathf.RoundToInt(e.MaxHits * 0.0055f)) : (e.IsGround ? 2 : 3);
            e.TakeHit(CombatManager.PlayerDamage(e, dmg), e.transform.position);
            if (Random.value < 0.35f) mgr.SpawnHitSpark(e.transform.position + Random.insideUnitSphere * 2f, 1.4f);
        }
    }

    private void StormStep(float t)
    {
        float fade = Mathf.Clamp01((duration - t) / 0.3f);
        if (Time.time < nextTick) { JitterBolts(); return; }
        nextTick = Time.time + StormInterval;

        Vector3 o = Origin;
        var targets = new List<EnemyPlane>();
        foreach (var e in mgr.Enemies)
        {
            if (e == null || e.IsDead) continue;
            if ((e.transform.position - o).sqrMagnitude > StormRange * StormRange) continue;
            targets.Add(e);
        }
        targets.Sort((a, b) => (a.transform.position - o).sqrMagnitude.CompareTo((b.transform.position - o).sqrMagnitude));
        if (targets.Count > StormTargets) targets.RemoveRange(StormTargets, targets.Count - StormTargets);

        for (int i = 0; i < bolts.Count; i++) bolts[i].enabled = false;
        Vector3 handL = plane.transform.TransformPoint(PlaneBuilder.WeaponOffset(model, 1, 1f));
        Vector3 handR = plane.transform.TransformPoint(PlaneBuilder.WeaponOffset(model, 0, 1f));
        for (int i = 0; i < targets.Count; i++)
        {
            EnemyPlane e = targets[i];
            int dmg = e.IsBoss ? Mathf.Max(4, Mathf.RoundToInt(e.MaxHits * 0.009f)) : (e.IsGround ? 3 : 4);
            e.TakeHit(CombatManager.PlayerDamage(e, dmg), e.transform.position);
            mgr.SpawnHitSpark(e.transform.position, 1.8f);
            DrawBolt(bolts[i], i % 2 == 0 ? handL : handR, e.transform.position, fade);
        }
        if (targets.Count > 0)
        {
            Sfx.PlayAt(Sfx.Zap, o, 0.5f);
            FollowCamera.AddShake(0.35f, 0.12f);
        }
    }

    private void JitterBolts()
    {
        // Yildirim her kare titrer: ayni uclar, yeni kirik cizgi
        foreach (var b in bolts)
        {
            if (!b.enabled) continue;
            Vector3 a = b.GetPosition(0), z = b.GetPosition(b.positionCount - 1);
            FillBolt(b, a, z);
        }
    }

    private void DrawBolt(LineRenderer lr, Vector3 from, Vector3 to, float fade)
    {
        SetLine(lr, 0.9f, new Color(0.8f, 0.55f, 1f, fade));
        lr.startWidth = 1.2f;
        lr.endWidth = 2.6f;
        FillBolt(lr, from, to);
        lr.enabled = true;
    }

    private static void FillBolt(LineRenderer lr, Vector3 from, Vector3 to)
    {
        int n = lr.positionCount;
        float len = Vector3.Distance(from, to);
        for (int i = 0; i < n; i++)
        {
            float f = (float)i / (n - 1);
            Vector3 p = Vector3.Lerp(from, to, f);
            if (i > 0 && i < n - 1) p += Random.insideUnitSphere * len * 0.045f;
            lr.SetPosition(i, p);
        }
    }

    private static void SetLine(LineRenderer lr, float width, Color c)
    {
        lr.startWidth = width;
        lr.endWidth = width;
        lr.startColor = c;
        lr.endColor = c;
    }

    private void OnDestroy()
    {
        if (mat != null) Destroy(mat);
        if (coreMat != null) Destroy(coreMat);
        if (glowMat != null) Destroy(glowMat);
    }
}
