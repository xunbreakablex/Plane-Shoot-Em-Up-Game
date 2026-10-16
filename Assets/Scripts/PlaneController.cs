using UnityEngine;
using UnityEngine.InputSystem;

// Kuvvet tabanli ucus modeli:
//  - Hucum acisina (AoA) bagli kaldirma, stall, indüklenmis surtunme, yan kayma
//  - Kumanda "G komutu" gibi calisir: nötr = ucus yolunu tut, cek = daha fazla G
//  - Yerde tekerlek surtunmesi, frenleme, pistten kalkis/inis
// FARE semasinda havada fizik yerine oyun tipi ucus calisir (bkz. ArcadeFlight).
// Klavye: W/S yunuslama, A/D donus (Arcade) ya da yatis (Klasik), Q/E yatis (Arcade) ya da dumen (Klasik),
//         Shift/Ctrl gaz, Space fren,
//         T oto-duzeltme, M fare ile ucus, R havada basla, P pistte basla.
[RequireComponent(typeof(Rigidbody))]
public class PlaneController : MonoBehaviour
{
    public enum PlanePreset { Glider, Fighter, Biplane, LightJet, HeavyJet, MiniShark }

    /// <summary>Ucus ozellikleri paketi: ApplyPreset bunu uygular, hangar puanlari da buradan okur.</summary>
    public struct PresetData
    {
        public string name;
        public float cruise, maxSpeed, thrust, cruiseAoA, stallAoA, liftSlope, maxG;
        public float roll, yaw, pitchGain, responsiveness;
        public float inputResponse;      // kumanda girisinin tam degere ulasma hizi carpani
    }

    public static PresetData DataOf(PlanePreset preset)
    {
        switch (preset)
        {
            // Yunuslama hizi seyir hizinda ~ cekme yetkisi: G = yunuslama_hizi * seyir_hizi / 9,81.
            // Kaldirma kapasitesi (azami AoA / seyir AoA) bu G'yi karsilayacak sekilde secildi;
            // cruiseAoA kucuk = yuksek G rezervi. Burun asagi de ayni kuvvette (bkz. FixedUpdate).
            case PlanePreset.Glider:
                // ~70 derece/sn
                return new PresetData
                {
                    name = "Planor", cruise = 30f, maxSpeed = 58f, thrust = 6.5f,
                    cruiseAoA = 2.4f, stallAoA = 17f, liftSlope = 5.6f, maxG = 6f,
                    roll = 95f, yaw = 22f, pitchGain = 6f, responsiveness = 6f, inputResponse = 1.1f
                };
            case PlanePreset.Fighter:
                // ~110 derece/sn (eskiden ~28)
                return new PresetData
                {
                    name = "WW2 Avci", cruise = 65f, maxSpeed = 115f, thrust = 15f,
                    cruiseAoA = 1.2f, stallAoA = 16f, liftSlope = 4.8f, maxG = 13f,
                    roll = 240f, yaw = 30f, pitchGain = 10f, responsiveness = 13f, inputResponse = 1.25f
                };
            case PlanePreset.LightJet:
                // Atmaca: ~125 derece/sn, cok yuksek yatis ve hizli kumanda tepkisi
                return new PresetData
                {
                    name = "Atmaca", cruise = 70f, maxSpeed = 135f, thrust = 22f,
                    cruiseAoA = 1.5f, stallAoA = 26f, liftSlope = 5.5f, maxG = 16f,
                    roll = 420f, yaw = 48f, pitchGain = 11f, responsiveness = 14f, inputResponse = 1.7f
                };
            case PlanePreset.HeavyJet:
                // Kartal: ~115 derece/sn
                return new PresetData
                {
                    name = "Kartal", cruise = 74f, maxSpeed = 140f, thrust = 22f,
                    cruiseAoA = 1.5f, stallAoA = 24f, liftSlope = 5.2f, maxG = 15f,
                    roll = 330f, yaw = 40f, pitchGain = 10f, responsiveness = 13f, inputResponse = 1.5f
                };
            case PlanePreset.MiniShark:
                // ~140 derece/sn
                return new PresetData
                {
                    name = "Yavru Kopekbaligi", cruise = 55f, maxSpeed = 105f, thrust = 16f,
                    cruiseAoA = 1.9f, stallAoA = 28f, liftSlope = 5.5f, maxG = 14f,
                    roll = 470f, yaw = 55f, pitchGain = 11f, responsiveness = 15f, inputResponse = 1.9f
                };
            default:
                // WW1: ~90 derece/sn
                return new PresetData
                {
                    name = "WW1 Cift Kanat", cruise = 42f, maxSpeed = 68f, thrust = 9f,
                    cruiseAoA = 2.2f, stallAoA = 16f, liftSlope = 5.2f, maxG = 7f,
                    roll = 150f, yaw = 38f, pitchGain = 8f, responsiveness = 10f, inputResponse = 1.15f
                };
        }
    }

    [Header("Info")]
    public string displayName = "Plane";

    [Header("Performance")]
    [Tooltip("Seyir hizi (m/s)")] public float cruiseSpeed = 38f;
    [Tooltip("Tam gazda azami hiz (m/s)")] public float maxSpeed = 62f;
    [Tooltip("Tam gaz statik itki (m/s^2)")] public float maxThrust = 7f;
    [Tooltip("Seyirdeki hucum acisi (derece)")] public float cruiseAoA = 4f;
    [Tooltip("Stall acisi (derece)")] public float stallAoA = 16f;
    [Tooltip("Kaldirma egimi (CL / radyan)")] public float liftSlope = 5f;
    [Tooltip("Azami G yuku")] public float maxG = 5f;
    public float inducedDrag = 1.5f;

    [Header("Throttle")]
    [Tooltip("Gazin azami artis hizi (1/sn); ease-in ile bu hiza ulasilir")] public float throttleRiseRate = 1f;
    [Tooltip("Shift basili tutulunca artis hizinin tam hiza ulasma suresi (sn); buyuk = daha yumusak baslangic")] public float throttleEaseInTime = 1f;
    [Tooltip("Tus birakilinca gazin bosta degerine inme hizi; buyuk deger = daha hizli (ease-out)")] public float throttleReleaseRate = 1.2f;
    [Tooltip("Tus birakilinca gazin inecegi deger")] [Range(0f, 1f)] public float idleThrottle = 0f;

