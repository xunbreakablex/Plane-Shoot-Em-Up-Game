using UnityEngine;

// MidBoss: asama ortasi mini boss (olmesi asamayi bitirmez). Boss/FinalBoss: asama sonu.
// GroundTurret / GroundBuilding / GroundCore: yer ustu birimleri (dusman ussu); hareket etmezler.
// Not: siralama onemli: IsBoss "Kind >= MidBoss"; yer birimleri bossun onunde durmali.
public enum EnemyKind { Swarmer, Fighter, Heavy, Kamikaze, Sniper, GroundTurret, GroundBuilding, GroundCore, MidBoss, Boss, FinalBoss }

// Chase: oyuncuyu kovalar (dogfight). Strafe: formasyonla duz bir hat uzerinde gecer ve ates eder.
public enum EnemyBehavior { Chase, Strafe }

public struct EnemyKindInfo
{
    public string title;
    public Color tint;        // govde rengi
    public Color marker;      // HUD/radar rengi
    public float scale;
    public float speedMul;
    public float turnMul;
    public float radius;      // carpma yaricapi (metre)
    public int ramDamage;
    public int score;         // vurulunca kazanilan puan (kombo carpani oncesi)
}

// Dusman turleri: gorunum, hiz, puan. Dengeyi buradan ayarla.
public static class EnemyKinds
{
    public static EnemyKindInfo Get(EnemyKind kind)
    {
        switch (kind)
        {
            case EnemyKind.Swarmer:
                return new EnemyKindInfo
                {
                    title = "Sürü Avcısı", tint = new Color(1f, 0.72f, 0.1f), marker = new Color(1f, 0.78f, 0.2f),
                    scale = 1.5f, speedMul = 1.2f, turnMul = 1.25f, radius = 5f, ramDamage = 1, score = 100
                };
            case EnemyKind.Heavy:
                return new EnemyKindInfo
                {
                    title = "Ağır Bombardıman", tint = new Color(0.55f, 0.2f, 0.75f), marker = new Color(0.85f, 0.45f, 1f),
                    scale = 2.6f, speedMul = 0.75f, turnMul = 0.7f, radius = 11f, ramDamage = 4, score = 800
                };
            case EnemyKind.Kamikaze:
                return new EnemyKindInfo
                {
                    title = "Kamikaze", tint = new Color(1f, 0.38f, 0.08f), marker = new Color(1f, 0.45f, 0.12f),
                    scale = 1.5f, speedMul = 1.45f, turnMul = 1.4f, radius = 5f, ramDamage = 3, score = 250
                };
            case EnemyKind.Sniper:
                return new EnemyKindInfo
                {
                    title = "Nişancı", tint = new Color(0.2f, 0.78f, 0.85f), marker = new Color(0.3f, 0.92f, 1f),
                    scale = 1.9f, speedMul = 0.9f, turnMul = 1.1f, radius = 6.5f, ramDamage = 2, score = 650
                };
            case EnemyKind.GroundTurret:
                return new EnemyKindInfo
                {
                    title = "Uçaksavar", tint = new Color(0.4f, 0.42f, 0.36f), marker = new Color(1f, 0.55f, 0.2f),
                    scale = 1f, speedMul = 0f, turnMul = 0f, radius = 4f, ramDamage = 0, score = 400
                };
            case EnemyKind.GroundBuilding:
                return new EnemyKindInfo
                {
                    title = "Üs Yapısı", tint = new Color(0.5f, 0.5f, 0.46f), marker = new Color(0.95f, 0.8f, 0.4f),
                    scale = 1f, speedMul = 0f, turnMul = 0f, radius = 12f, ramDamage = 0, score = 600
                };
            case EnemyKind.GroundCore:
                return new EnemyKindInfo
                {
                    title = "Komuta Merkezi", tint = new Color(0.6f, 0.15f, 0.12f), marker = new Color(1f, 0.3f, 0.25f),
                    scale = 1f, speedMul = 0f, turnMul = 0f, radius = 24f, ramDamage = 0, score = 8000
                };
            case EnemyKind.MidBoss:
                return new EnemyKindInfo
                {
                    title = "Orta Boss", tint = new Color(0.5f, 0.35f, 0.2f), marker = new Color(1f, 0.62f, 0.2f),
                    scale = 2.2f, speedMul = 0.7f, turnMul = 0.5f, radius = 12f, ramDamage = 4, score = 3000
                };
            case EnemyKind.Boss:
                return new EnemyKindInfo
                {
                    title = "Komutan", tint = new Color(0.3f, 0.3f, 0.36f), marker = new Color(1f, 0.85f, 0.3f),
                    scale = 2.8f, speedMul = 0.6f, turnMul = 0.45f, radius = 14f, ramDamage = 5, score = 5000
                };
            case EnemyKind.FinalBoss:
                return new EnemyKindInfo
                {
                    title = "Kızıl Komutan", tint = new Color(0.55f, 0.02f, 0.02f), marker = new Color(1f, 0.25f, 0.25f),
                    scale = 3.6f, speedMul = 0.62f, turnMul = 0.5f, radius = 18f, ramDamage = 6, score = 20000
                };
            default:
                return new EnemyKindInfo
                {
                    title = "Avcı", tint = new Color(0.9f, 0.15f, 0.12f), marker = new Color(1f, 0.3f, 0.25f),
                    scale = 1.8f, speedMul = 1f, turnMul = 1f, radius = 6f, ramDamage = 2, score = 300
                };
        }
    }
}

// Oyuncuya gelen, ates eden kinematik dusman ucak.
// Vurus sayisi kadar can; her vurus 1 hasar. Turune gore boyut, hiz ve mermi deseni degisir.
[RequireComponent(typeof(Rigidbody))]
public class EnemyPlane : MonoBehaviour, IDamageable
{
    public int MaxHits { get; private set; }
    public int HitsRemaining { get; private set; }
    public Vector3 Velocity { get; private set; }
    public EnemyKind Kind { get; private set; }
    public EnemyBehavior Behavior { get; private set; }
    public float HitRadius { get; private set; } = 4.5f;
    public bool IsBoss => Kind >= EnemyKind.MidBoss;
    public bool IsStageBoss => Kind >= EnemyKind.Boss;
    public bool IsGround => Kind >= EnemyKind.GroundTurret && Kind <= EnemyKind.GroundCore;
    public bool IsCore => Kind == EnemyKind.GroundCore;
    public bool IsDead => dead;
    public string Title { get; private set; }

    /// <summary>PickupKind ya da -1. Vurulunca bu guclendirmeyi dusurur.</summary>
    public int CarrierDrop { get; private set; } = -1;
    public bool IsCarrier => CarrierDrop >= 0;

