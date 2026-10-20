using UnityEngine;

// Oyuncu ucaginin cani + kalkani.
// Kalkan gorunmez bir kuredir: kalkan varken dusman mermileri kurenin yuzeyinde parcalanir,
// carptiklari noktada kisa sureligine renkli bir alan belirir. Kalkan bitince mermiler govdeye ulasir.
[RequireComponent(typeof(PlaneController))]
public class PlayerHealth : MonoBehaviour, IDamageable
{
    public int maxHealth = 12;
    public float maxShield;
    public float shieldRegen;
    public float shieldRegenDelay = 3f;
    public float shieldRadius = 4.6f;
    public float invulnerableAfterRespawn = 2f;

    /// <summary>Test komutu: hasar ve carpma oldurmez (GameCheats.God).</summary>
    public static bool God;

    public int Health { get; private set; }
    public float Shield { get; private set; }
    public float LastHitTime { get; private set; } = -100f;

    public bool ShieldUp => Shield > 0.01f && plane != null && !plane.IsCrashed;
    public Vector3 ShieldCenter => transform.position + transform.forward * 0.2f;

    private PlaneController plane;
    private Renderer[] renderers;
    private float invulnerableUntil;
    private bool initialized;

    // Hangar donanimindan gelen taban degerler (upgrade'ler bunlarin uzerine biner)
    private int baseHealth = 12;
    private float baseShield;
    private float baseRegen;
    private const float BaseRegenDelay = 3f;
    private const float UpgradeOnlyRegen = 0.5f;

    public void Configure(int hp, float shield, float regen)
    {
        baseHealth = Mathf.Max(1, hp);
        baseShield = Mathf.Max(0f, shield);
        baseRegen = Mathf.Max(0f, regen);

        maxHealth = baseHealth;
        maxShield = baseShield;
        shieldRegen = baseRegen;

        if (initialized)
        {
            Health = maxHealth;
            Shield = maxShield;
        }
    }

    /// <summary>Magaza upgrade'lerini taban degerlerin uzerine uygular; artan kismi aninda ekler.</summary>
    public void ApplyUpgrades(UpgradeMods m)
    {
        int newMaxHealth = Mathf.Max(1, baseHealth + Mathf.RoundToInt(m.hpAdd));
        float newMaxShield = Mathf.Max(0f, baseShield + m.shieldAdd);

        // Kalkan jeneratoru yokken upgrade ile gelen kalkan icin makul bir yenilenme taban hizi
        float regenBase = baseRegen > 0f ? baseRegen : (newMaxShield > 0f ? UpgradeOnlyRegen : 0f);

        if (initialized)
        {
            Health = Mathf.Clamp(Health + Mathf.Max(0, newMaxHealth - maxHealth), 0, newMaxHealth);
            Shield = Mathf.Clamp(Shield + Mathf.Max(0f, newMaxShield - maxShield), 0f, newMaxShield);
        }

        maxHealth = newMaxHealth;
        maxShield = newMaxShield;
        shieldRegen = regenBase * m.shieldRegenMul;
        shieldRegenDelay = Mathf.Max(1f, BaseRegenDelay + m.shieldDelayAdd);
    }

    public void FullRepair()
    {
        Health = maxHealth;
    }

    /// <summary>Can ve kalkani dogrudan ayarlar (sinirlar icinde): donanim yeniden uygulanirken durum korunur.</summary>
    public void SetState(int health, float shield)
    {
        Health = Mathf.Clamp(health, 1, maxHealth);
        Shield = Mathf.Clamp(shield, 0f, maxShield);
        initialized = true;
    }

    public float HealthFraction => maxHealth > 0 ? (float)Health / maxHealth : 1f;
    public float ShieldFraction => maxShield > 0f ? Shield / maxShield : 1f;

    /// <summary>Kalkana verilen miktar kadar ekler (tavan: maxShield).</summary>
    public void AddShield(float amount)
    {
        Shield = Mathf.Min(maxShield, Shield + Mathf.Max(0f, amount));
    }

    public void RechargeShield()
    {
        Shield = maxShield;
    }

    /// <summary>Onarim guclendirmesi: cani verilen miktar kadar doldurur.</summary>
    public void Heal(int amount)
    {
        Health = Mathf.Clamp(Health + Mathf.Max(0, amount), 0, maxHealth);
    }