    [Header("Handling")]
    [Tooltip("Azami yatis hizi (derece/sn)")] public float rollRate = 140f;
    [Tooltip("Dumen hizi (derece/sn)")] public float yawRate = 35f;
    public float pitchGain = 6f;
    public float responsiveness = 7f;
    [Tooltip("Kumanda girisinin (W/S, A/D, Q/E) tam degere ulasma hizi carpani; jetlerde buyuk")]
    public float inputResponse = 1f;
    public float yawStability = 3f;
    public bool autoLevel = true;
    [Range(0f, 1f)] public float turnAssist = 1f;

    [Header("Crash")]
    public float gearCrashSpeed = 13f;
    public float bodyCrashSpeed = 6f;
    public float respawnDelay = 1.5f;

    [Header("Debug")]
    [Tooltip("Test icin: klavye yerine debugStick (x=yunuslama +yukari, y=yatis +sag, z=dumen) kullanilir")]
    public bool debugOverride;
    public Vector3 debugStick;
    [Range(-1f, 1f)] public float debugThrottle;
    [Tooltip("Test icin: Arcade donus girisi (-1 sol, +1 sag)")]
    [Range(-1f, 1f)] public float debugTurn;

    [Header("Effects")]
    public TrailRenderer[] trails;
    public float trailGForce = 3f;

    // --- HUD / disaridan okunanlar ---
    public event System.Action Crashed;
    public event System.Action Respawned;

    public float Speed => rb != null ? rb.linearVelocity.magnitude : 0f;
    public Vector3 Velocity => rb != null ? rb.linearVelocity : Vector3.zero;
    public float Throttle => throttle;
    public float StallSpeed => stallSpeed;
    public float AoADeg => aoa * Mathf.Rad2Deg;
    public float GForce => gForce;
    public float VerticalSpeed => rb != null ? rb.linearVelocity.y : 0f;
    public bool IsGrounded => groundedTimer > 0f;
    public bool IsStalling => !crashed && groundedTimer <= 0f && (Mathf.Abs(aoa) > stallAoA * Mathf.Deg2Rad || Speed < stallSpeed * 0.95f);
    public bool IsCrashed => crashed;
    public bool MouseFlight => mouseFlight;
    public Vector3 SpawnPosition => spawnPosition;
    public Quaternion SpawnRotation => spawnRotation;

    private Rigidbody rb;
    private Propeller propeller;
    private Collider[] colliders;

    private const float G = 9.81f;
    private const float AgileMax = 1.6f;        // dusuk hizda kaldirma artis siniri
    private const float DownGFraction = 0.75f;  // burun asagi azami G, yukari azami G'nin kesri
    private float liftK, dragK, stallSpeed;

    private float throttle = 0.5f;
    private float riseHold;
    private float launchSpeed;
    private float aoa, gForce;
    private Vector3 localOmega;

    private float pitchIn, rollIn, yawIn, turnIn;
    private float pitchRaw, rollRaw, yawRaw, turnRaw, throttleRaw;
    private bool brake;
    private bool mouseFlight;
    private Vector2 mouseStick;
    private Vector2 steerStick;      // FARE semasi (fizik modeli): fare hareketinden gelen sanal cubuk
    private Vector2 mouseAimPending; // FARE semasi (arcade): henuz uygulanmamis fare donusu (derece; x saga, y burun yukari)
    private Vector2 mouseAimVel;     // FARE semasi (arcade): birikimin SmoothDamp hizi
    private float visualBank;        // FARE semasi (arcade): donuse gore gorsel yatis (derece, saga pozitif)
    private bool arcadeActive;       // son fizik adiminda arcade ucus mu calisti

    private float groundedTimer;
    private bool crashed;
    [SerializeField, HideInInspector] private Vector3 spawnPosition;
    [SerializeField, HideInInspector] private Quaternion spawnRotation = Quaternion.identity;
    [SerializeField, HideInInspector] private Vector3 runwayPosition;
    [SerializeField, HideInInspector] private Quaternion runwayRotation = Quaternion.identity;
    [SerializeField, HideInInspector] private bool hasRunway;

    // ---------------------------------------------------------------- setup

    public void ApplyPreset(PlanePreset preset)
    {
        PresetData d = DataOf(preset);
        displayName = d.name;
        cruiseSpeed = d.cruise; maxSpeed = d.maxSpeed; maxThrust = d.thrust;
        cruiseAoA = d.cruiseAoA; stallAoA = d.stallAoA; liftSlope = d.liftSlope; maxG = d.maxG;
        rollRate = d.roll; yawRate = d.yaw; pitchGain = d.pitchGain; responsiveness = d.responsiveness;
        inputResponse = d.inputResponse;
    }

    /// <summary>Baska bir ucagin dogus ve pist noktalarini kopyalar (yeni eklenen ucaklar icin).</summary>
    public void CopyLocationsFrom(PlaneController other)
    {
        spawnPosition = other.spawnPosition;
        spawnRotation = other.spawnRotation;
        runwayPosition = other.runwayPosition;
        runwayRotation = other.runwayRotation;
        hasRunway = other.hasRunway;
    }

    private bool baseCaptured;
    private float baseCruise, baseMaxSpeed, baseThrust, baseRoll, baseMaxG;
    private float lastEffThrust = 1f, lastEffRoll = 1f, lastEffMaxSpeed = 1f;
    private float upgradeThrust = 1f, upgradeRoll = 1f;

    /// <summary>
    /// Magaza upgrade'leri (itki/yatis): ucus sirasinda hiz ve gaza dokunmadan carpanlari gunceller.
    /// Itki artinca azami hiz da karekokuyle artar; boylece upgrade sadece ivme degil hiz da verir.
    /// </summary>
    public void SetUpgradeMultipliers(float thrust, float roll)
    {
        upgradeThrust = Mathf.Max(0.5f, thrust);
        upgradeRoll = Mathf.Max(0.5f, roll);

        if (!baseCaptured) return;   // ApplyStats henuz calismadi; carpanlar o zaman uygulanir

        maxThrust = baseThrust * lastEffThrust * upgradeThrust;
        rollRate = baseRoll * lastEffRoll * upgradeRoll;
        maxSpeed = Mathf.Max(cruiseSpeed * 1.15f, baseMaxSpeed * lastEffMaxSpeed * Mathf.Sqrt(upgradeThrust));
        ComputeConstants();
    }

