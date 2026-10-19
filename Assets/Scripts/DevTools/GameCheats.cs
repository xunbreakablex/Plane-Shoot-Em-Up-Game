using AtomicConsole;
using AtomicConsole.debug;
using UnityEngine;

// Atomic Console test komutlari (konsol: ` veya F1). Yeni komut eklersen Unity'de
// "Tools/Atomic Console/Generate Assembly" calistir; metod adlari benzersiz olmali.
// Ondalik sayi gerekirse virgul kullan (konsol '.' karakterini grup ayirici sayar).
public static class GameCheats
{
    private static bool InBattle()
    {
        if (CombatManager.Instance != null && CombatManager.Instance.flight != null) return true;
        AtomicDebug.Error("Bu komut sadece savaş sahnesinde çalışır (SampleScene).");
        return false;
    }

    // ------------------------------------------------------------- hayatta kalma

    [AtomicCommand("", "god", "Ölümsüzlük aç/kapat: mermi, temas ve yere çarpma hasar vermez")]
    public static void God()
    {
        PlayerHealth.God = !PlayerHealth.God;
        AtomicDebug.Console("Ölümsüzlük: " + (PlayerHealth.God ? "AÇIK" : "KAPALI"));
    }

    [AtomicCommand("", "heal", "Can, kalkan ve füzeleri doldurur")]
    public static void Heal()
    {
        if (!InBattle()) return;
        CombatManager cm = CombatManager.Instance;
        PlayerHealth ph = cm.CurrentPlayerHealth;
        if (ph != null) { ph.FullRepair(); ph.RechargeShield(); }
        if (cm.gun != null) cm.gun.RefillMissiles();
        AtomicDebug.Console("Can, kalkan ve füzeler dolduruldu.");
    }

    // ------------------------------------------------------------- kaynaklar

    [AtomicCommand("", "power", "Silah güç seviyesini ayarlar (0-4)")]
    public static void Power(int level)
    {
        RunState.DebugSetPower(level);
        AtomicDebug.Console("Güç seviyesi: " + RunState.PowerLevel);
    }

    [AtomicCommand("", "bombs", "Bomba sayısını ayarlar (0-5)")]
    public static void Bombs(int count)
    {
        RunState.DebugSetBombs(count);
        AtomicDebug.Console("Bomba: " + RunState.Bombs);
    }

    [AtomicCommand("", "scrap", "Hurda ekler")]
    public static void Scrap(int amount)
    {
        RunUpgrades.AddScrap(amount);
        AtomicDebug.Console("Hurda: " + RunUpgrades.Scrap);
    }

    // ------------------------------------------------------------- akis

    [AtomicCommand("", "stage", "Aşamaya atlar (1-3)")]
    public static void Stage(int number)
    {
        if (!InBattle()) return;
        CombatManager.Instance.DebugJumpToStage(number - 1);
        AtomicDebug.Console("Aşama " + number + " başladı.");
    }

    [AtomicCommand("", "skipstage", "Bu aşamayı bitirir (son aşamada oyunu kazanır)")]
    public static void SkipStage()
    {
        if (!InBattle()) return;
        CombatManager.Instance.DebugSkipStage();
    }

    [AtomicCommand("", "boss", "Boss çağırır: jet shark crab octopus zeppelin dragon demon angel ki magic")]
    public static void Boss(string name)
    {
        if (!InBattle()) return;

        BossType type;
        switch ((name ?? "").ToLowerInvariant())
        {
            case "jet": case "sahin": type = BossType.Jet; break;
            case "shark": case "kopekbaligi": type = BossType.Shark; break;
            case "crab": case "yengec": type = BossType.Crab; break;
            case "octopus": case "ahtapot": type = BossType.Octopus; break;
            case "zeppelin": case "zeplin": type = BossType.Zeppelin; break;
            case "dragon": case "ejderha": type = BossType.Dragon; break;
            case "demon": case "iblis": type = BossType.Demon; break;
            case "angel": case "melek": type = BossType.Angel; break;
            case "ki": case "kiwarrior": type = BossType.KiWarrior; break;
            case "magic": case "buyucu": type = BossType.MagicalGirl; break;
            default:
                AtomicDebug.Error("Bilinmeyen boss. Seçenekler: jet shark crab octopus zeppelin dragon demon angel ki magic");
                return;
        }

        EnemyPlane e = CombatManager.Instance.DebugSpawnBoss(type);
        AtomicDebug.Console(e != null ? $"{e.Title} çağrıldı (can {e.MaxHits})." : "Boss doğurulamadı.");
    }

    [AtomicCommand("", "killall", "Tüm düşmanları ve düşman mermilerini temizler")]
    public static void KillAll()
    {
        if (!InBattle()) return;
        int n = CombatManager.Instance.DebugClearEnemies();
        AtomicDebug.Console(n + " düşman temizlendi.");
    }

    [AtomicCommand("", "killboss", "Aktif boss'u öldürür (ödül ve kilit açma çalışır)")]
    public static void KillBoss()
    {
        if (!InBattle()) return;
        EnemyPlane b = CombatManager.Instance.ActiveBoss;
        if (b == null) { AtomicDebug.Error("Aktif boss yok."); return; }
        b.TakeHit(b.MaxHits * 2, b.transform.position);
    }

