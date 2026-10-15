using UnityEngine;
using UnityEngine.InputSystem;

// Mouse ile nisan, sol tik ile ates. Hangarda secilen silahlarin her biri kendi noktasindan,
// kendi atis hizi/hasariyla ates eder. Imlec bir dusmanin yakinindaysa yardimli nisan
// (hedefe kilitlenir ve onde tutma/lead uygular).
public class PlayerGun : MonoBehaviour
{
    public FlightTestManager flight;
    public Camera cam;
    public Bullet bulletPrefab;

    [Tooltip("Onde tutma hesabi icin varsayilan mermi hizi (silah takili degilse)")]
    public float bulletSpeed = 380f;
    [Tooltip("Ucak burnundan en fazla bu kadar derece sapabilir")] public float maxAimCone = 35f;
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
            float perShot = d.damage + sides * 0.5f;   // yan mermiler her iki atista bir cikar
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

        // Onde tutma icin ortalama mermi hizi
        float speedSum = 0f; int count = 0;
        foreach (int w in loadout.weapons)
            if (w > 0) { speedSum += PartCatalog.Weapons[w].speed; count++; }
        HasWeapons = count > 0;
        float leadSpeed = count > 0 ? speedSum / count : bulletSpeed;

        if (ControlSettings.AutoAim) UpdateAutoAim(plane, leadSpeed);
        else UpdateAim(plane, leadSpeed);

        // Ates: SPACE basili tut (her iki nisan modunda) ya da sol tik. SPACE yerde ayrica frendir.
        bool trigger = (Mouse.current != null && Mouse.current.leftButton.isPressed)
                       || (Keyboard.current != null && Keyboard.current.spaceKey.isPressed);
        IsFiring = trigger && !plane.IsCrashed && HasWeapons;

        if (IsFiring) FireWeapons(plane, loadout);

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
        if (kb != null && kb.fKey.wasPressedThisFrame && !plane.IsCrashed && Time.time >= nextMissile)
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

    // Otomatik nisan: imlec burnun onunde (ucagin baktigi nokta). Burun konisindeki en yakin dusman
    // hedeflenir ve onde tutma uygulanir; hedef yoksa dumduz ileri ates edilir.
    private void UpdateAutoAim(PlaneController plane, float leadSpeed)
    {
        Transform t = plane.transform;
        Vector3 bore = t.position + t.forward * defaultAimDistance;
        Vector3 sp = cam.WorldToScreenPoint(bore);
        CrosshairScreenPos = sp.z > 0f ? new Vector2(sp.x, sp.y) : new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);

        LockedEnemy = FindAutoTarget(t, LockedEnemy);

        if (LockedEnemy != null)
        {
            Vector3 muzzleCenter = t.position + t.forward * 1.0f;
            float time = Vector3.Distance(muzzleCenter, LockedEnemy.transform.position) / Mathf.Max(leadSpeed, 1f);
            AimPoint = LockedEnemy.transform.position + LockedEnemy.Velocity * time;
            return;
        }
        AimPoint = bore;
    }

    // Burun konisindeki en yakin dusman. Mevcut hedef hala gecerliyse, yenisi belirgin sekilde (%30)
    // daha yakin olmadikca degistirilmez: hedef iki dusman arasinda titremesin.
    private EnemyPlane FindAutoTarget(Transform plane, EnemyPlane current)
    {
        if (CombatManager.Instance == null) return null;

        // Koni ve menzil GameSettings'ten (Inspector'dan ayarlanir)
        float autoAimRange = GameSettings.Current.autoAimRange;
        float autoAimCone = GameSettings.Current.autoAimCone;

        EnemyPlane best = null;
        float bestDist = autoAimRange;
        float currentDist = float.MaxValue;

        foreach (var e in CombatManager.Instance.Enemies)
        {
            if (e == null || e.IsDead) continue;
            Vector3 to = e.transform.position - plane.position;
            float d = to.magnitude;
            if (d > autoAimRange || Vector3.Angle(plane.forward, to) > autoAimCone) continue;

            if (e == current) currentDist = d;
            if (d < bestDist)
            {
                bestDist = d;
                best = e;
            }
        }

        if (current != null && currentDist < float.MaxValue && best != current && bestDist > currentDist * 0.7f)
            return current;
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
            if (type <= 0 || Time.time < nextFire[i]) continue;

            PartDef def = PartCatalog.Weapons[type];
            UpgradeMods mods = RunUpgrades.Mods;

            // Guc seviyesi (P ikonlari): ates hizi, hasar ve yan mermi sayisi artar
            int power = RunState.PowerLevel;
            float rateMul = mods.fireRateMul * (1f + PowerRateStep * power);
            nextFire[i] = Time.time + 1f / Mathf.Max(def.rate * rateMul, 0.1f);
            shotCount[i]++;

            // Namlu noktasi yoksa (eski kurulum) ayni konum hesaplanir
            MuzzleFlash muzzlePoint = MuzzleFor(t, i);
            Vector3 muzzle = muzzlePoint != null
                ? PlayerShot.MuzzlePoint(muzzlePoint)
                : t.TransformPoint(PlaneBuilder.WeaponOffset(flight.CurrentIndex, i, span) + new Vector3(0f, 0f, PlayerShot.TipClearance));
            Vector3 dir = (AimPoint - muzzle).normalized;

            // Ucagin arkasina ates etmeyi engelle: burun konisi ile sinirla
            if (Vector3.Angle(t.forward, dir) > maxAimCone)
                dir = Vector3.RotateTowards(t.forward, dir, maxAimCone * Mathf.Deg2Rad, 0f).normalized;

            if (def.spread > 0f)
                dir = (dir + Random.insideUnitSphere * Mathf.Tan(def.spread * Mathf.Deg2Rad)).normalized;

            // Kritik: ek +1 hasar
            int dmg = Mathf.RoundToInt(def.damage) + (Random.value < mods.critChance ? 1 : 0);
            float bulletSpd = def.speed * mods.bulletSpeedMul;
            // Standart oyuncu atisi: namlu ucundan mermi + parlama + kisa gorunmezlik
            PlayerShot.Fire(bulletPrefab, muzzlePoint, muzzle, dir, bulletSpd, dmg, t, mods.pierce);

            // Guc 2+: iki yan mermi; guc 4: daha genis ikinci cift. Yan mermiler her iki atista bir
            // cikar (DPS patlamasin diye).
            if ((shotCount[i] & 1) == 0)
            {
                if (power >= 2) SideBullets(muzzle, dir, t, bulletSpd, 3f, mods.pierce);
                if (power >= 4) SideBullets(muzzle, dir, t, bulletSpd, 6.5f, mods.pierce);
            }

            if (!played)
            {
                audioSource.PlayOneShot(Sfx.Shot, 0.2f);
                played = true;
            }
        }
    }
}