    private enum Pattern { Aimed, Fan, FanRing, Spiral, Snipe, None }

    private CombatManager mgr;
    private Rigidbody rb;
    private Propeller[] propellers;

    private float cruiseSpeed;
    private float turnRate;
    private float bank;

    // Organik donus: hedef yon yumusatilir (ani yon degisimi yok), donus hizi ivmeyle artar (yavas baslar) ve hedefe
    // yaklastikca azalir (yavaslayarak biter). Eskiden RotateTowards sabit hizla aninda baslayip aninda duruyordu.
    private const float DesireSmoothing = 3f;     // hedef yonun yumusatma hizi (1/sn): ~0,33 sn'de yeni yone
    private const float TurnAccelMul = 2.5f;      // tam donus hizina ~0,4 sn'de cikar
    private const float TurnEaseOut = 3f;         // kalan aci (derece) x bu = istenen donus hizi (yaklastikca yavaslar)
    private Vector3 smoothDesired;
    private float turnVel;                        // anlik donus hizi (derece/sn)
    private float climbVel;                       // duz gecen bombardimancinin tirmanma hizi
    private float weavePhase;
    private float weaveRate;

    private Vector3 heading;      // Strafe: sabit ucus yonu
    private float born;

    private float nextBurstTime;
    private int burstLeft;
    private float nextShotTime;
    private int volleyIndex;
    private bool dead;
    private GameObject smoke;
    private GameObject fire;

    // ---- Uzuv hasari: vurus noktasina gore kanat / kuyruk / motor / govde. Kanat, kuyruk ya da motor
    // bitince ucak "indirilmis" sayilir (puan, odul hemen) ve enkaz olarak yanarak dusmeye baslar.
    public enum HitZone { Body, WingL, WingR, Tail, Engine }
    private readonly int[] zoneHp = new int[5];

    // Vurus flasi (beyaz) ve enkaz durumu
    private Renderer[] rends;
    private float flashUntil;
    private bool flashing;
    private static MaterialPropertyBlock flashBlock;
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private bool wreck;
    private HitZone wreckCause;
    private Vector3 wreckVel;
    private Vector3 spin;        // yerel eksenlerde derece/sn (x yunuslama, y sapma, z yatis)
    private float wreckBorn;

    /// <summary>Indirildi ve dusuyor (artik hedef degil, mermi islemez).</summary>
    public bool IsWreck => wreck;

    /// <summary>As pilot: adli, daha dayanikli ve agresif; puani bes kat.</summary>
    public bool IsAce { get; private set; }

    public void MakeAce(string name)
    {
        IsAce = true;
        Title = "AS: " + name;
        MaxHits = Mathf.RoundToInt(MaxHits * 2.5f);
        HitsRemaining = MaxHits;
        SetupZones();
        burstSize += 3;
        burstCdMin *= 0.7f; burstCdMax *= 0.7f;
        turnRate *= 1.25f;
        cruiseSpeed *= 1.1f;
        canPeel = true;
    }
    private float breakUntil;
    private Vector3 breakDir;
    private float nextRamTime;

    // Son dalis: toplam canin %30'unun altina inen yarali ucak once niyetini gosterir (kirmizi cizgi), sonra oyuncuya dalar
    private const float DiveTelegraph = 1.0f;
    private bool diveTriggered;
    private float diveAt;
    private LineRenderer diveLine;
    /// <summary>Dalisa baslamis ucak. Vurularak dusurulurse ekstra odul verir; carpip olmesi vermez.</summary>
    public bool IsDiving { get; private set; }

    // Ates duzeni (turden gelir)
    private Pattern pattern = Pattern.Aimed;
    private int burstSize = 6;
    private float shotGap = 0.12f;
    private float aimSpread = 1f;   // nisan sapmasi carpani: surunun serisi dagilir, avcinin serisi isabetli
    private float burstCdMin = 2.5f, burstCdMax = 4f;
    private int bulletDamage = 1;
    private float fireRange = 650f;
    private float fireCone = 8f;
    private float bulletSpeed = 300f;
    private int ramDamage = 2;
    private bool canPeel = true;

    private const float MinClearance = 90f;
    private const float AimWindup = 0.5f;   // dogfight serisinin ilk mermisinden once uyari suresi (kilit sesi + '!')

    // Carpma (ram) menzili: mermi isabet yaricapindan (HitRadius) kucuk tutulur; kalkan/carpma "uzaktan" olmasin
    private float RamRange
    {
        get
        {
            GameSettings gs = GameSettings.Current;
            return HitRadius * gs.ramRangeScale + gs.ramPlayerRadius;
        }
    }
    private const float StrafeLifetime = 70f;

    // Ozel boss govdeleri: hareket ve ates BossBrain'dedir
    private bool scripted;
    private BossBrain brain;

    /// <summary>Kodla uretilen boss govdesi icin baslatma (BossBrain hareketi ve atesi yonetir).</summary>
    public void InitScripted(CombatManager manager, int hits, EnemyKind kind, string title, float radius,
        int ram, int carrierDrop)
    {
        mgr = manager;
        Kind = kind;
        Behavior = EnemyBehavior.Chase;
        CarrierDrop = carrierDrop;
        MaxHits = hits;
        HitsRemaining = hits;
        Title = title;
        HitRadius = radius;
        ramDamage = ram;
        scripted = true;
        born = Time.time;

        rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

        brain = GetComponent<BossBrain>();
    }

    /// <summary>Yer ustu birimi (dusman ussu): hareket etmez, carpma hasari vermez; ates GroundUnit'te.</summary>
    public void InitGround(CombatManager manager, int hits, EnemyKind kind, string title, float radius, int carrierDrop)
    {
        mgr = manager;
        Kind = kind;
        Behavior = EnemyBehavior.Chase;
        CarrierDrop = carrierDrop;
        MaxHits = hits;
        HitsRemaining = hits;
        Title = title;
        HitRadius = radius;
        ramDamage = 0;
        scripted = true;
        born = Time.time;

        rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
        brain = null;
        Velocity = Vector3.zero;
    }

    /// <summary>Yer birimi parcalari kurulduktan sonra (BaseBuilder) vurus flasi icin renderer'lari toplar.</summary>
    public void CollectRenderers()
    {
        rends = GetComponentsInChildren<Renderer>();
    }

    public void SetVelocity(Vector3 v) { Velocity = v; }

    /// <summary>Motor efekti icin gaz (0-1): seyirde ~0,4, yetisirken / dalarken / one gecerken artar.</summary>
    public float EngineThrottle => cruiseSpeed > 1f ? Mathf.Clamp01(0.4f + (Velocity.magnitude / cruiseSpeed - 1f) * 1.3f) : 0.4f;