    /// <summary>Bomba sirasinda kisa sure hasar almaz (mermiler yine kalkanda parcalanir).</summary>
    public void GrantInvulnerability(float seconds)
    {
        invulnerableUntil = Mathf.Max(invulnerableUntil, Time.time + seconds);
    }

    // Savusturma: govdeye gelen hasar belirli ihtimalle yok sayilir
    private bool Dodged()
    {
        return RunUpgrades.Mods.dodgeChance > 0f && Random.value < RunUpgrades.Mods.dodgeChance;
    }

    private void Awake()
    {
        plane = GetComponent<PlaneController>();
        renderers = GetComponentsInChildren<Renderer>(true);

        // Bilesen pasif bir ucaga eklendiyse Awake ucak aktiflesince calisir; o arada SetState ile
        // ayarlanan can (hangar devami) ezilmesin
        if (!initialized)
        {
            Health = maxHealth;
            Shield = maxShield;
            initialized = true;
        }

        plane.Crashed += OnCrashed;
        plane.Respawned += OnRespawned;
    }

    private void OnDestroy()
    {
        if (plane == null) return;
        plane.Crashed -= OnCrashed;
        plane.Respawned -= OnRespawned;
    }

    private void Update()
    {
        if (GameState.Paused) return;

        // Kalkan yenilenmesi
        if (Shield < maxShield && Time.time - LastHitTime > shieldRegenDelay && !plane.IsCrashed)
            Shield = Mathf.Min(maxShield, Shield + shieldRegen * Time.deltaTime);
    }

    /// <summary>
    /// Dusman mermisi kalkan yuzeyine degdi. Kalkan yoksa false doner (mermi govdeye devam eder).
    /// </summary>
    public bool AbsorbBullet(Vector3 point, int damage)
    {
        if (!ShieldUp) return false;

        LastHitTime = Time.time;

        // Yeniden dogus korumasinda mermi yine kalkanda parcalanir ama hasar vermez
        if (Time.time >= invulnerableUntil && !God)
        {
            float remaining = damage;
            float absorbed = Mathf.Min(Shield, remaining);
            Shield -= absorbed;
            remaining -= absorbed;

            if (remaining > 0.001f && !Dodged())
            {
                Health = Mathf.Max(0, Health - Mathf.CeilToInt(remaining));
                if (CombatManager.Instance != null) CombatManager.Instance.OnPlayerHit(point, true);
                if (Health <= 0) plane.Crash();
            }
        }

        Vector3 normal = (point - ShieldCenter).normalized;
        if (CombatManager.Instance != null)
            CombatManager.Instance.SpawnShieldHit(point, normal, transform);

        return true;
    }

    public void TakeHit(int damage, Vector3 point)
    {
        TakeHit(damage, point, false);
    }

    /// <summary>ignoreShield: hasar kalkani atlayip dogrudan govdeye isler (carpma, meteor).</summary>
    public void TakeHit(int damage, Vector3 point, bool ignoreShield)
    {
        if (plane.IsCrashed || Time.time < invulnerableUntil || God) return;

        LastHitTime = Time.time;
        float remaining = damage;
        bool hullHit = false;

        if (Shield > 0f && !ignoreShield)
        {
            float absorbed = Mathf.Min(Shield, remaining);
            Shield -= absorbed;
            remaining -= absorbed;
        }

        if (remaining > 0.001f && !Dodged())
        {
            Health = Mathf.Max(0, Health - Mathf.CeilToInt(remaining));
            hullHit = true;
        }

        if (CombatManager.Instance != null)
            CombatManager.Instance.OnPlayerHit(point, hullHit);

        if (Health <= 0)
            plane.Crash();
    }

    private void OnCrashed()
    {
        if (CombatManager.Instance != null)
        {
            CombatManager.Instance.SpawnExplosion(transform.position);
            CombatManager.Instance.OnPlayerDied();
        }
        SetVisible(false);
    }

    private void OnRespawned()
    {
        Health = maxHealth;
        Shield = maxShield;
        invulnerableUntil = Time.time + invulnerableAfterRespawn;
        SetVisible(true);
    }

    private void SetVisible(bool visible)
    {
        // Donanim parcalari sonradan eklendigi icin renderer listesi her seferinde taze alinir
        renderers = GetComponentsInChildren<Renderer>(true);
        foreach (var r in renderers)
        {
            if (r == null || r is TrailRenderer) continue;
            r.enabled = visible;
        }
    }
}
