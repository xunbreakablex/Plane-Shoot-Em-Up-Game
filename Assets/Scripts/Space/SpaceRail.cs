using System.Collections.Generic;
using UnityEngine;

// Uzay prototipi: dogrusal (raylı) koridor. Gemi bu nesnenin ileri yonunde kendiliginden ilerler,
// WASD ile yalnizca koridorun kesitinde (sag-sol, yukari-asagi) hareket eder. Koridorun cevresi
// meteor duvarlariyla kaplidir; gemi koridordan cikamaz, duvara degerse hasar alir.
// Meteorlar geminin onunde parca parca (segment) uretilir, geride kalanlar geri donusturulur.
// Ayarlar: GameSettings.space.
public class SpaceRail : MonoBehaviour
{
    public static SpaceRail Instance { get; private set; }
    public static bool Active => Instance != null && Instance.isActiveAndEnabled;

    [Tooltip("Meteor malzemesi (URP Lit). Bos birakilirsa kodla uretilir.")]
    public Material meteorMaterial;
    [Tooltip("Meteor yerlesimi icin tohum: ayni tohum = ayni koridor")]
    public int seed = 1907;

    // ---------------------------------------------------------- koridor durumu (gemiler paylasir)

    /// <summary>Koridor boyunca kat edilen mesafe (m).</summary>
    [System.NonSerialized] public float Distance;
    /// <summary>Kesitteki konum: x sag, y yukari (m).</summary>
    [System.NonSerialized] public Vector2 Offset;
    /// <summary>Kesitteki hiz (m/s).</summary>
    [System.NonSerialized] public Vector2 OffsetVelocity;

    public Vector3 Origin => origin;
    public Vector3 Forward => forward;
    public Vector3 Right => right;
    public Vector3 Up => up;

    private Vector3 origin, forward, right, up;

    private static SpacePrototypeSettings Cfg => GameSettings.Current.space;

    // ------------------------------------------------------------------- meteorlar

    private struct Meteor
    {
        public Transform t;
        public float radius;
        public Vector3 spinAxis;
        public float spinSpeed;
    }

    private readonly Dictionary<int, List<Meteor>> segments = new Dictionary<int, List<Meteor>>();
    private readonly Stack<Meteor> pool = new Stack<Meteor>();
    private readonly List<int> toRemove = new List<int>();
    private Mesh[] rockMeshes;
    private Transform meteorRoot;

    // ------------------------------------------------------------------- yasam dongusu

