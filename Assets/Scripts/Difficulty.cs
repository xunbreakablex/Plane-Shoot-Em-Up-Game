using UnityEngine;

// Zorluk seviyesi (oyuncu tercihi, PlayerPrefs). Dusman cani, ates sikligi ve puan carpani buradan gelir.
// Zor mod daha cok puan verir: skor sartli acilmalar zor modda daha kolay yakalanir.
public static class Difficulty
{
    private const string Key = "PlaneShootEmUp.Pref.Difficulty";

    public static readonly string[] Names = { "KOLAY", "NORMAL", "ZOR" };
    private static readonly float[] HitsMuls = { 0.75f, 1f, 1.35f };
    private static readonly float[] FireCdMuls = { 1.3f, 1f, 0.8f };
    private static readonly float[] ScoreMuls = { 0.7f, 1f, 1.5f };
    private static readonly float[] BossMuls = { 0.8f, 1f, 1.25f };

    private static int level = -1;

    /// <summary>0 = kolay, 1 = normal, 2 = zor.</summary>
    public static int Level
    {
        get { if (level < 0) level = Mathf.Clamp(PlayerPrefs.GetInt(Key, 1), 0, 2); return level; }
        set { level = Mathf.Clamp(value, 0, 2); PlayerPrefs.SetInt(Key, level); }
    }

    public static string Label => Names[Level];
    public static float HitsMul => HitsMuls[Level];
    public static float BossHitsMul => BossMuls[Level];
    public static float FireCooldownMul => FireCdMuls[Level];
    public static float ScoreMul => ScoreMuls[Level];

    public static void Next() { Level = (Level + 1) % Names.Length; }
}
