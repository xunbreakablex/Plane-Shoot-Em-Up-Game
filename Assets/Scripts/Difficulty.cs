using UnityEngine;

// Zorluk seviyesi (oyuncu tercihi, PlayerPrefs). Carpanlarin hepsi tek yerden ayarlanir:
// GameSettings > Zorluk (difficultyEasy / difficultyNormal / difficultyHard). Kodda Difficulty.<Carpan> okunur.
// Zor mod daha cok puan verir: skor sartli acilmalar zor modda daha kolay yakalanir.
public static class Difficulty
{
    private const string Key = "PlaneShootEmUp.Pref.Difficulty";

    public static readonly string[] Names = { "KOLAY", "NORMAL", "ZOR" };

    private static int level = -1;

    /// <summary>0 = kolay, 1 = normal, 2 = zor.</summary>
    public static int Level
    {
        get { if (level < 0) level = Mathf.Clamp(PlayerPrefs.GetInt(Key, 1), 0, 2); return level; }
        set { level = Mathf.Clamp(value, 0, 2); PlayerPrefs.SetInt(Key, level); }
    }

    private static DifficultyTuning T
    {
        get
        {
            GameSettings gs = GameSettings.Current;
            DifficultyTuning t = Level == 0 ? gs.difficultyEasy : (Level == 2 ? gs.difficultyHard : gs.difficultyNormal);
            return t ?? DifficultyTuning.Normal();
        }
    }

    public static string Label => Names[Level];
    public static float HitsMul => T.enemyHits;
    public static float BossHitsMul => T.bossHits;
    public static float FireCooldownMul => T.enemyFireCooldown;
    public static float ScoreMul => T.score;
    public static float AimErrorMul => T.aimError;

    public static float BossMoveSpeedMul => Mathf.Max(0.1f, T.bossMoveSpeed);
    public static float BossDamageMul => Mathf.Max(0f, T.bossDamage);
    public static float BossProjectileSpeedMul => Mathf.Max(0.1f, T.bossProjectileSpeed);
    public static float BossWarningMul => Mathf.Max(0.2f, T.bossWarning);
    public static float BossAttackIntervalMul => Mathf.Max(0.1f, T.bossAttackInterval);

    public static void Next() { Level = (Level + 1) % Names.Length; }
}

// Bir zorluk seviyesinin carpanlari (1 = Normal'in ayarlandigi his). GameSettings varliginda Inspector'dan duzenlenir.
[System.Serializable]
public class DifficultyTuning
{
    [Tooltip("Düşman uçak ve yer birimi canı")]
    [Range(0.3f, 3f)] public float enemyHits = 1f;
    [Tooltip("Boss canı")]
    [Range(0.3f, 3f)] public float bossHits = 1f;
    [Tooltip("Düşman uçak ateş aralığı (büyük = seyrek)")]
    [Range(0.3f, 3f)] public float enemyFireCooldown = 1f;
    [Tooltip("Düşman nişan hatası (küçük = isabetli). Boss nişanı Normal'e (0,75) göre oranlanır")]
    [Range(0f, 3f)] public float aimError = 0.75f;
    [Tooltip("Puan çarpanı")]
    [Range(0f, 3f)] public float score = 1f;

    [Tooltip("Boss hareket hızı: konum alma, salınım ve dalış hızı")]
    [Range(0.3f, 2f)] public float bossMoveSpeed = 1f;
    [Tooltip("Boss saldırılarının (mermi, ışın, çarpma, bölge, yumruk...) verdiği hasar. Kesirli kısım birikir: 0,7 = 10 vuruşta ~7 hasar")]
    [Range(0f, 3f)] public float bossDamage = 1f;
    [Tooltip("Boss mermisi, perde duvarı ve çark yıldızlarının hızı")]
    [Range(0.3f, 2f)] public float bossProjectileSpeed = 1f;
    [Tooltip("Uyarı / kilit süreleri (büyük = kaçmaya daha çok zaman): atış uyarısı, ışın ve dalış kilidi, yumruk, bölgeler, süpürme, fırtına, Ki dalgası, çark")]
    [Range(0.3f, 3f)] public float bossWarning = 1f;
    [Tooltip("Boss saldırıları arası süre (büyük = seyrek)")]
    [Range(0.3f, 3f)] public float bossAttackInterval = 1f;

    public static DifficultyTuning Easy()
    {
        return new DifficultyTuning
        {
            enemyHits = 0.75f, bossHits = 0.8f, enemyFireCooldown = 1.3f, aimError = 1.3f, score = 0.7f,
            bossMoveSpeed = 0.85f, bossDamage = 0.7f, bossProjectileSpeed = 0.85f, bossWarning = 1.25f, bossAttackInterval = 1.3f
        };
    }

    public static DifficultyTuning Normal()
    {
        return new DifficultyTuning();
    }

    public static DifficultyTuning Hard()
    {
        return new DifficultyTuning
        {
            enemyHits = 1.35f, bossHits = 1.25f, enemyFireCooldown = 0.8f, aimError = 0.5f, score = 1.5f,
            bossMoveSpeed = 1.15f, bossDamage = 1.3f, bossProjectileSpeed = 1.12f, bossWarning = 0.85f, bossAttackInterval = 0.8f
        };
    }
}
