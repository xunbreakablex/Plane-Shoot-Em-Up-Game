using System;
using UnityEngine;

// Bir ucagin secili parcalari.
[Serializable]
public class PlaneLoadout
{
    // Bu donanimin ait oldugu ucak modeli (LoadoutStore yukleme sirasinda atar); kilit kontrolu icin
    public int model;
    public int paint;
    public int engine;
    public int wings;
    public int armor;
    public int[] weapons = { 1, 1, 0, 0 };
    public int[] shields = { 0, 0, 0 };
    public int[] turrets = { 0, 0 };
    public int[] missiles = { 1, 0 };

    public static PlaneLoadout CreateDefault()
    {
        return new PlaneLoadout();
    }

    // Eksik/bozuk kayit verisini duzeltir
    public void Sanitize()
    {
        paint = Mathf.Clamp(paint, 0, PartCatalog.Paints.Length - 1);
        engine = Mathf.Clamp(engine, 0, PartCatalog.Engines.Length - 1);
        wings = Mathf.Clamp(wings, 0, PartCatalog.Wings.Length - 1);
        armor = Mathf.Clamp(armor, 0, PartCatalog.Armors.Length - 1);
        weapons = Fix(weapons, PartCatalog.WeaponSlots, PartCatalog.Weapons.Length);
        shields = Fix(shields, PartCatalog.ShieldSlots, PartCatalog.Shields.Length);
        turrets = Fix(turrets, PartCatalog.TurretSlots, PartCatalog.Turrets.Length);
        missiles = Fix(missiles, PartCatalog.MissileSlots, PartCatalog.Missiles.Length);

        // Bu ucakta olmayan ya da henuz acilmamis slotlar bos kalir (bkz. AircraftCatalog, Unlocks)
        model = Mathf.Clamp(model, 0, AircraftCatalog.All.Length - 1);
        for (int i = 0; i < weapons.Length; i++) if (!Unlocks.IsOpen(model, SlotKind.Weapon, i)) weapons[i] = 0;
        for (int i = 0; i < shields.Length; i++) if (!Unlocks.IsOpen(model, SlotKind.Shield, i)) shields[i] = 0;
        for (int i = 0; i < turrets.Length; i++) if (!Unlocks.IsOpen(model, SlotKind.Turret, i)) turrets[i] = 0;
        for (int i = 0; i < missiles.Length; i++) if (!Unlocks.IsOpen(model, SlotKind.Missile, i)) missiles[i] = 0;

        // Kahramanlar yalniz kendi silah grubunu takar, ucaklar yalniz ucak silahlarini (yanlis grup -> grubun ilk silahi)
        int group = AircraftCatalog.Get(model).weaponGroup;
        for (int i = 0; i < weapons.Length; i++)
            if (weapons[i] > 0 && PartCatalog.Weapons[weapons[i]].group != group) weapons[i] = PartCatalog.FirstWeaponOfGroup(group);
    }

    private static int[] Fix(int[] src, int length, int optionCount)
    {
        var result = new int[length];
        for (int i = 0; i < length; i++)
            result[i] = src != null && i < src.Length ? Mathf.Clamp(src[i], 0, optionCount - 1) : 0;
        return result;
    }

    public PlaneLoadout Clone()
    {
        return JsonUtility.FromJson<PlaneLoadout>(JsonUtility.ToJson(this));
    }
}

// Hesaplanmis performans/istatistikler
public struct LoadoutStats
{
    public float weight;
    public float weightRatio;
    public float cruiseMul;
    public float maxSpeedMul;
    public float thrustMul;
    public float rollMul;
    public float maxGMul;
    public float span;
    public int blades;

    public float hp;
    public float shieldMax;
    public float shieldRegen;

    public float powerUsed;
    public float powerCap;

    public float weaponDps;
    public float turretDps;
    public int weaponCount;
    public int shieldCount;
    public int turretCount;
    public int missileLaunchers;
    public int missileRounds;

    // Agirlik etkisi dahil, varsayilan donanima gore etkin carpanlar (1 = varsayilan)
    public float EffCruise => cruiseMul * Mathf.Sqrt(weightRatio);
    public float EffMaxSpeed => maxSpeedMul / Mathf.Pow(weightRatio, 0.25f);
    public float EffThrust => thrustMul / weightRatio;
    public float EffRoll => rollMul / Mathf.Sqrt(weightRatio);
    public float EffMaxG => maxGMul / Mathf.Pow(weightRatio, 0.3f);
}