    /// <summary>Hangar donanimi: agirlik ve parca carpanlarini preset degerlerinin uzerine uygular.</summary>
    public void ApplyStats(LoadoutStats s)
    {
        if (!baseCaptured)
        {
            baseCruise = cruiseSpeed;
            baseMaxSpeed = maxSpeed;
            baseThrust = maxThrust;
            baseRoll = rollRate;
            baseMaxG = maxG;
            baseCaptured = true;
        }

        lastEffThrust = s.EffThrust;
        lastEffRoll = s.EffRoll;
        lastEffMaxSpeed = s.EffMaxSpeed;

        cruiseSpeed = baseCruise * s.EffCruise;
        maxSpeed = Mathf.Max(cruiseSpeed * 1.15f, baseMaxSpeed * s.EffMaxSpeed * Mathf.Sqrt(upgradeThrust));
        maxThrust = baseThrust * s.EffThrust * upgradeThrust;
        rollRate = baseRoll * s.EffRoll * upgradeRoll;
        maxG = Mathf.Max(3f, baseMaxG * s.EffMaxG);

        ComputeConstants();
        throttle = EquilibriumThrottle();
        launchSpeed = cruiseSpeed;
        if (rb != null) rb.linearVelocity = transform.forward * cruiseSpeed;
    }

    public void SetSpawn(Vector3 position, Quaternion rotation)
    {
        spawnPosition = position;
        spawnRotation = rotation;
    }

    public void SetRunway(Vector3 position, Quaternion rotation)
    {
        runwayPosition = position;
        runwayRotation = rotation;
        hasRunway = true;
    }

    public void InheritState(float newSpeed, float newThrottle)
    {
        launchSpeed = newSpeed;
        throttle = newThrottle;
        crashed = false;
        CancelInvoke(nameof(Respawn));
    }

    public void Respawn()
    {
        CancelInvoke(nameof(Respawn));
        crashed = false;
        localOmega = Vector3.zero;
        transform.SetPositionAndRotation(spawnPosition, spawnRotation);
        rb.position = spawnPosition;
        rb.rotation = spawnRotation;
        Physics.SyncTransforms();
        rb.linearVelocity = transform.forward * cruiseSpeed;
        rb.angularVelocity = Vector3.zero;
        throttle = EquilibriumThrottle();
        Respawned?.Invoke();
    }

    /// <summary>Otomatik yeniden dogusu iptal eder (savas bitti / ozel yeniden dogus).</summary>
    public void CancelRespawn()
    {
        CancelInvoke(nameof(Respawn));
    }

    /// <summary>Verilen noktada, havada ve seyir hiziyla yeniden dogurur (Yeniden Dogus upgrade'i).</summary>
    public void ReviveAt(Vector3 position, Quaternion rotation)
    {
        CancelInvoke(nameof(Respawn));
        crashed = false;
        localOmega = Vector3.zero;
        transform.SetPositionAndRotation(position, rotation);
        rb.position = position;
        rb.rotation = rotation;
        Physics.SyncTransforms();
        rb.linearVelocity = transform.forward * cruiseSpeed;
        rb.angularVelocity = Vector3.zero;
        throttle = EquilibriumThrottle();
        Respawned?.Invoke();
    }

    /// <summary>Ucagi yok eder (carpma, dusman ates vb.); respawnDelay sonra yeniden dogar.</summary>
    public void Crash()
    {
        if (crashed) return;
        crashed = true;
        Invoke(nameof(Respawn), respawnDelay);
        Crashed?.Invoke();
    }

    public void PlaceOnRunway()
    {
        if (!hasRunway) { Respawn(); return; }

        CancelInvoke(nameof(Respawn));
        crashed = false;
        localOmega = Vector3.zero;

        transform.SetPositionAndRotation(runwayPosition, runwayRotation);
        Physics.SyncTransforms();

        // En alt collider noktasini pistin ustune oturt
        float lowest = float.MaxValue;
        foreach (var c in colliders)
            if (c != null && c.enabled) lowest = Mathf.Min(lowest, c.bounds.min.y);
        if (lowest < float.MaxValue)
            transform.position += Vector3.up * (runwayPosition.y - lowest + 0.05f);

        // Ikinci tasima da fizige bildirilmeli, yoksa ucak pistin icinde dogup disari firlar
        rb.position = transform.position;
        rb.rotation = transform.rotation;
        Physics.SyncTransforms();

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        throttle = 0f;
        Respawned?.Invoke();
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;
        rb.linearDamping = 0f;
        rb.angularDamping = 0f;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        propeller = GetComponentInChildren<Propeller>();
        colliders = GetComponentsInChildren<Collider>(true);

        var gearMat = new PhysicsMaterial("Gear")
        {
            dynamicFriction = 0.05f, staticFriction = 0.05f, bounciness = 0f,
            frictionCombine = PhysicsMaterialCombine.Minimum
        };
        var bodyMat = new PhysicsMaterial("Body")
        {
            dynamicFriction = 0.6f, staticFriction = 0.6f, bounciness = 0f
        };
        foreach (var c in colliders)
            c.sharedMaterial = IsGearCollider(c) ? gearMat : bodyMat;

        ComputeConstants();
        throttle = EquilibriumThrottle();
        launchSpeed = cruiseSpeed;
    }

    private void OnEnable()
    {
        pitchIn = rollIn = yawIn = turnIn = 0f;
        localOmega = Vector3.zero;
        if (rb != null)
        {
            rb.linearVelocity = transform.forward * launchSpeed;
            rb.angularVelocity = Vector3.zero;
        }
    }

    private void OnDisable()
    {
        if (mouseFlight) SetMouseFlight(false);
    }

    private void ComputeConstants()
    {
        float ac = cruiseAoA * Mathf.Deg2Rad;
        liftK = G / (cruiseSpeed * cruiseSpeed * liftSlope * ac);
        dragK = maxThrust * 0.5f / (maxSpeed * maxSpeed);
        float clMax = LiftCoefficient(stallAoA * Mathf.Deg2Rad * 0.999f);
        stallSpeed = Mathf.Sqrt(G / (liftK * clMax));
    }

    private float ThrustAccel(float t, float speed)
    {
        return maxThrust * t * (1f - 0.5f * Mathf.Clamp01(speed / maxSpeed));
    }

    private float EquilibriumThrottle()
    {
        float v2 = cruiseSpeed * cruiseSpeed;
        float drag = dragK * v2 + inducedDrag * G * G / Mathf.Max(v2, 100f);
        float full = ThrustAccel(1f, cruiseSpeed);
        return Mathf.Clamp(drag / Mathf.Max(full, 0.01f), 0.2f, 1f);
    }