    /// <summary>
    /// Bu dusman oyuncuya ates etmek uzere mi (HUD uyarisi)? Seri atiyor, menzilde ve burnu oyuncuya
    /// donuk ya da desenli atisa 0,8 sn kala. Boss'lar icin her zaman false (zaten hep isaretli).
    /// </summary>
    public bool IsAimingAt(Vector3 targetPos)
    {
        if (dead || IsBoss || IsGround || !mgr.enemiesShoot) return false;

        float dist = Vector3.Distance(transform.position, targetPos);
        if (dist > fireRange) return false;

        if (pattern == Pattern.None) return Kind == EnemyKind.Kamikaze && dist < 650f;   // kamikaze: yaklasiyor uyarisi
        if (pattern == Pattern.Aimed)
            return burstLeft > 0 || Vector3.Angle(transform.forward, targetPos - transform.position) < fireCone * 2.5f;

        return nextBurstTime - Time.time < 0.8f;
    }

    /// <summary>Boss mermisi: sahibi bu govde, kalkan/dost ates kurallari Bullet'ta.</summary>
    public Bullet FireBullet(Vector3 pos, Vector3 dir, float speed, int damage, float lifetime = 0f)
    {
        if (mgr == null || mgr.EnemyBulletPrefab == null) return null;
        // Boss mermileri yavaslatilir (kacmasi kolay); menzil ayni kalsin diye omur ayni oranda uzar
        float mul = BossBrain.BulletSpeedMul;   // ayar x zorluk
        Bullet b = Bullet.Spawn(mgr.EnemyBulletPrefab, pos, dir, speed * mul, damage, transform, false);
        if (b != null) b.lifetime = (lifetime > 0f ? lifetime : b.lifetime) / mul;
        return b;
    }

    private void ScriptedStep(Transform target)
    {
        if (IsGround) return;   // yer birimleri carpmaz (GroundUnit ates eder)

        // Temas: boss parcalarindan herhangi birine yaklasma
        float d = brain != null ? brain.DistanceTo(target.position) : Vector3.Distance(rb.position, target.position);
        if (d < GameSettings.Current.ramPlayerRadius + 1f && Time.time >= nextRamTime)
        {
            nextRamTime = Time.time + 1.2f;
            DamagePlayerByRam(target.position);
        }
    }

    public void Init(CombatManager manager, int hits, float cruise, float turn, EnemyKind kind,
        EnemyBehavior behavior, Vector3 flightHeading, int carrierDrop)
    {
        mgr = manager;
        Kind = kind;
        Behavior = behavior;
        CarrierDrop = carrierDrop;
        MaxHits = hits;
        HitsRemaining = hits;
        cruiseSpeed = cruise;
        turnRate = turn;
        weavePhase = Random.value * 10f;
        weaveRate = Random.Range(0.6f, 1.2f);
        born = Time.time;

        heading = flightHeading.sqrMagnitude > 0.01f ? flightHeading.normalized : transform.forward;

        EnemyKindInfo info = EnemyKinds.Get(kind);
        HitRadius = info.radius;
        ramDamage = info.ramDamage;

        switch (kind)
        {
            case EnemyKind.Swarmer:
                // Hafif avci: dogfight, kisa seriler; kopup tekrar gelir
                pattern = Pattern.Aimed;
                // Suru: uzun, dagilan seri (sikistirir ama kolay kacilir)
                burstSize = 7; shotGap = 0.065f; burstCdMin = 3.5f; burstCdMax = 5f; aimSpread = 1.9f;
                bulletDamage = 1; fireRange = 600f; fireCone = 7f; bulletSpeed = 330f;
                break;
            case EnemyKind.Heavy:
                pattern = Pattern.FanRing;
                burstCdMin = 3f; burstCdMax = 4.5f;
                bulletDamage = 2; fireRange = 760f; bulletSpeed = 190f;
                break;
            case EnemyKind.Kamikaze:
                pattern = Pattern.None;
                canPeel = false;
                break;
            case EnemyKind.Sniper:
                pattern = Pattern.Snipe;
                burstCdMin = 2.6f; burstCdMax = 3.8f;
                bulletDamage = 2; fireRange = 950f; bulletSpeed = 340f;
                canPeel = false;
                break;
            case EnemyKind.Boss:
                pattern = Pattern.FanRing;
                burstCdMin = 1.4f; burstCdMax = 2.2f;
                bulletDamage = 2; fireRange = 1000f; bulletSpeed = 210f;
                canPeel = false;
                break;
            case EnemyKind.FinalBoss:
                pattern = Pattern.Spiral;
                burstCdMin = 1.0f; burstCdMax = 1.6f;
                bulletDamage = 2; fireRange = 1100f; bulletSpeed = 210f;
                canPeel = false;
                break;
            default:
                // Avci: kisa, hizli ve isabetli seri (yerinde duran oyuncuyu cezalandirir)
                pattern = Pattern.Aimed;
                burstSize = 3; shotGap = 0.16f; burstCdMin = 2.8f; burstCdMax = 4.2f; aimSpread = 0.45f;
                bulletDamage = 1; fireRange = 650f; fireCone = 7f; bulletSpeed = 420f;
                break;
        }

        burstCdMin *= Difficulty.FireCooldownMul;
        burstCdMax *= Difficulty.FireCooldownMul;
        nextBurstTime = Time.time + Random.Range(1.5f, 3.5f);

        rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

        Velocity = (behavior == EnemyBehavior.Strafe ? heading : transform.forward) * cruiseSpeed;

        propellers = GetComponentsInChildren<Propeller>();
        foreach (var p in propellers) p.Rpm = 1800f;

        SetupZones();
        rends = GetComponentsInChildren<Renderer>();
    }

    // Uzuv canlari: kanat %50, kuyruk %55, motor %65 (govde = toplam). Kanada nisan alan daha cabuk indirir.
    private void SetupZones()
    {
        int w = Mathf.Max(1, Mathf.CeilToInt(MaxHits * 0.5f));
        zoneHp[(int)HitZone.WingL] = w;
        zoneHp[(int)HitZone.WingR] = w;
        zoneHp[(int)HitZone.Tail] = Mathf.Max(1, Mathf.CeilToInt(MaxHits * 0.55f));
        zoneHp[(int)HitZone.Engine] = Mathf.Max(1, Mathf.CeilToInt(MaxHits * 0.65f));
        zoneHp[(int)HitZone.Body] = MaxHits;
    }

