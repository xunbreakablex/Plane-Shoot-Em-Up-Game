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
    private static Material rimMat, haloMat, coreMat, bigCoreMat, starCoreMat, starHaloMat;
    private static Material kiCoreMat, kiHaloMat, yellowCoreMat, yellowHaloMat, darkCoreMat, darkHaloMat;
    private bool visible = true;
    private static Camera cam;

    private Transform core, halo, rim, tracer;
    private Bullet bullet;
    private Renderer coreRenderer, haloRenderer;
    private int look = -1;          // 0 normal, 1 buyuk kure (perde), 2 pembe yildiz (cark)
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
        if (starCoreMat == null) starCoreMat = Tinted(baseMat != null ? baseMat : softMat, new Color(1f, 0.38f, 0.82f, 1f));
        if (starHaloMat == null) starHaloMat = Tinted(softMat, new Color(0.95f, 0.1f, 0.62f, 0.95f));
        if (kiCoreMat == null) kiCoreMat = Tinted(baseMat != null ? baseMat : softMat, new Color(0.75f, 0.95f, 1f, 1f));
        if (kiHaloMat == null) kiHaloMat = Tinted(softMat, new Color(0.2f, 0.55f, 1f, 0.95f));
        if (yellowCoreMat == null) yellowCoreMat = Tinted(baseMat != null ? baseMat : softMat, new Color(1f, 0.98f, 0.7f, 1f));
        if (yellowHaloMat == null) yellowHaloMat = Tinted(softMat, new Color(1f, 0.75f, 0.1f, 0.95f));
        if (darkCoreMat == null) darkCoreMat = Tinted(baseMat != null ? baseMat : softMat, new Color(0.85f, 0.55f, 1f, 1f));
        if (darkHaloMat == null) darkHaloMat = Tinted(softMat, new Color(0.45f, 0.1f, 0.8f, 0.95f));

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

    /// <summary>Oyuncu mermisi havuzdan standart olarak donunce top katmanlari gizlenir, iz geri gelir.</summary>
    public void SetVisible(bool on)
    {
        if (visible == on && core != null && core.gameObject.activeSelf == on) return;
        visible = on;
        if (core != null) core.gameObject.SetActive(on);
        if (halo != null) halo.gameObject.SetActive(on);
        if (rim != null) rim.gameObject.SetActive(on);
        if (!on)
        {
            look = -1;
            if (tracer != null) tracer.gameObject.SetActive(true);
        }
    }

    private void LateUpdate()
    {
        if (!visible) return;
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
        // 0 normal, 1 buyuk kure (perde), 2 pembe yildiz, 3 mavi ki, 4 sari ki, 5 mor kure (kahraman mermileri de bunlari kullanir)
        int want = bullet != null && bullet.look == 1 ? 2 : (bullet != null && bullet.look >= 2 ? bullet.look : (big ? 1 : 0));
        if (want != look)
        {
            // Buyuk kure (perde) turuncu-kirmizi cekirdekle tehlike olarak okunsun, yildiz carki pembe; havuzdan
            // donunce normale doner
            look = want;
            if (coreRenderer == null) coreRenderer = core.GetComponent<Renderer>();
            if (haloRenderer == null) haloRenderer = halo.GetComponent<Renderer>();
            Material cm, hm;
            switch (want)
            {
                case 2: cm = starCoreMat; hm = starHaloMat; break;
                case 3: cm = kiCoreMat; hm = kiHaloMat; break;
                case 4: cm = yellowCoreMat; hm = yellowHaloMat; break;
                case 5: cm = darkCoreMat; hm = darkHaloMat; break;
                case 1: cm = bigCoreMat; hm = haloMat; break;
                default: cm = coreMat; hm = haloMat; break;
            }
            if (coreRenderer != null && cm != null) coreRenderer.sharedMaterial = cm;
            if (haloRenderer != null && hm != null) haloRenderer.sharedMaterial = hm;
            if (tracer != null) tracer.gameObject.SetActive(want < 2);   // yildiz/kurenin izi yok
        }
        bool magic = want >= 2;
        // Kameraya cok yakin (oyuncuyu gecmis) mermi ekrani kaplamasin: 30 m'den yakinda hale ve cekirdek kuculur
        // (cark yildizlari kameranin dibinden gecerken dev pembe toplar olup boss'u gizliyordu). Isabet yaricapi ayni.
        float near = Mathf.Clamp01((dist - 4f) / 26f);
        float coreNear = Mathf.Lerp(0.55f, 1f, near), glowNear = Mathf.Lerp(0.35f, 1f, near);
        core.localScale = Vector3.one * size * coreNear;
        halo.localScale = Vector3.one * (size * (big ? 1.5f : (magic ? 2.4f : 3.1f)) * pulse * glowNear);
        rim.localScale = Vector3.one * (size * (big ? 1.9f : (magic ? 2.9f : 4.0f)) * pulse * glowNear);

        // Top kameraya doner (billboard); izdusumde yuvarlak kalir
        Quaternion face = cam.transform.rotation;
        halo.rotation = face;
        rim.rotation = face;

        // Iz: topun arkasina uzanan ince cubuk; yon hissi verir
        if (tracer != null && want != 2)
        {
            // Buyuk kurede iz kisa ve ince: dev kirmizi cubuklar ekrani doldurmasin
            float len = Mathf.Max(6f, size * (big ? 1.2f : 3f));
            float w = Mathf.Max(0.18f, size * (big ? 0.1f : 0.22f));
            tracer.localScale = new Vector3(w, w, len);
            tracer.localPosition = new Vector3(0f, 0f, -len * 0.5f);
        }
    }
}