    [AtomicCommand("", "bosshp", "Aktif boss canını yüzde n yapar (faz geçişlerini denemek için)")]
    public static void BossHp(int percent)
    {
        if (!InBattle()) return;
        EnemyPlane b = CombatManager.Instance.ActiveBoss;
        if (b == null) { AtomicDebug.Error("Aktif boss yok."); return; }
        b.DebugSetHitsFraction(percent / 100f);
        AtomicDebug.Console($"{b.Title} canı: {b.HitsRemaining} / {b.MaxHits}");
    }

    // ------------------------------------------------------------- kilitler

    [AtomicCommand("", "unlockall", "Tüm uçakları ve slotları açar")]
    public static void UnlockAll()
    {
        Unlocks.UnlockAll();
        AtomicDebug.Console("Tüm uçaklar ve slotlar açıldı. Hangarda parçaları tak.");
    }

    [AtomicCommand("", "lockall", "Tüm kilitleri sıfırlar (ilk oyun durumu)")]
    public static void LockAll()
    {
        Unlocks.ResetAll();
        AtomicDebug.Console("Kilitler sıfırlandı.");
    }

    // Savasta aktif ucak, hangarda secili ucak
    private static int CurrentModel()
    {
        CombatManager cm = CombatManager.Instance;
        return cm != null && cm.flight != null ? cm.flight.CurrentIndex : LoadoutStore.SelectedModel;
    }

    [AtomicCommand("", "slots", "Aktif uçağın slot kilit durumunu listeler")]
    public static void Slots()
    {
        int model = CurrentModel();
        AircraftDef a = AircraftCatalog.Get(model);
        AtomicDebug.Console($"{a.name}: {a.weapons} silah, {a.missiles} füze, {a.shields} kalkan, {a.turrets} taret  (can x{a.hpMul})");

        if (a.rules.Length == 0) AtomicDebug.Console("Kilitli slot yok.");
        foreach (var r in a.rules)
        {
            bool open = Unlocks.IsMet(r.req);
            AtomicDebug.Console($"{(open ? "[AÇIK]   " : "[KİLİTLİ]")} {r.Label}  <-  {Unlocks.Describe(r.req)}");
        }
    }

    [AtomicCommand("", "aircraft", "Tüm uçakları ve açılma durumlarını listeler")]
    public static void Aircraft()
    {
        for (int i = 0; i < AircraftCatalog.All.Length; i++)
        {
            AircraftDef a = AircraftCatalog.All[i];
            bool owned = Unlocks.OwnsAircraft(i);
            AtomicDebug.Console($"{i + 1}. {(owned ? "[AÇIK]   " : "[KİLİTLİ]")} {a.name}" + (owned ? "" : "  <-  " + Unlocks.Describe(a.unlock)));
        }
    }

    [AtomicCommand("", "plane", "Savaşta uçağı değiştirir (1-6; açık olmalı)")]
    public static void Plane(int number)
    {
        if (!InBattle()) return;
        bool ok = CombatManager.Instance.flight.SelectAircraft(number - 1);
        AtomicDebug.Console(ok ? "Uçak değişti: " + AircraftCatalog.Get(number - 1).name : "Uçak kilitli ya da geçersiz.");
    }

    // ------------------------------------------------------------- olcum

    [AtomicCommand("", "info", "Savaş durumu, teorik DPS ve boss hasar ölçümü")]
    public static void Info()
    {
        if (!InBattle()) return;
        CombatManager cm = CombatManager.Instance;

        PlaneLoadout loadout = LoadoutStore.Get(cm.flight.CurrentIndex);
        float dps = PlayerGun.TheoreticalDps(loadout);

        AtomicDebug.Console($"Aşama {RunState.Stage + 1}/{Stages.All.Length}  süre {RunState.StageTime:0}s  faz {RunState.Phase}  skor {RunState.Score:N0}");
        int model = cm.flight.CurrentIndex;
        AtomicDebug.Console($"Uçak: {AircraftCatalog.Get(model).name}  güç {RunState.PowerLevel}/{RunState.MaxPower}  bomba {RunState.Bombs}  hurda {RunUpgrades.Scrap}  açılan slot {Unlocks.OpenRuleCount(model)}/{Unlocks.RuleCount(model)}");
        AtomicDebug.Console($"Teorik DPS (her mermi isabetli): {dps:0.0}/sn   düşman {cm.Enemies.Count}  düşman mermisi {Bullet.EnemyBulletCount}");

        EnemyPlane b = cm.ActiveBoss;
        if (b == null)
        {
            AtomicDebug.Console("Aktif boss yok.");
            return;
        }

        float eta = b.Dps > 0.1f ? b.HitsRemaining / b.Dps : -1f;
        AtomicDebug.Console($"{b.Title}: {b.HitsRemaining}/{b.MaxHits}  verilen {b.TotalDamage}  ölçülen DPS {b.Dps:0.0}"
                            + (eta > 0f ? $"  tahmini kalan {eta:0} sn" : ""));
        if (dps > 0f)
            AtomicDebug.Console($"Mükemmel isabetle en kısa süre: {b.MaxHits / dps:0} sn");
    }
}
