using UnityEngine;

// MidBoss: asama ortasi mini boss (olmesi asamayi bitirmez). Boss/FinalBoss: asama sonu.
public enum EnemyKind { Swarmer, Fighter, Heavy, MidBoss, Boss, FinalBoss }

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
                    scale = 0.8f, speedMul = 1.2f, turnMul = 1.25f, radius = 3.4f, ramDamage = 1, score = 100
                };
            case EnemyKind.Heavy:
                return new EnemyKindInfo
                {
                    title = "Ağır Bombardıman", tint = new Color(0.55f, 0.2f, 0.75f), marker = new Color(0.85f, 0.45f, 1f),
                    scale = 1.7f, speedMul = 0.75f, turnMul = 0.7f, radius = 8f, ramDamage = 4, score = 800
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
                    scale = 1f, speedMul = 1f, turnMul = 1f, radius = 4.5f, ramDamage = 2, score = 300
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
    public bool IsDead => dead;
    public string Title { get; private set; }

    /// <summary>PickupKind ya da -1. Vurulunca bu guclendirmeyi dusurur.</summary>
    public int CarrierDrop { get; private set; } = -1;
    public bool IsCarrier => CarrierDrop >= 0;

    private enum Pattern { Aimed, Fan, FanRing, Spiral }

    private CombatManager mgr;
    private Rigidbody rb;
    private Propeller[] propellers;

    private float cruiseSpeed;
    private float turnRate;
    private float bank;
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
    private float breakUntil;
    private Vector3 breakDir;
    private float nextRamTime;

    // Ates duzeni (turden gelir)
    private Pattern pattern = Pattern.Aimed;
    private int burstSize = 6;
    private float shotGap = 0.12f;
    private float burstCdMin = 2.5f, burstCdMax = 4f;
    private int bulletDamage = 1;
    private float fireRange = 650f;
    private float fireCone = 8f;
    private float bulletSpeed = 300f;
    private int ramDamage = 2;
    private bool canPeel = true;

    private const float MinClearance = 90f;
    private const float PlayerRadius = 4f;
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

    public void SetVelocity(Vector3 v) { Velocity = v; }

    /// <summary>
    /// Bu dusman oyuncuya ates etmek uzere mi (HUD uyarisi)? Seri atiyor, menzilde ve burnu oyuncuya
    /// donuk ya da desenli atisa 0,8 sn kala. Boss'lar icin her zaman false (zaten hep isaretli).
    /// </summary>
    public bool IsAimingAt(Vector3 targetPos)
    {
        if (dead || IsBoss || !mgr.enemiesShoot) return false;

        float dist = Vector3.Distance(transform.position, targetPos);
        if (dist > fireRange) return false;

        if (pattern == Pattern.Aimed)
            return burstLeft > 0 || Vector3.Angle(transform.forward, targetPos - transform.position) < fireCone * 2.5f;

        return nextBurstTime - Time.time < 0.8f;
    }

    /// <summary>Boss mermisi: sahibi bu govde, kalkan/dost ates kurallari Bullet'ta.</summary>
    public Bullet FireBullet(Vector3 pos, Vector3 dir, float speed, int damage, float lifetime = 0f)
    {
        if (mgr == null || mgr.EnemyBulletPrefab == null) return null;
        Bullet b = Bullet.Spawn(mgr.EnemyBulletPrefab, pos, dir, speed, damage, transform, false);
        if (b != null && lifetime > 0f) b.lifetime = lifetime;
        return b;
    }

    private void ScriptedStep(Transform target)
    {
        // Temas: boss parcalarindan herhangi birine yaklasma
        float d = brain != null ? brain.DistanceTo(target.position) : Vector3.Distance(rb.position, target.position);
        if (d < PlayerRadius + 1.5f && Time.time >= nextRamTime)
        {
            nextRamTime = Time.time + 1.2f;
            PlayerHealth ph = mgr.CurrentPlayerHealth;
            if (ph != null) ph.TakeHit(ramDamage, target.position);
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
                pattern = Pattern.Fan;
                burstCdMin = 2.4f; burstCdMax = 4f;
                bulletDamage = 1; fireRange = 620f; bulletSpeed = 230f;
                canPeel = false;
                break;
            case EnemyKind.Heavy:
                pattern = Pattern.FanRing;
                burstCdMin = 3f; burstCdMax = 4.5f;
                bulletDamage = 2; fireRange = 760f; bulletSpeed = 190f;
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
                pattern = Pattern.Aimed;
                burstSize = 6; shotGap = 0.12f; burstCdMin = 2.5f; burstCdMax = 4f;
                bulletDamage = 1; fireRange = 650f; fireCone = 8f; bulletSpeed = 300f;
                break;
        }

        nextBurstTime = Time.time + Random.Range(1.5f, 3.5f);

        rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

        Velocity = (behavior == EnemyBehavior.Strafe ? heading : transform.forward) * cruiseSpeed;

        propellers = GetComponentsInChildren<Propeller>();
        foreach (var p in propellers) p.Rpm = 1800f;
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
        if (pos.y - ground < 70f)
        {
            heading.y = Mathf.MoveTowards(heading.y, 0.4f, 0.9f * dt);
            heading.Normalize();
        }

        Vector3 newPos = pos + heading * cruiseSpeed * dt;
        if (newPos.y < mgr.GroundHeight(newPos) + 2.5f)
        {
            Die(IsBoss);
            return;
        }

        // Hafif yatis (gorsel)
        Quaternion newRot = Quaternion.LookRotation(heading, Vector3.up);

        Velocity = heading * cruiseSpeed;
        rb.MovePosition(newPos);
        rb.MoveRotation(newRot);

        if (dist < HitRadius + PlayerRadius && Time.time >= nextRamTime)
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

        // Hiz: uzaktaysa yetis, yakindaysa biraz yavasla
        float speed = cruiseSpeed;
        if (dist > 700f) speed *= 1.4f;
        else if (dist < 150f) speed *= 0.9f;
        speed = Mathf.Max(speed, mgr.PlayerSpeed * (IsBoss ? 0.45f : 0.7f));

        // Onde tutma (lead) ile takip
        float leadTime = Mathf.Clamp(dist / Mathf.Max(speed, 1f), 0f, 2.5f);
        Vector3 aim = target.position + mgr.PlayerVelocity * leadTime * 0.8f;
        Vector3 desired = (aim - pos).normalized;

        // Yalpalama: nisan almayi zorlastirir
        if (dist > 250f && !IsBoss)
        {
            float weave = Mathf.Sin(Time.time * weaveRate + weavePhase) * 10f;
            desired = Quaternion.AngleAxis(weave, Vector3.up) * desired;
        }

        // Carpisma onleme: cok yakinda ve oyuncuya dogru uciyorsa yana/yukari kirilir (peel-off)
        float peelDist = 80f * transform.localScale.x;
        if (Time.time < breakUntil)
        {
            desired = breakDir;
        }
        else if (canPeel && dist < peelDist && Vector3.Dot(fwd, toPlayer / Mathf.Max(dist, 0.1f)) > 0.4f)
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

        Vector3 newFwd = Vector3.RotateTowards(fwd, desired, turnRate * Mathf.Deg2Rad * dt, 0f);

        // Gorsel yatis: donuse gore
        float yawRate = Vector3.SignedAngle(fwd, newFwd, Vector3.up) / dt;
        float bankTarget = Mathf.Clamp(yawRate * 0.7f, -70f, 70f);
        bank = Mathf.Lerp(bank, bankTarget, 4f * dt);

        Quaternion newRot = Quaternion.LookRotation(newFwd, Vector3.up) * Quaternion.Euler(0f, 0f, -bank);
        Vector3 newPos = pos + newFwd * speed * dt;

        // Yere carpti
        if (newPos.y < mgr.GroundHeight(newPos) + 2.5f)
        {
            Die(IsBoss);   // boss yere carpsa da boss odulu/asama sonu verilir
            return;
        }

        Velocity = newFwd * speed;
        rb.MovePosition(newPos);
        rb.MoveRotation(newRot);

        // Temas: oyuncu fizigi dusmanlarla carpismaz (layer); hasar yakinliktan hesaplanir
        if (dist < HitRadius + PlayerRadius && Time.time >= nextRamTime)
        {
            nextRamTime = Time.time + 1.2f;
            Ram(newPos);
            if (dead) return;
        }

        TryShoot(newPos, newRot, newFwd, target, dist);
    }

    private void Ram(Vector3 point)
    {
        PlayerHealth ph = mgr.CurrentPlayerHealth;
        if (ph != null) ph.TakeHit(ramDamage, point);

        // Boss carpmadan etkilenmez; digerleri patlar (oldurme sayilir)
        if (!IsBoss) Die(true);
    }

    // -------------------------------------------------------------- ates

    private void TryShoot(Vector3 pos, Quaternion rot, Vector3 fwd, Transform target, float dist)
    {
        if (!mgr.enemiesShoot || mgr.EnemyBulletPrefab == null) return;

        if (pattern == Pattern.Aimed)
        {
            // Dogfight: nisan alinca kisa seri
            if (burstLeft <= 0 && Time.time >= nextBurstTime && dist < fireRange
                && Vector3.Angle(fwd, target.position - pos) < fireCone)
            {
                burstLeft = burstSize;
                nextBurstTime = Time.time + Random.Range(burstCdMin, burstCdMax);
            }

            if (burstLeft > 0 && Time.time >= nextShotTime)
            {
                burstLeft--;
                nextShotTime = Time.time + shotGap;

                float s = transform.localScale.x;
                Vector3 muzzle = pos + rot * new Vector3((Random.value < 0.5f ? -0.9f : 0.9f) * s, 0f, 1.6f * s);
                Vector3 aim = target.position + mgr.PlayerVelocity * (dist / bulletSpeed)
                              + Random.insideUnitSphere * mgr.enemyAimError;
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
                      + Random.insideUnitSphere * (mgr.enemyAimError * 0.5f) - origin;
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

    public void TakeHit(int damage, Vector3 point)
    {
        if (dead) return;

        if (FirstHitTime < 0f) FirstHitTime = Time.time;
        TotalDamage += damage;
        HitsRemaining -= damage;
        mgr.OnEnemyHit(this, point);

        if (HitsRemaining <= 0)
        {
            Die(true);
            return;
        }

        // Hasar gorunumu: duman izi
        if (smoke == null && mgr.SmokeTrailPrefab != null)
        {
            smoke = Instantiate(mgr.SmokeTrailPrefab, transform);
            smoke.transform.localPosition = new Vector3(0f, 0.2f, -0.5f);
        }
    }

    private void Die(bool byPlayer)
    {
        if (dead) return;
        dead = true;

        mgr.OnEnemyDestroyed(this, byPlayer);

        // Duman izi sahnede kalsin (kendi omru kadar)
        if (smoke != null)
        {
            smoke.transform.SetParent(null);
            var ps = smoke.GetComponent<ParticleSystem>();
            if (ps != null) ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            Destroy(smoke, 6f);
        }

        Destroy(gameObject);
    }

    /// <summary>Asama sonu / yeniden dogus: patlama efektiyle yok et (oldurme ve puan sayilmaz).</summary>
    public void Vanish()
    {
        if (dead) return;
        dead = true;
        if (mgr != null) mgr.SpawnExplosion(transform.position);
        Destroy(gameObject);
    }
}
