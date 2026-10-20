using System.Collections.Generic;
using UnityEngine;

// CombatManager'in dusman ussu kismi: asama basinda ussu kurar (kara pedi ya da deniz ustunde ada), gerekirse oyuncuyu
// denizdeki baslangic noktasina isinlar ("denizi gec"), komuta merkezi yikilinca ussu susturur.
// Yapi yerlesimi BaseBuilder'da, ates/davranis GroundUnit'te, asama verisi Stages.cs'de (BaseDef).
public partial class CombatManager
{
    public const float SeaLevel = 4f;

    private GameObject baseRoot;
    private EnemyPlane baseCore;
    private bool baseCleared = true;
    private bool waterPrepared;
    private BaseDef baseDef;
    private static Material rubbleMat;
    private readonly Hud.TextCache baseText = new Hud.TextCache();

    // Savas alani: yumusak sinir. Disina cikinca uyari, GameSettings.arenaGraceSeconds sonra hasar.
    private Vector2 arenaCenter;
    private float arenaRadius = 3000f;
    private float outsideSince = -1f;
    private float nextArenaHit;

    public bool OutsideArena { get; private set; }
    public float OutsideSeconds => outsideSince >= 0f ? Time.time - outsideSince : 0f;

    /// <summary>Komuta merkezi yikildi mi (ya da bu asamada us yok)? Yikilinca uc aksavarlar susar, boss gelebilir.</summary>
    public bool BaseCleared => baseCleared;
    public EnemyPlane BaseCore => baseCore;

    public int BaseUnitsLeft
    {
        get
        {
            int n = 0;
            foreach (var e in enemies)
                if (e != null && !e.IsDead && e.IsGround) n++;
            return n;
        }
    }

    /// <summary>Yer birimleri haric canli hava dusmani sayisi (boss'un gelmesi icin "ortalik sakin mi").</summary>
    private int AirEnemyCount()
    {
        int n = 0;
        foreach (var e in enemies)
            if (e != null && !e.IsDead && !e.IsGround) n++;
        return n;
    }

    // Deniz yuzeyi arazinin disindaki ada ussleri icin yeterince genis olmali
    private void EnsureWater()
    {
        if (waterPrepared) return;
        waterPrepared = true;

        GameObject water = GameObject.Find("Water");
        if (water == null) return;
        Vector3 s = water.transform.localScale;
        // Boss fazinda acik denizde haritanin cevresinde dolasilir: deniz, sisin otesinde kenari gorunmeyecek kadar genis
        water.transform.localScale = new Vector3(Mathf.Max(s.x, 90000f), s.y, Mathf.Max(s.z, 90000f));
    }

