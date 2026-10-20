using UnityEngine;

public enum UpgradeCategory { Weapon, Defense, Ordnance, Flight }

public enum UpgradeId
{
    // Silah
    Crit, FireRate, BulletSpeed, Pierce, HeatCap, CoolRate, CoolRelease,
    // Kalkan / govde
    ShieldCap, ShieldRegen, ShieldDelay, Armor, Dodge,
    // Fuze / taret
    MissileDmg, MissileAmmo, MissileReload, MissileGuide, TurretRate, TurretRange,
    // Ucus / ekonomi
    Thrust, Roll, ScrapMul, StreakBonus, Discount,
    // Guclendirme mıknatisi ve ekstra can
    Magnet, Revive,
    Count
}

public enum UpgradeRequires { None, Missile, Turret }

public enum Consumable { Repair, ShieldCharge, MissileRefill }

public class UpgradeDef
{
    public UpgradeId id;
    public string name;
    public string description;
    public UpgradeCategory category;
    public int maxLevel;
    public float baseCost;
    public UpgradeRequires requires;
}

// Magaza/secim kartlarinda cikan upgrade'ler. Fiyat ve etkiler tek yerden ayarlanir.
// Etki degerleri RunUpgrades.Recompute icinde (seviye basina).
public static class UpgradeCatalog
{
    public const float CostGrowth = 1.45f;

    public static readonly string[] CategoryNames = { "SİLAH", "KALKAN VE GÖVDE", "FÜZE VE TARET", "UÇUŞ VE EKONOMİ" };

    public static readonly UpgradeDef[] All = Build();

