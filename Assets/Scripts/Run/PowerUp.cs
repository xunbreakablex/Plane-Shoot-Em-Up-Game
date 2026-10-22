using System.Collections.Generic;
using UnityEngine;

// Isaretli dusmanlar vurulunca dusen guclendirme: parlar, uzaktan gorunur, yakininda ucaga dogru akar.
// Ucakla yaklasinca toplanir; toplanmazsa Lifetime sonunda kaybolur.
public class PowerUp : MonoBehaviour
{
    public static readonly List<PowerUp> All = new List<PowerUp>();

    // Omur, mıknatıs menzili ve toplama yaricapi GameSettings'ten (guclendirmeyi yakalamak zordu)
    private static float Lifetime => GameSettings.Current.pickupLifetime;
    private static float BaseMagnet => GameSettings.Current.pickupMagnetRange;
    private static float CollectRadius => GameSettings.Current.pickupCollectRadius;
    private const float RushMagnet = 28f;   // boss fazinda odule oyuncu gider (eskiden 150 m'den cekiliyordu)

    public Transform core;
    public Transform glow;
    public Renderer coreRenderer;
    public Renderer glowRenderer;
    public Material[] kindMaterials;

    public static readonly Color[] KindColors =
    {
        new Color(1f, 0.7f, 0.15f),     // P - guc
        new Color(0.35f, 0.75f, 1f),    // S - kalkan
        new Color(1f, 0.4f, 0.35f),     // M - fuze
        new Color(0.8f, 0.45f, 1f),     // B - bomba
        new Color(0.4f, 1f, 0.5f),      // + - onarim
        new Color(0.95f, 1f, 0.25f),    // O - asiri yuk (hizli ates)
        new Color(1f, 0.85f, 0.3f),     // $ - hurda
        new Color(0.35f, 0.95f, 0.95f)  // W - yardimci IHA
    };

    public static readonly string[] Letters = { "P", "S", "M", "B", "+", "O", "$", "W" };
    public static readonly string[] Names = { "GÜÇ", "KALKAN", "FÜZE", "BOMBA", "ONARIM", "AŞIRI YÜK", "HURDA", "İHA" };

    public PickupKind Kind { get; private set; }
    public float Born { get; private set; }

    private Vector3 drift;
    private float seed;
    private bool collected;
    private bool frameDrop;          // boss fazi: cercevedeki hedef noktaya suzulur ve orada durur
    private Vector2 frameTarget;     // x saga, y yukari (CombatManager.RushOrigin'e gore, oyuncunun derinliginde)

    /// <summary>Boss fazi: odul oyuncunun derinligine, verilen cerceve noktasina suzulur ve orada bekler.</summary>
    public void FlyToFrame(Vector2 target)
    {
        frameDrop = true;
        frameTarget = target;
        drift = Vector3.zero;
    }

    private static MaterialPropertyBlock block;
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    public static PowerUp Spawn(PowerUp prefab, Vector3 position, PickupKind kind, Vector3 scatter)
    {
        if (prefab == null) return null;

        PowerUp p = Instantiate(prefab, position, Quaternion.identity);
        p.Setup(kind, scatter);
        return p;
    }

    private void Setup(PickupKind kind, Vector3 scatter)
    {
        Kind = kind;
        Born = Time.time;
        seed = Random.value * 10f;
        drift = scatter;

        if (core != null) core.localScale = Vector3.one * 2.2f;

        int i = (int)kind;
        if (coreRenderer != null && kindMaterials != null && i < kindMaterials.Length && kindMaterials[i] != null)
            coreRenderer.sharedMaterial = kindMaterials[i];
        else if (coreRenderer != null)
        {
            // Yeni turlerin ozel malzemesi yok: cekirdek rengi dogrudan boyanir
            if (block == null) block = new MaterialPropertyBlock();
            coreRenderer.GetPropertyBlock(block);
            block.SetColor(ColorId, KindColors[i]);
            coreRenderer.SetPropertyBlock(block);
        }

        if (glowRenderer != null)
        {
            if (block == null) block = new MaterialPropertyBlock();
            Color c = KindColors[i];
            c.a = 0.85f;
            glowRenderer.GetPropertyBlock(block);
            block.SetColor(ColorId, c);
            glowRenderer.SetPropertyBlock(block);
        }
    }

