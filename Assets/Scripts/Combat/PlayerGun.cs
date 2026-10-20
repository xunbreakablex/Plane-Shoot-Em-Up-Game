using UnityEngine;
using UnityEngine.InputSystem;

// Mouse ile nisan, sol tik ile ates. Hangarda secilen silahlarin her biri kendi noktasindan,
// kendi atis hizi/hasariyla ates eder. Imlec bir dusmanin yakinindaysa yardimli nisan
// (hedefe kilitlenir ve onde tutma/lead uygular).
public partial class PlayerGun : MonoBehaviour
{
    public FlightTestManager flight;
    public Camera cam;
    public Bullet bulletPrefab;

    [Tooltip("Onde tutma hesabi icin varsayilan mermi hizi (silah takili degilse)")]
    public float bulletSpeed = 380f;
    [Tooltip("Imlecin dusmana kilitlenme yaricapi (piksel)")] public float assistRadius = 90f;
    public float defaultAimDistance = 900f;

    [Header("Missiles")]
    public Missile missilePrefab;
    [Tooltip("Imlec kilidi yoksa burun onundeki bu aci icindeki en yakin dusman hedeflenir")]
    public float missileLockCone = 35f;
    public float missileInterval = 0.45f;

    public Vector2 CrosshairScreenPos { get; private set; }
    public EnemyPlane LockedEnemy { get; private set; }
    public EnemyPlane MissileTarget { get; private set; }
    public Vector3 AimPoint { get; private set; }
    public bool IsFiring { get; private set; }

    // Silah isisi: atesle yukselir, birakinca soyur; 1'e ulasirsa silahlar kilitlenir ve esige kadar sogumasi beklenir
    public float Heat { get; private set; }
    public bool Overheated { get; private set; }
    private const float HeatPerSecond = 0.14f;
    private const float CoolPerSecond = 0.22f;
    private const float BaseReleaseHeat = 0.30f;
    public static float ReleaseHeat => Mathf.Min(0.8f, BaseReleaseHeat + RunUpgrades.Mods.coolReleaseAdd);

    private void TickHeat(bool firing)
    {
        UpgradeMods m = RunUpgrades.Mods;
        float dt = Time.deltaTime;
        if (firing)
        {
            Heat += HeatPerSecond * m.heatMul * dt;
            if (Heat >= 1f)
            {
                Heat = 1f;
                Overheated = true;
                audioSource.PlayOneShot(Sfx.Warning, 0.6f);
                FollowCamera.AddShake(0.25f, 0.25f);
            }
        }
        else
        {
            Heat = Mathf.Max(0f, Heat - CoolPerSecond * m.coolMul * dt);
        }
        if (Overheated && Heat <= ReleaseHeat) Overheated = false;
    }
    public bool HasWeapons { get; private set; }
    public bool HasMissiles { get; private set; }
    public string MissileStatus { get; private set; } = "";
    private int missileStatusKey = int.MinValue;
    private bool missileStatusDirty = true;

    private readonly float[] nextFire = new float[PartCatalog.WeaponSlots];
    private readonly int[] shotCount = new int[PartCatalog.WeaponSlots];

    // Guc seviyesi basina ates hizi artisi (silah DPS hesabi ve ates kodu ayni sabiti kullanir)
    public const float PowerRateStep = 0.05f;

    /// <summary>
    /// Verilen donanimin, her mermi isabet etse bile, saniyede verecegi teorik hasar (silah + taret).
    /// Denge ayarinda boss canini bununla kiyasla.
    /// </summary>
    public static float TheoreticalDps(PlaneLoadout loadout)
    {
        UpgradeMods mods = RunUpgrades.Mods;
        int power = RunState.PowerLevel;
        float rateMul = mods.fireRateMul * (1f + PowerRateStep * power);
        int sides = (power >= 2 ? 2 : 0) + (power >= 4 ? 2 : 0);

        float dps = 0f;
        foreach (int w in loadout.weapons)
        {
            if (w <= 0) continue;
            PartDef d = PartCatalog.Weapons[w];
            float perShot = d.beam > 0 ? d.damage : d.ShotDamage + sides * 0.5f;   // yan mermiler her iki atista bir cikar (isinlarda yok)
            dps += d.rate * rateMul * perShot;
        }

        foreach (int t in loadout.turrets)
        {
            if (t <= 0) continue;
            PartDef d = PartCatalog.Turrets[t];
            dps += d.rate * mods.turretRateMul * d.damage;
        }
        return dps;
    }