    // Vurus noktasi ucagin yerel uzayinda hangi uzuvda? (Tek govde collider'i: nokta siluetin uzerindedir;
    // |x| genis = kanat, arkada = kuyruk, onde = motor.)
    private HitZone ZoneAt(Vector3 point)
    {
        Vector3 p = transform.InverseTransformPoint(point);
        if (Mathf.Abs(p.x) > 1.0f) return p.x < 0f ? HitZone.WingL : HitZone.WingR;
        if (p.z < -1.3f) return HitZone.Tail;
        if (p.z > 1.0f) return HitZone.Engine;
        return HitZone.Body;
    }

    public static string ZoneName(HitZone z)
    {
        switch (z)
        {
            case HitZone.WingL: case HitZone.WingR: return "KANAT";
            case HitZone.Tail: return "KUYRUK";
            case HitZone.Engine: return "MOTOR";
            default: return "";
        }
    }

    private void Update()
    {
        // Vurus flasi: kisa beyaz parlama (tum parcalar)
        bool want = Time.time < flashUntil && !wreck;
        if (want != flashing)
        {
            flashing = want;
            ApplyFlash(want);
        }

        if (wreck) WreckStep();
    }

    private void ApplyFlash(bool on)
    {
        if (rends == null) return;
        if (flashBlock == null)
        {
            flashBlock = new MaterialPropertyBlock();
            flashBlock.SetColor(BaseColorId, new Color(1.6f, 1.6f, 1.6f));
            flashBlock.SetColor(ColorId, new Color(1.6f, 1.6f, 1.6f));
        }
        foreach (var r in rends)
        {
            if (r == null || r is TrailRenderer || r is ParticleSystemRenderer) continue;
            r.SetPropertyBlock(on ? flashBlock : null);
        }
    }

    private void FixedUpdate()
    {
        if (dead || mgr == null) return;

        Transform target = mgr.PlayerTransform;
        if (target == null) return;

        if (scripted) ScriptedStep(target);
        else if (Behavior == EnemyBehavior.Strafe) StrafeStep(target);
        else ChaseStep(target);
    }

    // ------------------------------------------------------------- strafe

    private void StrafeStep(Transform target)
    {
        float dt = Time.fixedDeltaTime;
        Vector3 pos = rb.position;
        Vector3 toPlayer = target.position - pos;
        float dist = toPlayer.magnitude;

        // Oyuncuyu gecip uzaklastiysa ya da cok uzun suredir ucuyorsa elenir (kacti)
        float along = Vector3.Dot(pos - target.position, heading);
        if (along > 900f || Time.time - born > StrafeLifetime)
        {
            Destroy(gameObject);
            return;
        }

        // Arazi: yaklasan tepeyi asmak icin burnunu kaldirir
        Vector3 ahead = pos + heading * cruiseSpeed * 2.2f;
        float ground = Mathf.Max(mgr.GroundHeight(pos), mgr.GroundHeight(ahead));
        // Tirmanma yumusak baslar ve biter (eskiden 0,9 rad/sn ile aninda burun kaldiriyordu)
        float wantClimb = pos.y - ground < 70f ? 0.9f : 0f;
        climbVel = Mathf.MoveTowards(climbVel, wantClimb, 2.5f * dt);
        if (climbVel > 0.001f)
        {
            heading.y = Mathf.MoveTowards(heading.y, 0.4f, climbVel * dt);
            heading.Normalize();
        }

        Vector3 newPos = pos + heading * cruiseSpeed * dt;
        if (newPos.y < mgr.GroundHeight(newPos) + 2.5f)
        {
            Die(IsBoss, HitZone.Body, false);
            return;
        }

        // Hafif yatis (gorsel)
        Quaternion newRot = Quaternion.LookRotation(heading, Vector3.up);

        Velocity = heading * cruiseSpeed;
        rb.MovePosition(newPos);
        rb.MoveRotation(newRot);

        if (dist < RamRange && Time.time >= nextRamTime)
        {
            nextRamTime = Time.time + 1.2f;
            Ram(newPos);
            if (dead) return;
        }

        // Yalnizca oyuncuya dogru ilerlerken ates eder
        if (along < 0f) TryShoot(newPos, newRot, heading, target, dist);
    }

    // -------------------------------------------------------------- chase

