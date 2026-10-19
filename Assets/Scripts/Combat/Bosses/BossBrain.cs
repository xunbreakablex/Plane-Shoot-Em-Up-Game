using System.Collections.Generic;
using UnityEngine;

// Kodla uretilen boss govdelerinin beyni: oyuncunun onunde konumlanir (shmup duzeni), fazlara gore
// mermi desenleri, isin, hamle ve dalis saldirilari uygular. Boss'a ozgu hareket, saldiri ve animasyon
// BossBrain.<Tur>.cs dosyalarindadir. Can/olum/puan EnemyPlane'dedir.
public partial class BossBrain : MonoBehaviour
{
    public BossType type;

    private CombatManager mgr;
    private EnemyPlane enemy;
    private Rigidbody rb;
    private Transform player;

    private Collider[] colliders;
    private Renderer[] renderers;
    private Color[] baseColors;

    private Vector3 pos;
    private Quaternion rot;
    private Vector3 vel;
    private Vector3 anchorFwd = Vector3.forward;

    private float age;
    private int phase;          // 0, 1, 2: can azaldikca sertlesir
    private float nextAttack;
    private int step;
    private bool ready;

    private readonly Dictionary<string, Transform> named = new Dictionary<string, Transform>();

    // Hasar parlamasi
    private int lastHits;
    private float flashUntil;
    private bool flashing;
    private static MaterialPropertyBlock block;

    // ------------------------------------------------------------ boss bilgileri

    public static string TitleOf(BossType t)
    {
        switch (t)
        {
            case BossType.Jet: return "ŞAHİN-X";
            case BossType.Shark: return "ÇELİK KÖPEKBALIĞI";
            case BossType.Crab: return "ZIRHLI YENGEÇ";
            case BossType.Octopus: return "MEKANİK AHTAPOT";
            case BossType.Zeppelin: return "HAVA ZIRHLISI";
            case BossType.Demon: return "İBLİS";
            case BossType.Angel: return "MELEK";
            case BossType.KiWarrior: return "Kİ SAVAŞÇISI";
            case BossType.MagicalGirl: return "YILDIZ BÜYÜCÜSÜ";
            case BossType.Eagle: return "ÇELİK KARTAL";
            case BossType.Golem: return "TAŞ DEV";
            default: return "KIZIL EJDERHA";
        }
    }

    // Govde olcegi: Darius tarzi, ekrani dolduran dev boss'lar (builder metreleri x bu olcek)
    public static float ScaleOf(BossType t)
    {
        switch (t)
        {
            case BossType.Jet: return 2.7f;
            case BossType.Shark: return 2.4f;
            case BossType.Crab: return 2.6f;
            case BossType.Octopus: return 2.3f;
            case BossType.Zeppelin: return 1.9f;
            case BossType.Demon: return 2.4f;
            case BossType.Angel: return 2.3f;
            case BossType.KiWarrior: return 2.3f;
            case BossType.MagicalGirl: return 2.2f;
            case BossType.Eagle: return 2.3f;
            case BossType.Golem: return 2.4f;
            default: return 2.2f;
        }
    }

    // Bomba/isaretleme icin yaklasik buyukluk (metre, olcek dahil)
    public static float RadiusOf(BossType t)
    {
        switch (t)
        {
            case BossType.Jet: return 14f * ScaleOf(t);
            case BossType.Shark: return 24f * ScaleOf(t);
            case BossType.Crab: return 18f * ScaleOf(t);
            case BossType.Octopus: return 22f * ScaleOf(t);
            case BossType.Zeppelin: return 36f * ScaleOf(t);
            case BossType.Demon: return 16f * ScaleOf(t);
            case BossType.Angel: return 16f * ScaleOf(t);
            case BossType.KiWarrior: return 14f * ScaleOf(t);
            case BossType.MagicalGirl: return 15f * ScaleOf(t);
            case BossType.Eagle: return 20f * ScaleOf(t);
            case BossType.Golem: return 14f * ScaleOf(t);
            default: return 18f * ScaleOf(t);
        }
    }

    public static int RamOf(BossType t)
    {
        return t == BossType.Dragon || t == BossType.Zeppelin ? 4 : 3;
    }

