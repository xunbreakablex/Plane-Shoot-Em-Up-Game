using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// Savas: olaylar (vurus, olum, toplama), skor/kombo, bomba. Dusman uretimi ve asamalar
// CombatManager.Run.cs'de, bomba CombatManager.Bomb.cs'de, HUD CombatManager.Hud.cs'dedir.
public partial class CombatManager : MonoBehaviour
{
    public static CombatManager Instance { get; private set; }

    [Header("References")]
    public FlightTestManager flight;
    public Camera cam;
    public PlayerGun gun;
    [Tooltip("flight.planes ile ayni sirada ucak model prefab'lari (dusman modeli olarak kullanilir)")]
    public GameObject[] modelPrefabs;
    public Bullet enemyBulletPrefab;
    [Tooltip("Oyuncu mermisi (otomatik tareler kullanir)")] public Bullet playerBulletPrefab;
    public GameObject explosionPrefab;
    public GameObject hitSparkPrefab;
    public GameObject smokeTrailPrefab;
    [Tooltip("Mermi kalkana carpinca: parcalanma + renkli alan")] public GameObject shieldHitPrefab;
    [Tooltip("Dusen guclendirme (P/S/M/B/+)")] public PowerUp powerUpPrefab;
    [Tooltip("Bomba halkasi icin Sprites/Default malzeme")] public Material fxMaterial;
    [Tooltip("Boss govdeleri: URP Lit (metal)")] public Material bossLit;
    [Tooltip("Boss govdeleri: URP Unlit (parlayan goz/motor)")] public Material bossGlow;

    [Header("Enemies")]
    public int baseHits = 4;
    [Tooltip("Asama icinde her bu kadar saniyede yeni gelen avcilarin cani +1 vurus artar")]
    public float hitIncreaseInterval = 30f;
    [Tooltip("Her asamada yeni dusmanlara eklenen vurus")] public int hitsPerStage = 3;
    public bool enemiesShoot = true;
    [Tooltip("Dusman nisan hatasi (metre); buyudukce daha az isabet")] public float enemyAimError = 14f;
    public float[] modelCruise = { 48f, 70f, 40f };
    public float[] modelTurn = { 75f, 85f, 55f };

    [Header("Stage flow")]
    [Tooltip("Son dalgadan sonra bossun gelmesi icin azami bekleme (sn)")] public float bossWaitAfterLastWave = 25f;
    [Tooltip("Asama tamamlandi ekraninin acilma gecikmesi (sn)")] public float stageClearDelay = 2.8f;

    [Header("Combo")]
    [Tooltip("Ardi ardina oldurme icin iki oldurme arasindaki azami sure (sn)")]
    public float streakWindow = 5f;
    public string[] streakWords =
    {
        "KILL", "DOUBLE KILL", "ULTRA KILL", "MEGA KILL", "MONSTER KILL", "LEGENDARY", "UNSTOPPABLE", "GODLIKE"
    };
    // streakWords[i] bu seri sayisinda gorunur (yogun savasta her oldurmede yazi cikmasin)
    public int[] streakThresholds = { 1, 3, 5, 8, 12, 17, 24, 32 };

    public IReadOnlyList<EnemyPlane> Enemies => enemies;
    public Bullet EnemyBulletPrefab => enemyBulletPrefab;
    public GameObject SmokeTrailPrefab => smokeTrailPrefab;

    public Transform PlayerTransform => flight != null && flight.Current != null ? flight.Current.transform : null;
    public float PlayerSpeed => flight != null && flight.Current != null ? flight.Current.Speed : 0f;
    public Vector3 PlayerVelocity => flight != null && flight.Current != null ? flight.Current.Velocity : Vector3.zero;

    public int Kills => RunState.Kills;
    public int Streak { get; private set; }
    public int BestStreak => RunState.BestCombo;
    public float ComboMultiplier => 1f + 0.1f * Mathf.Min(Mathf.Max(Streak - 1, 0), 20);

    private readonly List<EnemyPlane> enemies = new List<EnemyPlane>();
    private readonly List<FloatingText> texts = new List<FloatingText>();