    private void ChaseStep(Transform target)
    {
        float dt = Time.fixedDeltaTime;
        Vector3 pos = rb.position;
        Quaternion rot = rb.rotation;
        Vector3 fwd = rot * Vector3.forward;

        Vector3 toPlayer = target.position - pos;
        float dist = toPlayer.magnitude;

        // Cok uzaklastiysa elenir (boss haric)
        if (dist > 3200f && !IsBoss)
        {
            Destroy(gameObject);
            return;
        }

        // Son dalis: yarali ucak (toplam canin %30'unun altinda) niyetini gosterir, sonra oyuncuya dalar
        if (!diveTriggered && !IsBoss && HitsRemaining * 10 <= MaxHits * 3 && dist < 900f && dist > 220f)
        {
            diveTriggered = true;
            diveAt = Time.time + DiveTelegraph;
            Sfx.PlayAt(Sfx.Warning, pos, 0.5f);
        }
        if (diveTriggered && !IsDiving && Time.time >= diveAt) IsDiving = true;
        UpdateDiveLine(pos, target.position, diveTriggered && !IsDiving);

        // Hiz: uzaktaysa yetis, yakindaysa biraz yavasla
        float speed = cruiseSpeed;
        if (dist > 700f) speed *= 1.4f;
        else if (dist < 150f) speed *= 0.9f;
        if (Kind == EnemyKind.Kamikaze && dist < 500f) speed *= 1.25f;   // son atilim
        if (IsDiving) speed *= 1.35f;
        speed = Mathf.Max(speed, mgr.PlayerSpeed * (IsBoss ? 0.45f : 0.95f));

        // Onde tutma (lead) ile takip: kapanma hiziyla (karsidan gelirken kisa, kuyruktan takipte uzun)
        float closing = Mathf.Max(20f, speed - Vector3.Dot(mgr.PlayerVelocity, toPlayer / Mathf.Max(dist, 0.1f)));
        float leadTime = Mathf.Clamp(dist / closing, 0f, 2.5f);
        Vector3 aim = target.position + mgr.PlayerVelocity * leadTime * 0.8f;
        Vector3 desired = (aim - pos).normalized;

        // Arkadan sinsice vurmak yok: oyuncunun arkasina dusen ucak once onune gecer (hizlanir, yandan asar),
        // sonra karsidan dalis yapar. Boyle ucaklarin nereye gittigi bellidir ve hep gorus alaninda vurusurlar.
        Vector3 pf = PlayerForward(target);
        float along = Vector3.Dot(pos - target.position, pf);
        // Histerezis: arkaya dusunce one gecme baslar, oyuncunun yeterince onune varinca biter (esikte titremesin)
        if (!IsBoss && !IsDiving && along < OvertakeBehind) overtaking = true;
        else if (overtaking && (along > OvertakeDone || IsDiving))
        {
            overtaking = false;
            nextBurstTime = Mathf.Min(nextBurstTime, Time.time + 0.2f);   // karsidan dalisa hazir: seri bekletilmez
        }
        if (overtaking)
        {
            Vector3 pr = Vector3.Cross(Vector3.up, pf);
            float sideSign = Vector3.Dot(pos - target.position, pr) >= 0f ? 1f : -1f;
            Vector3 passPoint = target.position + pf * OvertakeAhead + pr * (sideSign * OvertakeSide) + Vector3.up * 50f;
            desired = (passPoint - pos).normalized;
            speed = Mathf.Max(speed * 1.35f, mgr.PlayerSpeed * 1.8f + 20f);
        }

        // Yalpalama: nisan almayi zorlastirir
        if (dist > 250f && !IsBoss && !IsDiving && !overtaking)
        {
            float weave = Mathf.Sin(Time.time * weaveRate + weavePhase) * 10f;
            desired = Quaternion.AngleAxis(weave, Vector3.up) * desired;
        }

        // Nisanci: mesafeyi korur (yakinsa uzaklasir, orta mesafede etrafinda doner, uzaksa yaklasir)
        if (Kind == EnemyKind.Sniper && dist > 1f && !overtaking)
        {
            Vector3 dirTo = toPlayer / dist;
            Vector3 side = Vector3.Cross(Vector3.up, dirTo) * (Mathf.Sin(weavePhase) > 0f ? 1f : -1f);
            if (dist < 380f) desired = (-dirTo * 0.8f + side * 0.6f + Vector3.up * 0.1f).normalized;
            else if (dist < 580f) desired = (side + dirTo * 0.25f).normalized;
        }

        // Carpisma onleme: cok yakinda ve oyuncuya dogru uciyorsa yana/yukari kirilir (peel-off)
        float peelDist = 80f + 25f * transform.localScale.x;
        if (Time.time < breakUntil)
        {
            desired = breakDir;
        }
        else if (canPeel && !IsDiving && dist < peelDist && Vector3.Dot(fwd, toPlayer / Mathf.Max(dist, 0.1f)) > 0.4f)
        {
            float side = Random.value < 0.5f ? -1f : 1f;
            breakDir = (fwd * 0.5f + Vector3.up * 0.55f + Vector3.Cross(Vector3.up, fwd).normalized * side * 0.9f).normalized;
            breakUntil = Time.time + Random.Range(1.6f, 2.6f);
            desired = breakDir;
        }

        // Zemine carpma onleme
        Vector3 ahead = pos + fwd * speed * 2.5f;
        float ground = Mathf.Max(mgr.GroundHeight(pos), mgr.GroundHeight(ahead));
        float clearance = pos.y - ground;
        if (clearance < MinClearance)
        {
            float pull = Mathf.Clamp01((MinClearance - clearance) / MinClearance);
            desired.y = Mathf.Max(desired.y, pull * 0.9f);
        }

        // Dikey aci siniri (LookRotation icin)
        desired.y = Mathf.Clamp(desired.y, -0.7f, 0.7f);
        Vector3 flat = new Vector3(desired.x, 0f, desired.z);
        if (flat.sqrMagnitude < 1e-4f) flat = new Vector3(fwd.x, 0f, fwd.z);
        flat = flat.normalized * Mathf.Sqrt(1f - desired.y * desired.y);
        desired = new Vector3(flat.x, desired.y, flat.z).normalized;

        float maxTurn = turnRate * (IsDiving ? 1.4f : 1f);
        if (smoothDesired.sqrMagnitude < 0.5f) smoothDesired = fwd;
        smoothDesired = Vector3.Slerp(smoothDesired, desired, 1f - Mathf.Exp(-DesireSmoothing * dt)).normalized;
        float wantTurn = Mathf.Min(maxTurn, Vector3.Angle(fwd, smoothDesired) * TurnEaseOut);
        turnVel = Mathf.MoveTowards(turnVel, wantTurn, maxTurn * TurnAccelMul * dt);
        Vector3 newFwd = Vector3.RotateTowards(fwd, smoothDesired, turnVel * Mathf.Deg2Rad * dt, 0f);

        // Gorsel yatis: donuse gore
        float yawRate = Vector3.SignedAngle(fwd, newFwd, Vector3.up) / dt;
        float bankTarget = Mathf.Clamp(yawRate * 0.7f, -70f, 70f);
        if (HitsRemaining * 10 <= MaxHits * 3) bankTarget += Mathf.Sin(Time.time * 17f + weavePhase) * 14f;   // agir hasar: yalpalar
        bank = Mathf.Lerp(bank, bankTarget, 4f * dt);

        Quaternion newRot = Quaternion.LookRotation(newFwd, Vector3.up) * Quaternion.Euler(0f, 0f, -bank);
        Vector3 newPos = pos + newFwd * speed * dt;

        // Yere carpti
        if (newPos.y < mgr.GroundHeight(newPos) + 2.5f)
        {
            Die(IsBoss, HitZone.Body, false);   // boss yere carpsa da boss odulu/asama sonu verilir
            return;
        }

        Velocity = newFwd * speed;
        rb.MovePosition(newPos);
        rb.MoveRotation(newRot);

        // Temas: oyuncu fizigi dusmanlarla carpismaz (layer); hasar yakinliktan hesaplanir
        if (dist < RamRange && Time.time >= nextRamTime)
        {
            nextRamTime = Time.time + 1.2f;
            Ram(newPos);
            if (dead) return;
        }

        TryShoot(newPos, newRot, newFwd, target, dist);
    }

    private void UpdateDiveLine(Vector3 pos, Vector3 targetPos, bool on)
    {
        if (!on) { if (diveLine != null) diveLine.enabled = false; return; }
        Vector3 to = targetPos - pos;
        if (to.sqrMagnitude < 1e-4f) return;
        if (diveLine == null) MakeDiveLine();
        diveLine.enabled = true;
        diveLine.SetPosition(0, pos);
        diveLine.SetPosition(1, pos + to.normalized * Mathf.Min(to.magnitude, 500f));
    }

    private void MakeDiveLine()
    {
        var go = new GameObject("DiveWarn");
        go.transform.SetParent(transform, false);
        diveLine = go.AddComponent<LineRenderer>();
        diveLine.useWorldSpace = true;
        diveLine.positionCount = 2;
        diveLine.startWidth = 0.6f;
        diveLine.endWidth = 0.6f;
        diveLine.numCapVertices = 2;
        diveLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        diveLine.sharedMaterial = new Material(mgr.bossGlow != null ? mgr.bossGlow : mgr.fxMaterial);
        Color c = new Color(1f, 0.2f, 0.15f);
        diveLine.sharedMaterial.color = c;
        if (diveLine.sharedMaterial.HasProperty("_BaseColor")) diveLine.sharedMaterial.SetColor("_BaseColor", c);
    }

