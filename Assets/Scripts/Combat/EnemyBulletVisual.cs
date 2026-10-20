using UnityEngine;

// Dusman mermisinin okunabilir gorunumu: parlak cekirdek + kirmizi hale + koyu kenar.
// Boyut mesafeyle buyur: eskiden mermi 0,18 m'lik bir cubuktu ve 100 m otesinde yarim pikseldi.
// Kamera mesafesine ve gorus acisina gore ekranin belirli bir orani kadar gorunur kalir.
// Oyuncu mermilerinden (sari, uzun iz) sekliyle de ayrilir: yuvarlak kirmizi top.
public class EnemyBulletVisual : MonoBehaviour
{
    // Ekran yuksekliginin orani olarak cekirdek capi (720p'de ~12 piksel)
    private const float ScreenFraction = 0.017f;
    private const float MinCore = 1.4f;     // yakinda en az vurus yaricapi kadar (merminin vurus capi 2 m)
    private const float MaxCore = 9f;       // cok uzakta sinirsiz buyumesin

    private static Mesh sphereMesh;
    private static Mesh quadMesh;
    private static Material rimMat, haloMat, coreMat, bigCoreMat;
    private static Camera cam;

    private Transform core, halo, rim, tracer;
    private Bullet bullet;
    private Renderer coreRenderer;
    private bool coreBig;
    private float phase;

    /// <summary>Mermi prefab'indaki ince cubuk izi korur, uzerine top gorunumu ekler.</summary>
    public static void Attach(Bullet bullet)
    {
        if (bullet == null) return;

        EnsureMeshes();

        Transform tr = bullet.transform;
        Transform tracerTr = tr.Find("Tracer");
        Material baseMat = null;
        if (tracerTr != null)
        {
            var r = tracerTr.GetComponent<Renderer>();
            if (r != null) baseMat = r.sharedMaterial;
        }

        CombatManager cm = CombatManager.Instance;
        Material softMat = cm != null ? cm.fxMaterial : null;

        var v = bullet.gameObject.AddComponent<EnemyBulletVisual>();
        v.tracer = tracerTr;
        v.bullet = bullet;
        v.phase = Random.value * 6.28f;

        // Malzemeler bir kez uretilir ve tum mermilerde paylasilir (mermi basina yeni malzeme sizdirir)
        if (rimMat == null) rimMat = Tinted(softMat, new Color(0.18f, 0f, 0.05f, 0.55f));
        if (haloMat == null) haloMat = Tinted(softMat, new Color(1f, 0.12f, 0.2f, 0.95f));
        if (coreMat == null) coreMat = Tinted(baseMat != null ? baseMat : softMat, new Color(1f, 0.93f, 0.8f, 1f));
        if (bigCoreMat == null) bigCoreMat = Tinted(baseMat != null ? baseMat : softMat, new Color(1f, 0.42f, 0.22f, 1f));

        // Koyu kenar: acik bulut/gokyuzu uzerinde de ayrilsin; hale kirmizi; cekirdek parlak
        v.rim = MakeLayer("Rim", tr, quadMesh, rimMat);
        v.halo = MakeLayer("Halo", tr, quadMesh, haloMat);
        v.core = MakeLayer("Core", tr, sphereMesh, coreMat);

        v.Resize();
    }

    private static Material Tinted(Material source, Color color)
    {
        if (source == null) return null;
        var m = new Material(source) { color = color };
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
        return m;
    }

    private static Transform MakeLayer(string name, Transform parent, Mesh mesh, Material mat)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;

        var mr = go.AddComponent<MeshRenderer>();
        if (mat != null) mr.sharedMaterial = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        return go.transform;
    }

    // Collider'siz mesh'ler: primitive'in collider'i Destroy ile gecikmeli silinir ve oyuncuya carpar
    private static void EnsureMeshes()
    {
        if (sphereMesh != null && quadMesh != null) return;

        var s = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphereMesh = s.GetComponent<MeshFilter>().sharedMesh;
        DestroyImmediate(s);

        var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quadMesh = q.GetComponent<MeshFilter>().sharedMesh;
        DestroyImmediate(q);
    }

    private void LateUpdate()
    {
        Resize();
    }

    private void Resize()
    {
        if (cam == null) cam = Camera.main;
        if (cam == null || core == null) return;

        float dist = Vector3.Distance(cam.transform.position, transform.position);
        float tanHalf = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
        // Yakinda cekirdek en az vurus capinin %85'i: buyuk mermi (perde) ekranda da buyuk gorunur, gorunmeyen yerden vurmaz
        float minCore = Mathf.Max(MinCore, bullet != null ? bullet.radius * 1.7f : MinCore);
        float size = Mathf.Clamp(ScreenFraction * 2f * dist * tanHalf, minCore, Mathf.Max(MaxCore, minCore));

        float pulse = 1f + 0.1f * Mathf.Sin(Time.time * 16f + phase);

        // Buyuk mermide (perde kuresi) hale ve kenar dar: cekirdek zaten buyuk, isilti ekrani kaplamasin
        bool big = bullet != null && bullet.radius > 1.5f;
        if (big != coreBig)
        {
            // Buyuk kure (perde) turuncu-kirmizi cekirdekle tehlike olarak okunsun; havuzdan donunce normale doner
            coreBig = big;
            if (coreRenderer == null) coreRenderer = core.GetComponent<Renderer>();
            Material m = big ? bigCoreMat : coreMat;
            if (coreRenderer != null && m != null) coreRenderer.sharedMaterial = m;
        }
        core.localScale = Vector3.one * size;
        halo.localScale = Vector3.one * (size * (big ? 1.5f : 3.1f) * pulse);
        rim.localScale = Vector3.one * (size * (big ? 1.9f : 4.0f) * pulse);

        // Top kameraya doner (billboard); izdusumde yuvarlak kalir
        Quaternion face = cam.transform.rotation;
        halo.rotation = face;
        rim.rotation = face;

        // Iz: topun arkasina uzanan ince cubuk; yon hissi verir
        if (tracer != null)
        {
            float len = Mathf.Max(6f, size * 3f);
            float w = Mathf.Max(0.18f, size * 0.22f);
            tracer.localScale = new Vector3(w, w, len);
            tracer.localPosition = new Vector3(0f, 0f, -len * 0.5f);
        }
    }
}