    /// <summary>Asama basinda: eski ussu kaldirir, gerekirse oyuncuyu denize isinlar, yeni ussu kurar.</summary>
    private void PrepareStageWorld(int index)
    {
        EnsureWater();

        // Onceki asamadan kalan guclendirmeler yeni asamada (ve denizde) isaret olarak gorunmesin
        foreach (var pu in PowerUp.All.ToArray())
            if (pu != null) Destroy(pu.gameObject);

        if (baseRoot != null) Destroy(baseRoot);
        baseRoot = null;
        baseCore = null;
        foreach (var ship in shipRoots) if (ship != null) Destroy(ship);
        shipRoots.Clear();

        baseDef = stage.bas;
        baseCleared = !baseDef.enabled;
        if (!baseDef.enabled) return;

        PlaneController plane = flight != null ? flight.Current : null;
        if (plane == null) return;

        Vector3 center = new Vector3(baseDef.center.x, 0f, baseDef.center.y);

        // Deniz gecisi: oyuncu denizdeki baslangic noktasina isinlanir ve ussun yonune bakar
        if (baseDef.hasStart)
        {
            Vector3 start = new Vector3(baseDef.startXZ.x, baseDef.startAlt, baseDef.startXZ.y);
            Vector3 dir = center - start;
            dir.y = 0f;
            plane.Teleport(start, Quaternion.LookRotation(dir.normalized, Vector3.up));

            FollowCamera fc = cam != null ? cam.GetComponent<FollowCamera>() : null;
            if (fc != null) fc.Snap();
        }

        Vector3 approach = center - plane.transform.position;
        approach.y = 0f;

        // Savas alani: baslangic ile us arasinin ortasi, yaricap = mesafenin yarisi + pay
        Vector2 startXZ = new Vector2(plane.transform.position.x, plane.transform.position.z);
        arenaCenter = (startXZ + baseDef.center) * 0.5f;
        arenaRadius = Vector2.Distance(startXZ, baseDef.center) * 0.5f + GameSettings.Current.arenaMargin;
        outsideSince = -1f;
        OutsideArena = false;

        if (baseDef.island)
        {
            center.y = 10f;
        }
        else
        {
            // Kara ussu: pedin altindaki en yuksek arazi noktasi + biraz
            float top = float.MinValue;
            for (int i = -2; i <= 2; i++)
            for (int j = -2; j <= 2; j++)
                top = Mathf.Max(top, GroundHeight(center + new Vector3(i * baseDef.radius * 0.45f, 0f, j * baseDef.radius * 0.4f)));
            center.y = top + 0.3f;
        }

        BaseBuilder.Result built = BaseBuilder.Build(this, baseDef, Mathf.Min(index, 7), center, approach, modelPrefabs, bossLit, bossGlow);
        baseRoot = built.root;
        baseCore = built.core;
        foreach (var u in built.units)
        {
            enemies.Add(u);
            RunState.RegisterSpawn(1);
        }

        // Deniz gecisinde rotanin uzerinde yavasca giden savas gemileri
        if (baseDef.hasStart)
            SpawnShips(index, new Vector3(baseDef.startXZ.x, 0f, baseDef.startXZ.y), center);

        if (!string.IsNullOrEmpty(baseDef.intro))
            Announce(baseDef.intro, new Color(1f, 0.82f, 0.4f), 3.2f);
    }

    private readonly List<GameObject> shipRoots = new List<GameObject>();

    /// <summary>Deniz gecisi: baslangic ile us arasindaki rotaya, rotaya dik yonde yavasca giden savas gemileri koyar.</summary>
    private void SpawnShips(int index, Vector3 start, Vector3 center)
    {
        Vector3 route = center - start;
        route.y = 0f;
        float len = route.magnitude;
        if (len < 800f) return;
        Vector3 rd = route / len;
        Vector3 side = Vector3.Cross(Vector3.up, rd);

        int count = Mathf.Clamp(2 + index / 2, 2, 5);
        for (int i = 0; i < count; i++)
        {
            float t = Mathf.Lerp(0.22f, 0.78f, (i + 0.5f) / count);
            float lateral = (i % 2 == 0 ? 1f : -1f) * Random.Range(80f, 260f);
            Vector3 pos = start + rd * (len * t) + side * lateral;
            pos.y = SeaLevel - 3f;

            // Rotayi kesecek sekilde yanlamasina gider (yarisi bir yone, yarisi diger yone)
            Vector3 heading = side * (i % 2 == 0 ? -1f : 1f);
            BaseBuilder.Result built = BaseBuilder.BuildShip(this, bossLit, bossGlow, pos, heading, Mathf.Min(index, 7));
            built.root.AddComponent<Warship>().Setup(heading, Random.Range(5f, 8f), built.units[0]);
            shipRoots.Add(built.root);
            foreach (var u in built.units)
            {
                enemies.Add(u);
                RunState.RegisterSpawn(1);
            }
        }
    }

    /// <summary>Bir noktada, belirli aralikla birden cok patlama (yikilan buyuk yapi): kareye yayilir.</summary>
    public void SpawnExplosionBurst(Vector3 pos, float radius, int count, float interval)
    {
        if (count <= 0) return;
        StartCoroutine(ExplosionBurstRoutine(pos, radius, count, interval));
    }

    private System.Collections.IEnumerator ExplosionBurstRoutine(Vector3 pos, float radius, int count, float interval)
    {
        for (int i = 0; i < count; i++)
        {
            yield return new WaitForSeconds(interval * Random.Range(0.7f, 1.3f));
            if (GameState.Paused) continue;
            SpawnExplosion(pos + new Vector3(Random.Range(-radius, radius), Random.Range(1f, radius * 0.7f), Random.Range(-radius, radius)) * 0.6f);
        }
    }

