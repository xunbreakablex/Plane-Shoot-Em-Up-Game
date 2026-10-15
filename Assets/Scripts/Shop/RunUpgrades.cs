using System;
using UnityEngine;

// Hesaplanmis upgrade etkileri (her biri 'yok' = notr deger).
public struct UpgradeMods
{
    public float critChance;
    public float fireRateMul;
    public float bulletSpeedMul;
    public int pierce;

    public float shieldAdd;
    public float shieldRegenMul;
    public float shieldDelayAdd;      // negatif = daha erken
    public float hpAdd;
    public float dodgeChance;

    public int missileDmgAdd;
    public int missileAmmoAdd;
    public float missileReloadMul;
    public float missileTurnMul;
    public float blastMul;
    public float turretRateMul;
    public float turretRangeMul;

    public float thrustMul;
    public float rollMul;
    public float scrapMul;
    public float streakBonus;
    public float discount;

    public float magnetMul;        // guclendirme cekim menzili carpani
    public int revives;
}

// Bu savasa ozel hurda ve upgrade durumu. Kaydedilmez; her savasta sifirlanir
// (LoadoutApplier savas sahnesi basinda Reset cagirir). Olup yeniden dogunca korunur.
public static class RunUpgrades
{
    public const int BaseKillScrap = 10;

    private static readonly int[] levels = new int[(int)UpgradeId.Count];

    public static int Scrap { get; private set; }
    public static int TotalEarned { get; private set; }
    public static UpgradeMods Mods { get; private set; } = Neutral();

    public static event Action Changed;

    public static UpgradeMods Neutral()
    {
        return new UpgradeMods
        {
            fireRateMul = 1f, bulletSpeedMul = 1f, shieldRegenMul = 1f,
            missileReloadMul = 1f, missileTurnMul = 1f, blastMul = 1f,
            turretRateMul = 1f, turretRangeMul = 1f, thrustMul = 1f, rollMul = 1f, scrapMul = 1f,
            magnetMul = 1f
        };
    }

    // Tek seferlik urunler: asama basina alim sayisi (GameSettings.consumableLimitPerStage)
    private static readonly int[] consumableBought = new int[3];
    private static int consumableStage = -1;

    private static void SyncConsumableStage()
    {
        if (consumableStage == RunState.Stage) return;
        consumableStage = RunState.Stage;
        Array.Clear(consumableBought, 0, consumableBought.Length);
    }

    /// <summary>Bu asamada bu urunden kac tane daha alinabilir (-1 = sinirsiz).</summary>
    public static int ConsumablesLeft(Consumable c)
    {
        int limit = GameSettings.Current.consumableLimitPerStage;
        if (limit <= 0) return -1;
        SyncConsumableStage();
        return Mathf.Max(0, limit - consumableBought[Mathf.Clamp((int)c, 0, consumableBought.Length - 1)]);
    }

    public static void Reset()
    {
        Scrap = 0;
        TotalEarned = 0;
        consumableStage = -1;
        Array.Clear(consumableBought, 0, consumableBought.Length);
        Array.Clear(levels, 0, levels.Length);
        Recompute();
        Changed?.Invoke();
    }

    public static int Level(UpgradeId id)
    {
        return levels[(int)id];
    }

    public static bool IsMax(UpgradeId id)
    {
        return levels[(int)id] >= UpgradeCatalog.Get(id).maxLevel;
    }

    public static int Cost(UpgradeId id)
    {
        UpgradeDef def = UpgradeCatalog.Get(id);
        float cost = def.baseCost * Mathf.Pow(UpgradeCatalog.CostGrowth, levels[(int)id]);
        return Round5(cost * (1f - Mods.discount));
    }

    public static int ConsumableCost(Consumable c)
    {
        return Round5(UpgradeCatalog.ConsumableBaseCost(c) * (1f - Mods.discount));
    }

    private static int Round5(float v)
    {
        return Mathf.Max(5, Mathf.RoundToInt(v / 5f) * 5);
    }

    public static void AddScrap(int amount)
    {
        if (amount <= 0) return;
        Scrap += amount;
        TotalEarned += amount;
        Changed?.Invoke();
    }

    public static bool CanBuy(UpgradeId id)
    {
        return !IsMax(id) && Scrap >= Cost(id);
    }