    /// <summary>
    /// Donanimin temel DPS'i: guc seviyesi ve yukseltmeler dahil degil. Dusman canini bununla belirlenince
    /// guclenen oyuncu dusmani daha hizli dusurur; yukseltme dusmani tanklastirmaz.
    /// </summary>
    public static float ReferenceDps(PlaneLoadout loadout)
    {
        float dps = 0f;
        foreach (int w in loadout.weapons)
        {
            if (w <= 0) continue;
            PartDef d = PartCatalog.Weapons[w];
            dps += d.rate * (d.beam > 0 ? d.damage : d.ShotDamage);
        }
        foreach (int t in loadout.turrets)
        {
            if (t <= 0) continue;
            PartDef d = PartCatalog.Turrets[t];
            dps += d.rate * d.damage;
        }
        return dps;
    }

    // Model basina: rampa mühimmatı, yenilenme zamanı, takili parca (degisince doldurulur)
    private readonly int[][] missileAmmo = new int[LoadoutStore.ModelCount][];
    private readonly float[][] missileRefill = new float[LoadoutStore.ModelCount][];
    private readonly int[][] missileType = new int[LoadoutStore.ModelCount][];
    private float nextMissile;
    private AudioSource audioSource;
    private readonly RaycastHit[] rayHits = new RaycastHit[16];

    private void Awake()
    {
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.spatialBlend = 0f;
        audioSource.playOnAwake = false;
    }

    private void Update()
    {
        if (GameState.Paused) return;
        if (flight == null || cam == null) return;
        PlaneController plane = flight.Current;
        if (plane == null) return;

        PlaneLoadout loadout = LoadoutStore.Get(flight.CurrentIndex);

        // Onde tutma icin ortalama mermi hizi (isinlar aninda vurur: onde tutma gerekmez, hesaba katilmaz)
        float speedSum = 0f; int count = 0; int any = 0;
        foreach (int w in loadout.weapons)
        {
            if (w <= 0) continue;
            any++;
            if (PartCatalog.Weapons[w].beam > 0) continue;
            speedSum += PartCatalog.Weapons[w].speed;
            count++;
        }
        HasWeapons = any > 0;
        // Onde tutma, merminin gercek hiziyla (upgrade ve GameSettings carpani dahil) hesaplanir
        float leadSpeed = (count > 0 ? speedSum / count : (any > 0 ? 1000000f / (RunUpgrades.Mods.bulletSpeedMul * GameSettings.Current.playerBulletSpeedMultiplier) : bulletSpeed))
                          * RunUpgrades.Mods.bulletSpeedMul * GameSettings.Current.playerBulletSpeedMultiplier;

        if (ControlSettings.EffectiveAutoAim) UpdateAutoAim(plane, leadSpeed);
        else UpdateAim(plane, leadSpeed);

        // Ates: SPACE basili tut (her iki nisan modunda) ya da sol tik. SPACE yerde ayrica frendir.
        bool trigger = (Mouse.current != null && Mouse.current.leftButton.isPressed)
                       || (Keyboard.current != null && Keyboard.current.spaceKey.isPressed)
                       || (Gamepad.current != null && Gamepad.current.rightTrigger.ReadValue() > 0.35f);
        IsFiring = trigger && !plane.IsCrashed && HasWeapons && !Overheated;
        TickHeat(IsFiring);

        if (IsFiring) FireWeapons(plane, loadout);
        else StopBeams();

        UpdateMissiles(plane, loadout);
    }

    // ------------------------------------------------------------- missiles