    private void Awake()
    {
        Instance = this;

        origin = transform.position;
        forward = transform.forward;
        right = transform.right;
        up = transform.up;

        Distance = 0f;
        Offset = Vector2.zero;
        OffsetVelocity = Vector2.zero;

        rockMeshes = new Mesh[8];
        var rnd = new System.Random(seed);
        for (int i = 0; i < rockMeshes.Length; i++)
            rockMeshes[i] = MakeRockMesh(rnd);

        if (meteorMaterial == null)
        {
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            meteorMaterial = new Material(lit != null ? lit : Shader.Find("Standard")) { color = new Color(0.36f, 0.32f, 0.3f) };
        }
        meteorMaterial.enableInstancing = true;

        meteorRoot = new GameObject("Meteors").transform;
        meteorRoot.SetParent(transform, false);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Start()
    {
        // Oyuncu ucaklari koridora baglanir: konum/donus RailShipController'dan gelir
        FlightTestManager flight = FindFirstObjectByType<FlightTestManager>();
        if (flight != null && flight.planes != null)
        {
            foreach (PlaneController p in flight.planes)
            {
                if (p == null) continue;
                p.SetSpawn(origin, Quaternion.LookRotation(forward, up));
                if (p.GetComponent<RailShipController>() == null) p.gameObject.AddComponent<RailShipController>();
            }
        }

        CreateSpaceDust();
        UpdateSegments();
    }

    private void Update()
    {
        UpdateSegments();

        // Meteorlar yavasca doner
        float dt = Time.deltaTime;
        foreach (var kv in segments)
        {
            List<Meteor> list = kv.Value;
            for (int i = 0; i < list.Count; i++)
                list[i].t.Rotate(list[i].spinAxis, list[i].spinSpeed * dt, Space.Self);
        }
    }

    // ------------------------------------------------------------------- koridor geometrisi

    /// <summary>Koridor koordinatina cevirir: x sag, y yukari, z ileri (koridor boyunca mesafe).</summary>
    public Vector3 Local(Vector3 world)
    {
        Vector3 d = world - origin;
        return new Vector3(Vector3.Dot(d, right), Vector3.Dot(d, up), Vector3.Dot(d, forward));
    }

    public Vector3 World(float x, float y, float z)
    {
        return origin + right * x + up * y + forward * z;
    }

    public Vector3 PointAt(float distance, Vector2 offset)
    {
        return World(offset.x, offset.y, distance);
    }

    /// <summary>Kesitte elips icine kirpar (yari eksenler koridor - margin).</summary>
    public static Vector2 ClampToEllipse(Vector2 p, float a, float b)
    {
        a = Mathf.Max(1f, a);
        b = Mathf.Max(1f, b);
        float e = (p.x / a) * (p.x / a) + (p.y / b) * (p.y / b);
        return e > 1f ? p / Mathf.Sqrt(e) : p;
    }

    /// <summary>Dunya konumunu koridorun icinde tutar (dusman/boss hareketi).</summary>
    public Vector3 ClampToCorridor(Vector3 world, float margin)
    {
        Vector3 l = Local(world);
        Vector2 c = ClampToEllipse(new Vector2(l.x, l.y), Cfg.corridorHalfWidth - margin, Cfg.corridorHalfHeight - margin);
        return World(c.x, c.y, l.z);
    }

    /// <summary>
    /// Dogus konumunu koridora sikistirir: merkeze yakin ofsetler korunur, uzaktakiler duvarin icine
    /// yumusakca bastirilir (formasyon dizilimi bozulmadan daralir).
    /// </summary>
    public Vector3 CompressIntoCorridor(Vector3 world, float margin)
    {
        Vector3 l = Local(world);
        float a = Mathf.Max(4f, (Cfg.corridorHalfWidth - margin) * 0.9f);
        float b = Mathf.Max(4f, (Cfg.corridorHalfHeight - margin) * 0.9f);
        float x = a * l.x / (Mathf.Abs(l.x) + a);
        float y = b * l.y / (Mathf.Abs(l.y) + b);
        Vector2 c = ClampToEllipse(new Vector2(x, y), a, b);
        return World(c.x, c.y, l.z);
    }

    /// <summary>Verilen kurenin bir meteora degip degmedigi (geminin yakinindaki segmentlere bakar).</summary>
    public bool TryMeteorContact(Vector3 world, float radius, out Vector3 meteorCenter)
    {
        meteorCenter = Vector3.zero;
        float L = Mathf.Max(1f, Cfg.segmentLength);
        int seg = Mathf.FloorToInt(Local(world).z / L);

        for (int s = seg - 1; s <= seg + 1; s++)
        {
            if (!segments.TryGetValue(s, out List<Meteor> list)) continue;
            for (int i = 0; i < list.Count; i++)
            {
                Meteor m = list[i];
                float r = m.radius * 0.85f + radius;
                if ((m.t.position - world).sqrMagnitude < r * r)
                {
                    meteorCenter = m.t.position;
                    return true;
                }
            }
        }
        return false;
    }

    // ------------------------------------------------------------------- segmentler

    private void UpdateSegments()
    {
        SpacePrototypeSettings cfg = Cfg;
        float L = Mathf.Max(20f, cfg.segmentLength);
        int first = Mathf.FloorToInt((Distance - 150f) / L);
        int last = Mathf.FloorToInt((Distance + cfg.viewAhead) / L);

        // Geride kalanlari geri donustur
        toRemove.Clear();
        foreach (var kv in segments)
            if (kv.Key < first || kv.Key > last) toRemove.Add(kv.Key);
        foreach (int k in toRemove)
        {
            foreach (Meteor m in segments[k])
            {
                m.t.gameObject.SetActive(false);
                pool.Push(m);
            }
            segments.Remove(k);
        }

        for (int s = Mathf.Max(first, 0); s <= last; s++)
            if (!segments.ContainsKey(s)) BuildSegment(s, cfg, L);
    }

    private void BuildSegment(int index, SpacePrototypeSettings cfg, float L)
    {
        var rnd = new System.Random(seed * 7919 + index * 104729);
        var list = new List<Meteor>(cfg.meteorsPerSegment);

        float W = cfg.corridorHalfWidth, H = cfg.corridorHalfHeight;
        for (int k = 0; k < cfg.meteorsPerSegment; k++)
        {
            float theta = (float)rnd.NextDouble() * Mathf.PI * 2f;
            // Cogu kucuk, azi buyuk
            float radius = Mathf.Lerp(cfg.meteorRadius.x, cfg.meteorRadius.y, Mathf.Pow((float)rnd.NextDouble(), 2.2f));

            // Elips sinirindaki nokta ve disa dogru normal
            float c = Mathf.Cos(theta), s = Mathf.Sin(theta);
            Vector2 boundary = new Vector2(W * c, H * s);
            Vector2 normal = new Vector2(c / W, s / H).normalized;

            // Merkez sinirin disinda; u < 1 ise meteorun bir kismi koridora tasar (duvara degmek tehlikeli)
            float u = Mathf.Lerp(0.78f, 1.25f, (float)rnd.NextDouble());
            float depth = Mathf.Pow((float)rnd.NextDouble(), 1.6f) * cfg.wallThickness;
            Vector2 p = boundary + normal * (radius * u + depth);

            float z = index * L + (float)rnd.NextDouble() * L;

            Meteor m = pool.Count > 0 ? pool.Pop() : CreateMeteor();
            m.radius = radius;
            m.t.position = World(p.x, p.y, z);
            m.t.rotation = Quaternion.Euler((float)rnd.NextDouble() * 360f, (float)rnd.NextDouble() * 360f, (float)rnd.NextDouble() * 360f);
            m.t.localScale = Vector3.one * radius;
            m.t.GetComponent<MeshFilter>().sharedMesh = rockMeshes[rnd.Next(rockMeshes.Length)];
            m.spinAxis = new Vector3((float)rnd.NextDouble() - 0.5f, (float)rnd.NextDouble() - 0.5f, (float)rnd.NextDouble() - 0.5f).normalized;
            m.spinSpeed = Mathf.Lerp(4f, 22f, (float)rnd.NextDouble()) * (rnd.Next(2) == 0 ? -1f : 1f);
            m.t.gameObject.SetActive(true);
            list.Add(m);
        }

        segments[index] = list;
    }

    private Meteor CreateMeteor()
    {
        var go = new GameObject("Meteor");
        go.transform.SetParent(meteorRoot, false);
        go.AddComponent<MeshFilter>().sharedMesh = rockMeshes[0];
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = meteorMaterial;

        // Mermiler meteorlara carpar (siper). Oyuncu kinematik oldugu icin fiziksel itme yok; temas mesafeden.
        var col = go.AddComponent<SphereCollider>();
        col.radius = 0.85f;

        return new Meteor { t = go.transform, radius = 1f };
    }

    // Dusuk poligonlu kaya: ikosahedron (1 kez bolunmus) + gurultuyle girintili cikintili, duz golgeli
    private static Mesh MakeRockMesh(System.Random rnd)
    {
        float t = (1f + Mathf.Sqrt(5f)) * 0.5f;
        var v = new List<Vector3>
        {
            new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
            new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
            new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1),
        };
        for (int i = 0; i < v.Count; i++) v[i] = v[i].normalized;

        int[] f =
        {
            0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
            3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1
        };

        // Bir kez bol: her kenarin ortasi kureye itilir
        var mid = new Dictionary<long, int>();
        int Mid(int a, int b)
        {
            long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
            if (mid.TryGetValue(key, out int idx)) return idx;
            v.Add(((v[a] + v[b]) * 0.5f).normalized);
            mid[key] = v.Count - 1;
            return v.Count - 1;
        }

        var tris = new List<int>();
        for (int i = 0; i < f.Length; i += 3)
        {
            int a = f[i], b = f[i + 1], c = f[i + 2];
            int ab = Mid(a, b), bc = Mid(b, c), ca = Mid(c, a);
            tris.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
        }

        // Girinti/cikinti: birkac rastgele dalganin toplami + eksenlerde hafif basiklik
        Vector3[] dirs = new Vector3[4];
        float[] phase = new float[4];
        for (int k = 0; k < 4; k++)
        {
            dirs[k] = new Vector3((float)rnd.NextDouble() - 0.5f, (float)rnd.NextDouble() - 0.5f, (float)rnd.NextDouble() - 0.5f).normalized;
            phase[k] = (float)rnd.NextDouble() * 6.28f;
        }
        Vector3 squash = new Vector3(Mathf.Lerp(0.75f, 1.15f, (float)rnd.NextDouble()),
            Mathf.Lerp(0.7f, 1.05f, (float)rnd.NextDouble()), Mathf.Lerp(0.8f, 1.2f, (float)rnd.NextDouble()));

        for (int i = 0; i < v.Count; i++)
        {
            Vector3 p = v[i];
            float n = 0f;
            for (int k = 0; k < 4; k++) n += Mathf.Sin(Vector3.Dot(p, dirs[k]) * (2.5f + k * 1.7f) + phase[k]) / (1f + k);
            p *= 1f + 0.16f * n + ((float)rnd.NextDouble() - 0.5f) * 0.12f;
            v[i] = Vector3.Scale(p, squash);
        }

        // Duz golge: her ucgene ayri kose
        var verts = new Vector3[tris.Count];
        var idxs = new int[tris.Count];
        for (int i = 0; i < tris.Count; i++)
        {
            verts[i] = v[tris[i]];
            idxs[i] = i;
        }

        var mesh = new Mesh { name = "Rock" };
        mesh.vertices = verts;
        mesh.triangles = idxs;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    // ------------------------------------------------------------------- uzay tozu

    // Hiz hissi: kameranin onunde dogan ve yanindan akip gecen toz zerrecikleri (kamera hiziyla uzayan cizgiler)
    private void CreateSpaceDust()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        var go = new GameObject("SpaceDust");
        go.transform.SetParent(cam.transform, false);
        go.transform.localPosition = new Vector3(0f, 0f, 260f);

        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.loop = true;
        main.startLifetime = 5f;
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.9f);
        main.startColor = new Color(0.75f, 0.85f, 1f, 0.55f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 3000;

        var em = ps.emission;
        em.rateOverTime = 260f;

        var sh = ps.shape;
        sh.shapeType = ParticleSystemShapeType.Box;
        sh.scale = new Vector3(260f, 170f, 40f);

        var r = go.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.Stretch;
        r.cameraVelocityScale = 0.04f;
        r.lengthScale = 1f;
        r.velocityScale = 0f;
        Material fx = CombatManager.Instance != null ? CombatManager.Instance.fxMaterial : null;
        if (fx != null) r.sharedMaterial = fx;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;

        ps.Play();
    }
}
