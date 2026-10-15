using UnityEngine;

public enum RequirementKind { None, Boss, Score, Wins }

// Bir ucagi ya da slotu acmak icin gereken sart. Kayit Unlocks'ta (boss maskesi) ve RunState'te (rekor/zafer).
public struct Requirement
{
    public RequirementKind kind;
    public BossType boss;
    public int value;

    public static Requirement None { get { return default; } }
    public static Requirement Boss(BossType b) { return new Requirement { kind = RequirementKind.Boss, boss = b }; }
    public static Requirement Score(int v) { return new Requirement { kind = RequirementKind.Score, value = v }; }
    public static Requirement Wins(int v) { return new Requirement { kind = RequirementKind.Wins, value = v }; }
}

public class SlotRule
{
    public SlotKind kind;
    public int index;
    public Requirement req;

    public SlotRule(SlotKind kind, int index, Requirement req)
    {
        this.kind = kind;
        this.index = index;
        this.req = req;
    }

    public string Label
    {
        get
        {
            switch (kind)
            {
                case SlotKind.Weapon: return $"Silah {index + 1}";
                case SlotKind.Missile: return $"Füze {index + 1}";
                case SlotKind.Shield: return $"Kalkan jeneratörü {index + 1}";
                case SlotKind.Turret: return $"Taret {index + 1}";
                default: return kind.ToString();
            }
        }
    }
}

// Bir ucak modelinin degismez tanimi: ucus ozellikleri (preset), slot sayilari, can carpani, acilma sartlari.
public class AircraftDef
{
    public string name;
    public string tagline;
    public PlaneController.PlanePreset preset;

    // Bu ucaktaki slot sayilari (PartCatalog.*Slots azami sinirdir)
    public int weapons;
    public int missiles;
    public int shields;
    public int turrets;

    // Zirh parcalarinin verdigi cana carpan (kirilgan jet < 1 < agir ucak)
    public float hpMul = 1f;

    // Ucagin kendisini acma sarti (None = baslangicta acik)
    public Requirement unlock;

    // Sartli slotlar; listede olmayan mevcut slotlar baslangicta aciktir
    public SlotRule[] rules = new SlotRule[0];

    public int SlotCount(SlotKind kind)
    {
        switch (kind)
        {
            case SlotKind.Weapon: return weapons;
            case SlotKind.Missile: return missiles;
            case SlotKind.Shield: return shields;
            case SlotKind.Turret: return turrets;
            default: return 1;
        }
    }

    public SlotRule FindRule(SlotKind kind, int index)
    {
        foreach (var r in rules)
            if (r.kind == kind && r.index == index) return r;
        return null;
    }
}

// Ucak listesi. Siralama = model indeksi (hangar sekmeleri, kayit, sahnedeki FlightTestManager.planes).
// Yeni ucak eklerken: buraya satir + PlaneBuilder dizilerine hardpoint + sahneye prefab (Tools/Create New Aircraft).
public static class AircraftCatalog
{
    public const int Count = 6;

    // Ilk uc model (WW1, WW2, planor) eski global kurallari kullanir
    private static SlotRule[] ClassicRules()
    {
        return new[]
        {
            new SlotRule(SlotKind.Missile, 0, Requirement.Boss(BossType.Jet)),
            new SlotRule(SlotKind.Shield,  0, Requirement.Boss(BossType.Shark)),
            new SlotRule(SlotKind.Weapon,  2, Requirement.Boss(BossType.Shark)),
            new SlotRule(SlotKind.Turret,  0, Requirement.Boss(BossType.Crab)),
            new SlotRule(SlotKind.Missile, 1, Requirement.Boss(BossType.Octopus)),
            new SlotRule(SlotKind.Shield,  1, Requirement.Boss(BossType.Octopus)),
            new SlotRule(SlotKind.Weapon,  3, Requirement.Boss(BossType.Zeppelin)),
            new SlotRule(SlotKind.Turret,  1, Requirement.Boss(BossType.Dragon)),
            new SlotRule(SlotKind.Shield,  2, Requirement.Boss(BossType.Dragon)),
        };
    }