    private void EnsureMissileState(int model, PlaneLoadout loadout)
    {
        int n = PartCatalog.MissileSlots;
        if (missileAmmo[model] == null)
        {
            missileAmmo[model] = new int[n];
            missileRefill[model] = new float[n];
            missileType[model] = new int[n];
            for (int i = 0; i < n; i++) missileType[model][i] = -1;
        }

        for (int i = 0; i < n; i++)
        {
            int type = loadout.missiles[i];
            if (missileType[model][i] != type)
            {
                // Parca degisti (hangar): rampa dolu baslasin
                missileType[model][i] = type;
                missileAmmo[model][i] = type > 0 ? PartCatalog.Missiles[type].ammo : 0;
                missileRefill[model][i] = 0f;
            }
        }
    }

    private void UpdateMissiles(PlaneController plane, PlaneLoadout loadout)
    {
        int model = flight.CurrentIndex;
        EnsureMissileState(model, loadout);

        int[] ammo = missileAmmo[model];
        float[] refill = missileRefill[model];

        int total = 0, launchers = 0;
        for (int i = 0; i < PartCatalog.MissileSlots; i++)
        {
            int type = loadout.missiles[i];
            if (type <= 0) continue;
            launchers++;

            PartDef def = PartCatalog.Missiles[type];
            UpgradeMods mods = RunUpgrades.Mods;
            int cap = def.ammo + mods.missileAmmoAdd;
            float reload = def.reload * mods.missileReloadMul;
            if (ammo[i] < cap)
            {
                if (refill[i] <= 0f) refill[i] = Time.time + reload;
                else if (Time.time >= refill[i])
                {
                    ammo[i]++;
                    refill[i] = ammo[i] < cap ? Time.time + reload : 0f;
                }
            }
            total += ammo[i];
        }

        HasMissiles = launchers > 0;
        if (!HasMissiles)
        {
            MissileTarget = null;
            MissileStatus = "";
            return;
        }

        float lockRange = 1400f;
        MissileTarget = FindMissileTarget(plane.transform, lockRange);

        // Metin yalniz cephane/kilit durumu degisince yeniden kurulur (her kare cop uretmesin)
        int statusKey = MissileTarget != null ? 1 : 0;
        for (int i = 0; i < PartCatalog.MissileSlots; i++)
            statusKey = statusKey * 31 + (loadout.missiles[i] <= 0 ? -1 : ammo[i] * 64 + loadout.missiles[i])
                        + RunUpgrades.Mods.missileAmmoAdd * 7;

        if (statusKey != missileStatusKey || missileStatusDirty)
        {
            missileStatusKey = statusKey;
            missileStatusDirty = false;

            string a = "";
            for (int i = 0; i < PartCatalog.MissileSlots; i++)
            {
                if (loadout.missiles[i] <= 0) continue;
                a += (a.Length > 0 ? "  |  " : "") + ammo[i] + "/" + (PartCatalog.Missiles[loadout.missiles[i]].ammo + RunUpgrades.Mods.missileAmmoAdd);
            }
            MissileStatus = $"FÜZE [F]: {a}" + (MissileTarget != null ? "   •   KİLİTLİ" : "");
        }

        var kb = Keyboard.current;
        var gp = Gamepad.current;
        bool missileKey = (kb != null && kb.fKey.wasPressedThisFrame) || (gp != null && gp.buttonNorth.wasPressedThisFrame);
        if (missileKey && !plane.IsCrashed && Time.time >= nextMissile)
            LaunchMissile(plane, loadout, ammo, refill);
    }

    /// <summary>Magaza: aktif ucagin tum fuze rampalarini doldurur.</summary>
    public void RefillMissiles()
    {
        if (flight == null) return;
        int model = flight.CurrentIndex;
        PlaneLoadout loadout = LoadoutStore.Get(model);
        EnsureMissileState(model, loadout);

        for (int i = 0; i < PartCatalog.MissileSlots; i++)
        {
            int type = loadout.missiles[i];
            if (type <= 0) continue;
            missileAmmo[model][i] = PartCatalog.Missiles[type].ammo + RunUpgrades.Mods.missileAmmoAdd;
            missileRefill[model][i] = 0f;
        }
    }

