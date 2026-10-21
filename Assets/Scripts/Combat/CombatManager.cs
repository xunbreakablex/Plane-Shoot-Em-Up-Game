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
    // Dusman nisani / boss dumeni icin oyuncu hizi. Boss fazinda cercevenin hizi cikarilir (goreli hiz): boss ve
    // mermiler cerceveyle birlikte tasindigi icin onde tutma goreli hizla yapilir.
    public float PlayerSpeed => PlayerVelocity.magnitude;
    public Vector3 PlayerVelocity => flight != null && flight.Current != null
        ? (RushActive ? flight.Current.Velocity - FrameVelocityAt(flight.Current.transform.position) : flight.Current.Velocity)
        : Vector3.zero;

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
        FrameVelocity = Vector3.zero;
        FrameYawRate = 0f;
    }

    private void Start()
    {
        if (cam == null) cam = Camera.main;

        // Muzik (savas / boss) ve kayitli ses seviyesi
        MusicPlayer.Ensure(gameObject, MusicTrack.None);
        GameAudio.Apply();
        StageAtmosphere.Reset();

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
        StageAtmosphere.Tick(Time.unscaledDeltaTime);

        // Yeni madalyalar savasta duyurulur
        var medalNews = Medals.TakeAnnouncements();
        if (medalNews != null)
            foreach (string m in medalNews) Announce("MADALYA: " + m, new Color(1f, 0.85f, 0.3f), 3.2f);

        if (GameState.Paused) return;

        // Imlec: gorunmez; nisangah OnGUI'de cizilir. FARE semasinda imlec kilitlenir (fare hareketi
        // sinirsiz okunur), digerlerinde pencereye hapsedilir.
        Cursor.visible = false;
        if (ControlSettings.MouseSteering || RushActive) Cursor.lockState = CursorLockMode.Locked;   // boss fazinda fare hep nisan
        else if (Cursor.lockState != CursorLockMode.Locked) Cursor.lockState = CursorLockMode.Confined;

        enemies.RemoveAll(e => e == null);

        RunState.Tick(Time.deltaTime);

        if (Streak > 0 && Time.time - lastKillTime > streakWindow) Streak = 0;

        UpdateWarningBeeps();

        var kb = Keyboard.current;
        var gpad = Gamepad.current;
        if ((kb != null && kb.bKey.wasPressedThisFrame) || (gpad != null && gpad.buttonWest.wasPressedThisFrame)) TryBomb();

        UpdateDirector();
        UpdateRush();
        UpdateArena();
        UpdateBombRings();

        texts.RemoveAll(t => Time.time - t.born > 2.2f);
        // Boss fazinda yuzen yazilar cerceveyle ilerler: yoksa kamera ~100 m/sn ile onlari geride birakip gizliyordu
        if (RushActive)
        {
            for (int i = 0; i < texts.Count; i++)
            {
                FloatingText t = texts[i];
                t.position += FrameVelocityAt(t.position) * Time.deltaTime;
                texts[i] = t;
            }
        }
    }

    /// <summary>Dunyada bir noktadan yukari suzulen kisa yazi (boss saldirisi geri bildirimi vb.).</summary>
    public void FloatText(Vector3 position, string text, Color color, float scale = 0.7f)
    {
        texts.Add(new FloatingText { position = position, text = text, born = Time.time, color = color, scale = scale });
    }

    private float nextBeep;

    // Dusuk can ve savas alani disi: aralikli uyari sesi
    private void UpdateWarningBeeps()
    {
        if (!RunState.Active || Time.time < nextBeep || uiAudio == null) return;

        PlayerHealth ph = CurrentPlayerHealth;
        PlaneController plane = flight != null ? flight.Current : null;
        if (plane == null || plane.IsCrashed) return;

        bool lowHealth = ph != null && ph.maxHealth > 0 && (float)ph.Health / ph.maxHealth <= 0.3f;
        if (OutsideArena) { nextBeep = Time.time + 0.8f; uiAudio.PlayOneShot(Sfx.Warning, 0.45f); }
        else if (lowHealth) { nextBeep = Time.time + 1.3f; uiAudio.PlayOneShot(Sfx.Warning, 0.35f); }
    }

    public float GroundHeight(Vector3 p)
    {
        Terrain t = Terrain.activeTerrain;
        if (t == null) return 0f;

        // Arazinin disi deniz (ada ussleri orada): SampleHeight kenar yuksekligini dondurmesin
        Vector3 o = t.transform.position;
        Vector3 size = t.terrainData.size;
        if (p.x < o.x || p.x > o.x + size.x || p.z < o.z || p.z > o.z + size.z) return SeaLevel;

        return t.SampleHeight(p) + o.y;
    }

    // ---------------------------------------------------------------- events

    // Dusman sana nisan aldi (seriye basladi): yakinsa kisa uyari tiki (sik tekrar etmesin)
    private float nextTargetTick;

    public void OnEnemyTargeting(EnemyPlane enemy, float dist)
    {
        if (dist > 520f || Time.time < nextTargetTick || uiAudio == null) return;
        nextTargetTick = Time.time + 1.1f;
        uiAudio.pitch = 1f;
        uiAudio.PlayOneShot(Sfx.Lock, 0.3f);
    }

    // Vurus onayi (nisangah isareti): normal vurus kucuk beyaz, oldurme buyuk kirmizi
    private float hitMarkerTime = -10f, killMarkerTime = -10f;

    public void OnEnemyHit(EnemyPlane enemy, Vector3 point)
    {
        // Kivilcim uzakta da gorunsun: mesafeyle buyur (300 m'de ~2,5 kat)
        float dist = cam != null ? Vector3.Distance(cam.transform.position, point) : 100f;
        SpawnHitSpark(point, Mathf.Clamp(dist / 120f, 1f, 4f));
        hitMarkerTime = Time.unscaledTime;

        // Yogun savasta ses yigilmasin. Vurus sesi oyuncunun kulaginda (2D): uzaktaki isabet de duyulur
        if (Time.time - lastHitSound > 0.05f)
        {
            lastHitSound = Time.time;
            if (uiAudio != null && !enemy.IsGround)
            {
                uiAudio.pitch = Random.Range(0.95f, 1.15f);
                uiAudio.PlayOneShot(Sfx.Hit, 0.22f);
                uiAudio.pitch = 1f;
            }
            else Sfx.PlayAt(Sfx.Hit, point, 0.45f);
        }
    }

    public void OnEnemyDestroyed(EnemyPlane enemy, bool byPlayer, EnemyPlane.HitZone cause = EnemyPlane.HitZone.Body)
    {
        Vector3 pos = enemy.transform.position;
        // Indirilen ucak listeden hemen cikar: enkaz hedef alinmaz, sayilmaz
        enemies.Remove(enemy);

        // Uzuv kopmasi: ucak hala havada, buyuk patlama yere carpinca gelir; simdilik kucuk bir alev
        bool partKill = cause != EnemyPlane.HitZone.Body && !enemy.IsGround && !enemy.IsBoss;
        // Patlama uzakta da okunsun: mesafeyle buyur; oyuncunun indirdigi hava hedefinde 2D gumbur
        float camDist = cam != null ? Vector3.Distance(cam.transform.position, pos) : 100f;
        float distScale = enemy.IsGround ? 1f : Mathf.Clamp(camDist / 220f, 1f, 2.6f);
        SpawnExplosion(pos, (partKill ? 0.6f : 1f) * distScale);
        if (byPlayer && !enemy.IsGround && uiAudio != null)
        {
            uiAudio.pitch = Random.Range(0.85f, 1.05f);
            uiAudio.PlayOneShot(Sfx.Explosion, 0.35f);
            uiAudio.pitch = 1f;
            FollowCamera.AddShake(0.25f, 0.18f);
        }

        // Yakin mesafede uzuv koparma: cok kisa bir donma (hit-stop) vurusu "oturtur"
        if (partKill && byPlayer && PlayerTransform != null && Vector3.Distance(PlayerTransform.position, pos) < 320f)
            HitStop(0.06f, 0.25f);
        if (byPlayer)
        {
            killMarkerTime = Time.unscaledTime;
            if (uiAudio != null && !enemy.IsGround) { uiAudio.pitch = 1f; uiAudio.PlayOneShot(Sfx.KillDing, 0.35f); }
            // Uzuv kopmasi: metal yirtilma sesi (patlamanin ustune)
            if (cause != EnemyPlane.HitZone.Body) Sfx.PlayAt(Sfx.Crack, pos, 0.9f);
        }

        // Hissiyat: boss olumunde guclu sarsinti + kisa yavaslama (hit-stop); komuta merkezinde sarsinti
        if (enemy.IsBoss) { FollowCamera.AddShake(1.7f, 1.3f); HitStop(0.35f, 0.18f); }
        else if (enemy.IsCore) FollowCamera.AddShake(1.3f, 1.0f);

        if (enemy.IsGround)
        {
            // Yer birimi: ek patlamalar + enkaz; komuta merkezi yikilinca us susar ve komutan yolu acilir
            var gu = enemy.GetComponent<GroundUnit>();
            if (gu != null) gu.OnKilled(this);
            if (enemy.IsCore)
            {
                baseCleared = true;
                Announce("KOMUTA MERKEZİ YIKILDI!  Uçaksavarlar sustu", new Color(1f, 0.85f, 0.3f), 3.4f, true);
            }
        }

        if (enemy.IsBoss)
        {
            // Boss: govdenin her yerinde art arda patlamalar
            var brain = enemy.GetComponent<BossBrain>();
            if (brain != null)
            {
                // Asamali olum gosterisi varsa patlamalar oradan gelir; yoksa eski toplu patlama
                if (!brain.Dying) brain.SpawnDeathExplosions(10);
                else brain.SpawnDeathExplosions(2);

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
        Medals.OnKill(enemy, Streak);

        // Puan: tur puani x kombo carpani
        EnemyKindInfo info = EnemyKinds.Get(enemy.Kind);
        int points = Mathf.RoundToInt(info.score * ComboMultiplier * (enemy.IsAce ? 5f : 1f));
        if (enemy.IsAce)
        {
            Announce("AS PİLOT İNDİRİLDİ!  " + enemy.Title.Replace("AS: ", "").ToUpper(), new Color(1f, 0.85f, 0.3f), 3f, true);
            RunUpgrades.AddScrap(80);
            FollowCamera.AddShake(0.8f, 0.5f);
            HitStop(0.2f, 0.2f);
            if (uiAudio != null) uiAudio.PlayOneShot(Sfx.Victory, 0.5f);
        }
        RunState.AddScore(points);

        // Vur ve koru: kalkan kirikken indirilen dusman kalkanin bir kismini geri verir (asama basina 4, 1,5 sn aralik)
        PlayerHealth shieldPh = CurrentPlayerHealth;
        if (shieldPh != null && shieldPh.maxShield > 0f && !shieldPh.ShieldUp && Time.time - lastShieldRefill > 1.5f)
        {
            if (shieldRefillStage != RunState.Stage) { shieldRefillStage = RunState.Stage; shieldRefills = 0; }
            if (shieldRefills < 4)
            {
                shieldRefills++;
                lastShieldRefill = Time.time;
                shieldPh.AddShield(shieldPh.maxShield * 0.3f);
                Announce("+KALKAN", new Color(0.4f, 0.8f, 1f), 1.2f);
            }
        }

        // Son dalis: yarali ucagi dalisinda dusurmek ekstra odul
        if (enemy.IsDiving)
        {
            RunState.AddScore(300);
            RunUpgrades.AddScrap(30);
            Announce("SON DALIŞ BOZULDU  +300", new Color(1f, 0.6f, 0.3f), 1.8f);
        }

        // Hurda odulu: dusman cani x seri carpani x upgrade carpani (bossta sinirli)
        // Bossta hurda sinirli kalsin; diger sert hedefler (agir, as, kara birimi) 15 vurustan sonra azalarak artar
        int rewardHits = enemy.IsBoss ? Mathf.Min(enemy.MaxHits, 15) : SoftHits(enemy.MaxHits);
        int reward = RunUpgrades.ComputeReward(rewardHits, Streak);
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

        // Her indirmede vurdugun yerde puan yazisi; uzuv kopmussa hangi uzuv oldugu da yazar (nisan ogretir)
        {
            string zone = EnemyPlane.ZoneName(cause);
            texts.Add(new FloatingText
            {
                position = pos + Vector3.up * 6f,
                text = "+" + points + (zone.Length > 0 ? "  " + zone : ""),
                born = Time.time,
                color = zone.Length > 0 ? new Color(1f, 0.75f, 0.35f) : new Color(1f, 0.95f, 0.6f),
                scale = zone.Length > 0 ? 0.7f : 0.55f
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

    /// <summary>Enkaz yere/suya carpti: patlama + kucuk bonus (indirme odulu zaten verildi).</summary>
    public void OnWreckImpact(Vector3 pos)
    {
        SpawnExplosion(pos);
        RunState.AddScore(50);
        texts.Add(new FloatingText
        {
            position = pos + Vector3.up * 8f,
            text = "ÇAKILDI  +50",
            born = Time.time,
            color = new Color(1f, 0.6f, 0.3f),
            scale = 0.5f
        });
    }

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

    private int shieldRefills, shieldRefillStage = -1;
    private float lastShieldRefill = -10f;

    // 15 vurustan sonra odul yavaslar: sert hedef sonsuz hurda vermesin, ama tek vurusla da kazandirmasin
    private static int SoftHits(int hits)
    {
        return hits <= 15 ? hits : 15 + Mathf.RoundToInt(2f * Mathf.Sqrt(hits - 15));
    }

    /// <summary>PowerUp, oyuncu ucagina deydiginde cagirir.</summary>
    public void OnPickup(PowerUp p)
    {
        PlayerHealth ph = CurrentPlayerHealth;
        string text = null;

        switch (p.Kind)
        {
            case PickupKind.Power:
                // HUD'da MaxPower+1 kutu var (seviye 0 = 1 kutu): bildirim de ayni 1 tabanli olcekte
                if (RunState.AddPower()) text = $"GÜÇ SEVİYE {RunState.PowerLevel + 1}/{RunState.MaxPower + 1}!";
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
                if (RunState.AddBomb()) text = "BOMBA +1  (" + ControlSettings.BombKey + ")";
                else { RunState.AddScore(2000); text = "BOMBA MAKSİMUM  +2000"; }
                break;

            case PickupKind.Overdrive:
                RunState.StartOverdrive();
                text = $"AŞIRI YÜK: ateş hızı +%50  ({RunState.OverdriveSeconds:0} sn)";
                break;

            case PickupKind.Wingman:
                Wingman.Grant(this);
                text = $"YARDIMCI İHA'LAR  ({Wingman.Seconds:0} sn)";
                break;

            case PickupKind.Scrap:
            {
                int gain = 60 + RunState.Stage * 25;
                RunUpgrades.AddScrap(gain);
                text = $"HURDA +{gain}";
                break;
            }

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
            // Buyuk guclendirmeler fanfarla, siradan toplamalar kisa tikla
            bool big = p.Kind == PickupKind.Power || p.Kind == PickupKind.Overdrive || p.Kind == PickupKind.Wingman;
            uiAudio.pitch = 1f;
            uiAudio.PlayOneShot(big ? Sfx.LevelUp : Sfx.Pickup, 0.5f);
        }
    }

    // Hasar yonu gostergesi: vurulan yonu ekran merkezinin cevresinde kirmizi yay olarak gosterir
    private struct DamageArc { public Vector3 from; public float born; public bool hull; }
    private readonly List<DamageArc> damageArcs = new List<DamageArc>();

    public void OnPlayerHit(Vector3 point, bool hullDamaged)
    {
        // Darbenin geldigi yon: carpma noktasindan oyuncuya degil, oyuncudan carpma noktasina (disariya) dogru
        if (damageArcs.Count > 6) damageArcs.RemoveAt(0);
        damageArcs.Add(new DamageArc { from = point, born = Time.unscaledTime, hull = hullDamaged });
        FollowCamera.AddShake(hullDamaged ? 0.9f : 0.3f, hullDamaged ? 0.4f : 0.2f);
        // Kalkan emdiyse ekran kirmizilasmaz; sadece govde hasarinda uyari
        if (hullDamaged)
        {
            hitFlash = 1f;
            RunState.RegisterHullHit();
        }
        SpawnHitSpark(point);
        Sfx.PlayAt(Sfx.Hit, point, hullDamaged ? 0.6f : 0.3f);
    }

    private bool hitStopActive;

    /// <summary>Kisa yavaslama (hit-stop): sure gercek zamanlidir; duraklatma ile cakismaz.</summary>
    public void HitStop(float seconds, float scale)
    {
        if (hitStopActive || GameState.Paused) return;
        StartCoroutine(HitStopRoutine(seconds, scale));
    }

    private System.Collections.IEnumerator HitStopRoutine(float seconds, float scale)
    {
        hitStopActive = true;
        float prev = Time.timeScale;
        if (prev > 0.01f) Time.timeScale = scale;
        float end = Time.unscaledTime + seconds;
        while (Time.unscaledTime < end && !GameState.Paused) yield return null;
        if (!GameState.Paused && prev > 0.01f) Time.timeScale = prev;
        hitStopActive = false;
    }

    public void SpawnExplosion(Vector3 pos) { SpawnExplosion(pos, 1f); }

    /// <summary>Patlama; scale 1 = normal, kucuk degerler uzuv kopmasi gibi kucuk alevler icin.</summary>
    public void SpawnExplosion(Vector3 pos, float scale)
    {
        // Yakin patlama: hafif kamera sarsintisi
        Transform pt = PlayerTransform;
        if (pt != null)
        {
            float d = Vector3.Distance(pt.position, pos);
            if (d < 140f) FollowCamera.AddShake(0.35f * scale * (1f - d / 140f), 0.3f);
        }

        if (explosionPrefab != null)
        {
            var go = Instantiate(explosionPrefab, pos, Quaternion.identity);
            if (Mathf.Abs(scale - 1f) > 0.001f) ScaleEffect(go, scale);
        }
        Sfx.PlayAt(Sfx.Explosion, pos, Mathf.Lerp(0.45f, 1f, scale));
    }

    /// <summary>Ucaksavar patlamasi: kucuk koyu bulut + kisa gumbur (yakindaysa hafif sarsinti).</summary>
    public void SpawnFlakPuff(Vector3 pos)
    {
        if (smokeTrailPrefab != null)
        {
            var go = Instantiate(smokeTrailPrefab, pos, Quaternion.identity);
            foreach (var ps in go.GetComponentsInChildren<ParticleSystem>())
            {
                var main = ps.main;
                main.loop = false;
                main.startSizeMultiplier *= 3.5f;
                main.startColor = new Color(0.08f, 0.08f, 0.08f, 0.9f);
                var em = ps.emission;
                em.rateOverTime = 0f;
                em.SetBursts(new[] { new ParticleSystem.Burst(0f, 12) });
                ps.Play();
            }
            Destroy(go, 4f);
        }
        if (explosionPrefab != null) ScaleEffect(Instantiate(explosionPrefab, pos, Quaternion.identity), 0.3f);
        Transform pt = PlayerTransform;
        if (pt != null)
        {
            float d = Vector3.Distance(pt.position, pos);
            if (d < 60f) FollowCamera.AddShake(0.25f * (1f - d / 60f), 0.15f);
            if (d < 250f) Sfx.PlayAt(Sfx.Explosion, pos, 0.35f);
        }
    }

    public void SpawnHitSpark(Vector3 pos) { SpawnHitSpark(pos, 1f); }

    public void SpawnHitSpark(Vector3 pos, float scale)
    {
        if (hitSparkPrefab == null) return;
        var go = Instantiate(hitSparkPrefab, pos, Quaternion.identity);
        if (scale > 1.001f) ScaleEffect(go, scale);
    }

    // Parcacik sistemleri "Local" olcekte: kok olcegi alt sistemlere gecsin diye Hierarchy'ye alinir
    private static void ScaleEffect(GameObject go, float scale)
    {
        go.transform.localScale = Vector3.one * scale;
        foreach (var ps in go.GetComponentsInChildren<ParticleSystem>())
        {
            var main = ps.main;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        }
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