    public static readonly AircraftDef[] All =
    {
        new AircraftDef
        {
            name = "Çift Kanatlı (WW1)", tagline = "Sağlam ve dengeli; yavaş ama kolay uçar.",
            preset = PlaneController.PlanePreset.Biplane,
            weapons = 4, missiles = 2, shields = 3, turrets = 2, hpMul = 1f,
            rules = ClassicRules()
        },
        new AircraftDef
        {
            name = "Avcı (WW2)", tagline = "Hızlı ve atik klasik av uçağı.",
            preset = PlaneController.PlanePreset.Fighter,
            weapons = 4, missiles = 2, shields = 3, turrets = 2, hpMul = 1f,
            rules = ClassicRules()
        },
        new AircraftDef
        {
            name = "Planör", tagline = "Çok hafif; süzülür, hantal döner.",
            preset = PlaneController.PlanePreset.Glider,
            weapons = 4, missiles = 2, shields = 3, turrets = 2, hpMul = 1f,
            rules = ClassicRules()
        },

        // ---- Yeni ucaklar: jetler ve yavru kopekbaligi
        new AircraftDef
        {
            name = "Atmaca", tagline = "Hafif av jeti: çok çevik, ama kırılgan ve az slotlu.",
            preset = PlaneController.PlanePreset.LightJet,
            weapons = 3, missiles = 1, shields = 1, turrets = 0, hpMul = 0.7f,
            unlock = Requirement.Boss(BossType.Jet),
            rules = new[]
            {
                new SlotRule(SlotKind.Weapon,  2, Requirement.Boss(BossType.Shark)),
                new SlotRule(SlotKind.Missile, 0, Requirement.Boss(BossType.Crab)),
                new SlotRule(SlotKind.Shield,  0, Requirement.Score(20000)),
            }
        },
        new AircraftDef
        {
            name = "Kartal", tagline = "Ağır av jeti: hâlâ çevik, daha çok slot ve can.",
            preset = PlaneController.PlanePreset.HeavyJet,
            weapons = 4, missiles = 2, shields = 2, turrets = 1, hpMul = 1.25f,
            unlock = Requirement.Boss(BossType.Crab),
            rules = new[]
            {
                new SlotRule(SlotKind.Weapon,  2, Requirement.Boss(BossType.Octopus)),
                new SlotRule(SlotKind.Weapon,  3, Requirement.Boss(BossType.Zeppelin)),
                new SlotRule(SlotKind.Missile, 0, Requirement.Boss(BossType.Octopus)),
                new SlotRule(SlotKind.Missile, 1, Requirement.Boss(BossType.Dragon)),
                new SlotRule(SlotKind.Shield,  0, Requirement.Score(30000)),
                new SlotRule(SlotKind.Shield,  1, Requirement.Boss(BossType.Dragon)),
                new SlotRule(SlotKind.Turret,  0, Requirement.Wins(1)),
            }
        },
        new AircraftDef
        {
            name = "Yavru Köpekbalığı", tagline = "Minik mekanik balık: en çevik, en hafif, en az canlı.",
            preset = PlaneController.PlanePreset.MiniShark,
            weapons = 2, missiles = 1, shields = 1, turrets = 1, hpMul = 0.55f,
            unlock = Requirement.Boss(BossType.Shark),
            rules = new[]
            {
                new SlotRule(SlotKind.Missile, 0, Requirement.Boss(BossType.Octopus)),
                new SlotRule(SlotKind.Shield,  0, Requirement.Score(15000)),
                new SlotRule(SlotKind.Turret,  0, Requirement.Boss(BossType.Zeppelin)),
            }
        },
    };

    public static AircraftDef Get(int model)
    {
        return All[Mathf.Clamp(model, 0, All.Length - 1)];
    }
}
