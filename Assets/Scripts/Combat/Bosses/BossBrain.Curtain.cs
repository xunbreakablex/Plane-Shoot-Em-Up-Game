using UnityEngine;

// BossBrain: boss fazina ozgu "perde" saldirisi: tek delikli enerji duvari.
// Boss fazinda oyuncu yalniz kayabildigi (sag-sol, yukari-asagi) icin tek duzlemli yelpaze ve seyrek halkalar capraz
// kaymayla kolayca atlatiliyordu. Perdenin ilk hali 30 m arali bir mermi agiydi; mermilerin arasi bos kaldigindan yerinde
// duran oyuncu cogu zaman hic vurulmuyordu. Simdi: oyuncunun kayabildigi alanin tamamini kaplayan turuncu bir enerji
// duvari bossun hizasinda dogar ve oyuncuya dogru ilerler; tek bir yesil cerceveli deligi vardir. Duvar oyuncunun
// duzlemini gecerken oyuncu delikte degilse hasar alir. Delik once yesil bir cizgi ve HUD'da yesil okla gosterilir; her
// zaman oyuncunun su anki yerinin disinda ve ulasilabilir mesafede acilir.
public partial class BossBrain
{
    private const float CurtainCell = 30f;       // duvar izgarasi (gorsel) (m)
    private const float CurtainHoleHalf = 30f;   // deligin yari kenari (m)
    private const float CurtainBodyMargin = 3f;  // oyuncunun merkezi deligin kenarindan en az bu kadar icerde olmali
    private const float CurtainSpeed = 190f;     // taban hiz (boss carpanlari ayrica uygulanir)
    private const float CurtainBorder = 25f;     // duvar kayma alanindan bu kadar tasar (kenarda bosluk yok)
    private const int CurtainEvery = 4;          // boss fazinda her bu kadar saldiridan biri perde (ara boss)
    private const int CurtainEveryStage = 3;     // asama sonu bosslarinda daha sik
    // Buyuk kure oyuncunun duzlemini bu kadar gecince soner (govdeyi tam gecer; kameranin onunden dev disk gibi gecmez)
    private const float PlanePassMargin = 16f;

    private int attackCount;
    private Vector3 curtainGap;                  // deligin merkezi (dunyada; HUD ve yesil cizgi icin her karede yenilenir)
    private float curtainGapUntil;
    private LineRenderer gapLine;

    // Duvar: cercevede (RushOrigin'e gore) tanimlidir; konumu her karede cerceveden hesaplanir
    private bool wallActive;
    private bool wallPassed;                     // oyuncunun duzlemini gecti (hasar denetimi yapildi)
    private float wallSpawnAt;                   // uyari bitince duvar dogar
    private float wallDepth0;                    // dogdugu andaki ileri mesafe (m); 0 = oyuncunun duzlemi
    private float wallSpeed;
    private Vector2 wallHole;                    // deligin merkezi (sag, yukari; RushOrigin'e gore)
    private Transform wallRoot;
    private Mesh wallMesh, wallFrameMesh;
    private Material wallMat, wallFrameMat;
    private static Texture2D wallGridTex;

    private bool CurtainTurn
    {
        get
        {
            int every = enemy != null && enemy.IsStageBoss ? CurtainEveryStage : CurtainEvery;
            return mgr.RushActive && attackCount % every == every - 1;
        }
    }

    /// <summary>HUD: perdenin deligi (uyari ve duvar oyuncuya varana kadar).</summary>
    public bool CurtainGap(out Vector3 position)
    {
        position = curtainGap;
        return wallActive && !wallPassed && enemy != null && !enemy.IsDead && mgr.RushActive;
    }