    private EnemyPlane FindMissileTarget(Transform plane, float range)
    {
        // Imlecin altindaki dusman onceliklidir
        if (LockedEnemy != null && Vector3.Distance(plane.position, LockedEnemy.transform.position) <= range)
            return LockedEnemy;

        if (CombatManager.Instance == null) return null;

        EnemyPlane best = null;
        float bestAngle = missileLockCone;
        foreach (var e in CombatManager.Instance.Enemies)
        {
            if (e == null) continue;
            Vector3 to = e.transform.position - plane.position;
            if (to.magnitude > range) continue;
            float angle = Vector3.Angle(plane.forward, to);
            if (angle < bestAngle)
            {
                bestAngle = angle;
                best = e;
            }
        }
        return best;
    }

    private void LaunchMissile(PlaneController plane, PlaneLoadout loadout, int[] ammo, float[] refill)
    {
        if (missilePrefab == null) return;

        // Dolu rampalar arasinda sirayla
        int slot = -1;
        for (int i = 0; i < PartCatalog.MissileSlots; i++)
        {
            if (loadout.missiles[i] > 0 && ammo[i] > 0 && (slot < 0 || ammo[i] > ammo[slot])) slot = i;
        }
        if (slot < 0) return;

        PartDef def = PartCatalog.Missiles[loadout.missiles[slot]];
        float span = PartCatalog.Wings[loadout.wings].span;
        Transform t = plane.transform;

        Vector3 pos = t.TransformPoint(PlaneBuilder.MissileOffset(flight.CurrentIndex, slot, span));
        Vector3 dir = t.forward;
        if (MissileTarget != null)
        {
            Vector3 to = (MissileTarget.transform.position - pos).normalized;
            if (Vector3.Angle(dir, to) < 70f) dir = Vector3.Slerp(dir, to, 0.35f).normalized;
        }

        // Upgrade'li kopya: hasar, donus hizi, patlama yaricapi
        UpgradeMods mods = RunUpgrades.Mods;
        PartDef eff = JsonUtility.FromJson<PartDef>(JsonUtility.ToJson(def));
        eff.damage = def.damage + mods.missileDmgAdd;
        eff.turn = def.turn * mods.missileTurnMul;
        eff.blast = def.blast * mods.blastMul;

        float launchSpeed = Mathf.Max(plane.Speed + 20f, 60f);
        Missile.Launch(missilePrefab, pos, Quaternion.LookRotation(dir), launchSpeed, MissileTarget, eff, t);

        ammo[slot]--;
        nextMissile = Time.time + missileInterval;
        if (refill[slot] <= 0f) refill[slot] = Time.time + def.reload * mods.missileReloadMul;

        audioSource.PlayOneShot(Sfx.MissileLaunch, 0.45f);
    }

    // Otomatik nisan: imlec burnun onunde (ucagin baktigi nokta). Ekranda imlece en yakin dusman
    // hedeflenir ve onde tutma uygulanir; hedef yoksa dumduz ileri ates edilir.
    private void UpdateAutoAim(PlaneController plane, float leadSpeed)
    {
        GameSettings gs = GameSettings.Current;
        Transform t = plane.transform;
        Vector3 bore = t.position + t.forward * defaultAimDistance;
        Vector3 sp = cam.WorldToScreenPoint(bore);
        CrosshairScreenPos = sp.z > 0f ? new Vector2(sp.x, sp.y) : new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);

        Vector3 camPos = cam.transform.position;
        float lockAngle = 0f;
        LockedEnemy = gs.autoAimStrength > 0.001f
            ? FindAutoTarget(t.position, camPos, bore - camPos, LockedEnemy, out lockAngle)
            : null;