    // Boss cani (vurus). Her boss, o noktada acik olan donanima gore ayarli (isabet ~%50 varsayimi):
    //   Sahin-X ~24-40 DPS (2 silah), Kopekbaligi ~55, Yengec ~80, Ahtapot ~90, Zeplin ~130, Ejderha ~180.
    // Hedef sure: orta boss ~30 sn, ana boss ~50-60 sn. Teorik DPS icin konsolda "info" yaz.
    public static int BaseHits(BossType t)
    {
        switch (t)
        {
            case BossType.Jet: return 450;
            case BossType.Shark: return 1100;
            case BossType.Crab: return 1300;
            case BossType.Octopus: return 2400;
            case BossType.Zeppelin: return 2000;
            case BossType.Demon: return 2200;
            case BossType.Angel: return 2800;
            case BossType.KiWarrior: return 4200;
            case BossType.MagicalGirl: return 6000;
            case BossType.Eagle: return 3200;
            case BossType.Golem: return 4000;
            default: return 5000;
        }
    }

    // ------------------------------------------------------------- baslatma

    public void Init(CombatManager manager, EnemyPlane e)
    {
        mgr = manager;
        enemy = e;
        rb = GetComponent<Rigidbody>();

        colliders = GetComponentsInChildren<Collider>();
        renderers = GetComponentsInChildren<Renderer>();
        baseColors = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            Material m = renderers[i].sharedMaterial;
            baseColors[i] = m != null && m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor")
                : (m != null ? m.color : Color.white);
        }

        pos = rb.position;
        rot = rb.rotation;
        lastHits = enemy.MaxHits;
        nextAttack = Time.time + 3f;     // girisinde kisa bir kurtulma payi

        Transform pt = mgr.PlayerTransform;
        if (pt != null)
        {
            Vector3 f = Vector3.ProjectOnPlane(pt.forward, Vector3.up);
            if (f.sqrMagnitude > 0.01f) anchorFwd = f.normalized;
        }

