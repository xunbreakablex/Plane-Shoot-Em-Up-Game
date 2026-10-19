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

// Asamanin dusman ussu (eski shoot 'em up gibi: yer hedefleri). Komuta merkezi yikilmadan asama sonu boss gelmez
// (dalgalar bittikten sonra GameSettings.baseBossFallbackSeconds kadar beklenir).
public struct BaseDef
{
    public string name;
    public bool enabled;
    public bool island;             // true: denizde ada, false: karada beton ped
    public Vector2 center;          // dunya XZ
    public float radius;            // ada/ped yaricapi
    public Color ground;            // ada rengi
    public bool hasStart;           // true: asama basinda oyuncu buraya isinlanir (deniz gecisi)
    public Vector2 startXZ;
    public float startAlt;
    public string intro;            // asama basi duyurusu
    public int flak, cannons, hangars, tanks, planes;
}

public class StageDef
{
    public BaseDef bas;

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
            bas = new BaseDef
            {
                name = "Kıyı Üssü", enabled = true, island = false, center = new Vector2(-100f, 740f), radius = 330f,
                intro = "KIYI ÜSSÜ: uçaksavarları, radarı ve komuta merkezini yok et",
                flak = 7, cannons = 2, hangars = 3, tanks = 3, planes = 4
            },
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
                W(88,  EnemyKind.Kamikaze, 2, Formation.Line),
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
            bas = new BaseDef
            {
                name = "Ada Üssü", enabled = true, island = true, center = new Vector2(-5200f, 380f), radius = 400f,
                ground = new Color(0.25f, 0.4f, 0.2f), hasStart = true, startXZ = new Vector2(-3350f, 380f), startAlt = 300f,
                intro = "DENİZİ GEÇ: ada üssüne saldır, komuta merkezini indir",
                flak = 9, cannons = 3, hangars = 3, tanks = 4, planes = 5
            },
            waves = new[]
            {
                W(2,   EnemyKind.Swarmer, 6,  Formation.V),
                W(9,   EnemyKind.Fighter, 3,  Formation.Pincer, PickupKind.Power),
                W(20,  EnemyKind.Swarmer, 8,  Formation.Ring),
                W(30,  EnemyKind.Heavy,   2,  Formation.Line, PickupKind.Shield),
                W(38,  EnemyKind.Swarmer, 7,  Formation.Column),
                W(44,  EnemyKind.Kamikaze, 3, Formation.Pincer),
                W(48,  EnemyKind.Fighter, 4,  Formation.V, PickupKind.Power),
                W(60,  EnemyKind.Swarmer, 6,  Formation.Pincer),
                W(63,  EnemyKind.Heavy,   1,  Formation.Line, PickupKind.Bomb),
                W(70,  EnemyKind.Sniper,  1,  Formation.Line, PickupKind.Power),
                W(74,  EnemyKind.Swarmer, 10, Formation.Ring),
                W(86,  EnemyKind.Fighter, 4,  Formation.Line, PickupKind.Repair),
                W(92,  EnemyKind.Kamikaze, 4, Formation.Ring),
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
            bas = new BaseDef
            {
                name = "Kızıl Kale", enabled = true, island = true, center = new Vector2(5400f, -420f), radius = 440f,
                ground = new Color(0.32f, 0.17f, 0.14f), hasStart = true, startXZ = new Vector2(3400f, -420f), startAlt = 300f,
                intro = "DENİZİ GEÇ: Kızıl Kale seni bekliyor",
                flak = 12, cannons = 4, hangars = 4, tanks = 4, planes = 6
            },
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
                W(14,  EnemyKind.Sniper,  2,  Formation.Pincer),
                W(34,  EnemyKind.Kamikaze, 4, Formation.V),
                W(62,  EnemyKind.Sniper,  2,  Formation.Line, PickupKind.Shield),
                W(76,  EnemyKind.Kamikaze, 5, Formation.Ring),
                W(100, EnemyKind.Sniper,  3,  Formation.V),
                W(116, EnemyKind.Kamikaze, 6, Formation.Pincer),
            }
        },
        new StageDef
        {
            title = "Cehennem Kapısı",
            subtitle = "Kanatlı iblisin ve ki savaşçısının topraklarına gir",
            midBoss = BossType.Demon, midBossTime = 60f,
            boss = BossType.KiWarrior, minFirepower = 32f,
            bas = new BaseDef
            {
                name = "Cehennem Kalesi", enabled = true, island = true, center = new Vector2(400f, 5600f), radius = 420f,
                ground = new Color(0.2f, 0.12f, 0.1f), hasStart = true, startXZ = new Vector2(400f, 3400f), startAlt = 300f,
                intro = "DENİZİ GEÇ: Cehennem Kalesi'ni yık, komuta merkezini indir",
                flak = 12, cannons = 4, hangars = 4, tanks = 4, planes = 6
            },
            waves = new[]
            {
                W(2,   EnemyKind.Fighter, 4,  Formation.V),
                W(10,  EnemyKind.Swarmer, 8,  Formation.Ring, PickupKind.Power),
                W(20,  EnemyKind.Heavy,   2,  Formation.Pincer, PickupKind.Shield),
                W(28,  EnemyKind.Swarmer, 10, Formation.Column),
                W(40,  EnemyKind.Fighter, 5,  Formation.Line, PickupKind.Power),
                W(52,  EnemyKind.Swarmer, 10, Formation.Pincer),
                W(55,  EnemyKind.Heavy,   2,  Formation.V, PickupKind.Bomb),
                W(68,  EnemyKind.Swarmer, 12, Formation.Ring),
                W(80,  EnemyKind.Fighter, 6,  Formation.V, PickupKind.Repair),
                W(92,  EnemyKind.Heavy,   3,  Formation.Line, PickupKind.Power),
                W(95,  EnemyKind.Swarmer, 12, Formation.Column),
                W(110, EnemyKind.Fighter, 6,  Formation.Pincer, PickupKind.Missile),
                W(122, EnemyKind.Swarmer, 14, Formation.Ring, PickupKind.Bomb),
                W(16,  EnemyKind.Sniper,  2,  Formation.Pincer),
                W(32,  EnemyKind.Kamikaze, 5, Formation.Ring),
                W(46,  EnemyKind.Kamikaze, 4, Formation.V),
                W(64,  EnemyKind.Sniper,  3,  Formation.Line, PickupKind.Shield),
                W(78,  EnemyKind.Kamikaze, 6, Formation.Pincer),
                W(98,  EnemyKind.Sniper,  3,  Formation.V),
                W(118, EnemyKind.Kamikaze, 7, Formation.Ring),
            }
        },
        new StageDef
        {
            title = "Gökyüzü Tapınağı",
            subtitle = "Melek ve Yıldız Büyücüsü: son sınav",
            midBoss = BossType.Angel, midBossTime = 64f,
            boss = BossType.MagicalGirl, minFirepower = 38f,
            bas = new BaseDef
            {
                name = "Gökyüzü Tapınağı", enabled = true, island = true, center = new Vector2(-300f, -5600f), radius = 440f,
                ground = new Color(0.85f, 0.85f, 0.9f), hasStart = true, startXZ = new Vector2(-300f, -3400f), startAlt = 300f,
                intro = "DENİZİ GEÇ: Gökyüzü Tapınağı'nı indir",
                flak = 12, cannons = 4, hangars = 4, tanks = 4, planes = 6
            },
            waves = new[]
            {
                W(2,   EnemyKind.Swarmer, 10, Formation.V),
                W(10,  EnemyKind.Fighter, 5,  Formation.Ring, PickupKind.Power),
                W(20,  EnemyKind.Heavy,   3,  Formation.Pincer, PickupKind.Shield),
                W(28,  EnemyKind.Swarmer, 12, Formation.Column),
                W(40,  EnemyKind.Fighter, 6,  Formation.Line, PickupKind.Power),
                W(52,  EnemyKind.Swarmer, 12, Formation.Pincer),
                W(55,  EnemyKind.Heavy,   3,  Formation.V, PickupKind.Bomb),
                W(68,  EnemyKind.Swarmer, 14, Formation.Ring),
                W(80,  EnemyKind.Fighter, 7,  Formation.V, PickupKind.Repair),
                W(92,  EnemyKind.Heavy,   4,  Formation.Line, PickupKind.Power),
                W(95,  EnemyKind.Swarmer, 14, Formation.Column),
                W(110, EnemyKind.Fighter, 7,  Formation.Pincer, PickupKind.Missile),
                W(124, EnemyKind.Swarmer, 16, Formation.Ring, PickupKind.Bomb),
                W(14,  EnemyKind.Sniper,  3,  Formation.V),
                W(34,  EnemyKind.Kamikaze, 6, Formation.Ring),
                W(48,  EnemyKind.Sniper,  3,  Formation.Pincer),
                W(66,  EnemyKind.Kamikaze, 6, Formation.Pincer, PickupKind.Repair),
                W(82,  EnemyKind.Sniper,  4,  Formation.Line),
                W(100, EnemyKind.Kamikaze, 8, Formation.Ring),
                W(118, EnemyKind.Sniper,  4,  Formation.V, PickupKind.Power),
            }
        },
    };

    // Dalgalar zamana gore siralanir (yeni dalga eklerken sirayi korumak gerekmez)
    static Stages()
    {
        foreach (StageDef st in All)
        {
            WaveDef[] w = st.waves;
            for (int i = 1; i < w.Length; i++)
            {
                WaveDef x = w[i];
                int j = i - 1;
                while (j >= 0 && w[j].time > x.time) { w[j + 1] = w[j]; j--; }
                w[j + 1] = x;
            }
        }
    }

    private static WaveDef W(float time, EnemyKind kind, int count, Formation formation, PickupKind? drop = null)
    {
        return new WaveDef
        {
            time = time, kind = kind, count = count, formation = formation,
            drop = drop.HasValue ? (int)drop.Value : -1
        };
    }
}