public static class LoadoutMath
{
    // Varsayilan donanimin agirligi: ucus hissi eski davranisla ayni kalsin diye referans
    // (2 makineli tufek + 1 guduml fuze rampasi)
    public static readonly float ReferenceWeight =
        PartCatalog.BaseWeight + 2f * PartCatalog.Weapons[1].weight + PartCatalog.Missiles[1].weight;

    public static LoadoutStats Compute(PlaneLoadout l)
    {
        l.Sanitize();

        PartDef engine = PartCatalog.Engines[l.engine];
        PartDef wings = PartCatalog.Wings[l.wings];
        PartDef armor = PartCatalog.Armors[l.armor];

        var s = new LoadoutStats();
        s.weight = PartCatalog.BaseWeight + engine.weight + wings.weight + armor.weight;
        s.cruiseMul = wings.cruise;
        s.maxSpeedMul = engine.maxSpeed * wings.maxSpeed;
        s.thrustMul = engine.thrust;
        s.rollMul = wings.roll;
        s.maxGMul = wings.maxG;
        s.span = wings.span;
        s.blades = engine.blades;
        s.hp = armor.hp * AircraftCatalog.Get(l.model).hpMul;   // ucaga ozgu can carpani
        s.powerCap = engine.power;

        foreach (int i in l.weapons)
        {
            if (i <= 0) continue;
            PartDef d = PartCatalog.Weapons[i];
            s.weight += d.weight;
            s.powerUsed += d.power;
            s.weaponDps += d.rate * d.ShotDamage;
            s.weaponCount++;
        }
        s.weaponDps *= AircraftCatalog.Get(l.model).fireRateMul;   // ucaga ozgu atis hizi

        foreach (int i in l.shields)
        {
            if (i <= 0) continue;
            PartDef d = PartCatalog.Shields[i];
            s.weight += d.weight;
            s.powerUsed += d.power;
            s.shieldMax += d.shield;
            s.shieldRegen += d.regen;
            s.shieldCount++;
        }
        foreach (int i in l.turrets)
        {
            if (i <= 0) continue;
            PartDef d = PartCatalog.Turrets[i];
            s.weight += d.weight;
            s.powerUsed += d.power;
            s.turretDps += d.rate * d.damage;
            s.turretCount++;
        }

        foreach (int i in l.missiles)
        {
            if (i <= 0) continue;
            PartDef d = PartCatalog.Missiles[i];
            s.weight += d.weight;
            s.powerUsed += d.power;
            s.missileLaunchers++;
            s.missileRounds += d.ammo;
        }

        s.weightRatio = Mathf.Max(0.5f, s.weight / ReferenceWeight);
        return s;
    }

    // Guc butcesi asilmadan bu secenek takilabilir mi?
    public static bool FitsPower(PlaneLoadout l, SlotKind kind, int slot, int option)
    {
        PlaneLoadout test = l.Clone();
        Set(test, kind, slot, option);
        LoadoutStats s = Compute(test);
        return s.powerUsed <= s.powerCap + 0.001f;
    }

    /// <summary>
    /// Ucagin acik yuvalarla ulasabilecegi en yuksek ates gucu (silah + taret): en guclu motor, her silah turu
    /// tum acik silah yuvalarina, guc kalirsa otomatik taret; butce asilirsa sondan silah cikarilir.
    /// Hangar kapisi bunu asmaz (Sonsuz Savas'ta artan kapi kahramanlarin ulasamayacagi degerlere cikiyordu).
    /// </summary>
    public static float MaxFirepower(int model)
    {
        AircraftDef craft = AircraftCatalog.Get(model);
        int engine = 0;
        for (int i = 1; i < PartCatalog.Engines.Length; i++)
            if (PartCatalog.Engines[i].power > PartCatalog.Engines[engine].power) engine = i;

        float best = 0f;
        for (int w = 1; w < PartCatalog.Weapons.Length; w++)
        {
            if (PartCatalog.Weapons[w].group != craft.weaponGroup) continue;
            var l = PlaneLoadout.CreateDefault();
            l.model = model;
            l.engine = engine;
            l.weapons = new int[PartCatalog.WeaponSlots];
            l.shields = new int[PartCatalog.ShieldSlots];
            l.turrets = new int[PartCatalog.TurretSlots];
            l.missiles = new int[PartCatalog.MissileSlots];
            for (int i = 0; i < l.weapons.Length; i++)
                if (Unlocks.IsOpen(model, SlotKind.Weapon, i)) l.weapons[i] = w;
            for (int i = 0; i < l.turrets.Length; i++)
            {
                if (!Unlocks.IsOpen(model, SlotKind.Turret, i)) continue;
                l.turrets[i] = 1;
                if (!Fits(l)) l.turrets[i] = 0;
            }
            for (int i = l.weapons.Length - 1; i >= 0 && !Fits(l); i--) l.weapons[i] = 0;
            LoadoutStats s = Compute(l.Clone());
            best = Mathf.Max(best, s.weaponDps + s.turretDps);
        }
        return best;
    }