    private static UpgradeDef[] Build()
    {
        var a = new UpgradeDef[(int)UpgradeId.Count];

        Add(a, UpgradeId.Crit, "Kritik Mermi", "Mermilerin %8 ihtimalle 2 hasar vurur.", UpgradeCategory.Weapon, 5, 40);
        Add(a, UpgradeId.FireRate, "Atış Hızı", "Tüm silahlar %10 daha hızlı ateş eder.", UpgradeCategory.Weapon, 5, 35);
        Add(a, UpgradeId.BulletSpeed, "Mermi Hızı", "Mermiler %8 daha hızlı uçar (uzak hedeflerde isabet).", UpgradeCategory.Weapon, 3, 30);
        Add(a, UpgradeId.Pierce, "Delici Mermi", "Mermi düşmanı delip arkadaki +1 hedefe de geçer.", UpgradeCategory.Weapon, 2, 80);

        Add(a, UpgradeId.HeatCap, "Isı Dayanımı", "Silahlar %12 daha yavaş ısınır.", UpgradeCategory.Weapon, 5, 35);
        Add(a, UpgradeId.CoolRate, "Soğutucu", "Silahlar %25 daha hızlı soğur.", UpgradeCategory.Weapon, 5, 35);
        Add(a, UpgradeId.CoolRelease, "Hızlı Devreye Alma", "Aşırı ısınmadan sonra silah %10 daha sıcakken tekrar ateşe başlar (taban %30).", UpgradeCategory.Weapon, 3, 60);

        Add(a, UpgradeId.ShieldCap, "Kalkan Kapasitesi", "+3 kalkan (jeneratör olmasa da çalışır).", UpgradeCategory.Defense, 5, 40);
        Add(a, UpgradeId.ShieldRegen, "Kalkan Yenilenme Hızı", "Kalkan %25 daha hızlı yenilenir.", UpgradeCategory.Defense, 5, 35);
        Add(a, UpgradeId.ShieldDelay, "Kalkan Gecikmesi", "Vurulduktan sonra yenilenme 0.5 sn daha erken başlar.", UpgradeCategory.Defense, 3, 45);
        Add(a, UpgradeId.Armor, "Zırh Plakası", "+3 azami can.", UpgradeCategory.Defense, 5, 40);
        Add(a, UpgradeId.Dodge, "Savuşturma", "Gövdeye gelen hasarın %10 ihtimalle yok sayılır.", UpgradeCategory.Defense, 4, 55);

        Add(a, UpgradeId.MissileDmg, "Füze Hasarı", "Füzeler +1 hasar verir.", UpgradeCategory.Ordnance, 2, 90, UpgradeRequires.Missile);
        Add(a, UpgradeId.MissileAmmo, "Füze Mühimmatı", "Her rampaya +1 füze.", UpgradeCategory.Ordnance, 3, 55, UpgradeRequires.Missile);
        Add(a, UpgradeId.MissileReload, "Füze Yenilenme", "Füze yenilenme süresi %15 kısalır.", UpgradeCategory.Ordnance, 4, 45, UpgradeRequires.Missile);
        Add(a, UpgradeId.MissileGuide, "Füze Yönlendirme", "Füzeler %15 daha keskin döner, patlama yarıçapı %20 büyür.", UpgradeCategory.Ordnance, 3, 60, UpgradeRequires.Missile);
        Add(a, UpgradeId.TurretRate, "Taret Ateş Hızı", "Taretler %20 daha hızlı ateş eder.", UpgradeCategory.Ordnance, 4, 45, UpgradeRequires.Turret);
        Add(a, UpgradeId.TurretRange, "Taret Menzili", "Taret menzili %15 artar.", UpgradeCategory.Ordnance, 3, 40, UpgradeRequires.Turret);

        Add(a, UpgradeId.Thrust, "Motor İtkisi", "İvme ve azami hız artar (+%6 itki).", UpgradeCategory.Flight, 5, 40);
        Add(a, UpgradeId.Roll, "Yatış Çevikliği", "Yatış hızı %6 artar.", UpgradeCategory.Flight, 4, 35);
        Add(a, UpgradeId.ScrapMul, "Hurda Çarpanı", "Düşmanlardan %15 daha fazla hurda.", UpgradeCategory.Flight, 4, 50);
        Add(a, UpgradeId.StreakBonus, "Seri Bonusu", "Seri öldürme hurda çarpanı %10 daha hızlı artar.", UpgradeCategory.Flight, 3, 50);
        Add(a, UpgradeId.Discount, "İndirim", "Mağaza fiyatları %8 düşer.", UpgradeCategory.Flight, 3, 60);

        Add(a, UpgradeId.Magnet, "Mıknatıs", "Düşen güçlendirmeleri %35 daha uzaktan çeker.", UpgradeCategory.Flight, 4, 35);
        Add(a, UpgradeId.Revive, "Ekstra Can", "Düşersen bir kez tam canla geri döner ve çevrendeki düşmanları patlatırsın.", UpgradeCategory.Defense, 2, 220);

        return a;
    }

    private static void Add(UpgradeDef[] a, UpgradeId id, string name, string desc, UpgradeCategory cat,
        int max, float cost, UpgradeRequires req = UpgradeRequires.None)
    {
        a[(int)id] = new UpgradeDef
        {
            id = id, name = name, description = desc, category = cat,
            maxLevel = max, baseCost = cost, requires = req
        };
    }

    public static UpgradeDef Get(UpgradeId id)
    {
        return All[(int)id];
    }

    // Tek seferlik tuketimler
    public static string ConsumableName(Consumable c)
    {
        switch (c)
        {
            case Consumable.Repair: return "Acil Onarım";
            case Consumable.ShieldCharge: return "Kalkan Şarjı";
            default: return "Füze Doldur";
        }
    }

    public static string ConsumableDescription(Consumable c)
    {
        switch (c)
        {
            case Consumable.Repair: return "Canını anında doldurur.";
            case Consumable.ShieldCharge: return "Kalkanını anında doldurur.";
            default: return "Tüm füze rampalarını anında doldurur.";
        }
    }

    public static float ConsumableBaseCost(Consumable c)
    {
        switch (c)
        {
            case Consumable.Repair: return 60f;
            case Consumable.ShieldCharge: return 40f;
            default: return 50f;
        }
    }
}