    private void Ram(Vector3 point)
    {
        DamagePlayerByRam(point);
        IsDiving = false;   // carpmayla olen ucak son dalis bonusu almaz

        // Boss carpmadan etkilenmez; digerleri patlar. Oldurme sayilip sayilmayacagi GameSettings'te.
        if (!IsBoss) Die(GameSettings.Current.ramKillGivesScore, HitZone.Body, false);
    }

    // Carpisma bize hasar verir: tur hasari x carpan; varsayilan olarak kalkani atlar (bedava olmasin)
    private void DamagePlayerByRam(Vector3 point)
    {
        PlayerHealth ph = mgr.CurrentPlayerHealth;
        if (ph == null) return;

        GameSettings gs = GameSettings.Current;
        int damage = Mathf.Max(1, Mathf.RoundToInt(ramDamage * gs.ramDamageMultiplier));
        ph.TakeHit(damage, point, gs.ramIgnoresShield);
    }

    // -------------------------------------------------------------- ates

    // Oyuncunun arkasi: bu cizginin gerisindeki ucak ates etmez, one gecmeye calisir
    private bool overtaking;                     // one gecme modunda (histerezisli)
    private const float OvertakeBehind = -70f;
    private const float OvertakeDone = 600f;     // bu kadar one gecince yeniden oyuncuya doner (karsidan dalis)
    private const float OvertakeAhead = 800f;    // one gecis noktasi: oyuncunun bu kadar onunde (yakin olursa yaninda yorungeye giriyor)
    private const float OvertakeSide = 350f;     // ve bu kadar yaninda (sonra donup karsidan dalis: hep gorus alaninda)

    private Vector3 PlayerForward(Transform target)
    {
        Vector3 v = mgr.PlayerVelocity;
        Vector3 f = v.sqrMagnitude > 25f ? v : target.forward;
        f = Vector3.ProjectOnPlane(f, Vector3.up);
        return f.sqrMagnitude > 0.01f ? f.normalized : Vector3.forward;
    }

    private void TryShoot(Vector3 pos, Quaternion rot, Vector3 fwd, Transform target, float dist)
    {
        if (!mgr.enemiesShoot || mgr.EnemyBulletPrefab == null || pattern == Pattern.None) return;
        if (!IsBoss && mgr.BossFightActive) return;   // boss savasinda yalniz boss ates eder
        // Arkadan ates yok: oyuncu goremedigi ucaktan vurulmaz (kamikaze zaten ates etmez)
        if (!IsBoss && Vector3.Dot(pos - target.position, PlayerForward(target)) < OvertakeBehind) return;

        if (pattern == Pattern.Aimed)
        {
            // Karsidan dalis (oyuncunun onunde, ona dogru): gecis kisa surdugu icin koni genis, hazirlik kisa.
            // Kuyruktan takipte (gorus icinde ama arkada) eski dar koni ve uyarili hazirlik kalir.
            bool headOn = Vector3.Dot(pos - target.position, PlayerForward(target)) > 0f;
            float cone = headOn ? fireCone * 3.5f : fireCone;
            float windup = headOn ? 0.1f : AimWindup;

            // Dogfight: nisan alinca kisa seri. Aci, oyuncunun SU ANKI yerine degil merminin varacagi noktaya gore
            // olculur: karsidan gecen ucak burnunu onde tutma noktasina ceviriyordu ve hic ates edemiyordu.
            Vector3 predicted = target.position + mgr.PlayerVelocity * (dist / bulletSpeed);
            if (burstLeft <= 0 && Time.time >= nextBurstTime && dist < fireRange
                && Vector3.Angle(fwd, predicted - pos) < cone)
            {
                burstLeft = burstSize;
                nextBurstTime = Time.time + Random.Range(burstCdMin, burstCdMax);
                nextShotTime = Time.time + windup;   // ilk mermi bir an sonra: oyuncu tepki verebilsin
                mgr.OnEnemyTargeting(this, dist);   // "nisan aldi" uyarisi
            }

            if (burstLeft > 0 && Time.time >= nextShotTime)
            {
                burstLeft--;
                nextShotTime = Time.time + shotGap;

                float s = transform.localScale.x;
                Vector3 muzzle = pos + rot * new Vector3((Random.value < 0.5f ? -0.9f : 0.9f) * s, 0f, 1.6f * s);
                Vector3 aim = target.position + mgr.PlayerVelocity * (dist / bulletSpeed)
                              + Random.insideUnitSphere * (mgr.enemyAimError * Difficulty.AimErrorMul * aimSpread);
                Spawn(muzzle, (aim - muzzle).normalized, bulletSpeed);
                Sfx.PlayAt(Sfx.Shot, muzzle, 0.25f);
            }
            return;
        }

        // Desenli atis: belirli aralikla tek salvo (yelpaze / halka / sarmal)
        if (Time.time < nextBurstTime || dist > fireRange) return;
        nextBurstTime = Time.time + Random.Range(burstCdMin, burstCdMax);

        Vector3 origin = pos + fwd * (2f * transform.localScale.x);
        Vector3 toT = target.position + mgr.PlayerVelocity * (dist / bulletSpeed) * 0.8f
                      + Random.insideUnitSphere * (mgr.enemyAimError * 0.5f * Difficulty.AimErrorMul) - origin;
        Vector3 dir = toT.normalized;

        FireVolley(origin, dir);
        volleyIndex++;
        Sfx.PlayAt(Sfx.Shot, origin, 0.3f);
    }

    private void FireVolley(Vector3 origin, Vector3 dir)
    {
        switch (pattern)
        {
            case Pattern.Fan:
                FanShot(origin, dir, 3, 12f);
                break;

            case Pattern.FanRing:
                // Heavy/Boss: bir salvo yelpaze, bir salvo hedefe yonelik halka
                if (volleyIndex % 2 == 0) FanShot(origin, dir, IsBoss ? 5 : 3, IsBoss ? 16f : 11f);
                else RingShot(origin, dir, IsBoss ? 14 : 10, volleyIndex * 9f);
                break;

            case Pattern.Snipe:
                // Tek, hizli ve isabetli mermi (oyuncu yerinde durursa vurur)
                Spawn(origin, dir, bulletSpeed);
                break;

            case Pattern.Spiral:
                // Final boss: donen halka + her ucuncu salvoda genis yelpaze
                if (volleyIndex % 3 == 2) FanShot(origin, dir, 7, 22f);
                else RingShot(origin, dir, 12, volleyIndex * 13f);
                break;
        }
    }