    private float LiftCoefficient(float a)
    {
        float s = stallAoA * Mathf.Deg2Rad;
        float abs = Mathf.Abs(a);
        if (abs <= s) return liftSlope * a;

        float linear = liftSlope * Mathf.Sign(a) * s;
        float plate = Mathf.Sin(2f * a) * 0.9f;
        float blend = Mathf.Clamp01((abs - s) / (10f * Mathf.Deg2Rad));
        return Mathf.Lerp(linear, plate, blend);
    }

    private static bool IsGearCollider(Collider c)
    {
        string n = c.name;
        return n.Contains("Wheel") || n.Contains("Skid");
    }

    // ---------------------------------------------------------------- input

    private void Update()
    {
        if (GameState.Paused) return;

        ReadInput();

        float dt = Time.deltaTime;
        float resp = Mathf.Max(0.3f, inputResponse);
        pitchIn = Mathf.MoveTowards(pitchIn, pitchRaw, 7f * resp * dt);
        rollIn = Mathf.MoveTowards(rollIn, rollRaw, 6f * resp * dt);
        yawIn = Mathf.MoveTowards(yawIn, yawRaw, 4f * resp * dt);
        turnIn = Mathf.MoveTowards(turnIn, turnRaw, 4f * resp * dt);
        UpdateThrottle(dt);

        if (propeller != null)
        {
            float target = crashed ? 0f : Mathf.Lerp(350f, 2600f, throttle);
            propeller.Rpm = Mathf.Lerp(propeller.Rpm, target, 4f * dt);
        }

        if (trails != null)
        {
            bool emit = !crashed && (Mathf.Abs(gForce) > trailGForce || Mathf.Abs(aoa) > stallAoA * Mathf.Deg2Rad * 0.8f);
            foreach (var t in trails)
                if (t != null) t.emitting = emit;
        }
    }

    // Shift basili: gaz yavasca artar. Ctrl: dogrusal azalir.
    // Hicbirine basilmiyorsa gaz ease-out ile (once hizli, sonra yavasca) bosta degerine iner.
    private void UpdateThrottle(float dt)
    {
        if (throttleRaw > 0.01f)
        {
            // Ease-in: tus basili kaldikca artis hizi k^2 ile hizlanir (yavas baslar)
            riseHold = Mathf.Min(riseHold + dt, throttleEaseInTime);
            float k = Mathf.Clamp01(riseHold / Mathf.Max(throttleEaseInTime, 0.01f));
            throttle = Mathf.Clamp01(throttle + throttleRaw * throttleRiseRate * k * k * dt);
        }
        else
        {
            riseHold = 0f;
            if (throttleRaw < -0.01f)
                throttle = Mathf.Clamp01(throttle + throttleRaw * throttleRiseRate * dt);
            else
            {
                // FARE semasinda W/S birakilinca gaz seyir hizina doner (hizlan/yavasla tuslari gibi);
                // diger semalarda rolantiye iner
                // (seyre donus hizli: bazi ucaklarin throttleReleaseRate'i 0,22 ve donus ~10 sn suruyordu)
                bool toCruise = ControlSettings.MouseSteering && GameSettings.Current.throttleReturnsToCruise;
                float rest = toCruise ? EquilibriumThrottle() : idleThrottle;
                float rate = toCruise ? Mathf.Max(throttleReleaseRate, 1.5f) : throttleReleaseRate;
                throttle = Mathf.Lerp(throttle, rest, 1f - Mathf.Exp(-rate * dt));
            }
        }
    }

    private void ReadInput()
    {
        pitchRaw = rollRaw = yawRaw = turnRaw = throttleRaw = 0f;
        brake = false;

        // Fare: fare yonlendirir (yukari-asagi + saga-sola), W/S hiz, A/D donus, Q/E yatis.
        // Arcade: A/D (sol cubuk x) otomatik yatisli donus, Q/E (omuz tuslari) yatis, W/S yunuslama.
        // Klasik: A/D (sol cubuk x) yatis, Q/E (omuz tuslari) dumen, W/S yunuslama.
        bool mouseScheme = ControlSettings.MouseSteering;
        bool arcade = ControlSettings.ArcadeSteering;

        var kb = Keyboard.current;
        if (kb != null)
        {
            // +1 = burun yukari / saga
            float wsKeys = 0f;
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed) wsKeys += 1f;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed) wsKeys -= 1f;
            if (mouseScheme)
                throttleRaw += wsKeys;                                   // W hizlan, S yavasla
            else
                pitchRaw += GameSettings.Current.wPitchesUp ? wsKeys : -wsKeys;   // varsayilan: W burun asagi