    private void Curtain()
    {
        Vector3 right = Vector3.Cross(Vector3.up, mgr.RushHeading).normalized;
        Vector3 rel = player.position - mgr.RushOrigin;
        float px = Vector3.Dot(rel, right), py = rel.y;

        // Delik: oyuncudan 50-80 m yanda (cogunlukla) ya da yukarida/asagida; alanin icinde ve oyuncunun yerini icermez
        float half = CurtainHoleHalf;
        float minX = -CombatManager.RushHalfWidth + half, maxX = CombatManager.RushHalfWidth - half;
        float minY = -CombatManager.RushDown + half, maxY = CombatManager.RushUp - half;
        bool found = false;
        for (int tries = 0; tries < 12 && !found; tries++)
        {
            bool lateral = Random.value < 0.6f;
            float dist = Random.Range(50f, 80f) * (Random.value < 0.5f ? -1f : 1f);
            float jitter = Random.Range(-15f, 15f);
            float hx = Mathf.Clamp(px + (lateral ? dist : jitter), minX, maxX);
            float hy = Mathf.Clamp(py + (lateral ? jitter : dist), minY, maxY);
            if (Mathf.Abs(hx - px) >= half + 8f || Mathf.Abs(hy - py) >= half + 8f)
            {
                wallHole = new Vector2(hx, hy);
                found = true;
            }
        }
        if (!found) wallHole = new Vector2(px > 0f ? minX : maxX, Mathf.Clamp(py, minY, maxY));

        wallSpeed = CurtainSpeed * Mathf.Max(0.05f, GameSettings.Current.bossBulletSpeedMultiplier) * RushBulletSpeed;
        wallSpawnAt = Time.time + ShotWarning;
        wallDepth0 = -1f;                            // dogunca bossun hizasindan baslar
        wallActive = true;
        wallPassed = false;
        UpdateCurtainGap();
        float flight = Vector3.Distance(transform.position, player.position) / Mathf.Max(wallSpeed, 1f);
        curtainGapUntil = Time.time + ShotWarning + flight + 0.25f;

        Sfx.PlayAt(Sfx.Warning, player.position, 0.6f);
        nextAttack = Mathf.Max(nextAttack, Time.time + 2.6f);
    }

    private void UpdateCurtainGap()
    {
        Vector3 right = Vector3.Cross(Vector3.up, mgr.RushHeading).normalized;
        curtainGap = mgr.RushOrigin + right * wallHole.x + Vector3.up * wallHole.y;
    }

    // Her karede (Update): duvari cerceveye gore konumlar, oyuncunun duzlemini gecerken deligi denetler
    private void TickWall()
    {
        if (!wallActive) return;
        if (!mgr.RushActive || enemy == null || enemy.IsDead) { HideWall(); return; }

        UpdateCurtainGap();
        if (Time.time < wallSpawnAt) return;

        Vector3 heading = mgr.RushHeading;
        if (wallDepth0 < 0f)
        {
            // Dogus: bossun hizasinda (en az 60 m onde) ve flasla
            wallDepth0 = Mathf.Max(60f, Vector3.Dot(transform.position - mgr.RushOrigin, heading));
            BuildWall();
            Sfx.PlayAt(Sfx.BossAttack, transform.position, 0.8f);
        }

        float depth = wallDepth0 - wallSpeed * (Time.time - wallSpawnAt);
        if (!wallPassed && depth <= 0f)
        {
            wallPassed = true;
            Vector3 right = Vector3.Cross(Vector3.up, heading).normalized;
            Vector3 rel = player.position - mgr.RushOrigin;
            float dx = Vector3.Dot(rel, right) - wallHole.x, dy = rel.y - wallHole.y;
            float inner = CurtainHoleHalf - CurtainBodyMargin;
            PlaneController plane = mgr.flight != null ? mgr.flight.Current : null;
            if (plane != null && !plane.IsCrashed)
            {
                if (Mathf.Abs(dx) <= inner && Mathf.Abs(dy) <= inner)
                {
                    mgr.FloatText(player.position + Vector3.up * 8f, "BOŞLUKTAN GEÇTİN", new Color(0.4f, 1f, 0.55f), 0.6f);
                    Sfx.PlayAt(Sfx.Pulse, player.position, 0.35f);
                }
                else
                {
                    PlayerHealth ph = mgr.CurrentPlayerHealth;
                    if (ph != null) ph.TakeHit(1, player.position);
                }
            }
        }

        if (depth < -10f) { HideWall(); return; }

        // Duvar cerceveyle birlikte ilerler; yaklastikca belirginlesir
        wallRoot.SetPositionAndRotation(mgr.RushOrigin + heading * depth, Quaternion.LookRotation(heading, Vector3.up));
        float near = 1f - Mathf.Clamp01(depth / Mathf.Max(1f, wallDepth0));
        float pulse = 0.85f + 0.15f * Mathf.Sin(Time.time * 18f);
        SetAlpha(wallMat, new Color(1f, 0.45f, 0.15f), (0.35f + 0.4f * near) * pulse);
        SetAlpha(wallFrameMat, new Color(0.3f, 1f, 0.45f), 0.95f * pulse);
    }