    private static bool Fits(PlaneLoadout l)
    {
        LoadoutStats s = Compute(l.Clone());
        return s.powerUsed <= s.powerCap + 0.001f;
    }

    public static void Set(PlaneLoadout l, SlotKind kind, int slot, int option)
    {
        switch (kind)
        {
            case SlotKind.Engine: l.engine = option; break;
            case SlotKind.Wings: l.wings = option; break;
            case SlotKind.Armor: l.armor = option; break;
            case SlotKind.Weapon: l.weapons[slot] = option; break;
            case SlotKind.Shield: l.shields[slot] = option; break;
            case SlotKind.Turret: l.turrets[slot] = option; break;
            case SlotKind.Missile: l.missiles[slot] = option; break;
        }
    }
}

// PlayerPrefs ile kalici kayit. Her ucak modeli icin ayri donanim.
public static class LoadoutStore
{
    private const string Key = "PlaneShootEmUp.Loadouts.v1";
    public const int ModelCount = 12;  // AircraftCatalog.Count ile ayni olmali

    [Serializable]
    private class SaveData
    {
        public int selected;
        public PlaneLoadout[] items;
    }

    private static SaveData data;

    public static int SelectedModel
    {
        get { EnsureLoaded(); return data.selected; }
        set { EnsureLoaded(); data.selected = Mathf.Clamp(value, 0, ModelCount - 1); Save(); }
    }

    public static PlaneLoadout Get(int model)
    {
        EnsureLoaded();
        return data.items[Mathf.Clamp(model, 0, ModelCount - 1)];
    }

    /// <summary>Kilitler degisince (debug sifirlama) kayitli donanimlari yeniden temizler.</summary>
    public static void Resanitize()
    {
        EnsureLoaded();
        for (int i = 0; i < data.items.Length; i++)
        {
            data.items[i].model = i;
            data.items[i].Sanitize();
        }
        Save();
    }

    public static void Reset(int model)
    {
        EnsureLoaded();
        int index = Mathf.Clamp(model, 0, ModelCount - 1);
        data.items[index] = PlaneLoadout.CreateDefault();
        data.items[index].model = index;
        data.items[index].Sanitize();
        Save();
    }

    public static void Save()
    {
        EnsureLoaded();
        PlayerPrefs.SetString(Key, JsonUtility.ToJson(data));
        PlayerPrefs.Save();
    }

    private static void EnsureLoaded()
    {
        if (data != null) return;

        string json = PlayerPrefs.GetString(Key, "");
        if (!string.IsNullOrEmpty(json))
        {
            try { data = JsonUtility.FromJson<SaveData>(json); }
            catch { data = null; }
        }

        if (data == null) data = new SaveData();
        if (data.items == null || data.items.Length != ModelCount)
        {
            var fresh = new PlaneLoadout[ModelCount];
            for (int i = 0; i < ModelCount; i++)
                fresh[i] = data.items != null && i < data.items.Length && data.items[i] != null
                    ? data.items[i]
                    : PlaneLoadout.CreateDefault();
            data.items = fresh;
        }

        for (int i = 0; i < data.items.Length; i++)
        {
            data.items[i].model = i;
            data.items[i].Sanitize();
        }
        data.selected = Mathf.Clamp(data.selected, 0, ModelCount - 1);
    }
}

public enum SlotKind { Engine, Wings, Armor, Weapon, Shield, Turret, Missile }