            float side = 0f, aux = 0f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) side += 1f;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) side -= 1f;
            if (kb.eKey.isPressed) aux += 1f;
            if (kb.qKey.isPressed) aux -= 1f;
            if (arcade) { turnRaw += side; rollRaw += aux; }
            else { rollRaw += side; yawRaw += aux; }

            if (kb.leftShiftKey.isPressed) throttleRaw += 1f;
            if (kb.leftCtrlKey.isPressed) throttleRaw -= 1f;
            if (kb.spaceKey.isPressed) brake = true;

            if (kb.tKey.wasPressedThisFrame) autoLevel = !autoLevel;
            if (kb.mKey.wasPressedThisFrame && !mouseScheme) SetMouseFlight(!mouseFlight);
            if (kb.rKey.wasPressedThisFrame) Respawn();
            if (kb.pKey.wasPressedThisFrame) PlaceOnRunway();
        }

        var pad = Gamepad.current;
        if (pad != null)
        {
            Vector2 stick = pad.leftStick.ReadValue();
            pitchRaw += -stick.y;
            float shoulders = (pad.rightShoulder.isPressed ? 1f : 0f) - (pad.leftShoulder.isPressed ? 1f : 0f);
            if (arcade) { turnRaw += stick.x; rollRaw += shoulders; }
            else { rollRaw += stick.x; yawRaw += shoulders; }
            throttleRaw += pad.rightTrigger.ReadValue() - pad.leftTrigger.ReadValue();
            if (pad.buttonSouth.isPressed) brake = true;
            if (pad.buttonNorth.wasPressedThisFrame) autoLevel = !autoLevel;
        }

        // FARE semasi. Sag tik basiliyken fare kamerayi cevirir, ucagi degil.
        //  Arcade (varsayilan): fare burnu dogrudan cevirir (FPS gibi); birikim ArcadeFlight'ta yumusakca uygulanir.
        //  Fizik modeli (arcade kapali ya da yerde): fare hareketi sanal bir cubugu iter, fare durunca cubuk soner.
        if (mouseScheme && Mouse.current != null)
        {
            GameSettings gs = GameSettings.Current;
            Vector2 d = Mouse.current.rightButton.isPressed || Cursor.lockState != CursorLockMode.Locked
                ? Vector2.zero
                : Mouse.current.delta.ReadValue();
            if (gs.invertMouseY) d.y = -d.y;   // varsayilan: fare yukari = burun yukari

            if (arcadeActive)
            {
                mouseAimPending += d * (MouseDegPerPixel * gs.mouseSensitivity);
                steerStick = Vector2.zero;
            }
            else
            {
                mouseAimPending = mouseAimVel = Vector2.zero;
                float k = 0.008f * gs.mouseSensitivity;
                steerStick.x = Mathf.Clamp(steerStick.x + d.x * k, -1f, 1f);
                steerStick.y = Mathf.Clamp(steerStick.y + d.y * k, -1f, 1f);
                steerStick *= Mathf.Exp(-gs.mouseSteerDecay * Time.deltaTime);
                pitchRaw += steerStick.y;
                turnRaw += steerStick.x;
            }
        }
        else
        {
            steerStick = Vector2.zero;
            mouseAimPending = mouseAimVel = Vector2.zero;
        }

        if (mouseFlight && !mouseScheme && Mouse.current != null)
        {
            Vector2 d = Mouse.current.delta.ReadValue();
            mouseStick.x = Mathf.Clamp(mouseStick.x + d.x * 0.004f, -1f, 1f);
            mouseStick.y = Mathf.Clamp(mouseStick.y + d.y * 0.004f, -1f, 1f);
            mouseStick = Vector2.MoveTowards(mouseStick, Vector2.zero, 0.6f * Time.deltaTime);
            pitchRaw += -mouseStick.y;   // fareyi one it = burun asagi (joystick gibi)
            if (arcade) turnRaw += mouseStick.x;
            else rollRaw += mouseStick.x;
        }

        if (debugOverride)
        {
            pitchRaw = debugStick.x;
            rollRaw = debugStick.y;
            yawRaw = debugStick.z;
            turnRaw = debugTurn;
            throttleRaw = debugThrottle;
        }

        pitchRaw = Mathf.Clamp(pitchRaw, -1f, 1f);
        rollRaw = Mathf.Clamp(rollRaw, -1f, 1f);
        yawRaw = Mathf.Clamp(yawRaw, -1f, 1f);
        turnRaw = Mathf.Clamp(turnRaw, -1f, 1f);
        throttleRaw = Mathf.Clamp(throttleRaw, -1f, 1f);
    }

    private void SetMouseFlight(bool on)
    {
        mouseFlight = on;
        mouseStick = Vector2.zero;
        Cursor.lockState = on ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !on;
    }

    // -------------------------------------------------------------- physics

    private void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;
        groundedTimer -= dt;

        if (crashed)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            return;
        }

        if (transform.position.y < -50f)
        {
            Respawn();
            return;
        }

        Vector3 fwd = transform.forward;
        Vector3 up = transform.up;
        Vector3 right = transform.right;
        bool grounded = groundedTimer > 0f;

        // Yer: tekerlekler yana kaymaz, yuvarlanma direnci + fren
        Vector3 vel = rb.linearVelocity;
        if (grounded)
        {
            vel -= right * Vector3.Dot(vel, right) * Mathf.Clamp01(10f * dt);
            Vector3 flatFwd = Vector3.ProjectOnPlane(fwd, Vector3.up).normalized;
            float along = Vector3.Dot(vel, flatFwd);
            float decel = 0.6f + (brake ? 8f : 0f);
            vel -= flatFwd * Mathf.Clamp(along, -decel * dt, decel * dt);
            rb.linearVelocity = vel;
        }

        float speed = vel.magnitude;

        // FARE semasi havadayken oyun tipi ucus (fizik modeli yalniz yerde / kalkista ya da ayardan kapatilinca)
        GameSettings gs = GameSettings.Current;
        bool wasArcade = arcadeActive;
        arcadeActive = ControlSettings.MouseSteering && gs.mouseArcadeFlight && !grounded && speed > stallSpeed * 0.5f;
        if (arcadeActive)
        {
            if (!wasArcade) visualBank = 0f;
            ArcadeFlight(dt, speed, gs);
            return;
        }
        visualBank = 0f;

        Vector3 velDir = speed > 0.1f ? vel / speed : fwd;
        Vector3 lv = transform.InverseTransformDirection(vel);

        aoa = speed > 1f ? Mathf.Atan2(-lv.y, lv.z) : 0f;
        float beta = speed > 1f ? Mathf.Asin(Mathf.Clamp(lv.x / speed, -1f, 1f)) : 0f;

        // Dusuk hizda kumanda yetkisi korunur (arcade): yunuslama hizi ~ kaldirma/hiz oldugundan yavaslayinca
        // ucak "agirlasirdi". Kaldirma seyir hizinin altinda en fazla AgileMax kata kadar artar.
        float agile = Mathf.Clamp(cruiseSpeed / Mathf.Max(speed, 12f), 1f, AgileMax);
        float effLiftK = liftK * agile;

        // --- Kuvvetler ---
        float cl = LiftCoefficient(aoa);
        float liftAccel = Mathf.Clamp(effLiftK * speed * speed * cl, -DownGFraction * maxG * G, maxG * G);

        Vector3 liftDir = up - velDir * Vector3.Dot(up, velDir);
        liftDir = liftDir.sqrMagnitude > 1e-4f ? liftDir.normalized : up;

        float drag = dragK * speed * speed
                     + inducedDrag * liftAccel * liftAccel / Mathf.Max(speed * speed, 100f)
                     + Mathf.Abs(Mathf.Sin(beta)) * dragK * 4f * speed * speed;

        Vector3 accel = liftDir * liftAccel
                        + fwd * ThrustAccel(throttle, speed)
                        - velDir * drag
                        + Vector3.down * G;
        rb.AddForce(accel, ForceMode.Acceleration);

        gForce = Mathf.Lerp(gForce, Vector3.Dot(liftDir * liftAccel, up) / G, 8f * dt);

        // --- Kumanda: G komutu ---
        float airAuth = Mathf.Clamp01(speed / Mathf.Max(stallSpeed, 1f));

        // Arcade donusu (A/D): ucak hedef yatisa kendiliginden yatar ve o yatista irtifayi koruyacak kadar
        // G ceker (koordineli donus). Yatis girisle orantili; tam basista ~1/cos = ArcadeTurnG G'lik donus.
        // Donus gucu ve yatis hizi GameSettings'ten (arcadeTurnGFraction, arcadeBankRate)
        float turnGMax = Mathf.Max(1.5f, maxG * gs.arcadeTurnGFraction);
        // Yatis, gereken G (1/cos) tavanin %90'inda kalacak kadar: boylece irtifa tutulabilir, ucak donerken alcalmaz
        float maxBankRad = Mathf.Acos(1f / (turnGMax * 0.9f));
        bool arcadeTurn = !grounded && Mathf.Abs(turnIn) > 0.01f && Mathf.Abs(fwd.y) < 0.85f;
        // Alcaliyorsa (hiz/G yetmiyor) yatis azalir: uzun donuste spiral dalis olmasin
        if (arcadeTurn && velDir.y < 0f) maxBankRad *= Mathf.Clamp(1f + velDir.y * 2.5f, 0.75f, 1f);

        float pitchTargetUp;
        if (grounded || speed < stallSpeed * 0.4f)
        {
            // Yerde dogrudan yunuslama hizi; burun acisi sinirli
            float pitchAngle = Mathf.Asin(Mathf.Clamp(fwd.y, -1f, 1f)) * Mathf.Rad2Deg;
            float cmd = pitchIn;
            if (grounded && pitchAngle > 14f && cmd > 0f) cmd = 0f;
            if (grounded && pitchAngle < -6f && cmd < 0f) cmd = 0f;
            pitchTargetUp = cmd * 50f * Mathf.Deg2Rad * Mathf.Lerp(0.25f, 1f, airAuth);

            // Havada ve yavassa: burun agirligi kendiliginden asagi/ufka dogru iter (kuyruk kaymasini onler)
            if (!grounded) pitchTargetUp += -Mathf.Clamp(fwd.y, -1f, 1f) * 1.6f;
        }
        else
        {
            // Notr: ucus yolunu (flight path) ve yukseklegi koru. Yatiskta 1/cos(bank) ile tutar.
            float cosGamma = Mathf.Sqrt(Mathf.Max(0f, 1f - velDir.y * velDir.y));
            float flat = Mathf.Sqrt(Mathf.Max(0.0001f, 1f - fwd.y * fwd.y));
            float cosBank = up.y / flat;
            float sign = cosBank >= 0f ? 1f : -1f;
            float bankFactor = 1f / (sign * Mathf.Max(0.35f, Mathf.Abs(cosBank)));
            float nBase = cosGamma * Mathf.Lerp(1f, bankFactor, turnAssist);

            // Statik stabilite: dalista burun kendiliginden kalkar, hiz artinca hafif cekme olur
            float dive = Mathf.Clamp01(-velDir.y - 0.25f) * 3.5f;
            float overspeed = Mathf.Clamp((speed - cruiseSpeed) / cruiseSpeed, -0.5f, 1f) * 0.4f;
            nBase += dive + overspeed;
            nBase = Mathf.Clamp(nBase, -3f, maxG * 0.8f);

            // Arcade donusu: yatis arttikca irtifayi koruyan G (1/cos yatis), en fazla turnGMax.
            // Normal yatis telafisi 0,35'e kirpili (en fazla ~2,9 G) oldugundan dik donuste yetmez.
            if (arcadeTurn && cosBank > 0f)
                {
                // Irtifa tutma: alcaliyorsa biraz daha cek (yukseliyorsa biraz birak); %6 pay AoA gecikmesi icin
                float hold = 1.06f + Mathf.Clamp(-velDir.y * 5f, -0.3f, 1.5f);
                nBase = Mathf.Max(nBase, Mathf.Min(turnGMax, hold / Mathf.Max(cosBank, 1f / turnGMax)));
            }

            // Burun asagi de yukari kadar guclu: tam itis = -DownGFraction * maxG (eskiden yalniz -1,5 G)
            float n = pitchIn >= 0f
                ? nBase + pitchIn * (maxG - nBase)
                : nBase + pitchIn * (nBase + DownGFraction * maxG);

            float s = stallAoA * Mathf.Deg2Rad;
            float aCmd = n * G / (effLiftK * Mathf.Max(speed, 8f) * Mathf.Max(speed, 8f) * liftSlope);
            aCmd = Mathf.Clamp(aCmd, -DownGFraction * s, 0.92f * s);

            // Ucus yolu donus hizi (ileri besleme) + AoA hatasi
            float aUp = Vector3.Dot(accel, up);
            float feedForward = aUp / Mathf.Max(speed, 8f);
            pitchTargetUp = feedForward + (aCmd - aoa) * pitchGain;

            // Stall: burun kendiliginden duser
            float over = Mathf.Clamp01((Mathf.Abs(aoa) - s) / (5f * Mathf.Deg2Rad));
            pitchTargetUp -= Mathf.Sign(aoa) * 0.8f * over;
        }
        pitchTargetUp = Mathf.Clamp(pitchTargetUp, -3.2f, 3.2f);

        // Yatis
        float rollAuth = Mathf.Lerp(0.3f, 1f, airAuth);
        // Elle yatis (Arcade'de Q/E) GameSettings.maxManualRollRate ile sinirli: cok hizli yatan
        // ucaklarda (Kopekbaligi 470, Atmaca 420 derece/sn) donus kontrolden cikmasin
        float manualRollRate = Mathf.Min(rollRate, GameSettings.Current.maxManualRollRate);
        float rollTargetRight = rollIn * manualRollRate * Mathf.Deg2Rad * rollAuth;
        if (autoLevel && Mathf.Abs(fwd.y) < 0.85f)
        {
            // Kumanda birakilinca kanatlar duzelir. Gecis kademeli: eskiden esik asilinca yatis hizi tek
            // karede ters yone sicrardi (hizli yatan jetlerde belirgin sarsinti). Duzeltme hizi de
            // 110 derece/sn ile sinirli, yoksa 470 derece/sn yatan ucak birakilinca sert geri doner.
            float level = Mathf.Clamp(right.y * 3f, -1f, 1f);
            float levelRate = Mathf.Min(rollRate * 0.5f, 110f);
            float levelTarget = level * levelRate * Mathf.Deg2Rad * rollAuth;
            float stickWeight = Mathf.Clamp01(Mathf.Abs(rollIn) / 0.25f);
            rollTargetRight = Mathf.Lerp(levelTarget, rollTargetRight, stickWeight);
        }

        // Arcade donusu: hedef yatisa (saga pozitif) sinirli hizla yatar. Q/E ile elle yatis basilirsa o baskin.
        if (arcadeTurn)
        {
            float bankNow = Mathf.Atan2(-right.y, up.y);
            float bankTarget = turnIn * maxBankRad;
            float err = Mathf.DeltaAngle(bankNow * Mathf.Rad2Deg, bankTarget * Mathf.Rad2Deg) * Mathf.Deg2Rad;
            float bankRate = Mathf.Min(rollRate, gs.arcadeBankRate) * Mathf.Deg2Rad;
            float assistRoll = Mathf.Clamp(err * 6f, -bankRate, bankRate) * rollAuth;
            float manualWeight = Mathf.Clamp01(Mathf.Abs(rollIn) / 0.25f);
            rollTargetRight = Mathf.Lerp(assistRoll, rollTargetRight, manualWeight);
        }

        // Dumen + yan kayma dengesi (weathervane). Yerde A/D (Arcade) burun tekerlegi gibi yonlendirir.
        float yawAuth = Mathf.Lerp(0.5f, 1f, airAuth);
        float yawTargetRight = yawIn * yawRate * Mathf.Deg2Rad * yawAuth + beta * yawStability * airAuth;
        if (grounded) yawTargetRight += turnIn * yawRate * Mathf.Deg2Rad;

        Vector3 target = new Vector3(-pitchTargetUp, yawTargetRight, -rollTargetRight);
        localOmega = Vector3.Lerp(localOmega, target, 1f - Mathf.Exp(-responsiveness * dt));
        rb.angularVelocity = transform.TransformDirection(localOmega);
    }

    // ------------------------------------------------- FARE semasi: arcade ucus
    // Fizik yerine dogrudan donus: fare burnu FPS gibi cevirir (yumusatilmis, ucagin donus hiziyla sinirli),
    // A/D ve gamepad surekli doner, Q/E yatar. Hiz gaza baglidir; stall, hucum acisi ve G siniri yoktur.
    // Ucagin yatissiz "nisan cercevesi" ile gorsel yatis ayri tutulur: fare hareketi ekranda hep ayni yone
    // gider (yatis yunuslamayi capraz yapmaz), donuste ucak yalnizca gorunuste yatar.

    private const float MouseDegPerPixel = 0.07f;   // hassasiyet 1'de fare pikseli basina burun donusu (derece)
    private const float ArcadeYawFraction = 0.85f;  // saga-sola donus hizi = yunuslama hizi x bu
    private const float ArcadePendingMax = 0.35f;   // fare birikimi en fazla bu kadar saniyelik donus (savurmada uzun sure donmesin)
    private const float ArcadeLevelGain = 4f;       // ufka donme: yatis hatasi (derece) basina derece/sn
    private const float ArcadeLevelRate = 150f;     // ufka donme hiz siniri (derece/sn)
    private const float ArcadeBankResponse = 4f;    // gorsel yatisin hedefe yaklasma hizi (1/sn); kucuk = yumusak
    private const float ArcadeGrip = 10f;           // hiz yonunun burna hizalanma hizi (1/sn); kucuk = kayar
    private const float ArcadeSpeedResponse = 1.2f; // hizin gaz hedefine yaklasma hizi (1/sn)
    private const float ArcadeGravity = 0.5f;       // tirmanista yavaslama / dalista hizlanma (yercekiminin kesri)
    private const float ArcadeMinSpeed = 0.55f;     // gaz kapaliyken hiz = seyir hizi x bu

    /// <summary>Arcade ucusta azami yunuslama hizi (derece/sn): ucagin G rezervinden ve yatis upgrade'inden.</summary>
    public float ArcadePitchRate
    {
        get
        {
            float natural = maxG * G / Mathf.Max(cruiseSpeed, 10f) * Mathf.Rad2Deg;
            return Mathf.Clamp(natural, 60f, 170f) * GameSettings.Current.mouseTurnRate * Mathf.Sqrt(upgradeRoll);
        }
    }

    private void ArcadeFlight(float dt, float speed, GameSettings gs)
    {
        float maxPitch = ArcadePitchRate;
        float maxYaw = maxPitch * ArcadeYawFraction;
        float pitchCap = maxPitch * dt, yawCap = maxYaw * dt;

        // Fare birikimi kritik sonumlu yayla (SmoothDamp) uygulanir: donus yavas baslar, hizlanir, yavasca
        // durur (eskiden ustel yumusatma ilk karede en hizli donusu veriyordu, keskin hissettiriyordu).
        // Donus hizi ucagin sinirini asmaz.
        mouseAimPending.x = Mathf.Clamp(mouseAimPending.x, -maxYaw * ArcadePendingMax, maxYaw * ArcadePendingMax);
        mouseAimPending.y = Mathf.Clamp(mouseAimPending.y, -maxPitch * ArcadePendingMax, maxPitch * ArcadePendingMax);
        float smooth = Mathf.Max(gs.mouseSmoothing, 0.01f);
        float restX = Mathf.SmoothDamp(mouseAimPending.x, 0f, ref mouseAimVel.x, smooth, maxYaw, dt);
        float restY = Mathf.SmoothDamp(mouseAimPending.y, 0f, ref mouseAimVel.y, smooth, maxPitch, dt);
        float mouseYaw = mouseAimPending.x - restX;
        float mousePitch = mouseAimPending.y - restY;
        mouseAimPending = new Vector2(restX, restY);

        // A/D ve gamepad surekli doner
        float yawStep = Mathf.Clamp(mouseYaw + turnIn * yawCap, -yawCap, yawCap);
        float pitchStep = Mathf.Clamp(mousePitch + pitchIn * pitchCap, -pitchCap, pitchCap);

        // Nisan cercevesi = gercek donus eksi gorsel yatis
        Quaternion aim = rb.rotation * Quaternion.Euler(0f, 0f, visualBank);
        Vector3 r = aim * Vector3.right, u = aim * Vector3.up, f0 = aim * Vector3.forward;
        // Saga-sola donus FPS gibi dunya dikeyi etrafinda: tirmanirken/dalarken burun acisi korunur.
        // Dikeye yaklastikca ucagin kendi ust eksenine gecer (orada dunya dikeyi etrafinda donmek yatis olurdu).
        float flat0 = Mathf.Sqrt(Mathf.Max(0f, 1f - f0.y * f0.y));
        float worldW = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.3f, 0.7f, flat0));
        Vector3 yawAxis = Vector3.Lerp(u, u.y >= 0f ? Vector3.up : Vector3.down, worldW).normalized;
        aim = Quaternion.AngleAxis(yawStep, yawAxis) * Quaternion.AngleAxis(-pitchStep, r) * aim;

        // Q/E elle yatis (tonoz)
        float manualRollRate = Mathf.Min(rollRate, gs.maxManualRollRate);
        if (Mathf.Abs(rollIn) > 0.01f)
            aim = Quaternion.AngleAxis(-rollIn * manualRollRate * dt, aim * Vector3.forward) * aim;

        // Ufka donme: dikeye yakinken ufuk belirsiz oldugundan zayiflar; Q/E basiliyken calismaz.
        // Ters ucusta cekiliyorsa calismaz (takla bozulmasin); birakilinca ucak kendiliginden duzelir.
        Vector3 f = aim * Vector3.forward;
        u = aim * Vector3.up;
        float flat = Mathf.Sqrt(Mathf.Max(0f, 1f - f.y * f.y));
        float levelW = autoLevel ? Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.15f, 0.5f, flat)) : 0f;
        levelW *= 1f - Mathf.Clamp01(Mathf.Abs(rollIn) / 0.25f);
        if (u.y < 0f) levelW *= 1f - Mathf.Clamp01(Mathf.Abs(pitchStep) / (pitchCap * 0.3f));
        if (levelW > 0f)
        {
            Vector3 levelUp = (Vector3.up - f * f.y).normalized;
            float err = Vector3.SignedAngle(u, levelUp, f);
            float maxStep = ArcadeLevelRate * dt;
            aim = Quaternion.AngleAxis(Mathf.Clamp(err * ArcadeLevelGain * dt, -maxStep, maxStep) * levelW, f) * aim;
        }

        // Gorsel yatis: saga-sola donus hiziyla orantili
        float bankTarget = Mathf.Clamp(yawStep / Mathf.Max(yawCap, 1e-5f), -1f, 1f) * gs.mouseVisualBank;
        visualBank = Mathf.Lerp(visualBank, bankTarget, 1f - Mathf.Exp(-ArcadeBankResponse * dt));

        Quaternion next = aim * Quaternion.Euler(0f, 0f, -visualBank);
        Quaternion delta = next * Quaternion.Inverse(rb.rotation);
        delta.ToAngleAxis(out float ang, out Vector3 axis);
        if (ang > 180f) ang -= 360f;
        Vector3 omega = Mathf.Abs(ang) > 1e-4f && axis.sqrMagnitude > 0.5f && !float.IsNaN(axis.x)
            ? axis * (ang * Mathf.Deg2Rad / dt)
            : Vector3.zero;
        rb.angularVelocity = omega;
        localOmega = Quaternion.Inverse(rb.rotation) * omega;

        // Hiz: gaz seyir degerindeyken seyir hizi, tam gazda azami hiz, kapaliyken seyir hizinin ArcadeMinSpeed kati
        Vector3 nose = next * Vector3.forward;
        float eq = EquilibriumThrottle();
        float minSpd = cruiseSpeed * ArcadeMinSpeed;
        float targetSpd = throttle >= eq
            ? Mathf.Lerp(cruiseSpeed, maxSpeed, (throttle - eq) / Mathf.Max(1f - eq, 0.01f))
            : Mathf.Lerp(minSpd, cruiseSpeed, throttle / Mathf.Max(eq, 0.01f));
        float s = speed + (targetSpd - speed) * (1f - Mathf.Exp(-ArcadeSpeedResponse * dt));
        s -= G * ArcadeGravity * nose.y * dt;
        s = Mathf.Clamp(s, minSpd * 0.85f, maxSpeed * 1.25f);

        Vector3 velDir = speed > 0.1f ? rb.linearVelocity / speed : nose;
        Vector3 dir = Vector3.Slerp(velDir, nose, 1f - Mathf.Exp(-ArcadeGrip * dt));
        rb.linearVelocity = dir * s;

        // HUD / kamera / iz efekti icin temsili G (gercek degil; donus sertligiyle orantili)
        float pull = Mathf.Max(Mathf.Abs(pitchStep) / Mathf.Max(pitchCap, 1e-5f), Mathf.Abs(yawStep) / Mathf.Max(yawCap, 1e-5f));
        gForce = Mathf.Lerp(gForce, 1f + Mathf.Clamp01(pull) * (maxG - 1f) * 0.6f, 8f * dt);
        aoa = Mathf.Lerp(aoa, 0f, 8f * dt);
    }

    // ------------------------------------------------------------ collision

    private void OnCollisionEnter(Collision collision)
    {
        if (crashed || PlayerHealth.God) return;

        // Dusman ucaklarla temas hasari EnemyPlane'de hesaplanir; fiziksel carpisma oldurmez
        if (collision.collider.GetComponentInParent<EnemyPlane>() != null) return;

        // Sadece temas yuzeyine dik hiz carpmadir; yatay kayma (pist uzerinde ilerleme) carpma sayilmaz
        Vector3 rel = collision.relativeVelocity;
        Vector3 normal = collision.GetContact(0).normal;
        float impact = Mathf.Abs(Vector3.Dot(rel, normal));
        float sliding = Mathf.Sqrt(Mathf.Max(0f, rel.sqrMagnitude - impact * impact));

        bool onlyGear = true;
        for (int i = 0; i < collision.contactCount; i++)
        {
            if (!IsGearCollider(collision.GetContact(i).thisCollider)) { onlyGear = false; break; }
        }

        float limit = onlyGear ? gearCrashSpeed : bodyCrashSpeed;
        bool slidingCrash = !onlyGear && sliding > 22f;
        if (impact > limit || slidingCrash)
        {
            Debug.Log($"[{displayName}] carpti: dik={impact:0.0} m/s (limit {limit:0}), kayma={sliding:0.0} parca={collision.GetContact(0).thisCollider.name} ({collision.collider.name})");
            Crash();
        }
    }

    private void OnCollisionStay(Collision collision)
    {
        // Yalniz tekerlek/kayak temasi "yerde" sayilir
        for (int i = 0; i < collision.contactCount; i++)
        {
            if (IsGearCollider(collision.GetContact(i).thisCollider))
            {
                groundedTimer = 0.15f;
                return;
            }
        }
    }
}