    private static void SetAlpha(Material m, Color c, float a)
    {
        if (m == null) return;
        c.a = a;
        m.color = c;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
    }

    private void HideWall()
    {
        wallActive = false;
        if (wallRoot != null) wallRoot.gameObject.SetActive(false);
    }

    // Duvar ag kafesi: delik etrafinda dort serit (izgara dokulu) + deligin yesil cercevesi
    private void BuildWall()
    {
        if (wallRoot == null)
        {
            var go = new GameObject("CurtainWall");
            wallRoot = go.transform;   // boss'a bagli degil: boss donse de duvar cercevede duz kalir
            wallMesh = new Mesh { name = "CurtainWall" };
            wallFrameMesh = new Mesh { name = "CurtainFrame" };
            Material src = mgr.fxMaterial != null ? mgr.fxMaterial : mgr.bossGlow;
            wallMat = new Material(src) { mainTexture = GridTexture() };
            wallFrameMat = new Material(src) { mainTexture = Texture2D.whiteTexture };
            AddMeshChild(wallRoot, "Grid", wallMesh, wallMat);
            AddMeshChild(wallRoot, "HoleFrame", wallFrameMesh, wallFrameMat);
        }
        wallRoot.gameObject.SetActive(true);

        float x0 = -CombatManager.RushHalfWidth - CurtainBorder, x1 = CombatManager.RushHalfWidth + CurtainBorder;
        float y0 = -CombatManager.RushDown - CurtainBorder, y1 = CombatManager.RushUp + CurtainBorder;
        float hx0 = wallHole.x - CurtainHoleHalf, hx1 = wallHole.x + CurtainHoleHalf;
        float hy0 = wallHole.y - CurtainHoleHalf, hy1 = wallHole.y + CurtainHoleHalf;

        var v = new System.Collections.Generic.List<Vector3>(16);
        var uv = new System.Collections.Generic.List<Vector2>(16);
        var tri = new System.Collections.Generic.List<int>(48);
        AddQuad(v, uv, tri, x0, hx0, y0, y1, true);      // sol
        AddQuad(v, uv, tri, hx1, x1, y0, y1, true);      // sag
        AddQuad(v, uv, tri, hx0, hx1, y0, hy0, true);    // alt
        AddQuad(v, uv, tri, hx0, hx1, hy1, y1, true);    // ust
        wallMesh.Clear();
        wallMesh.SetVertices(v);
        wallMesh.SetUVs(0, uv);
        wallMesh.SetTriangles(tri, 0);
        wallMesh.RecalculateBounds();

        const float t = 2.5f;
        v.Clear(); uv.Clear(); tri.Clear();
        AddQuad(v, uv, tri, hx0 - t, hx0, hy0 - t, hy1 + t, false);
        AddQuad(v, uv, tri, hx1, hx1 + t, hy0 - t, hy1 + t, false);
        AddQuad(v, uv, tri, hx0, hx1, hy0 - t, hy0, false);
        AddQuad(v, uv, tri, hx0, hx1, hy1, hy1 + t, false);
        wallFrameMesh.Clear();
        wallFrameMesh.SetVertices(v);
        wallFrameMesh.SetUVs(0, uv);
        wallFrameMesh.SetTriangles(tri, 0);
        wallFrameMesh.RecalculateBounds();
    }