    public static bool TryBuy(UpgradeId id)
    {
        if (!CanBuy(id)) return false;
        Scrap -= Cost(id);
        levels[(int)id]++;
        Recompute();
        Changed?.Invoke();
        return true;
    }

    public static bool TryBuyConsumable(Consumable c, out string message)
    {
        int cost = ConsumableCost(c);
        if (Scrap < cost) { message = "Yetersiz hurda."; return false; }
        if (ConsumablesLeft(c) == 0)
        {
            message = $"Bu aşamada en fazla {GameSettings.Current.consumableLimitPerStage} kez alınabilir.";
            return false;
        }

        CombatManager cm = CombatManager.Instance;
        PlayerHealth ph = cm != null ? cm.CurrentPlayerHealth : null;

        switch (c)
        {
            case Consumable.Repair:
                if (ph == null || ph.Health >= ph.maxHealth) { message = "Can zaten dolu."; return false; }
                ph.FullRepair();
                break;
            case Consumable.ShieldCharge:
                if (ph == null || ph.maxShield <= 0f || ph.Shield >= ph.maxShield - 0.01f)
                { message = "Kalkan zaten dolu (veya yok)."; return false; }
                ph.RechargeShield();
                break;
            default:
                if (cm == null || cm.gun == null || !cm.gun.HasMissiles) { message = "Füze rampası yok."; return false; }
                cm.gun.RefillMissiles();
                break;
        }

        Scrap -= cost;
        SyncConsumableStage();
        consumableBought[Mathf.Clamp((int)c, 0, consumableBought.Length - 1)]++;
        Changed?.Invoke();
        message = UpgradeCatalog.ConsumableName(c) + " alındı.";
        return true;
    }

    private static void Recompute()
    {
        UpgradeMods m = Neutral();

        m.critChance = 0.08f * Level(UpgradeId.Crit);
        m.fireRateMul = 1f + 0.10f * Level(UpgradeId.FireRate);
        m.bulletSpeedMul = 1f + 0.08f * Level(UpgradeId.BulletSpeed);
        m.pierce = Level(UpgradeId.Pierce);

        m.shieldAdd = 3f * Level(UpgradeId.ShieldCap);
        m.shieldRegenMul = 1f + 0.25f * Level(UpgradeId.ShieldRegen);
        m.shieldDelayAdd = -0.5f * Level(UpgradeId.ShieldDelay);
        m.hpAdd = 3f * Level(UpgradeId.Armor);
        m.dodgeChance = 0.10f * Level(UpgradeId.Dodge);

        m.missileDmgAdd = Level(UpgradeId.MissileDmg);
        m.missileAmmoAdd = Level(UpgradeId.MissileAmmo);
        m.missileReloadMul = Mathf.Max(0.3f, 1f - 0.15f * Level(UpgradeId.MissileReload));
        m.missileTurnMul = 1f + 0.15f * Level(UpgradeId.MissileGuide);
        m.blastMul = 1f + 0.20f * Level(UpgradeId.MissileGuide);
        m.turretRateMul = 1f + 0.20f * Level(UpgradeId.TurretRate);
        m.turretRangeMul = 1f + 0.15f * Level(UpgradeId.TurretRange);

        m.thrustMul = 1f + 0.06f * Level(UpgradeId.Thrust);
        m.rollMul = 1f + 0.06f * Level(UpgradeId.Roll);
        m.scrapMul = 1f + 0.15f * Level(UpgradeId.ScrapMul);
        m.streakBonus = 0.10f * Level(UpgradeId.StreakBonus);
        m.discount = 0.08f * Level(UpgradeId.Discount);

        m.magnetMul = 1f + 0.35f * Level(UpgradeId.Magnet);
        m.revives = Level(UpgradeId.Revive);

        Mods = m;
    }

    // Hurda odulu: 10 x (dusman cani / 3) x seri carpani x hurda carpani
    public static int ComputeReward(int enemyMaxHits, int streak)
    {
        UpgradeMods m = Mods;
        float step = 0.25f * (1f + m.streakBonus);
        float cap = 2.5f * (1f + m.streakBonus);
        float streakMul = Mathf.Min(cap, 1f + Mathf.Max(0, streak - 1) * step);
        return Mathf.Max(1, Mathf.RoundToInt(BaseKillScrap * (enemyMaxHits / 3f) * streakMul * m.scrapMul));
    }
}
