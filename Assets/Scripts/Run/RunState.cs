using UnityEngine;

public enum RunPhase { Playing, BossFight, StageClear, Won, Lost }

// Bir oyunun (kampanyanin) durumu: asama, skor, guc seviyesi, bomba, sonuc.
// Kaydedilmez; her yeni oyunda NewCampaign ile sifirlanir. Asamalar arasi hangara gidilirse
// statik oldugu icin korunur ve LoadoutApplier "devam" olarak kaldigi yerden surdurur.
// Kalici olan tek sey rekor sayaclaridir.
public static class RunState
{
    public const int MaxPower = 4;
    public const int MaxBombs = 5;
    public const int StartBombs = 2;

    private const string HighKey = "PlaneShootEmUp.HighScore";
    private const string RunsKey = "PlaneShootEmUp.Runs";
    private const string WinsKey = "PlaneShootEmUp.Wins";

    public static RunPhase Phase { get; private set; }
    public static int Stage { get; private set; }            // 0 tabanli
    public static float StageTime { get; private set; }
    public static float Elapsed { get; private set; }
    public static int Score { get; private set; }
    public static int StageStartScore { get; private set; }
    public static int Kills { get; private set; }
    public static int StageKills { get; private set; }
    public static int StageSpawned { get; private set; }
    public static int StageHullHits { get; private set; }
    public static int PowerLevel { get; private set; }

    /// <summary>Boss Rush: bu oyun yalnizca boss'lardan olusur (hangarda secilir, NewCampaign'de uygulanir).</summary>
    public static bool Rush { get; private set; }
    public static bool NextRush { get; set; }

    /// <summary>Sonsuz Savas: kampanya bittikten sonra asamalar bitmeden devam eder.</summary>
    public static bool Endless { get; private set; }
    public static int LastStage => Endless ? int.MaxValue : Stages.All.Length - 1;
    public static bool IsLastStage => !Endless && Stage >= Stages.All.Length - 1;

    /// <summary>Zafer ekranindan: kampanya bitti, ayni oyuna sonsuz modda devam (asama tamam ekrani acilir).</summary>
    public static void ResumeAsEndless()
    {
        if (Phase != RunPhase.Won || Rush) return;
        Endless = true;
        NewRecord = false;
        Phase = RunPhase.StageClear;
        EndedAt = Time.unscaledTime - 10f;   // asama tamam ekrani hemen acilsin
    }

    public static float OverdriveUntil { get; private set; }
    public const float OverdriveSeconds = 12f;
    public static bool OverdriveActive => Time.time < OverdriveUntil;
    public static float OverdriveLeft => Mathf.Max(0f, OverdriveUntil - Time.time);
    public static void StartOverdrive() { OverdriveUntil = Time.time + OverdriveSeconds; }
    public static int Bombs { get; private set; }
    public static int BossKills { get; private set; }
    public static int BestCombo { get; private set; }
    public static int RevivesUsed { get; set; }
    public static float EndedAt { get; private set; }
    public static bool NewRecord { get; private set; }

    // Asama sonu hesaplanan bonuslar (sonuc ekraninda gosterilir)
    public static int ClearBonusAccuracy { get; private set; }
    public static int ClearBonusNoDamage { get; private set; }

    private static bool pending;
    private static int pendingStage;
    private static float pendingHealth = 1f;

    public static bool Active => Phase == RunPhase.Playing || Phase == RunPhase.BossFight;
    public static bool Ended => Phase == RunPhase.Won || Phase == RunPhase.Lost;
    public static bool StageIsClear => Phase == RunPhase.StageClear;

    public static int HighScore => PlayerPrefs.GetInt(HighKey, 0);
    public static int Wins => PlayerPrefs.GetInt(WinsKey, 0);
    public static int Runs => PlayerPrefs.GetInt(RunsKey, 0);

    public static bool HasPendingCampaign => pending;
    public static int PendingStage => pendingStage;

    // ------------------------------------------------------------ yasam dongusu

    public static void NewCampaign()
    {
        pending = false;
        Endless = false;
        Rush = NextRush;
        Medals.NewRun();
        Stage = 0;
        Score = 0;
        Kills = 0;
        BossKills = 0;
        BestCombo = 0;
        RevivesUsed = 0;
        PowerLevel = 0;
        Bombs = StartBombs;
        OverdriveUntil = 0f;
        Elapsed = 0f;
        EndedAt = 0f;
        NewRecord = false;

        BeginStage(0);
        PlayerPrefs.SetInt(RunsKey, Runs + 1);
    }