    private static void AddMeshChild(Transform parent, string name, Mesh mesh, Material mat)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
    }

    // Dikdortgen (yerel x sag, y yukari); izgara UV'si dunya metresine gore (hucreler seritler arasinda hizali)
    private static void AddQuad(System.Collections.Generic.List<Vector3> v, System.Collections.Generic.List<Vector2> uv,
        System.Collections.Generic.List<int> tri, float xa, float xb, float ya, float yb, bool gridUv)
    {
        if (xb - xa < 0.01f || yb - ya < 0.01f) return;
        int i = v.Count;
        v.Add(new Vector3(xa, ya, 0f)); v.Add(new Vector3(xb, ya, 0f));
        v.Add(new Vector3(xa, yb, 0f)); v.Add(new Vector3(xb, yb, 0f));
        if (gridUv)
        {
            uv.Add(new Vector2(xa, ya) / CurtainCell); uv.Add(new Vector2(xb, ya) / CurtainCell);
            uv.Add(new Vector2(xa, yb) / CurtainCell); uv.Add(new Vector2(xb, yb) / CurtainCell);
        }
        else
        {
            uv.Add(new Vector2(0f, 0f)); uv.Add(new Vector2(1f, 0f));
            uv.Add(new Vector2(0f, 1f)); uv.Add(new Vector2(1f, 1f));
        }
        // Oyuncunun kamerasindan (ileri bakarken) on yuz; Sprites/Default iki yuzu de cizer
        tri.Add(i); tri.Add(i + 2); tri.Add(i + 1);
        tri.Add(i + 2); tri.Add(i + 3); tri.Add(i + 1);
    }

    // Izgara dokusu: hafif dolgu + parlak hucre cizgileri (malzeme rengiyle boyanir)
    private static Texture2D GridTexture()
    {
        if (wallGridTex != null) return wallGridTex;
        const int size = 64;
        wallGridTex = new Texture2D(size, size, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat };
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            int ex = Mathf.Min(x, size - 1 - x), ey = Mathf.Min(y, size - 1 - y);
            int e = Mathf.Min(ex, ey);
            float a = e < 2 ? 1f : (e < 4 ? 0.55f : 0.16f);
            wallGridTex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
        }
        wallGridTex.Apply();
        return wallGridTex;
    }

    private void OnDestroy()
    {
        if (wallRoot != null) Destroy(wallRoot.gameObject);
        if (wallMesh != null) Destroy(wallMesh);
        if (wallFrameMesh != null) Destroy(wallFrameMesh);
        if (wallMat != null) Destroy(wallMat);
        if (wallFrameMat != null) Destroy(wallFrameMat);
        // Ki dalgasi hunisi de bagimsiz bir nesne
        if (kiWaveTube != null) Destroy(kiWaveTube.gameObject);
        if (kiWaveTubeMesh != null) Destroy(kiWaveTubeMesh);
        if (kiWaveTubeMat != null) Destroy(kiWaveTubeMat);
    }

    /// <summary>Bomba: yaklasan enerji duvarini dagitir.</summary>
    public void OnBomb()
    {
        if (wallActive) HideWall();
        curtainGapUntil = 0f;
    }

    // Uyari: bosstan delige uzanan yesil cizgi (DrawWarns ile her karede)
    private void DrawGapLine()
    {
        bool on = wallActive && !wallPassed && Time.time < curtainGapUntil && enemy != null && !enemy.IsDead;
        if (!on)
        {
            if (gapLine != null) gapLine.enabled = false;
            return;
        }

        if (gapLine == null)
        {
            gapLine = MakeWarnLine();
            gapLine.name = "CurtainGap";
            Color c = new Color(0.3f, 1f, 0.45f);
            gapLine.sharedMaterial.color = c;
            if (gapLine.sharedMaterial.HasProperty("_BaseColor")) gapLine.sharedMaterial.SetColor("_BaseColor", c);
        }
        float width = 1.1f * (0.75f + 0.25f * Mathf.Sin(Time.time * 20f));
        gapLine.enabled = true;
        gapLine.SetPosition(0, transform.position);
        gapLine.SetPosition(1, curtainGap);
        gapLine.startWidth = width;
        gapLine.endWidth = width;
    }
}