    /// <summary>Yikilan yapinin yerinde kararmis enkaz (ve duman) birakir.</summary>
    public void SpawnRubble(Vector3 pos, float radius, bool burning)
    {
        if (baseRoot == null) return;

        if (rubbleMat == null && bossLit != null)
        {
            rubbleMat = new Material(bossLit) { color = new Color(0.07f, 0.07f, 0.07f, 1f) };
            if (rubbleMat.HasProperty("_BaseColor")) rubbleMat.SetColor("_BaseColor", rubbleMat.color);
        }

        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "Rubble";
        go.transform.SetParent(baseRoot.transform, true);
        go.transform.position = pos + Vector3.up * 0.4f;
        go.transform.rotation = Quaternion.Euler(0f, Random.value * 360f, 0f);
        go.transform.localScale = new Vector3(radius * 1.5f, 0.9f, radius * 1.4f);
        Destroy(go.GetComponent<Collider>());
        var r = go.GetComponent<Renderer>();
        if (rubbleMat != null) r.sharedMaterial = rubbleMat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        if (burning && smokeTrailPrefab != null)
        {
            GameObject smoke = Instantiate(smokeTrailPrefab, pos + Vector3.up * 2f, Quaternion.identity);
            Destroy(smoke, 20f);
        }
    }

    /// <summary>Her karede: oyuncu savas alaninin disindaysa suresini tutar, sure dolunca periyodik hasar verir.</summary>
    private void UpdateArena()
    {
        PlaneController plane = flight != null ? flight.Current : null;
        // Boss fazinda (ve sonundaki donuste) savas alani siniri yok: acik denizde ilerlenir
        if (plane == null || plane.IsCrashed || !RunState.Active || !baseDef.enabled || RushActive || rushEndRoutine != null)
        {
            OutsideArena = false;
            outsideSince = -1f;
            return;
        }

        Vector3 p = plane.transform.position;
        bool outside = Vector2.Distance(new Vector2(p.x, p.z), arenaCenter) > arenaRadius;
        if (!outside)
        {
            OutsideArena = false;
            outsideSince = -1f;
            return;
        }

        OutsideArena = true;
        if (outsideSince < 0f) outsideSince = Time.time;

        GameSettings gs = GameSettings.Current;
        if (Time.time - outsideSince >= gs.arenaGraceSeconds && Time.time >= nextArenaHit)
        {
            nextArenaHit = Time.time + gs.arenaDamageInterval;
            PlayerHealth ph = CurrentPlayerHealth;
            if (ph != null) ph.TakeHit(1, p, true);
        }
    }

    // ----------------------------------------------------------------- HUD

    private void DrawBaseObjective(float y)
    {
        if (!baseDef.enabled || stage == null || RunState.StageIsClear || RunState.Ended || RushActive) return;

        float w = 560f;
        Rect r = new Rect(Hud.W * 0.5f - w * 0.5f, y, w, 20f);

        if (!baseCleared && baseCore != null && !baseCore.IsDead)
        {
            Transform pt = PlayerTransform;
            float d = pt != null ? Vector3.Distance(new Vector3(pt.position.x, 0f, pt.position.z),
                new Vector3(baseCore.transform.position.x, 0f, baseCore.transform.position.z)) : 0f;
            int left = BaseUnitsLeft;
            int dKey = Mathf.RoundToInt(d / 25f);
            if (baseText.Update(dKey, left, 1))
                baseText.Text = $"HEDEF  {baseDef.name}: KOMUTA MERKEZİNİ YOK ET  •  {Hud.Meters(dKey * 25f)}  •  kalan hedef {left}";
            Hud.Text(r, baseText.Text, new Color(1f, 0.82f, 0.4f), 13, TextAnchor.MiddleCenter);
        }
        else if (baseCleared && !bossSpawned)
        {
            if (baseText.Update(0, 0, 2)) baseText.Text = "ÜS ETKİSİZ  •  komutan geliyor";
            Hud.Text(r, baseText.Text, new Color(0.5f, 1f, 0.6f), 13, TextAnchor.MiddleCenter);
        }
    }
}