    public static void BeginStage(int stage)
    {
        Stage = stage;
        StageTime = 0f;
        StageKills = 0;
        StageSpawned = 0;
        StageHullHits = 0;
        StageStartScore = Score;
        ClearBonusAccuracy = 0;
        ClearBonusNoDamage = 0;
        Phase = RunPhase.Playing;
    }

    // Hangardan donunce kampanya kaldigi yerden surer. Can orani tasinir: hangar yolu "sonraki asama"
    // yolundan daha fazla onarmasin (ikisinde de ayni asama arasi onarim uygulanir).
    public static void PrepareContinue(int nextStage, float healthFraction = 1f)
    {
        pending = true;
        pendingStage = nextStage;
        pendingHealth = Mathf.Clamp01(healthFraction);
    }

    /// <summary>Hangar uzerinden devam edilirken, asamadan cikarkenki can orani.</summary>
    public static float ContinueHealthFraction => pendingHealth;

    public static void CancelPending()
    {
        pending = false;
    }

    public static bool TryContinue(out int stage)
    {
        stage = pendingStage;
        if (!pending) return false;
        pending = false;
        return true;
    }

    public static void Tick(float dt)
    {
        if (!Active) return;
        StageTime += dt;
        Elapsed += dt;
    }

    // ---------------------------------------------------------------- skor ve guc

    public static void AddScore(int amount)
    {
        if (amount <= 0 || Ended) return;
        Score += amount;
    }

    public static void RegisterSpawn(int count) { StageSpawned += count; }

    public static void RegisterKill(int combo)
    {
        Kills++;
        StageKills++;
        if (combo > BestCombo) BestCombo = combo;
    }

    public static void RegisterHullHit() { StageHullHits++; }
    public static void RegisterBossKill() { BossKills++; }

    /// <summary>Guc seviyesini artirir; zaten maksimumsa false doner.</summary>
    public static bool AddPower()
    {
        if (PowerLevel >= MaxPower) return false;
        PowerLevel++;
        return true;
    }

    public static void LosePower()
    {
        PowerLevel = Mathf.Max(0, PowerLevel - 1);
    }

    // Test komutlari (GameCheats)
    public static void DebugSetPower(int level) { PowerLevel = Mathf.Clamp(level, 0, MaxPower); }
    public static void DebugSetBombs(int count) { Bombs = Mathf.Clamp(count, 0, MaxBombs); }

    public static bool AddBomb()
    {
        if (Bombs >= MaxBombs) return false;
        Bombs++;
        return true;
    }

    public static bool UseBomb()
    {
        if (Bombs <= 0) return false;
        Bombs--;
        return true;
    }

    // ----------------------------------------------------------------- sonuclar

    public static void ClearStage()
    {
        if (Phase != RunPhase.BossFight && Phase != RunPhase.Playing) return;

        // Vurus orani: dusmanlarin ne kadarini yok ettin
        float ratio = StageSpawned > 0 ? Mathf.Clamp01((float)StageKills / StageSpawned) : 1f;
        ClearBonusAccuracy = Mathf.RoundToInt(ratio * 5000f);
        ClearBonusNoDamage = StageHullHits == 0 ? 3000 : 0;
        Score += ClearBonusAccuracy + ClearBonusNoDamage;

        Phase = RunPhase.StageClear;
        EndedAt = Time.unscaledTime;
    }

    public static void EnterBossFight()
    {
        if (Phase == RunPhase.Playing) Phase = RunPhase.BossFight;
    }

    public static void End(RunPhase result)
    {
        if (Ended) return;

        Phase = result;
        EndedAt = Time.unscaledTime;
        Medals.OnRunEnd(result == RunPhase.Won);

        NewRecord = Score > HighScore;
        if (NewRecord) PlayerPrefs.SetInt(HighKey, Score);
        if (result == RunPhase.Won) PlayerPrefs.SetInt(WinsKey, Wins + 1);
        PlayerPrefs.Save();

        // Rekor ve zafer sartli ucak/slotlar acilmis olabilir (sonuc ekrani duyurur)
        Unlocks.OnProgressChanged();
    }

    public static string FormatTime(float seconds)
    {
        int s = Mathf.Max(0, Mathf.FloorToInt(seconds));
        return $"{s / 60:00}:{s % 60:00}";
    }
}