    private void OnEnable() { All.Add(this); }
    private void OnDisable() { All.Remove(this); }

    private void Update()
    {
        if (GameState.Paused || collected) return;

        float dt = Time.deltaTime;
        float age = Time.time - Born;
        if (age > Lifetime)
        {
            Destroy(gameObject);
            return;
        }

        CombatManager mgr = CombatManager.Instance;
        Transform player = mgr != null ? mgr.PlayerTransform : null;
        PlaneController plane = mgr != null && mgr.flight != null ? mgr.flight.Current : null;
        bool alive = player != null && plane != null && !plane.IsCrashed;

        float distToCam = 100f;
        if (mgr != null && mgr.cam != null)
        {
            distToCam = Vector3.Distance(mgr.cam.transform.position, transform.position);
            if (glow != null) glow.rotation = mgr.cam.transform.rotation;
        }

        if (frameDrop && (mgr == null || !mgr.RushActive)) frameDrop = false;   // boss bitti: normal davranis

        if (alive)
        {
            Vector3 to = player.position - transform.position;
            float d = to.magnitude;

            if (d < CollectRadius)
            {
                Collect(mgr);
                return;
            }

            float range = (mgr.RushActive ? RushMagnet : BaseMagnet) * RunUpgrades.Mods.magnetMul;
            if (d < range)
            {
                frameDrop = false;
                // Yaklastikca hizlanir; ucagin hizini da asar ki yakalayabilsin (hizli ucaklar icin hizdan fazla)
                float k = 1f - d / range;
                float speed = Mathf.Lerp(70f, 190f, k) + mgr.PlayerSpeed * 1.15f;
                transform.position += to / Mathf.Max(d, 0.01f) * Mathf.Min(speed * dt, d);
                drift = Vector3.zero;
            }
        }

        if (frameDrop)
        {
            // Uzuvdan kopup oyuncunun derinligine suzulur (once hizli, yaklastikca yavas), sonra cercevede asili kalir
            Vector3 right = Vector3.Cross(Vector3.up, mgr.RushHeading).normalized;
            Vector3 goal = mgr.RushOrigin + right * frameTarget.x + Vector3.up * frameTarget.y;
            Vector3 toGoal = goal - transform.position;
            float gd = toGoal.magnitude;
            float step = Mathf.Max(60f, gd * 2.2f) * dt;
            transform.position = gd <= step ? goal : transform.position + toGoal / gd * step;
        }
        else
        {
            transform.position += (drift + CombatManager.FrameVelocityAt(transform.position)) * dt;   // boss fazinda cerceveyle tasinir
            drift = Vector3.Lerp(drift, Vector3.zero, 2.2f * dt);
        }

        // Gorsel: donus, havada asili salinim, uzakta buyuyen parlama
        if (core != null)
        {
            core.Rotate(0f, 160f * dt, 0f, Space.Self);
            core.localPosition = new Vector3(0f, Mathf.Sin(Time.time * 3f + seed) * 0.8f, 0f);
        }

        if (glow != null)
            glow.localScale = Vector3.one * 11f * Mathf.Clamp(distToCam / 110f, 0.8f, 4f);

        // Omrunun sonunda yanip soner
        bool blinkOff = age > Lifetime - 6f && Mathf.Sin(age * 22f) > 0.2f;
        if (coreRenderer != null) coreRenderer.enabled = !blinkOff;
        if (glowRenderer != null) glowRenderer.enabled = !blinkOff;
    }

    private void Collect(CombatManager mgr)
    {
        collected = true;
        if (mgr != null) mgr.OnPickup(this);
        Destroy(gameObject);
    }
}