    // Hedefe dogru yatay yelpaze (merkez mermi tam hedefe)
    private void FanShot(Vector3 origin, Vector3 dir, int count, float totalAngle)
    {
        float step = count > 1 ? totalAngle / (count - 1) : 0f;
        for (int i = 0; i < count; i++)
        {
            float off = count > 1 ? -totalAngle * 0.5f + step * i : 0f;
            Vector3 d = Quaternion.AngleAxis(off, Vector3.up) * dir;
            Spawn(origin, d, bulletSpeed);
        }
    }

    // Hedefe dogru giden konik halka: merkezde hedefe yonelik bir mermi + iki halka katmani.
    // Oyuncu yerinde durursa merkez mermi vurur; halkalar kenarlardan gecer.
    private void RingShot(Vector3 origin, Vector3 dir, int count, float angleOffset)
    {
        Vector3 up = Mathf.Abs(Vector3.Dot(dir, Vector3.up)) > 0.95f ? Vector3.right : Vector3.up;
        Vector3 u = Vector3.Cross(dir, up).normalized;
        Vector3 v = Vector3.Cross(dir, u).normalized;

        Spawn(origin, dir, bulletSpeed);

        for (int layer = 0; layer < 2; layer++)
        {
            float tilt = (layer == 0 ? 7f : 14f) * Mathf.Deg2Rad;
            int n = layer == 0 ? Mathf.Max(6, count / 2) : count;
            float offset = angleOffset + (layer == 1 ? 180f / n : 0f);

            for (int i = 0; i < n; i++)
            {
                float a = (offset + 360f * i / n) * Mathf.Deg2Rad;
                Vector3 d = dir * Mathf.Cos(tilt) + (u * Mathf.Cos(a) + v * Mathf.Sin(a)) * Mathf.Sin(tilt);
                Spawn(origin, d.normalized, bulletSpeed * (layer == 0 ? 1f : 0.9f));
            }
        }
    }

    private void Spawn(Vector3 pos, Vector3 dir, float speed)
    {
        Bullet.Spawn(mgr.EnemyBulletPrefab, pos, dir, speed, bulletDamage, transform, false);
    }

    // ------------------------------------------------------------- hasar

    // Denge olcumu (test komutu "info"): bu dusmana verilen toplam hasar ve ilk vurustan beri DPS
    public int TotalDamage { get; private set; }
    public float FirstHitTime { get; private set; } = -1f;
    public float SpawnTime => born;

    public float Dps
    {
        get { return FirstHitTime < 0f ? 0f : TotalDamage / Mathf.Max(0.5f, Time.time - FirstHitTime); }
    }

    public void DebugSetHitsFraction(float fraction)
    {
        HitsRemaining = Mathf.Clamp(Mathf.RoundToInt(MaxHits * Mathf.Clamp01(fraction)), 1, MaxHits);
    }

    /// <summary>Alan hasari (fuze, bomba, plazma): uzuv secilmez, yalnizca toplam can duser.</summary>
    public void TakeHit(int damage, Vector3 point)
    {
        ApplyDamage(damage, point, HitZone.Body);
    }

    /// <summary>
    /// Mermi / isin isabeti, vurulan collider ile: boss'ta collider'in ait oldugu uzvun (top, kol, kanat...) cani da duser.
    /// </summary>
    public void TakeHitLocal(int damage, Vector3 point, Collider hitCollider)
    {
        if (dead) return;
        if (brain != null && hitCollider != null) brain.OnPartHit(hitCollider, damage, point);
        TakeHitLocal(damage, point);
    }

    /// <summary>Mermi / isin isabeti: vurus noktasina gore uzuv hasari da islenir (kanat koparsa ucak duser).</summary>
    public void TakeHitLocal(int damage, Vector3 point)
    {
        if (dead) return;
        HitZone z = scripted || IsGround ? HitZone.Body : ZoneAt(point);
        if (z != HitZone.Body) zoneHp[(int)z] -= damage;
        ApplyDamage(damage, point, z);
    }

    private void ApplyDamage(int damage, Vector3 point, HitZone z)
    {
        if (dead) return;

        if (FirstHitTime < 0f) FirstHitTime = Time.time;
        TotalDamage += damage;
        HitsRemaining -= damage;
        flashUntil = Time.time + 0.07f;
        mgr.OnEnemyHit(this, point);

        if (HitsRemaining <= 0)
        {
            Die(true, HitZone.Body, true);
            return;
        }
        if (z != HitZone.Body && zoneHp[(int)z] <= 0)
        {
            Die(true, z, true);
            return;
        }

        UpdateDamageLook();
    }

    // Hasar halleri: %60'in altinda duman, %30'un altinda alev
    private void UpdateDamageLook()
    {
        if (mgr.SmokeTrailPrefab == null) return;
        if (smoke == null && HitsRemaining * 10 <= MaxHits * 6)
        {
            smoke = Instantiate(mgr.SmokeTrailPrefab, transform);
            smoke.transform.localPosition = new Vector3(0f, 0.2f, -0.5f);
        }
        if (fire == null && HitsRemaining * 10 <= MaxHits * 3) StartFire(1f);
    }

