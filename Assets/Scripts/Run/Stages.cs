using UnityEngine;

public enum Formation { Line, V, Column, Pincer, Ring, Scatter }

// Dusen guclendirme turleri (-1 = dusurmez)
public enum PickupKind { Power, Shield, Missile, Bomb, Repair, Overdrive, Scrap, Wingman }

public struct WaveDef
{
    public float time;              // asama basindan itibaren saniye
    public EnemyKind kind;
    public int count;
    public Formation formation;
    public int drop;                // PickupKind ya da -1; grubun son dusmani tasir ve vurulunca dusurur
    public bool ace;                // as pilot: adli, sert, buyuk odullu tek ucak (grubun ilki)
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

    // Dusman ucaklarinin gorunum temasi (EnemyThemes): davranis ayni, gorunum iblis / melek
    public EnemyTheme theme;

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
                W(3,   EnemyKind.Fighter,  1, Formation.Line),
                W(16,  EnemyKind.Swarmer,  2, Formation.V),
                W(30,  EnemyKind.Fighter,  1, Formation.Line, PickupKind.Power),
                W(42,  EnemyKind.Swarmer,  2, Formation.Pincer, PickupKind.Wingman),
                W(54,  EnemyKind.Fighter,  2, Formation.V),
                W(68,  EnemyKind.Heavy,    1, Formation.Line, PickupKind.Shield),
                W(80,  EnemyKind.Swarmer,  2, Formation.Column, PickupKind.Scrap),
                W(92,  EnemyKind.Kamikaze, 2, Formation.Line),
                W(104, EnemyKind.Fighter,  2, Formation.Pincer, PickupKind.Bomb),
                W(116, EnemyKind.Swarmer,  2, Formation.Ring, PickupKind.Overdrive),
                W(126, EnemyKind.Fighter,  1, Formation.Line, PickupKind.Missile),
                Ace(47),
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
                W(3,   EnemyKind.Swarmer,  2, Formation.V),
                W(15,  EnemyKind.Sniper,   1, Formation.Line, PickupKind.Power),
                W(28,  EnemyKind.Fighter,  2, Formation.Pincer),
                W(40,  EnemyKind.Kamikaze, 2, Formation.V),
                W(52,  EnemyKind.Heavy,    1, Formation.Line, PickupKind.Shield),
                W(64,  EnemyKind.Swarmer,  2, Formation.Ring, PickupKind.Overdrive),
                W(76,  EnemyKind.Fighter,  2, Formation.V, PickupKind.Wingman),
                W(88,  EnemyKind.Sniper,   1, Formation.Line, PickupKind.Repair),
                W(100, EnemyKind.Swarmer,  3, Formation.Column, PickupKind.Scrap),
                W(112, EnemyKind.Kamikaze, 3, Formation.Ring),
                W(122, EnemyKind.Fighter,  2, Formation.Pincer, PickupKind.Missile),
                W(134, EnemyKind.Heavy,    1, Formation.Line, PickupKind.Bomb),
                Ace(45),
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
                W(3,   EnemyKind.Fighter,  2, Formation.V),
                W(15,  EnemyKind.Sniper,   1, Formation.Line, PickupKind.Power),
                W(28,  EnemyKind.Swarmer,  3, Formation.Ring, PickupKind.Overdrive),
                W(40,  EnemyKind.Heavy,    1, Formation.Line, PickupKind.Shield),
                W(52,  EnemyKind.Kamikaze, 3, Formation.Pincer),
                W(64,  EnemyKind.Fighter,  2, Formation.Line, PickupKind.Wingman),
                W(78,  EnemyKind.Sniper,   2, Formation.Pincer),
                W(90,  EnemyKind.Swarmer,  3, Formation.Column, PickupKind.Scrap),
                W(102, EnemyKind.Heavy,    2, Formation.V, PickupKind.Bomb),
                W(114, EnemyKind.Fighter,  3, Formation.Pincer, PickupKind.Repair),
                W(126, EnemyKind.Kamikaze, 4, Formation.Ring),
                W(138, EnemyKind.Fighter,  2, Formation.V, PickupKind.Missile),
                Ace(47),
            }
        },
        new StageDef
        {
            title = "Cehennem Kapısı",
            theme = EnemyTheme.Demon,
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
                W(3,   EnemyKind.Fighter,  2, Formation.V),
                W(15,  EnemyKind.Kamikaze, 3, Formation.Ring),
                W(28,  EnemyKind.Sniper,   2, Formation.Pincer, PickupKind.Power),
                W(40,  EnemyKind.Swarmer,  3, Formation.Column, PickupKind.Scrap),
                W(52,  EnemyKind.Heavy,    2, Formation.Line, PickupKind.Shield),
                W(64,  EnemyKind.Fighter,  3, Formation.Pincer, PickupKind.Wingman),
                W(76,  EnemyKind.Kamikaze, 4, Formation.V),
                W(88,  EnemyKind.Swarmer,  3, Formation.Ring, PickupKind.Overdrive),
                W(100, EnemyKind.Sniper,   2, Formation.Line, PickupKind.Repair),
                W(112, EnemyKind.Fighter,  3, Formation.V, PickupKind.Bomb),
                W(124, EnemyKind.Heavy,    2, Formation.Pincer, PickupKind.Power),
                W(136, EnemyKind.Kamikaze, 5, Formation.Ring, PickupKind.Missile),
                Ace(46),
            }
        },
        new StageDef
        {
            title = "Gökyüzü Tapınağı",
            theme = EnemyTheme.Heaven,
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
                W(3,   EnemyKind.Swarmer,  3, Formation.V),
                W(15,  EnemyKind.Sniper,   2, Formation.Pincer, PickupKind.Power),
                W(28,  EnemyKind.Fighter,  3, Formation.Line),
                W(40,  EnemyKind.Kamikaze, 4, Formation.Ring, PickupKind.Wingman),
                W(52,  EnemyKind.Heavy,    2, Formation.V, PickupKind.Shield),
                W(64,  EnemyKind.Swarmer,  3, Formation.Column, PickupKind.Scrap),
                W(76,  EnemyKind.Sniper,   3, Formation.Line, PickupKind.Overdrive),
                W(88,  EnemyKind.Fighter,  3, Formation.Pincer, PickupKind.Repair),
                W(100, EnemyKind.Kamikaze, 5, Formation.V),
                W(112, EnemyKind.Heavy,    3, Formation.Line, PickupKind.Bomb),
                W(124, EnemyKind.Fighter,  4, Formation.Ring, PickupKind.Power),
                W(138, EnemyKind.Swarmer,  4, Formation.Pincer, PickupKind.Missile),
                Ace(47),
            }
        },
    };

    /// <summary>Asama tanimi. Kampanya asamalarindan sonrasi (Sonsuz Savas) kampanya asamalarindan turetilip guclendirilir.</summary>
    public static StageDef Get(int index)
    {
        index = Mathf.Max(0, index);
        if (RunState.Rush) return RushStage(index);
        if (index < All.Length) return All[index];
        if (!endless.TryGetValue(index, out StageDef def))
        {
            def = Generate(index);
            endless[index] = def;
        }
        return def;
    }

    // Boss Rush: dolgu dalgalar ve us yok; her asamada kampanyanin orta ve ana bossu arka arkaya gelir
    private static StageDef[] rush;

    private static StageDef RushStage(int index)
    {
        if (rush == null)
        {
            rush = new StageDef[All.Length + 1];
            for (int i = 0; i < All.Length; i++)
            {
                StageDef src = All[i];
                rush[i] = new StageDef
                {
                    title = $"Boss Rush {i + 1}",
                    subtitle = "Dolgu yok: yalnızca bosslar",
                    midBoss = src.midBoss, midBossTime = 3f,
                    boss = src.boss,
                    minFirepower = src.minFirepower,
                    waves = new[] { W(1f, EnemyKind.Fighter, 2, Formation.V) },
                    bas = new BaseDef { name = "", enabled = false }
                };
            }
            // Son tur: yalnizca Boss Rush'a ozel iki boss
            rush[All.Length] = new StageDef
            {
                title = $"Boss Rush {All.Length + 1}",
                subtitle = "Çelik Kartal ve Taş Dev",
                midBoss = BossType.Eagle, midBossTime = 3f,
                boss = BossType.Golem,
                minFirepower = All[All.Length - 1].minFirepower + 4f,
                waves = new[] { W(1f, EnemyKind.Fighter, 2, Formation.V) },
                bas = new BaseDef { name = "", enabled = false }
            };
        }
        return rush[Mathf.Clamp(index, 0, rush.Length - 1)];
    }

    private static readonly System.Collections.Generic.Dictionary<int, StageDef> endless = new System.Collections.Generic.Dictionary<int, StageDef>();

    // Sonsuz Savas: her tur kampanya asamalarini sirayla yeniden kullanir; dalgalar kalabaliklasir, bosslar karisir,
    // asgari ates gucu yukselir (hangar kapisi). k = kampanyadan sonra kacinci asama (0 tabanli).
    private static StageDef Generate(int index)
    {
        int k = index - All.Length;
        StageDef src = All[k % All.Length];
        StageDef mixed = All[(k + 2) % All.Length];
        BossType midType = k % 2 == 1 ? BossType.Eagle : mixed.midBoss;       // yeni bosslar sonsuz modda araya girer
        BossType mainType = k % 3 == 2 ? BossType.Golem : src.boss;

        float scale = 1f + 0.12f * (k + 1);
        var waves = new WaveDef[src.waves.Length];
        for (int i = 0; i < waves.Length; i++)
        {
            WaveDef w = src.waves[i];
            w.count = Mathf.Clamp(Mathf.RoundToInt(w.count * scale), 1, w.kind == EnemyKind.Heavy ? 7 : 24);
            waves[i] = w;
        }

        BaseDef bas = src.bas;
        bas.name = bas.name + " (sonsuz)";
        if (!bas.hasStart)
        {
            // Kiyi ussu normalde pistten baslanir; sonsuz modda her tur denizden yaklasilir
            bas.hasStart = true;
            bas.startXZ = bas.center + new Vector2(-2000f, 0f);
            bas.startAlt = 300f;
            bas.intro = "DENİZİ GEÇ: " + src.bas.name + " seni bekliyor";
        }

        return new StageDef
        {
            title = $"Sonsuz Savaş {k + 1}",
            theme = src.theme,
            subtitle = $"{src.title}: düşman geri çekilmiyor, daha çok ve daha güçlü",
            waves = waves,
            midBoss = midType, midBossTime = Mathf.Max(48f, src.midBossTime - 2f * k),
            boss = mainType,
            minFirepower = All[All.Length - 1].minFirepower + 3f * (k + 1),
            bas = bas
        };
    }

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

    // As pilot dalgasi: tek, adli, sert avci; garanti guc dusurur
    private static WaveDef Ace(float time)
    {
        return new WaveDef { time = time, kind = EnemyKind.Fighter, count = 1, formation = Formation.Line, drop = (int)PickupKind.Power, ace = true };
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