        InitType();
        ready = true;
    }

    private static bool IsFinite(Vector3 v)
    {
        return !(float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z) || float.IsInfinity(v.x) || float.IsInfinity(v.y) || float.IsInfinity(v.z));
    }

    private Transform Get(string childName)
    {
        if (named.TryGetValue(childName, out Transform t)) return t;
        t = FindDeep(transform, childName);
        named[childName] = t;
        return t;
    }

    private static Transform FindDeep(Transform parent, string childName)
    {
        foreach (Transform c in parent)
        {
            if (c.name == childName) return c;
            Transform r = FindDeep(c, childName);
            if (r != null) return r;
        }
        return null;
    }

    // ------------------------------------------------------------------ dongu

    private void FixedUpdate()
    {
        if (!ready || mgr == null || enemy == null || enemy.IsDead) return;

        player = mgr.PlayerTransform;
        if (player == null) return;

        PlaneController plane = mgr.flight != null ? mgr.flight.Current : null;
        bool playerAlive = plane != null && !plane.IsCrashed;

        float dt = Time.fixedDeltaTime;
        age += dt;
        pos = rb.position;
        rot = rb.rotation;

        // Koruma: bozuk (NaN/sonsuz) konum ya da donus gelirse oyuncunun onune sifirlanir
        if (!IsFinite(pos) || !IsFinite(vel) || float.IsNaN(rot.x) || float.IsNaN(rot.w))
        {
            pos = Anchor(520f, 0f, 30f);
            vel = Vector3.zero;
            rot = Quaternion.LookRotation(player.position - pos, Vector3.up);
        }

        UpdateAnchor(dt);

        // Cok uzakta kaldiysa (oyuncu hizla uzaklasti / yeniden dogdu) onune isinlanir
        if ((pos - player.position).sqrMagnitude > 1100f * 1100f)
        {
            pos = Anchor(520f, 0f, 30f);
            vel = Vector3.zero;
            chargeActive = false;
        }

        float frac = enemy.MaxHits > 0 ? (float)enemy.HitsRemaining / enemy.MaxHits : 1f;
        int newPhase = frac < 0.33f ? 2 : (frac < 0.66f ? 1 : 0);
        if (newPhase != phase)
        {
            if (newPhase > phase) BreakOffPart();   // her faz gecisinde govdeden bir parca kopar
            phase = newPhase;
            nextAttack = Mathf.Min(nextAttack, Time.time + 1.2f);
            if (mgr != null)
                mgr.Announce(TitleOf(type) + (phase == 2 ? " ÇILGINA DÖNDÜ!" : " ÖFKELENDİ!"),
                    new Color(1f, 0.45f, 0.3f), 2.2f);
        }

        bool overridden = TickCharge(dt);
        if (!overridden) Move(dt);

        TickSpray();
        TickBeam(dt);
        TickType(dt);

        if (playerAlive && !overridden && !beamActive && Time.time >= nextAttack)
        {
            Attack();
            Sfx.PlayAt(Sfx.BossAttack, pos, 0.7f);
        }

        // Zemin
        float ground = mgr.GroundHeight(pos);
        if (pos.y < ground + 110f) pos.y = ground + 110f;

        rb.MovePosition(pos);
        rb.MoveRotation(rot);
        enemy.SetVelocity(vel);
    }

    private void Update()
    {
        if (!ready || enemy == null || enemy.IsDead) return;

        // Hasar parlamasi
        if (enemy.HitsRemaining != lastHits)
        {
            lastHits = enemy.HitsRemaining;
            flashUntil = Time.time + 0.07f;
        }

        bool want = Time.time < flashUntil;
        if (want != flashing)
        {
            flashing = want;
            ApplyFlash(want);
        }
    }

    private void LateUpdate()
    {
        if (!ready || enemy == null || enemy.IsDead || GameState.Paused) return;
        AnimateType();
    }

    private void ApplyFlash(bool on)
    {
        if (block == null) block = new MaterialPropertyBlock();
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer r = renderers[i];
            if (r == null) continue;
            r.GetPropertyBlock(block);
            block.SetColor("_BaseColor", on ? Color.Lerp(baseColors[i], Color.white, 0.7f) : baseColors[i]);
            block.SetColor("_Color", on ? Color.Lerp(baseColors[i], Color.white, 0.7f) : baseColors[i]);
            r.SetPropertyBlock(block);
        }
    }

    // ---------------------------------------------------------- hareket yardimcilari

    // Oyuncunun baktigi yonun yumusatilmis yatay hali: boss hep onunde kalir
    private void UpdateAnchor(float dt)
    {
        PlaneController plane = mgr.flight != null ? mgr.flight.Current : null;
        Vector3 f = player.forward;
        if (plane != null && plane.Speed > 12f) f = plane.Velocity;
        f = Vector3.ProjectOnPlane(f, Vector3.up);
        if (f.sqrMagnitude < 0.01f) return;

        anchorFwd = Vector3.Slerp(anchorFwd, f.normalized, 1f - Mathf.Exp(-1.1f * dt)).normalized;
    }

    private Vector3 Anchor(float dist, float side, float height)
    {
        Vector3 right = Vector3.Cross(Vector3.up, anchorFwd);
        return player.position + anchorFwd * dist + right * side + Vector3.up * height;
    }

    private void Steer(Vector3 target, float maxSpeed, float accel, float dt)
    {
        // Oyuncu hizli ucuyorsa boss geride kalmasin: azami hiz oyuncununkinin ustunde tutulur
        float chase = mgr.PlayerSpeed * 1.3f + 8f;
        if (chase > maxSpeed)
        {
            accel *= chase / maxSpeed;
            maxSpeed = chase;
        }

        Vector3 to = target - pos;
        float d = to.magnitude;
        Vector3 desired = d > 0.1f ? to / d * Mathf.Min(maxSpeed, d * 1.4f) : Vector3.zero;
        vel = Vector3.MoveTowards(vel, desired, accel * dt);
        pos += vel * dt;
    }

    private void FaceDir(Vector3 dir, float degPerSec, float dt, float roll = 0f)
    {
        if (dir.sqrMagnitude < 0.01f) return;
        Quaternion want = Quaternion.LookRotation(dir.normalized, Vector3.up) * Quaternion.Euler(0f, 0f, roll);
        rot = Quaternion.RotateTowards(rot, want, degPerSec * dt);
    }

    // Faz gecisi: govdeden buyuk bir parca (kanat zari, yuzgec, kol, kuyruk...) patlayarak kopar ve duser.
    // Yalniz yaprak parcalar secilir (alt nesnesi olmayan): namlu/pivot referanslari bozulmaz. Govde, cekirdek,
    // goz, bas gibi adlar ve bossun yarisindan buyuk parcalar haric.
    private void BreakOffPart()
    {
        if (renderers == null || mgr == null) return;
        Bounds whole = new Bounds(transform.position, Vector3.zero);
        foreach (var r in renderers) if (r != null) whole.Encapsulate(r.bounds);
        float wholeSize = whole.size.magnitude;

        Renderer best = null;
        float bestSize = 0f;
        foreach (var r in renderers)
        {
            if (r == null || r.transform.childCount > 0 || r.transform == transform) continue;
            string n = r.name;
            if (n.Contains("Body") || n.Contains("Torso") || n.Contains("Hull") || n.Contains("Core") || n.Contains("Eye")
                || n.Contains("Head") || n.Contains("Muzzle") || n.Contains("Chest") || n.Contains("Halo")) continue;
            float size = r.bounds.size.magnitude;
            if (size > wholeSize * 0.55f) continue;
            if (size > bestSize) { bestSize = size; best = r; }
        }
        if (best == null) return;

        Transform part = best.transform;
        Vector3 c = best.bounds.center;
        part.SetParent(null, true);
        foreach (var col in part.GetComponents<Collider>()) col.enabled = false;
        var prb = part.gameObject.AddComponent<Rigidbody>();
        prb.mass = 3f;
        prb.useGravity = true;
        Vector3 outward = (c - transform.position).normalized;
        prb.linearVelocity = vel * 0.6f + outward * 18f + Vector3.up * 6f;
        prb.angularVelocity = Random.onUnitSphere * Random.Range(2f, 5f);
        Destroy(part.gameObject, 7f);

        // Kopan yerde ve parcanin uzerinde patlama; guclu sarsinti
        mgr.SpawnExplosion(c, 1.8f);
        mgr.SpawnExplosion(transform.position + outward * (bestSize * 0.3f), 1.2f);
        FollowCamera.AddShake(1f, 0.6f);
        Sfx.PlayAt(Sfx.Crack, c, 1f);
        if (mgr.SmokeTrailPrefab != null)
        {
            var smoke = Instantiate(mgr.SmokeTrailPrefab, part);
            smoke.transform.localPosition = Vector3.zero;
        }
    }

    /// <summary>Verilen noktanin boss parcalarina en kisa uzakligi (temas hasari icin).</summary>
    public float DistanceTo(Vector3 p)
    {
        float best = float.MaxValue;
        if (colliders == null) return Vector3.Distance(transform.position, p);

        foreach (var c in colliders)
        {
            if (c == null || !c.enabled) continue;
            float d = Vector3.Distance(c.ClosestPoint(p), p);
            if (d < best) best = d;
        }
        return best < float.MaxValue ? best : Vector3.Distance(transform.position, p);
    }

    /// <summary>Olumde govdenin farkli yerlerinde art arda patlamalar.</summary>
    public void SpawnDeathExplosions(int count)
    {
        if (mgr == null || renderers == null || renderers.Length == 0) return;
        for (int i = 0; i < count; i++)
        {
            Renderer r = renderers[Random.Range(0, renderers.Length)];
            if (r == null) continue;
            mgr.SpawnExplosion(r.bounds.center);
        }
    }

    // ------------------------------------------------------------ ates yardimcilari

    private Vector3 AimFrom(Vector3 origin, float speed, float error)
    {
        float dist = Vector3.Distance(origin, player.position);
        Vector3 target = player.position + mgr.PlayerVelocity * (dist / Mathf.Max(speed, 1f)) * 0.85f;
        target += Random.insideUnitSphere * error;
        return (target - origin).normalized;
    }

    private void Shoot(Vector3 origin, Vector3 dir, float speed, int damage, float life = 0f)
    {
        enemy.FireBullet(origin, dir, speed, damage, life);
    }

    // Yatay yelpaze: merkez mermi tam hedefe
    private void Fan(Vector3 origin, Vector3 dir, int count, float totalDeg, float speed, int damage)
    {
        float step = count > 1 ? totalDeg / (count - 1) : 0f;
        for (int i = 0; i < count; i++)
        {
            float off = count > 1 ? -totalDeg * 0.5f + step * i : 0f;
            Shoot(origin, Quaternion.AngleAxis(off, Vector3.up) * dir, speed, damage);
        }
    }

    // Hedefe yonelik konik halka: merkezde bir hedef mermisi + halka katmani
    private void Cone(Vector3 origin, Vector3 dir, int count, float tiltDeg, float speed, int damage, float offsetDeg)
    {
        Vector3 up = Mathf.Abs(Vector3.Dot(dir, Vector3.up)) > 0.95f ? Vector3.right : Vector3.up;
        Vector3 u = Vector3.Cross(dir, up).normalized;
        Vector3 v = Vector3.Cross(dir, u).normalized;
        float tilt = tiltDeg * Mathf.Deg2Rad;

        Shoot(origin, dir, speed, damage);
        for (int i = 0; i < count; i++)
        {
            float a = (offsetDeg + 360f * i / count) * Mathf.Deg2Rad;
            Vector3 d = dir * Mathf.Cos(tilt) + (u * Mathf.Cos(a) + v * Mathf.Sin(a)) * Mathf.Sin(tilt);
            Shoot(origin, d.normalized, speed, damage);
        }
    }

    // ----------------------------------------------------------- sarmal (spray)

    private Transform sprayOrigin;
    private float sprayEnd;
    private float sprayNext;
    private float sprayAngle;
    private int sprayArms;
    private float sprayTilt;
    private float sprayInterval;
    private float spraySpeed;
    private int sprayDamage;

    private bool Spraying => Time.time < sprayEnd;

    private void StartSpray(Transform origin, float duration, int arms, float tiltDeg, float interval, float speed, int damage = 1)
    {
        sprayOrigin = origin;
        sprayEnd = Time.time + duration;
        sprayNext = Time.time;
        sprayArms = Mathf.Max(1, arms);
        sprayTilt = tiltDeg;
        sprayInterval = interval;
        spraySpeed = speed;
        sprayDamage = damage;
        nextAttack = Mathf.Max(nextAttack, sprayEnd + 0.4f);
    }

    private void TickSpray()
    {
        if (!Spraying || sprayOrigin == null) return;

        while (Time.time >= sprayNext)
        {
            sprayNext += sprayInterval;

            Vector3 origin = sprayOrigin.position;
            Vector3 axis = (player.position - origin).normalized;
            Vector3 up = Mathf.Abs(Vector3.Dot(axis, Vector3.up)) > 0.95f ? Vector3.right : Vector3.up;
            Vector3 u = Vector3.Cross(axis, up).normalized;
            Vector3 v = Vector3.Cross(axis, u).normalized;
            float tilt = sprayTilt * Mathf.Deg2Rad;

            for (int a = 0; a < sprayArms; a++)
            {
                float ang = (sprayAngle + 360f * a / sprayArms) * Mathf.Deg2Rad;
                Vector3 d = axis * Mathf.Cos(tilt) + (u * Mathf.Cos(ang) + v * Mathf.Sin(ang)) * Mathf.Sin(tilt);
                Shoot(origin, d.normalized, spraySpeed, sprayDamage);
            }
            sprayAngle += 17f;
        }
    }

    // --------------------------------------------------------------- isin (beam)

    private const float BeamLength = 760f;

    private bool beamActive;
    private Transform beamOrigin;
    private float beamStart;
    private float beamTelegraph;
    private float beamFire;
    private Vector3 beamDir;
    private float beamNextTick;
    private LineRenderer beamLine;

    private void StartBeam(Transform origin, float telegraph, float fire)
    {
        if (origin == null) return;

        beamOrigin = origin;
        beamTelegraph = telegraph;
        beamFire = fire;
        beamStart = Time.time;
        beamActive = true;
        Sfx.PlayAt(Sfx.BeamCharge, origin.position, 0.8f);
        beamDir = (player.position - origin.position).normalized;
        beamNextTick = 0f;

        if (beamLine == null)
        {
            var go = new GameObject("BossBeam");
            go.transform.SetParent(transform, false);
            beamLine = go.AddComponent<LineRenderer>();
            beamLine.useWorldSpace = true;
            beamLine.positionCount = 2;
            beamLine.numCapVertices = 4;
            beamLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            beamLine.sharedMaterial = new Material(mgr.bossGlow != null ? mgr.bossGlow : mgr.fxMaterial);
        }
        beamLine.enabled = true;

        nextAttack = Mathf.Max(nextAttack, Time.time + telegraph + fire + 0.5f);
    }

    private void TickBeam(float dt)
    {
        if (!beamActive || beamOrigin == null) return;

        float t = Time.time - beamStart;
        Vector3 o = beamOrigin.position;
        Vector3 want = (player.position - o).normalized;

        if (t < beamTelegraph)
        {
            // Uyari: ince sari cizgi hedefi takip eder
            beamDir = Vector3.Slerp(beamDir, want, 1f - Mathf.Exp(-5f * dt));
            SetBeamVisual(o, 0.5f, new Color(1f, 0.85f, 0.2f));
        }
        else if (t < beamTelegraph + beamFire)
        {
            // Ates: yavasca oyuncuya dogru suruklenir (GameSettings.bossBeamTurnRate)
            GameSettings gs = GameSettings.Current;
            beamDir = Vector3.RotateTowards(beamDir, want, gs.bossBeamTurnRate * Mathf.Deg2Rad * dt, 0f);
            SetBeamVisual(o, 4.5f, new Color(1f, 0.25f, 0.2f));

            Vector3 toP = player.position - o;
            float along = Vector3.Dot(toP, beamDir);
            if (along > 0f && along < BeamLength && Time.time >= beamNextTick)
            {
                float d = (toP - beamDir * along).magnitude;
                // Eskiden isabet yaricapi (6,5 m) gorunen isinin (2,75 m) iki katindan genisti ve vurus basina 2
                // hasar 0,25 sn'de bir veriyordu: lazer cok guclu hissettiriyordu. Ayarlar GameSettings'te.
                if (d < gs.bossBeamHitRadius)
                {
                    beamNextTick = Time.time + gs.bossBeamTick;
                    PlayerHealth ph = mgr.CurrentPlayerHealth;
                    if (ph != null) ph.TakeHit(gs.bossBeamDamage, player.position);
                }
            }
        }
        else
        {
            beamActive = false;
            if (beamLine != null) beamLine.enabled = false;
        }
    }

    private void SetBeamVisual(Vector3 origin, float width, Color color)
    {
        if (beamLine == null) return;
        beamLine.SetPosition(0, origin);
        beamLine.SetPosition(1, origin + beamDir * BeamLength);
        beamLine.startWidth = width;
        beamLine.endWidth = width;
        beamLine.sharedMaterial.color = color;
        if (beamLine.sharedMaterial.HasProperty("_BaseColor")) beamLine.sharedMaterial.SetColor("_BaseColor", color);
    }

    // ----------------------------------------------------------- hamle (charge)

    private bool chargeActive;
    private float chargeStart;
    private float chargeTelegraph;
    private float chargeSpeed;
    private float chargeDuration;
    private Vector3 chargeDir;
    private bool chargeLocked;

    private bool Charging => chargeActive;

    private void StartCharge(float telegraph, float speed, float duration)
    {
        chargeActive = true;
        chargeLocked = false;
        chargeStart = Time.time;
        chargeTelegraph = telegraph;
        chargeSpeed = speed;
        chargeDuration = duration;
        nextAttack = Mathf.Max(nextAttack, Time.time + telegraph + duration + 1f);
    }

    // true: hareketi bu kare hamle yonetiyor
    private bool TickCharge(float dt)
    {
        if (!chargeActive) return false;

        float t = Time.time - chargeStart;
        if (t < chargeTelegraph)
        {
            // Hazirlik: yavasca durur, hedefe doner (parcalar parlar)
            vel = Vector3.MoveTowards(vel, Vector3.zero, 90f * dt);
            pos += vel * dt;
            FaceDir(player.position - pos, 160f, dt);
            return true;
        }

        if (!chargeLocked)
        {
            chargeLocked = true;
            chargeDir = (player.position + mgr.PlayerVelocity * 0.35f - pos).normalized;
        }

        if (t < chargeTelegraph + chargeDuration)
        {
            vel = chargeDir * chargeSpeed;
            pos += vel * dt;
            FaceDir(chargeDir, 240f, dt);
            return true;
        }

        chargeActive = false;
        return false;
    }
}