        // Istenen nisan: burnun onu, kilitli dusman varsa onde tutma noktasina dogru. Yardim imlecin dibinde
        // tam, koninin kenarina dogru sifira iner: kenardaki dusman icin mermiler imlecten kivrilip gitmez.
        Vector3 origin = t.position + t.forward * 1.0f;
        Vector3 desiredPoint = bore;
        bool assisting = false;
        if (LockedEnemy != null)
        {
            float time = Vector3.Distance(origin, LockedEnemy.transform.position) / Mathf.Max(leadSpeed, 1f);
            Vector3 lead = LockedEnemy.transform.position + LockedEnemy.Velocity * time;

            float cone = Mathf.Max(gs.autoAimCone, 0.1f);
            float w = gs.autoAimStrength * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(cone * 0.35f, cone, lockAngle)));
            Vector3 toLead = lead - origin;
            float leadDist = Mathf.Max(20f, toLead.magnitude);
            desiredPoint = origin + Vector3.Slerp(t.forward, toLead / leadDist, w) * leadDist;
            assisting = w > 0.001f;
        }

        // Nisan yonu anlik atlamaz: ucaga gore (yerel) yon, hedefe aimAcquireTime, hedef birakilinca
        // aimReleaseTime surede yumusakca doner. Ucak donerken nisan geride kalmasin diye yumusatma yerel uzayda.
        Vector3 toDesired = desiredPoint - origin;
        float desiredDist = Mathf.Max(20f, toDesired.magnitude);
        Vector3 desiredLocal = t.InverseTransformDirection(toDesired / desiredDist);

        float acquire = assisting ? gs.aimAcquireTime : gs.aimReleaseTime;
        if (!aimSmoothInit || acquire <= 0.001f)
        {
            aimLocal = desiredLocal;
            aimLocalVel = Vector3.zero;
            aimSmoothInit = true;
        }
        else
        {
            aimLocal = Vector3.SmoothDamp(aimLocal, desiredLocal, ref aimLocalVel, acquire);
            if (aimLocal.sqrMagnitude < 1e-6f) aimLocal = desiredLocal;
            aimLocal.Normalize();
        }

        AimPoint = origin + t.TransformDirection(aimLocal) * desiredDist;
    }

    // Otomatik nisanin yumusatilmis yonu (ucak yerel uzayinda)
    private Vector3 aimLocal = Vector3.forward;
    private Vector3 aimLocalVel;
    private bool aimSmoothInit;

    // Ekranda imlece en yakin dusman: aci kameradan, imlecin gosterdigi noktaya gore olculur (oyuncunun
    // gordugu). Eskiden aci ucagin burnundan olculup koni icindeki mesafece en yakin dusman seciliyordu:
    // imlecin dibindeki dusman yerine kenardaki yakin dusmana kilitlenip mermiler imlecten uzaga gidiyordu.
    // Mevcut hedef konideyse, yenisi imlece belirgin sekilde (%40) daha yakin olmadikca degismez (titremesin).
    private EnemyPlane FindAutoTarget(Vector3 planePos, Vector3 camPos, Vector3 crossDir, EnemyPlane current, out float angle)
    {
        angle = 0f;
        if (CombatManager.Instance == null) return null;

        // Koni ve menzil GameSettings'ten (Inspector'dan ayarlanir)
        float range = GameSettings.Current.autoAimRange;
        float cone = GameSettings.Current.autoAimCone;

        EnemyPlane best = null;
        float bestAngle = cone;
        float currentAngle = float.MaxValue;

        foreach (var e in CombatManager.Instance.Enemies)
        {
            if (e == null || e.IsDead) continue;
            Vector3 p = e.transform.position;
            if ((p - planePos).sqrMagnitude > range * range) continue;
            float a = Vector3.Angle(crossDir, p - camPos);
            if (a > cone) continue;

            // Hava hedefi oncelikli: yer birimi sanki imlecten 1 derece daha uzaktaymis gibi puanlanir
            float score = a + (e.IsGround ? 1f : 0f);
            if (e == current) currentAngle = score;
            if (score < bestAngle)
            {
                bestAngle = score;
                best = e;
            }
        }

        if (current != null && currentAngle < float.MaxValue && best != current && bestAngle > currentAngle * 0.6f)
        {
            angle = currentAngle;
            return current;
        }
        angle = bestAngle;
        return best;
    }

    private void UpdateAim(PlaneController plane, float leadSpeed)
    {
        Vector2 mouse = Cursor.lockState == CursorLockMode.Locked || Mouse.current == null
            ? new Vector2(Screen.width * 0.5f, Screen.height * 0.5f)
            : Mouse.current.position.ReadValue();
        CrosshairScreenPos = mouse;

        // Yardimli nisan: imlece en yakin dusman
        LockedEnemy = null;
        float bestPx = assistRadius;
        if (CombatManager.Instance != null)
        {
            foreach (var e in CombatManager.Instance.Enemies)
            {
                if (e == null) continue;
                Vector3 sp = cam.WorldToScreenPoint(e.transform.position);
                if (sp.z <= 0f) continue;
                float d = Vector2.Distance(new Vector2(sp.x, sp.y), mouse);
                if (d < bestPx)
                {
                    bestPx = d;
                    LockedEnemy = e;
                }
            }
        }

        Vector3 muzzleCenter = plane.transform.position + plane.transform.forward * 1.0f;

        if (LockedEnemy != null)
        {
            float t = Vector3.Distance(muzzleCenter, LockedEnemy.transform.position) / Mathf.Max(leadSpeed, 1f);
            AimPoint = LockedEnemy.transform.position + LockedEnemy.Velocity * t;
            return;
        }

        // Kamera isini: kendi ucagimizi yoksayarak zemini/engelleri bul
        Ray ray = cam.ScreenPointToRay(mouse);
        Vector3 point = ray.GetPoint(defaultAimDistance);
        int n = Physics.RaycastNonAlloc(ray, rayHits, defaultAimDistance, ~0, QueryTriggerInteraction.Ignore);
        float nearest = float.MaxValue;
        for (int i = 0; i < n; i++)
        {
            RaycastHit h = rayHits[i];
            if (h.collider.transform.IsChildOf(plane.transform)) continue;
            if (h.distance < nearest)
            {
                nearest = h.distance;
                point = h.point;
            }
        }
        AimPoint = point;
    }

    private void SideBullets(Vector3 muzzle, Vector3 dir, Transform owner, float speed, float angle, int pierce)
    {
        for (int s = -1; s <= 1; s += 2)
        {
            Vector3 d = Quaternion.AngleAxis(angle * s, owner.up) * dir;
            // Ayni namlu ucundan; parlama ana atista zaten tetiklendi
            PlayerShot.Fire(bulletPrefab, null, muzzle, d, speed, 1, owner, pierce);
        }
    }

    // Namlu noktalari (MuzzleFlash): ucak degisince ya da donanim yeniden kurulunca (eski nesne yok olur) yeniden bulunur
    private readonly MuzzleFlash[] muzzles = new MuzzleFlash[PartCatalog.WeaponSlots];
    private Transform muzzleOwner;

    private MuzzleFlash MuzzleFor(Transform plane, int slot)
    {
        if (slot < 0 || slot >= muzzles.Length) return null;

        if (muzzleOwner != plane)
        {
            muzzleOwner = plane;
            System.Array.Clear(muzzles, 0, muzzles.Length);
        }

        MuzzleFlash m = muzzles[slot];
        if (m == null)
        {
            Transform mt = plane.Find(PlaneBuilder.GunFlashName(slot));
            if (mt == null) return null;
            m = mt.GetComponent<MuzzleFlash>();
            muzzles[slot] = m;
        }
        return m;
    }

    private void FireWeapons(PlaneController plane, PlaneLoadout loadout)
    {
        Transform t = plane.transform;
        float span = PartCatalog.Wings[loadout.wings].span;
        bool played = false;

        for (int i = 0; i < loadout.weapons.Length && i < nextFire.Length; i++)
        {
            int type = loadout.weapons[i];
            if (type <= 0) continue;

            PartDef def = PartCatalog.Weapons[type];
            // Lazerler surekli isin olarak islenir (PlayerGun.Beam.cs); atis araligi/mermi yok
            if (def.beam > 0)
            {
                BeamStep(i, def, plane, span);
                continue;
            }
            if (Time.time < nextFire[i]) continue;

            UpgradeMods mods = RunUpgrades.Mods;

            // Guc seviyesi (P ikonlari): ates hizi, hasar ve yan mermi sayisi artar
            int power = RunState.PowerLevel;
            float rateMul = mods.fireRateMul * (1f + PowerRateStep * power) * (RunState.OverdriveActive ? 1.5f : 1f);
            nextFire[i] = Time.time + 1f / Mathf.Max(def.rate * rateMul, 0.1f);
            shotCount[i]++;

            // Namlu noktasi yoksa (eski kurulum) ayni konum hesaplanir
            MuzzleFlash muzzlePoint = MuzzleFor(t, i);
            Vector3 muzzle = muzzlePoint != null
                ? PlayerShot.MuzzlePoint(muzzlePoint)
                : t.TransformPoint(PlaneBuilder.WeaponOffset(flight.CurrentIndex, i, span) + new Vector3(0f, 0f, PlayerShot.TipClearance));
            GameSettings gs = GameSettings.Current;
            float aimDist = Mathf.Max(20f, Vector3.Distance(muzzle, AimPoint));
            Vector3 dir = (AimPoint - muzzle).normalized;

            // Mermi burnun baktigi yondan en fazla GameSettings.fireCone kadar sapar (genis koni komik duruyordu)
            if (Vector3.Angle(t.forward, dir) > gs.fireCone)
                dir = Vector3.RotateTowards(t.forward, dir, gs.fireCone * Mathf.Deg2Rad, 0f).normalized;

            if (def.spread > 0f && def.pellets <= 1)
                dir = (dir + Random.insideUnitSphere * Mathf.Tan(def.spread * Mathf.Deg2Rad)).normalized;

            // Kritik: ek +1 hasar
            int dmg = Mathf.RoundToInt(def.damage) + (Random.value < mods.critChance ? 1 : 0);
            float bulletSpd = def.speed * mods.bulletSpeedMul * gs.playerBulletSpeedMultiplier;

            // Standart oyuncu atisi: namlu ucundan mermi + parlama + kisa gorunmezlik. Mermi namlu yonunde
            // (burun) cikar, nisan noktasina ease-in ile kivrilir: namludan acili cikis goze batmasin.
            int pierce = mods.pierce + def.pierce;
            if (def.pellets > 1)
            {
                // Saçma: ayni anda birden cok pelet, her biri rastgele sapar; menzilleri kisadir
                float pelletLife = def.range / Mathf.Max(bulletSpd, 1f);
                for (int p = 0; p < def.pellets; p++)
                {
                    Vector3 pd = (dir + Random.insideUnitSphere * Mathf.Tan(def.spread * Mathf.Deg2Rad)).normalized;
                    Bullet pb = PlayerShot.Fire(bulletPrefab, p == 0 ? muzzlePoint : null, muzzle, pd, bulletSpd * Random.Range(0.9f, 1.05f), dmg, t, pierce);
                    if (pb != null) pb.lifetime = pelletLife;
                }
            }
            else
            {
                Vector3 aimPoint = muzzle + dir * aimDist;
                Bullet shot = PlayerShot.FireEased(bulletPrefab, muzzlePoint, muzzle, t.forward, aimPoint, bulletSpd, dmg, t, pierce);
                if (shot != null)
                {
                    shot.splash = def.splash;
                    if (def.splash > 0f) shot.lifetime = def.range / Mathf.Max(bulletSpd, 1f);
                }
            }

            // Guc 2+: iki yan mermi; guc 4: daha genis ikinci cift. Yan mermiler her iki atista bir
            // cikar (DPS patlamasin diye).
            if ((shotCount[i] & 1) == 0)
            {
                if (power >= 2 && def.pellets <= 1) SideBullets(muzzle, dir, t, bulletSpd, 3f, pierce);
                if (power >= 4 && def.pellets <= 1) SideBullets(muzzle, dir, t, bulletSpd, 6.5f, pierce);
            }

            if (!played)
            {
                audioSource.PlayOneShot(def.pellets > 1 ? Sfx.Shotgun : (def.splash > 0f ? Sfx.Plasma : (def.pierce >= 3 ? Sfx.Rail : Sfx.Shot)), def.pellets > 1 || def.pierce >= 3 ? 0.35f : 0.2f);
                played = true;
            }
        }
    }
}
