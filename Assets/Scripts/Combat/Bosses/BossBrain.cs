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

    // Boss carpma (temas) hasari: eskiden 3-4 ile oyundaki en buyuk tek vurustu; dalis artik uyarili ve kacilabilir,
    // yine de tek temas en fazla 2
    public static int RamOf(BossType t)
    {
        return 2;
    }

    // Boss cani (vurus). Her boss, o noktada acik olan donanima gore ayarli (isabet ~%50 varsayimi):
    //   Sahin-X ~24-40 DPS (2 silah), Kopekbaligi ~55, Yengec ~80, Ahtapot ~90, Zeplin ~130, Ejderha ~180.
    // Hedef sure: orta boss ~30 sn, ana boss ~50-60 sn. Teorik DPS icin konsolda "info" yaz.
    // 2026-10-22 boss olcumu (Roket, guc 4): Ejderha / Melek ~125 sn, Ki ~145 sn, Yildiz Buyucusu ~195 sn suruyordu
    // (kahramanla 4 dakikayi asiyordu). Hedef: orta 30-50, asama 50-80, son boss 70-90 sn; uzun olanlar kisaltildi.
    public static int BaseHits(BossType t)
    {
        switch (t)
        {
            case BossType.Jet: return 580;       // isinma degisikligi (+%30 hasar) dengelendi: sure ayni
            case BossType.Shark: return 1400;
            case BossType.Crab: return 1700;
            case BossType.Octopus: return 3100;
            case BossType.Zeppelin: return 2600;
            case BossType.Demon: return 1900;
            case BossType.Angel: return 1400;
            case BossType.KiWarrior: return 2800;
            case BossType.MagicalGirl: return 3400;
            case BossType.Eagle: return 2600;
            case BossType.Golem: return 3200;
            default: return 3900;   // Ejderha
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
        InitLimbs();
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
        if (dying && mgr != null && enemy != null) { StepDeath(Time.fixedDeltaTime); return; }
        if (!ready || mgr == null || enemy == null || enemy.IsDead) return;

        player = mgr.PlayerTransform;
        if (player == null) return;

        PlaneController plane = mgr.flight != null ? mgr.flight.Current : null;
        bool playerAlive = plane != null && !plane.IsCrashed;

        float dt = Time.fixedDeltaTime;
        age += dt;

        // Boss fazi: ilerleyen ve donen cerceve boss'u ve onun dunyadaki ara durumlarini (hamle hedefi, uyari
        // cizgileri, ejderha izi) birlikte tasir ve dondurur; boss mantigi duragan savastaki gibi calisir
        mgr.RushStepDelta();
        if (mgr.RushActive)
        {
            pos = mgr.RushMapPoint(rb.position);
            rot = mgr.RushStepRotation * rb.rotation;
            ApplyFrameMap();
        }
        else
        {
            pos = rb.position;
            rot = rb.rotation;
        }

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

        // Faz: can esikleri (%66, %33) ya da (asama sonu bosslarinda) silah uzuvlarinin kopmasi
        float frac = enemy.MaxHits > 0 ? (float)enemy.HitsRemaining / enemy.MaxHits : 1f;
        int hpPhase = frac < 0.33f ? 2 : (frac < 0.66f ? 1 : 0);
        int newPhase = Mathf.Max(hpPhase, LimbPhase);
        if (newPhase > phase)
        {
            PhaseBurst();   // her faz gecisinde govdeden parcalar sacilir (buyuk parca kopmaz)
            phase = newPhase;
            nextAttack = Mathf.Min(nextAttack, Time.time + 1.2f);
            if (mgr != null)
                mgr.Announce(TitleOf(type) + (phase == 2 ? " ÇILGINA DÖNDÜ!" : " ÖFKELENDİ!"),
                    new Color(1f, 0.45f, 0.3f), 2.2f);
        }

        bool overridden = TickCharge(dt);
        if (!overridden) Move(dt);

        TickShots();
        TickSpray();
        TickWheel();
        TickKiWave(dt);
        TickSweep();
        TickZones(dt);
        TickGale();
        TickBeam(dt);
        TickType(dt);

        if (playerAlive && !overridden && !beamActive && !WheelBusy && !KiWaveBusy && !FistBusy && !SweepBusy && !ZonesBusy && !GaleBusy && !Spraying && !CrabSweeping && Time.time >= nextAttack)
        {
            // Boss fazinda her CurtainEvery saldiridan biri perde (bosluklu mermi duvari)
            if (CurtainTurn) Curtain();
            else Attack();
            attackCount++;
            // Zorluk: saldirilar arasi sure (Kolay seyrek, Zor sik); Normal'de degismez
            if (nextAttack > Time.time)
                nextAttack = Time.time + (nextAttack - Time.time) * Difficulty.FireCooldownMul * (mgr.RushActive ? RushCadence : 1f);
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
        if (!ready || enemy == null) return;
        if (enemy.IsDead) { ClearWarns(); return; }
        TickWall();
        DrawKiWave();
        DrawFistLines();
        DrawChargeWarn();
        DrawSweep();
        DrawZones();
        DrawGale();
        DrawWarns();

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
        // Boss fazi: boss hep ilerleme yonunde, oyuncunun onunde
        if (mgr.RushActive)
        {
            anchorFwd = mgr.RushHeading;
            return;
        }

        PlaneController plane = mgr.flight != null ? mgr.flight.Current : null;
        Vector3 f = player.forward;
        if (plane != null && plane.Speed > 12f) f = plane.Velocity;
        f = Vector3.ProjectOnPlane(f, Vector3.up);
        if (f.sqrMagnitude < 0.01f) return;

        anchorFwd = Vector3.Slerp(anchorFwd, f.normalized, 1f - Mathf.Exp(-1.1f * dt)).normalized;
    }

    // Boss fazi ayarlari: oyuncu yalniz kayabildigi icin desenler daha yakindan, daha hizli ve daha sik gelir
    private const float RushAnchorScale = 0.72f;   // boss oyuncuya bu oranda daha yakin durur (daha buyuk gorunur)
    private const float RushBulletSpeed = 1.2f;    // mermi hizi carpani
    private const float RushCadence = 0.8f;        // saldirilar arasi sure carpani
    private const float RushAimError = 0.7f;       // nisan sapmasi carpani

    private const float RushSideScale = 0.4f;      // boss fazinda yana salinim bu oranda kucuk: boss hep tam karsida
    private const float RushHeightScale = 0.65f;   // yukari-asagi salinim

    private Vector3 Anchor(float dist, float side, float height)
    {
        if (mgr.RushActive)
        {
            dist *= RushAnchorScale;
            side *= RushSideScale;
            height *= RushHeightScale;
        }
        Vector3 right = Vector3.Cross(Vector3.up, anchorFwd);
        return player.position + anchorFwd * dist + right * side + Vector3.up * height;
    }

    private void Steer(Vector3 target, float maxSpeed, float accel, float dt)
    {
        // Yanan kanat / motor boss'u agirlastirir; hamleden eski yerine donus hizlidir
        maxSpeed *= wingSpeedMul;
        if (ChargeReturning)
        {
            maxSpeed *= 3.4f;
            accel *= 5f;
        }
        else if (mgr.RushActive)
        {
            // Boss fazinda yerini hizli alir, sonra (kucuk salinimla) sabit durur
            maxSpeed *= 1.3f;
            accel *= 1.6f;
        }

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
        // Onde tutma merminin GERCEK hiziyla (boss mermisi yavaslatma ve boss fazi carpanlari dahil). Boss fazinda tam
        // onde tutulur: oyuncu yalniz kayabildigi icin rastgele kaymak tek basina yetmesin, yon degistirmek gereksin.
        float effSpeed = speed * Mathf.Max(0.05f, GameSettings.Current.bossBulletSpeedMultiplier) * (mgr.RushActive ? RushBulletSpeed : 1f);
        float lead = mgr.RushActive ? 1f : 0.85f;
        Vector3 target = player.position + mgr.PlayerVelocity * (dist / Mathf.Max(effSpeed, 1f)) * lead;
        // Zorluk: nisan sapmasi (Normal'e gore; Kolay daha sapmali, Zor daha isabetli)
        target += Random.insideUnitSphere * error * (Difficulty.AimErrorMul / 0.75f) * (mgr.RushActive ? RushAimError : 1f);
        return (target - origin).normalized;
    }

    private Bullet Shoot(Vector3 origin, Vector3 dir, float speed, int damage, float life = 0f)
    {
        if (mgr.RushActive) speed *= RushBulletSpeed;
        return enemy.FireBullet(origin, dir, speed, damage, life);
    }

    // ----------------------------------------------------------- uyari (telegraf)

    // Yelpaze, halka ve sarmal atislari bu kadar gecikir; yonu kirmizi-turuncu bir cizgiyle gosterilir.
    // Atis yonu uyari basladiginda sabitlenir: oyuncu o an hareket ederek kacabilir.
    private const float ShotWarning = 0.55f;
    private const float WarnLength = 260f;

    // fire: atis aninda cagrilir; parametre, uyari basladigindan beri cercevenin (boss fazi) kayma ve donusudur
    private struct PendingShot { public float at; public System.Action<CombatManager.RushMap> fire; public CombatManager.RushSnap snap; }
    private readonly List<PendingShot> pendingShots = new List<PendingShot>();

    private struct WarnLine { public Vector3 origin; public Vector3 dir; public float until; }
    private readonly List<WarnLine> warns = new List<WarnLine>();
    private readonly List<LineRenderer> warnPool = new List<LineRenderer>();

    private void Telegraphed(Vector3 origin, Vector3 dir, System.Action<CombatManager.RushMap> fire)
    {
        Vector3 d = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector3.forward;
        float at = Time.time + ShotWarning;
        pendingShots.Add(new PendingShot { at = at, fire = fire, snap = mgr.TakeRushSnap() });
        warns.Add(new WarnLine { origin = origin, dir = d, until = at });
        Sfx.PlayAt(Sfx.Lock, origin, 0.5f);
    }

    private void TickShots()
    {
        for (int i = pendingShots.Count - 1; i >= 0; i--)
        {
            if (Time.time < pendingShots[i].at) continue;
            PendingShot p = pendingShots[i];
            pendingShots.RemoveAt(i);
            if (enemy != null && !enemy.IsDead && !enemy.IsWreck) p.fire(mgr.MapSince(p.snap));
        }
    }

    private void DrawWarns()
    {
        for (int i = warns.Count - 1; i >= 0; i--)
            if (Time.time >= warns[i].until) warns.RemoveAt(i);

        int used = 0;
        for (int i = 0; i < warns.Count; i++)
        {
            if (used >= warnPool.Count) warnPool.Add(MakeWarnLine());
            LineRenderer lr = warnPool[used++];
            WarnLine w = warns[i];
            float width = 0.7f * (0.75f + 0.25f * Mathf.Sin(Time.time * 28f));
            lr.enabled = true;
            lr.SetPosition(0, w.origin);
            lr.SetPosition(1, w.origin + w.dir * WarnLength);
            lr.startWidth = width;
            lr.endWidth = width;
        }
        for (int i = used; i < warnPool.Count; i++) warnPool[i].enabled = false;
        DrawGapLine();
    }

    private LineRenderer MakeWarnLine()
    {
        var go = new GameObject("BossWarn");
        go.transform.SetParent(transform, false);
        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.positionCount = 2;
        lr.numCapVertices = 2;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.sharedMaterial = new Material(mgr.bossGlow != null ? mgr.bossGlow : mgr.fxMaterial);
        Color c = new Color(1f, 0.45f, 0.15f);
        lr.sharedMaterial.color = c;
        if (lr.sharedMaterial.HasProperty("_BaseColor")) lr.sharedMaterial.SetColor("_BaseColor", c);
        return lr;
    }

    private void ClearWarns()
    {
        warns.Clear();
        pendingShots.Clear();
        curtainGapUntil = 0f;
        wheelEnd = 0f;
        HideWall();
        EndKiWave();
        EndFists();
        EndSweep();
        ClearZones();
        EndGale();
        HideChargeWarn();
        if (gapLine != null) gapLine.enabled = false;
        foreach (var lr in warnPool) if (lr != null) lr.enabled = false;
    }

    // Uyarili atis uyari bitince cikar: hedefe donuk yon, oyuncunun bu surede (ayni yonde surerse) gidecegi yere kaydirilir.
    // Uyari cizgisi bu noktayi gosterir; yonunu degistiren oyuncu kacar, duz kaymaya devam eden vurulur.
    private Vector3 LeadForWarning(Vector3 origin, Vector3 dir)
    {
        if (player == null || dir.sqrMagnitude < 0.0001f) return dir;
        float dist = Vector3.Distance(origin, player.position);
        Vector3 to = origin + dir.normalized * dist + mgr.PlayerVelocity * ShotWarning - origin;
        return to.sqrMagnitude > 0.01f ? to.normalized : dir;
    }

    // Yatay yelpaze: merkez mermi tam hedefe
    private void Fan(Vector3 origin, Vector3 dir, int count, float totalDeg, float speed, int damage)
    {
        dir = LeadForWarning(origin, dir);
        Telegraphed(origin, dir, m => FanNow(m.Point(origin), m.Dir(dir), count, totalDeg, speed, damage));
    }

    private int fanCount;

    private void FanNow(Vector3 origin, Vector3 dir, int count, float totalDeg, float speed, int damage)
    {
        // Boss fazinda yelpaze sirayla yatay ve dikey gelir: tek bir yukari-asagi kayma ile hepsinden kacilmaz
        Vector3 axis = Vector3.up;
        if (mgr.RushActive && (fanCount++ & 1) == 1)
        {
            Vector3 side = Vector3.Cross(dir, Vector3.up);
            if (side.sqrMagnitude > 0.0001f) axis = side.normalized;
        }

        float step = count > 1 ? totalDeg / (count - 1) : 0f;
        for (int i = 0; i < count; i++)
        {
            float off = count > 1 ? -totalDeg * 0.5f + step * i : 0f;
            Shoot(origin, Quaternion.AngleAxis(off, axis) * dir, speed, damage);
        }
    }

    // Hedefe yonelik konik halka: merkezde bir hedef mermisi + halka katmani
    private void Cone(Vector3 origin, Vector3 dir, int count, float tiltDeg, float speed, int damage, float offsetDeg)
    {
        dir = LeadForWarning(origin, dir);
        Telegraphed(origin, dir, m => ConeNow(m.Point(origin), m.Dir(dir), count, tiltDeg, speed, damage, offsetDeg));
    }

    private void ConeNow(Vector3 origin, Vector3 dir, int count, float tiltDeg, float speed, int damage, float offsetDeg)
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
    private float sprayStart;
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
        sprayStart = Time.time + ShotWarning;
        sprayEnd = sprayStart + duration;
        sprayNext = sprayStart;
        Vector3 aim = player != null ? player.position - origin.position : origin.forward;
        warns.Add(new WarnLine { origin = origin.position, dir = aim.sqrMagnitude > 0.0001f ? aim.normalized : Vector3.forward, until = sprayStart });
        Sfx.PlayAt(Sfx.Lock, origin.position, 0.5f);
        sprayArms = Mathf.Max(1, arms);
        sprayTilt = tiltDeg;
        sprayInterval = interval;
        spraySpeed = speed;
        sprayDamage = damage;
        nextAttack = Mathf.Max(nextAttack, sprayEnd + 0.4f);
    }

    private void TickSpray()
    {
        if (!Spraying || sprayOrigin == null || Time.time < sprayStart) return;

        while (Time.time >= sprayNext)
        {
            sprayNext += sprayInterval;

            Vector3 origin = sprayOrigin.position;
            Vector3 axis = (player.position - origin).normalized;
            Vector3 up = Mathf.Abs(Vector3.Dot(axis, Vector3.up)) > 0.95f ? Vector3.right : Vector3.up;
            Vector3 u = Vector3.Cross(axis, up).normalized;
            Vector3 v = Vector3.Cross(axis, u).normalized;
            // Hedefli sarmal: halka periyodik daralip oyuncunun uzerine kapanir (eskiden sabit egimle ici bos kaliyor,
            // yerinde duran oyuncuya hic degmiyordu)
            float breathe = 0.15f + 0.85f * Mathf.Abs(Mathf.Sin((Time.time - sprayStart) * 2.4f));
            float tilt = sprayTilt * breathe * Mathf.Deg2Rad;

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
    // Uyari: ince sari cizgi oyuncuyu izler; son BeamLockTime saniyede KILITLENIR (kalin, yanip sonen kirmizi cizgi,
    // HUD "ISIN! YANA KAY" oku). Ates kilitli yone gider, oyuncuyu izlemez: kilit aninda yana kayan oyuncu kurtulur.
    // Isin basina en fazla BeamMaxHits vurus. Cizgi oyuncunun derinliginde biter (eskiden 760 m uzayip kameranin
    // yanindan geciyor, ekranin %40'ini kaplayan dev bir kirmizi kama gibi gorunuyordu); ince parlak cekirdek + vurus
    // genisliginde saydam parilti olarak cizilir.

    private const float BeamLength = 760f;
    private const float BeamLockTime = 0.45f;     // uyarinin sonunda yonun sabitlendigi sure
    private const int BeamMaxHits = 2;
    private const float BeamCoreWidth = 3f;       // atesteki parlak cekirdek (m); parilti = vurus genisligi

    private bool beamActive;
    private Transform beamOrigin;
    private float beamStart;
    private float beamTelegraph;
    private float beamFire;
    private Vector3 beamDir;
    private float beamNextTick;
    private int beamHits;
    private bool beamLockedSet;
    private Vector2 beamLockFrame;   // boss fazinda kilit noktasi cercevede (yol kivrilsa da ekranda sabit kalir)
    private LineRenderer beamLine, beamGlow;

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
        beamHits = 0;
        beamLockedSet = false;

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

            var gg = new GameObject("BossBeamGlow");
            gg.transform.SetParent(transform, false);
            beamGlow = gg.AddComponent<LineRenderer>();
            beamGlow.useWorldSpace = true;
            beamGlow.positionCount = 2;
            beamGlow.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var gm = new Material(mgr.fxMaterial != null ? mgr.fxMaterial : mgr.bossGlow);
            gm.mainTexture = Texture2D.whiteTexture;
            beamGlow.sharedMaterial = gm;
        }
        beamLine.enabled = true;
        beamGlow.enabled = false;

        nextAttack = Mathf.Max(nextAttack, Time.time + telegraph + fire + 0.5f);
    }

    private void TickBeam(float dt)
    {
        if (!beamActive || beamOrigin == null) return;

        float t = Time.time - beamStart;
        Vector3 o = beamOrigin.position;
        Vector3 want = (player.position - o).normalized;

        GameSettings gs = GameSettings.Current;
        if (t >= beamTelegraph - BeamLockTime)
        {
            // Kilit: boss fazinda hedef, oyuncunun kilit anindaki cerceve noktasi; isin her adim oraya yeniden
            // dogrultulur (dunyada sabit yon, kivrilan yolda ekranda kayiyordu)
            if (!beamLockedSet) { beamLockedSet = true; beamLockFrame = mgr.RushActive ? PlayerFrame() : Vector2.zero; }
            if (mgr.RushActive) beamDir = (KiWaveWorld(beamLockFrame) - o).normalized;
        }
        if (t < beamTelegraph - BeamLockTime)
        {
            // Nisan: ince sari cizgi oyuncuyu izler
            beamDir = Vector3.Slerp(beamDir, want, 1f - Mathf.Exp(-5f * dt));
            SetBeamVisual(o, 0.5f, new Color(1f, 0.85f, 0.2f), 0f);
        }
        else if (t < beamTelegraph)
        {
            // Kilit: yon sabit, kalin ve yanip sonen cizgi (simdi yana kay)
            float blink = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(t * 28f));
            SetBeamVisual(o, 1.4f, new Color(1f, 0.35f * blink, 0.15f), 0f);
        }
        else if (t < beamTelegraph + beamFire)
        {
            // Ates: kilitli yone; gercek vurus genisliginde
            SetBeamVisual(o, BeamCoreWidth, new Color(1f, 0.35f, 0.25f), gs.bossBeamHitRadius * 2f);

            Vector3 toP = player.position - o;
            float along = Vector3.Dot(toP, beamDir);
            if (beamHits < BeamMaxHits && along > 0f && along < BeamLength && Time.time >= beamNextTick)
            {
                float d = (toP - beamDir * along).magnitude;
                if (d < gs.bossBeamHitRadius)
                {
                    beamNextTick = Time.time + gs.bossBeamTick;
                    beamHits++;
                    PlayerHealth ph = mgr.CurrentPlayerHealth;
                    if (ph != null) ph.TakeHit(gs.bossBeamDamage, player.position);
                }
            }
        }
        else
        {
            beamActive = false;
            if (beamLine != null) beamLine.enabled = false;
            if (beamGlow != null) beamGlow.enabled = false;
        }
    }

    private void SetBeamVisual(Vector3 origin, float width, Color color, float glowWidth)
    {
        if (beamLine == null) return;
        // Oyuncunun derinliginde biter: kameranin yanindan gecip ekrani kaplamasin
        float along = player != null ? Vector3.Dot(player.position - origin, beamDir) : BeamLength;
        float len = along > 0f ? Mathf.Min(BeamLength, along) : BeamLength;
        beamLine.SetPosition(0, origin);
        beamLine.SetPosition(1, origin + beamDir * len);
        if (beamGlow != null)
        {
            beamGlow.enabled = glowWidth > 0f;
            beamGlow.SetPosition(0, origin);
            beamGlow.SetPosition(1, origin + beamDir * len);
            beamGlow.startWidth = beamGlow.endWidth = glowWidth;
            SetLineColor(beamGlow, new Color(1f, 0.3f, 0.2f, 0.28f));
        }
        beamLine.startWidth = width;
        beamLine.endWidth = width;
        beamLine.sharedMaterial.color = color;
        if (beamLine.sharedMaterial.HasProperty("_BaseColor")) beamLine.sharedMaterial.SetColor("_BaseColor", color);
    }

    /// <summary>
    /// HUD: boss isini nisan aliyor ya da kilitli / ateste. locked: yon sabit (artik kacmanin zamani); inside: oyuncu
    /// vurus koridorunda; exit: koridorun disinda, cercevede en yakin guvenli nokta (kenara sikismissa obur yan).
    /// </summary>
    public bool BeamDanger(out Vector3 exit, out bool locked, out bool inside)
    {
        exit = Vector3.zero;
        locked = inside = false;
        if (!beamActive || beamOrigin == null || player == null || !mgr.RushActive) return false;
        float t = Time.time - beamStart;
        locked = t >= beamTelegraph - BeamLockTime;

        GameSettings gs = GameSettings.Current;
        Vector3 o = beamOrigin.position;
        Vector3 toP = player.position - o;
        float along = Mathf.Max(0f, Vector3.Dot(toP, beamDir));
        Vector3 off = toP - beamDir * along;
        Vector3 right = Vector3.Cross(Vector3.up, mgr.RushHeading).normalized;
        Vector2 off2 = new Vector2(Vector3.Dot(off, right), off.y);
        float margin = gs.bossBeamHitRadius + 6f;
        inside = off2.magnitude < margin;

        Vector2 dir2 = off2.magnitude > 0.5f ? off2.normalized : Vector2.right;
        Vector2 pf = PlayerFrame();
        Vector2 target = pf + dir2 * (margin + 8f - off2.magnitude);
        if (Mathf.Abs(target.x) > CombatManager.RushHalfWidth - 6f || target.y > CombatManager.RushUp - 6f || target.y < -CombatManager.RushDown + 6f)
            target = pf - dir2 * (margin + 8f + off2.magnitude);   // kenara sikisti: obur yan
        exit = KiWaveWorld(target);
        return true;
    }

    // ----------------------------------------------------------- hamle (charge)
    // Hazirlik (yavaslar, oyuncuya doner) -> kilitlenen noktaya dalis (oyuncu yerinde kalirsa carpar, temas hasari
    // EnemyPlane.ScriptedStep'te) -> eski yerine, oyuncunun onundeki konumuna hizla geri donus. Arkaya gecip kaybolmaz.
    // Boss fazinda uyarili: hazirlik boyunca hedefte sari halka oyuncuyu izler; dalistan ChargeLockTime once hedef
    // oyuncunun o anki yerine KILITLENIR (halka kizarir, boss'tan hedefe kirmizi cizgi, HUD "DALIS! KAC" oku).
    // Halka boss govdesinin dalis yonune dik yaricapi kadardir: disina cikan oyuncuya carpmaz.

    private const float ChargeLockTime = 0.65f;
    private const float ChargeMinTelegraphRush = 1.05f;
    private bool chargeLocked;
    private float chargeRadius;
    private LineRenderer chargeRing, chargeLine;

    private bool chargeActive;
    private int chargeStage;            // 0 hazirlik, 1 dalis, 2 donus
    private float chargeStart;
    private float chargeTelegraph;
    private float chargeSpeed;
    private float chargeDuration;
    private float chargeStageEnd;
    private Vector3 chargeDir;
    private Vector3 chargeTarget;

    private bool Charging => chargeActive;
    private bool ChargeReturning => chargeActive && chargeStage == 2;

    private void StartCharge(float telegraph, float speed, float duration)
    {
        // Vurkac hizli olsun: boss fazinda hazirlik kisa, dalis hizli; disarida da eskisinden cevik
        if (mgr.RushActive) { telegraph = Mathf.Max(ChargeMinTelegraphRush, telegraph * 0.9f); speed *= 1.5f; }
        else { telegraph *= 0.85f; speed *= 1.25f; }
        chargeActive = true;
        chargeStage = 0;
        chargeStart = Time.time;
        chargeTelegraph = telegraph;
        chargeSpeed = speed;
        chargeDuration = duration;
        chargeLocked = false;
        chargeRadius = 0f;
        nextAttack = Mathf.Max(nextAttack, Time.time + telegraph + duration + 2.2f);
    }

    /// <summary>Govdenin (tum collider'lar) verilen yone dik en buyuk yaricapi + temas payi (m).</summary>
    private float BodyRadiusAcross(Vector3 dir)
    {
        float r = 6f;
        foreach (var c in GetComponentsInChildren<Collider>())
        {
            if (c == null || !c.enabled) continue;
            Bounds b = c.bounds;
            Vector3 off = Vector3.ProjectOnPlane(b.center - pos, dir);
            r = Mathf.Max(r, off.magnitude + Mathf.Max(b.extents.x, Mathf.Max(b.extents.y, b.extents.z)));
        }
        return r + GameSettings.Current.ramPlayerRadius + 1f;
    }

    // true: hareketi bu kare hamle yonetiyor (donus asamasinda normal hareket hizlandirilmis calisir)
    private bool TickCharge(float dt)
    {
        if (!chargeActive) return false;

        if (chargeStage == 0)
        {
            // Boss fazi: dalistan ChargeLockTime once hedef kilitlenir (yerinde kalan carpar, kayan kurtulur)
            if (mgr.RushActive && !chargeLocked && Time.time - chargeStart >= chargeTelegraph - ChargeLockTime)
            {
                chargeLocked = true;
                chargeTarget = player.position;
                chargeRadius = BodyRadiusAcross((chargeTarget - pos).normalized);
                Sfx.PlayAt(Sfx.Lock, player.position, 1f);
            }
            if (Time.time - chargeStart < chargeTelegraph)
            {
                // Hazirlik: yavasca durur, hedefe doner (parcalar parlar)
                vel = Vector3.MoveTowards(vel, Vector3.zero, 90f * dt);
                pos += vel * dt;
                FaceDir(player.position - pos, 160f, dt);
                return true;
            }

            // Kilit: oyuncunun o anki yeri (boss disinda biraz onde tutma; boss fazinda erken kilitlendi)
            if (!chargeLocked) chargeTarget = player.position + mgr.PlayerVelocity * 0.3f;
            Vector3 to = chargeTarget - pos;
            float d = to.magnitude;
            chargeDir = d > 0.1f ? to / d : transform.forward;
            chargeStage = 1;
            chargeStageEnd = Time.time + Mathf.Min(chargeDuration + 0.6f, d / Mathf.Max(chargeSpeed, 1f) + 0.35f);
            Sfx.PlayAt(Sfx.BossAttack, pos, 0.9f);
        }

        if (chargeStage == 1)
        {
            vel = chargeDir * chargeSpeed;
            pos += vel * dt;
            FaceDir(chargeDir, 240f, dt);

            // Kilit noktasini gecti ya da sure doldu: geri donus
            if (Vector3.Dot(chargeTarget - pos, chargeDir) <= 0f || Time.time >= chargeStageEnd)
            {
                chargeStage = 2;
                chargeStageEnd = Time.time + 2.0f;
                vel = -chargeDir * chargeSpeed * 0.45f;   // carpinca geri seker: oyuncunun ustunde beklemez
                nextAttack = Mathf.Max(nextAttack, chargeStageEnd + 0.3f);
                OnChargePassed();
            }
            return true;
        }

        // Donus: tur hareketi (Move) hizlandirilmis olarak calisir; oyuncunun onundeki yerine varinca biter
        Vector3 rel = pos - player.position;
        float backAt = 220f * (mgr.RushActive ? RushAnchorScale : 1f);
        if (Vector3.Dot(rel, anchorFwd) > backAt || Time.time >= chargeStageEnd) chargeActive = false;
        return false;
    }

    // Dalis uyarisi (Update): hazirlikta oyuncuyu izleyen sari halka, kilitte yanip sonen kirmizi halka + boss'tan
    // hedefe cizgi. Halka bolge halkalari gibi oyuncunun onundeki duzlemde (OverlayPoint) cizilir.
    private void DrawChargeWarn()
    {
        bool show = chargeActive && chargeStage == 0 && mgr.RushActive && player != null;
        if (!show) { HideChargeWarn(); return; }
        if (chargeRing == null) { chargeRing = MakeZoneLine("ChargeRing", true); chargeLine = MakeZoneLine("ChargeLine", false); }
        Vector3 right = Vector3.Cross(Vector3.up, mgr.RushHeading).normalized;
        Vector3 aimAt = chargeLocked ? chargeTarget : player.position;
        Vector3 rel = aimAt - mgr.RushOrigin;
        Vector2 frame = new Vector2(Vector3.Dot(rel, right), rel.y);
        float r = chargeLocked ? chargeRadius : BodyRadiusAcross((player.position - pos).normalized);
        float blink = chargeLocked ? 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(Time.time * 24f)) : 0.8f;
        Color c = chargeLocked ? new Color(1f, 0.2f, 0.12f, blink) : new Color(1f, 0.85f, 0.25f, 0.75f);
        SetCircle(chargeRing, OverlayPoint(frame, ZoneAhead), right, r, chargeLocked ? 2.2f : 1.2f, c);
        chargeLine.loop = false;
        chargeLine.positionCount = 2;
        chargeLine.SetPosition(0, pos);
        chargeLine.SetPosition(1, aimAt);
        chargeLine.startWidth = chargeLocked ? 2.2f : 0.8f;
        chargeLine.endWidth = 0.4f;
        SetLineColor(chargeLine, c);
        chargeLine.enabled = true;
    }

    private void HideChargeWarn()
    {
        if (chargeRing != null) chargeRing.enabled = false;
        if (chargeLine != null) chargeLine.enabled = false;
    }

    /// <summary>
    /// HUD: dalis hedefi kilitliyken (hazirlik sonu ya da dalis) oyuncu govdenin yolundaysa en yakin guvenli nokta ve
    /// dalisa kalan sure.
    /// </summary>
    public bool ChargeDanger(out Vector3 exit, out float timeLeft)
    {
        exit = Vector3.zero;
        timeLeft = 0f;
        if (!chargeActive || !chargeLocked || chargeStage > 1 || player == null || !mgr.RushActive) return false;
        Vector3 right = Vector3.Cross(Vector3.up, mgr.RushHeading).normalized;
        Vector3 d = player.position - chargeTarget;
        Vector2 off = new Vector2(Vector3.Dot(d, right), d.y);
        float margin = chargeRadius + 6f;
        if (off.magnitude >= margin) return false;
        Vector2 dir = off.magnitude > 0.5f ? off.normalized : Vector2.up;
        Vector2 pf = PlayerFrame();
        Vector2 target = pf + dir * (margin + 6f - off.magnitude);
        if (Mathf.Abs(target.x) > CombatManager.RushHalfWidth - 6f || target.y > CombatManager.RushUp - 6f || target.y < -CombatManager.RushDown + 6f)
            target = pf - dir * (margin + 6f + off.magnitude);
        exit = KiWaveWorld(target);
        timeLeft = chargeStage == 0 ? chargeTelegraph - (Time.time - chargeStart) : 0f;
        return true;
    }

    // Boss fazi: cerceve kayip dondugunde, dunyada tutulan ara noktalar ve yonler de cerceveyle birlikte tasinir
    private void ApplyFrameMap()
    {
        vel = mgr.RushMapDir(vel);
        chargeTarget = mgr.RushMapPoint(chargeTarget);
        chargeDir = mgr.RushMapDir(chargeDir);
        beamDir = mgr.RushMapDir(beamDir);
        crabSweepBase = mgr.RushMapDir(crabSweepBase);
        fistPosL = mgr.RushMapPoint(fistPosL); fistPosR = mgr.RushMapPoint(fistPosR);
        fistAimL = mgr.RushMapPoint(fistAimL); fistAimR = mgr.RushMapPoint(fistAimR);
        fistLockAim = mgr.RushMapPoint(fistLockAim);
        fistDirL = mgr.RushMapDir(fistDirL); fistDirR = mgr.RushMapDir(fistDirR);
        for (int i = 0; i < warns.Count; i++)
        {
            WarnLine w = warns[i];
            w.origin = mgr.RushMapPoint(w.origin);
            w.dir = mgr.RushMapDir(w.dir);
            warns[i] = w;
        }
        for (int i = 0; i < trail.Count; i++) trail[i] = mgr.RushMapPoint(trail[i]);
    }
}