    // Alev: duman prefabinin turuncu, iri ve sik kopyasi (burunda)
    private void StartFire(float scale)
    {
        if (mgr.SmokeTrailPrefab == null) return;
        if (fire == null)
        {
            fire = Instantiate(mgr.SmokeTrailPrefab, transform);
            fire.transform.localPosition = new Vector3(0f, 0f, 0.8f);
        }
        foreach (var ps in fire.GetComponentsInChildren<ParticleSystem>())
        {
            // Duman prefabinin gri omur-rengi alevi kahverengiye bogar: parlak sari -> turuncu -> kizil -> koyu duman
            var main = ps.main;
            main.startColor = Color.white;
            main.startSizeMultiplier = main.startSizeMultiplier * 1.1f * scale;
            main.startLifetimeMultiplier = main.startLifetimeMultiplier * 0.55f;
            var em = ps.emission;
            em.rateOverTimeMultiplier = em.rateOverTimeMultiplier * 3f * scale;

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(1f, 0.95f, 0.6f), 0f),
                    new GradientColorKey(new Color(1f, 0.55f, 0.1f), 0.3f),
                    new GradientColorKey(new Color(0.75f, 0.18f, 0.04f), 0.6f),
                    new GradientColorKey(new Color(0.15f, 0.15f, 0.15f), 1f)
                },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.9f, 0.5f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(g);
        }
    }

    private void Die(bool byPlayer, HitZone cause, bool wreckage)
    {
        if (dead) return;
        dead = true;
        if (diveLine != null) diveLine.enabled = false;

        // Boss: govde hemen yok olmaz, uclardan iceri asamali patlar (BossBrain.Death.cs)
        BossBrain brain = IsBoss ? GetComponent<BossBrain>() : null;
        bool staged = brain != null && brain.BeginDeath();

        mgr.OnEnemyDestroyed(this, byPlayer, cause);
        if (staged)
        {
            ReleaseSmoke();
            return;
        }

        // Hava ucagi oyuncu tarafindan indirildiyse yanarak duser; yere carpan / bindiren / yer birimi hemen gider
        if (wreckage && !IsGround && !scripted)
        {
            BeginWreck(cause);
            return;
        }

        ReleaseSmoke();
        Destroy(gameObject);
    }

    // Duman ve alev izi sahnede kalsin (kendi omru kadar)
    private void ReleaseSmoke()
    {
        foreach (var g in new[] { smoke, fire })
        {
            if (g == null) continue;
            g.transform.SetParent(null);
            foreach (var ps in g.GetComponentsInChildren<ParticleSystem>()) ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            Destroy(g, 6f);
        }
        smoke = null;
        fire = null;
    }

    // -------------------------------------------------------------- enkaz

    private void BeginWreck(HitZone cause)
    {
        wreck = true;
        wreckCause = cause;
        wreckBorn = Time.time;
        wreckVel = Velocity;
        ApplyFlash(false);
        flashing = false;

        // Artik hedef degil: mermi gecsin, pervane dursun, kanat ucu izleri acilsin
        foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false;
        if (propellers != null) foreach (var p in propellers) if (p != null) p.Rpm = 0f;
        foreach (var tr in GetComponentsInChildren<TrailRenderer>()) tr.emitting = true;

        DetachParts(cause);
        StartFire(2.2f);

        // Dusus bicimi sebebe gore: kanat kopunca o yana yatarak spiral, kuyruk gidince burun asagi takla,
        // motor gidince suzulerek yavas donus, govde patlayinca hizli takla
        switch (cause)
        {
            case HitZone.WingL: spin = new Vector3(35f, 0f, 240f); break;
            case HitZone.WingR: spin = new Vector3(35f, 0f, -240f); break;
            case HitZone.Tail: spin = new Vector3(-170f, 90f, 50f); break;
            case HitZone.Engine: spin = new Vector3(-45f, 0f, 60f); break;
            default: spin = new Vector3(Random.Range(90f, 150f), Random.Range(-90f, 90f), Random.value < 0.5f ? 260f : -260f); break;
        }
    }

    // Kopan parcalar: kaybedilen taraftaki cocuklar (kanat, kanat ucu, destek, isaret) ayri nesne olarak doner ve
    // duser. Tek parca kanatlar (planor "Wing", biplan "UpperWing/LowerWing") ortadan ikiye bolunur: bir yarisi kalir,
    // digeri kopar. Kuyruk kaybinda gerideki parcalar (z < -1,3) kopar. Iz ve parcacik nesneleri dokunulmaz.
    private void DetachParts(HitZone cause)
    {
        if (cause == HitZone.Engine || cause == HitZone.Body) return;
        float side = cause == HitZone.WingL ? -1f : 1f;

        var loose = new System.Collections.Generic.List<Transform>();
        var children = new System.Collections.Generic.List<Transform>();
        foreach (Transform c in transform) children.Add(c);

        foreach (Transform c in children)
        {
            if (c.GetComponent<TrailRenderer>() != null || c.GetComponent<ParticleSystem>() != null || c.GetComponent<Propeller>() != null) continue;
            Vector3 lp = c.localPosition;

            if (cause == HitZone.Tail)
            {
                if (lp.z < -1.3f) loose.Add(c);
                continue;
            }

            // Kaybedilen taraftaki parca
            if (lp.x * side > 1.0f) { loose.Add(c); continue; }

            // Ortadan gecen genis parca (tek parca kanat): ikiye bol
            var mf = c.GetComponent<MeshFilter>();
            float width = c.localScale.x;
            if (Mathf.Abs(lp.x) <= 1.0f && width > 3f && mf != null && mf.sharedMesh != null && mf.sharedMesh.name == "Cube")
            {
                Transform half = Instantiate(c.gameObject, transform).transform;
                half.name = c.name + "Broken";
                half.localRotation = c.localRotation;
                half.localScale = new Vector3(width * 0.5f, c.localScale.y, c.localScale.z);
                half.localPosition = lp + c.localRotation * new Vector3(side * width * 0.25f, 0f, 0f);
                c.localScale = half.localScale;
                c.localPosition = lp + c.localRotation * new Vector3(-side * width * 0.25f, 0f, 0f);
                loose.Add(half);
            }
        }

        foreach (Transform p in loose)
        {
            p.SetParent(null, true);
            var prb = p.gameObject.AddComponent<Rigidbody>();
            prb.mass = 0.3f;
            prb.useGravity = true;
            prb.linearVelocity = wreckVel * 0.8f + Random.insideUnitSphere * 6f + Vector3.up * 4f;
            prb.angularVelocity = Random.onUnitSphere * Random.Range(6f, 12f);
            foreach (var col in p.GetComponentsInChildren<Collider>()) Destroy(col);
            Destroy(p.gameObject, 6f);
        }
    }

    private void WreckStep()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f || mgr == null) return;

        // Yercekimi + hava direnci; donen govdenin "kaldirma" yonu degistikce yol burgu cizer
        wreckVel += Vector3.down * 24f * dt;
        wreckVel += transform.up * 7f * dt;
        wreckVel -= wreckVel * 0.3f * dt;
        spin *= 1f + 0.5f * dt;

        Vector3 pos = transform.position + wreckVel * dt;
        transform.rotation = transform.rotation * Quaternion.Euler(spin * dt);
        transform.position = pos;

        float ground = Mathf.Max(mgr.GroundHeight(pos), CombatManager.SeaLevel);
        if (pos.y <= ground + 1.5f || Time.time - wreckBorn > 9f)
        {
            mgr.OnWreckImpact(pos);
            ReleaseSmoke();
            Destroy(gameObject);
        }
    }

    /// <summary>Asama sonu / yeniden dogus: patlama efektiyle yok et (oldurme ve puan sayilmaz).</summary>
    public void Vanish()
    {
        if (dead && !wreck) return;
        dead = true;
        wreck = false;
        // Yer birimlerinin hepsi patlarsa kare duser: yalnizca birkacinda efekt
        if (mgr != null && (!IsGround || Random.value < 0.2f)) mgr.SpawnExplosion(transform.position);
        Destroy(gameObject);
    }
}
