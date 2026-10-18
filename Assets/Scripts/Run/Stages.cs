using UnityEngine;

public enum Formation { Line, V, Column, Pincer, Ring, Scatter }

// Dusen guclendirme turleri (-1 = dusurmez)
public enum PickupKind { Power, Shield, Missile, Bomb, Repair }

public struct WaveDef
{
    public float time;              // asama basindan itibaren saniye
    public EnemyKind kind;
    public int count;
    public Formation formation;
    public int drop;                // PickupKind ya da -1; grubun son dusmani tasir ve vurulunca dusurur
}

public class StageDef
{
    public string title;
    public string subtitle;
    public WaveDef[] waves;

    // Asama ortasi boss: dalga saati bu saniyeye gelince cikar; oldurulene kadar dalgalar durur
    public BossType midBoss;
    public float midBossTime;

    // Asama sonu boss (son dalgadan sonra); son asamada Ejderha oyunu bitirir
    public BossType boss;

    // Savasa girmek icin gereken asgari ates gucu (silah + taret saniyedeki hasar, hangardaki "SILAH /sn" + "TARET /sn").
    // Altindaki donanimla bu asamaya girilemez (silahsiz / cok zayif ucak olmasin).
    public float minFirepower = 10f;
}

// Asama senaryolari. Dalgalarin zamanlari, formasyonlari ve gucelendirme tasiyicilari buradan ayarlanir.
public static class Stages
{
    public static readonly StageDef[] All =
    {
        new StageDef
        {
            title = "Sabah Devriyesi",
            subtitle = "Düşman devriyesini dağıt, jet gemisini ve mekanik köpekbalığını indir",
            midBoss = BossType.Jet, midBossTime = 62f,
            boss = BossType.Shark, minFirepower = 12f,
            waves = new[]
            {
                W(2,   EnemyKind.Swarmer, 4,  Formation.Line),
                W(10,  EnemyKind.Swarmer, 5,  Formation.V),
                W(19,  EnemyKind.Fighter, 2,  Formation.Line, PickupKind.Power),
                W(31,  EnemyKind.Swarmer, 6,  Formation.Pincer),
                W(42,  EnemyKind.Swarmer, 6,  Formation.Column),
                W(54,  EnemyKind.Fighter, 3,  Formation.V, PickupKind.Power),
                W(66,  EnemyKind.Swarmer, 8,  Formation.Ring),
                W(79,  EnemyKind.Heavy,   1,  Formation.Line, PickupKind.Shield),
                W(82,  EnemyKind.Swarmer, 4,  Formation.V),
                W(94,  EnemyKind.Fighter, 3,  Formation.Pincer, PickupKind.Bomb),
                W(106, EnemyKind.Swarmer, 10, Formation.Column, PickupKind.Missile),
            }
        },
        new StageDef
        {
            title = "Fırtına Hattı",
            subtitle = "Ağır bombardıman uçakları, zırhlı yengeç ve mekanik ahtapot",
            midBoss = BossType.Crab, midBossTime = 58f,
            boss = BossType.Octopus, minFirepower = 20f,
            waves = new[]
            {
                W(2,   EnemyKind.Swarmer, 6,  Formation.V),
                W(9,   EnemyKind.Fighter, 3,  Formation.Pincer, PickupKind.Power),
                W(20,  EnemyKind.Swarmer, 8,  Formation.Ring),
                W(30,  EnemyKind.Heavy,   2,  Formation.Line, PickupKind.Shield),
                W(38,  EnemyKind.Swarmer, 7,  Formation.Column),
                W(48,  EnemyKind.Fighter, 4,  Formation.V, PickupKind.Power),
                W(60,  EnemyKind.Swarmer, 6,  Formation.Pincer),
                W(63,  EnemyKind.Heavy,   1,  Formation.Line, PickupKind.Bomb),
                W(74,  EnemyKind.Swarmer, 10, Formation.Ring),
                W(86,  EnemyKind.Fighter, 4,  Formation.Line, PickupKind.Repair),
                W(98,  EnemyKind.Heavy,   2,  Formation.V, PickupKind.Power),
                W(100, EnemyKind.Swarmer, 8,  Formation.Column),
                W(114, EnemyKind.Fighter, 5,  Formation.Pincer, PickupKind.Missile),
            }
        },
        new StageDef
        {
            title = "Kızıl Hat",
            subtitle = "Son savunma hattı: hava zırhlısı ve Kızıl Ejderha seni bekliyor",
            midBoss = BossType.Zeppelin, midBossTime = 66f,
            boss = BossType.Dragon, minFirepower = 26f,
            waves = new[]
            {
                W(2,   EnemyKind.Fighter, 4,  Formation.V),
                W(10,  EnemyKind.Swarmer, 8,  Formation.Ring, PickupKind.Power),
                W(20,  EnemyKind.Heavy,   2,  Formation.Pincer, PickupKind.Shield),
                W(28,  EnemyKind.Swarmer, 10, Formation.Column),
                W(40,  EnemyKind.Fighter, 5,  Formation.Line, PickupKind.Power),
                W(52,  EnemyKind.Swarmer, 8,  Formation.Pincer),
                W(55,  EnemyKind.Heavy,   2,  Formation.V, PickupKind.Bomb),
                W(68,  EnemyKind.Swarmer, 12, Formation.Ring),
                W(80,  EnemyKind.Fighter, 5,  Formation.V, PickupKind.Repair),
                W(92,  EnemyKind.Heavy,   3,  Formation.Line, PickupKind.Power),
                W(95,  EnemyKind.Swarmer, 10, Formation.Column),
                W(110, EnemyKind.Fighter, 6,  Formation.Pincer, PickupKind.Missile),
                W(122, EnemyKind.Swarmer, 12, Formation.Ring, PickupKind.Bomb),
            }
        },
    };

    private static WaveDef W(float time, EnemyKind kind, int count, Formation formation, PickupKind? drop = null)
    {
        return new WaveDef
        {
            time = time, kind = kind, count = count, formation = formation,
            drop = drop.HasValue ? (int)drop.Value : -1
        };
    }
}