    private AudioSource uiAudio;

    private float lastKillTime = -100f;
    private float hitFlash;
    private float lastHitSound;
    private float powerFlash;

    private struct FloatingText
    {
        public Vector3 position;
        public string text;
        public float born;
        public Color color;
        public float scale;
    }

    private void Awake()
    {
        Instance = this;

        uiAudio = gameObject.AddComponent<AudioSource>();
        uiAudio.spatialBlend = 0f;
        uiAudio.playOnAwake = false;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Start()
    {
        if (cam == null) cam = Camera.main;

        // Dusman ucaklar (layer 2) oyuncuyla fiziksel carpismaz; temas hasari EnemyPlane'de yakinliktan hesaplanir.
        // Mermi/fuze/nisan isinlari tum katmanlari taradigi icin dusmanlara yine isabet eder.
        Physics.IgnoreLayerCollision(0, EnemyLayer, true);

        // Tum oyuncu ucaklarina can ekle (sahnedeki degisimler icin)
        if (flight != null && flight.planes != null)
        {
            foreach (var p in flight.planes)
                if (p != null && p.GetComponent<PlayerHealth>() == null)
                    p.gameObject.AddComponent<PlayerHealth>();
        }
    }

    private void Update()
    {
        // Hasar cercevesi duraklatmada da sonmeli (zaman durdugu icin unscaled)
        hitFlash = Mathf.Max(0f, hitFlash - 2.5f * Time.unscaledDeltaTime);
        bombFlash = Mathf.Max(0f, bombFlash - 1.6f * Time.unscaledDeltaTime);
        powerFlash = Mathf.Max(0f, powerFlash - 2f * Time.unscaledDeltaTime);

        UpdateBanner();

        if (GameState.Paused) return;

        // Imlec: gorunmez ve pencereye kilitli; nisangah OnGUI'de cizilir
        Cursor.visible = false;
        if (Cursor.lockState != CursorLockMode.Locked) Cursor.lockState = CursorLockMode.Confined;

        enemies.RemoveAll(e => e == null);

        RunState.Tick(Time.deltaTime);

        if (Streak > 0 && Time.time - lastKillTime > streakWindow) Streak = 0;

        var kb = Keyboard.current;
        if (kb != null && kb.bKey.wasPressedThisFrame) TryBomb();

        UpdateDirector();
        UpdateBombRings();

        texts.RemoveAll(t => Time.time - t.born > 2.2f);
    }

    public float GroundHeight(Vector3 p)
    {
        Terrain t = Terrain.activeTerrain;
        return t != null ? t.SampleHeight(p) + t.transform.position.y : 0f;
    }

    // ---------------------------------------------------------------- events

    public void OnEnemyHit(EnemyPlane enemy, Vector3 point)
    {
        SpawnHitSpark(point);

        // Yogun savasta ses yigilmasin
        if (Time.time - lastHitSound > 0.04f)
        {
            lastHitSound = Time.time;
            Sfx.PlayAt(Sfx.Hit, point, 0.45f);
        }
    }

    public void OnEnemyDestroyed(EnemyPlane enemy, bool byPlayer)
    {
        Vector3 pos = enemy.transform.position;
        SpawnExplosion(pos);

        if (enemy.IsBoss)
        {
            // Boss: govdenin her yerinde art arda patlamalar
            var brain = enemy.GetComponent<BossBrain>();
            if (brain != null)
            {
                brain.SpawnDeathExplosions(10);

                // Ilk kez yenilen boss yeni ucak ve/veya slot acar (Unlocks.RegisterBossDefeat otomatik takar)
                Unlocks.RegisterBossDefeat(brain.type);
                pendingUnlockBanners.AddRange(Unlocks.TakeMessages());

                // Denge olcumu: konsolda gorunur
                float fight = enemy.FirstHitTime < 0f ? 0f : Time.time - enemy.FirstHitTime;
                Debug.Log($"[Denge] {enemy.Title} yenildi: {fight:0.0} sn, {enemy.TotalDamage} hasar, ort. {enemy.Dps:0.0} DPS (can {enemy.MaxHits})");
            }
            else
            {
                for (int i = 0; i < 4; i++)
                    SpawnExplosion(pos + Random.insideUnitSphere * enemy.HitRadius);
            }

            if (boss == enemy) boss = null;
            if (midBoss == enemy) midBoss = null;
            RunState.RegisterBossKill();
        }
        else if (!byPlayer)
        {
            return;     // kendi kendine yere carpan siradan dusman puan vermez
        }

        // Kombo
        Streak = (Time.time - lastKillTime <= streakWindow) ? Streak + 1 : 1;
        lastKillTime = Time.time;
        RunState.RegisterKill(Streak);

        // Puan: tur puani x kombo carpani
        EnemyKindInfo info = EnemyKinds.Get(enemy.Kind);
        int points = Mathf.RoundToInt(info.score * ComboMultiplier);
        RunState.AddScore(points);

        // Hurda odulu: dusman cani x seri carpani x upgrade carpani (bossta sinirli)
        int reward = RunUpgrades.ComputeReward(Mathf.Min(enemy.MaxHits, 15), Streak);
        RunUpgrades.AddScrap(reward);

        // Guclendirme: isaretli dusman birakir; boss bomba + onarim birakir
        if (enemy.IsCarrier) DropPickup((PickupKind)enemy.CarrierDrop, pos);
        if (enemy.IsStageBoss)
        {
            DropPickup(PickupKind.Bomb, pos + Vector3.right * 14f);
            DropPickup(PickupKind.Repair, pos - Vector3.right * 14f);
        }
        else if (enemy.IsBoss)
        {
            // Orta boss: guc ve kalkan (Power zaten tasiyici olarak dusuyor)
            DropPickup(PickupKind.Shield, pos - Vector3.right * 14f);
            Announce("ORTA BOSS YENİLDİ!", new Color(1f, 0.75f, 0.3f), 2.4f, true);
        }

        // Seri kelimesi: sadece yeni bir kademeye gecildiginde
        int tier = StreakTier(Streak);
        if (tier >= 0 && Streak == streakThresholds[tier])
        {
            texts.Clear();
            texts.Add(new FloatingText
            {
                position = pos + Vector3.up * 12f,
                text = streakWords[Mathf.Min(tier, streakWords.Length - 1)],
                born = Time.time,
                color = TierColor(tier),
                scale = 1f + tier * 0.15f
            });
        }
        else if (points >= 300)
        {
            // Buyuk puan: kisa "+puan" yazisi
            texts.Add(new FloatingText
            {
                position = pos + Vector3.up * 6f,
                text = "+" + points,
                born = Time.time,
                color = new Color(1f, 0.95f, 0.6f),
                scale = 0.55f
            });
        }

        // Yalnizca asama sonu boss'u asamayi bitirir
        if (enemy.IsStageBoss) OnBossDefeated(enemy);

        // Yeni acilan slotlar: boss afisinden sonra siraya girer (urgent duyurular kuyrugu temizler)
        foreach (string line in pendingUnlockBanners)
            Announce(line, new Color(0.5f, 1f, 0.7f), 3.2f);
        pendingUnlockBanners.Clear();
    }

    private readonly List<string> pendingUnlockBanners = new List<string>();

    private int StreakTier(int streak)
    {
        int tier = -1;
        for (int i = 0; i < streakThresholds.Length; i++)
            if (streak >= streakThresholds[i]) tier = i;
        return tier;
    }

    private void DropPickup(PickupKind kind, Vector3 pos)
    {
        if (powerUpPrefab == null) return;
        Vector3 scatter = Random.insideUnitSphere * 12f;
        scatter.y = Mathf.Abs(scatter.y) * 0.3f;
        PowerUp.Spawn(powerUpPrefab, pos, kind, scatter);
    }

    /// <summary>PowerUp, oyuncu ucagina deydiginde cagirir.</summary>
    public void OnPickup(PowerUp p)
    {
        PlayerHealth ph = CurrentPlayerHealth;
        string text = null;

        switch (p.Kind)
        {
            case PickupKind.Power:
                if (RunState.AddPower()) text = $"GÜÇ SEVİYE {RunState.PowerLevel + 1}!";
                else { RunState.AddScore(2000); text = "GÜÇ MAKSİMUM  +2000"; }
                break;

            case PickupKind.Shield:
                if (ph != null && ph.maxShield > 0f && ph.Shield < ph.maxShield - 0.01f)
                {
                    ph.RechargeShield();
                    text = "KALKAN DOLDU";
                }
                else { RunState.AddScore(1000); text = "KALKAN  +1000"; }
                break;

            case PickupKind.Missile:
                if (gun != null && gun.HasMissiles) { gun.RefillMissiles(); text = "FÜZELER DOLDU"; }
                else { RunState.AddScore(1000); text = "FÜZE  +1000"; }
                break;

            case PickupKind.Bomb:
                if (RunState.AddBomb()) text = "BOMBA +1  (B)";
                else { RunState.AddScore(2000); text = "BOMBA MAKSİMUM  +2000"; }
                break;

            default:
                if (ph != null && ph.Health < ph.maxHealth)
                {
                    ph.Heal(Mathf.Max(1, Mathf.RoundToInt(ph.maxHealth * 0.4f)));
                    text = "ONARIM";
                }
                else { RunState.AddScore(1000); text = "ONARIM  +1000"; }
                break;
        }

        powerFlash = 1f;
        Announce(text, PowerUp.KindColors[(int)p.Kind], 1.6f);

        if (uiAudio != null)
        {
            uiAudio.pitch = 1f;
            uiAudio.PlayOneShot(Sfx.LevelUp, 0.5f);
        }
    }

    public void OnPlayerHit(Vector3 point, bool hullDamaged)
    {
        // Kalkan emdiyse ekran kirmizilasmaz; sadece govde hasarinda uyari
        if (hullDamaged)
        {
            hitFlash = 1f;
            RunState.RegisterHullHit();
        }
        SpawnHitSpark(point);
        Sfx.PlayAt(Sfx.Hit, point, hullDamaged ? 0.6f : 0.3f);
    }

    public void SpawnExplosion(Vector3 pos)
    {
        if (explosionPrefab != null) Instantiate(explosionPrefab, pos, Quaternion.identity);
        Sfx.PlayAt(Sfx.Explosion, pos, 1f);
    }

    public void SpawnHitSpark(Vector3 pos)
    {
        if (hitSparkPrefab != null) Instantiate(hitSparkPrefab, pos, Quaternion.identity);
    }

    /// <summary>Mermi kalkan yuzeyinde parcalanir; carpma noktasinda renkli bir alan belirir.</summary>
    public void SpawnShieldHit(Vector3 point, Vector3 normal, Transform parent)
    {
        if (shieldHitPrefab != null)
        {
            var go = Instantiate(shieldHitPrefab, point, Quaternion.LookRotation(normal));
            go.transform.SetParent(parent, true);   // ucakla birlikte hareket etsin
        }
        Sfx.PlayAt(Sfx.ShieldHit, point, 0.5f);
    }

    private PlayerHealth cachedHealth;
    private PlaneController cachedHealthPlane;

    public PlayerHealth CurrentPlayerHealth
    {
        get
        {
            PlaneController p = flight != null ? flight.Current : null;
            if (p == null) return null;
            if (p != cachedHealthPlane)
            {
                cachedHealthPlane = p;
                cachedHealth = p.GetComponent<PlayerHealth>();
            }
            return cachedHealth;
        }
    }

    private static Color TierColor(int tier)
    {
        switch (tier)
        {
            case 0: return new Color(1f, 1f, 1f);
            case 1: return new Color(1f, 0.95f, 0.35f);
            case 2: return new Color(1f, 0.65f, 0.15f);
            case 3: return new Color(1f, 0.3f, 0.2f);
            case 4: return new Color(1f, 0.25f, 0.75f);
            case 5: return new Color(0.4f, 0.9f, 1f);
            default: return new Color(0.75f, 0.5f, 1f);
        }
    }
}
