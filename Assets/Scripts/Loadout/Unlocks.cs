using System;
using System.Collections.Generic;
using UnityEngine;

// Ucak ve slot kilitlerinin kalici durumu. Sartlar AircraftCatalog'da tanimlidir; burada degerlendirilir.
// Kayit: yenilen boss'lar (PlayerPrefs). Rekor skor ve zafer sayisi RunState'ten okunur.
public static class Unlocks
{
    private const string Key = "PlaneShootEmUp.Unlocks.v1";

    [Serializable]
    private class Save
    {
        public int defeatedMask;
        public bool all;           // test komutu: her sey acik
    }

    private struct State
    {
        public bool[] owned;
        public bool[][] rules;
    }

    private static int defeatedMask;
    private static bool all;
    private static bool loaded;
    private static State last;
    private static bool hasLast;

    private static readonly List<string> messages = new List<string>();

    /// <summary>Kilitler degisti (boss yenildi, oyun bitti, test komutu). Hangar ve ucus sahnesi kendini yeniler.</summary>
    public static event Action Changed;

    // ---------------------------------------------------------------- kayit

    private static void EnsureLoaded()
    {
        if (loaded) return;
        loaded = true;

        string json = PlayerPrefs.GetString(Key, "");
        if (!string.IsNullOrEmpty(json))
        {
            try
            {
                Save s = JsonUtility.FromJson<Save>(json);
                defeatedMask = s.defeatedMask;
                all = s.all;
            }
            catch { defeatedMask = 0; all = false; }
        }

        // Taban durum yuklemede alinir: oturumun ilk degerlendirmesi (ornegin oyun sonu rekor/zafer)
        // de yeni acilanlari bulup duyurur ve otomatik takar. Eskiden ilk degerlendirme yalnizca
        // tabani kaydediyordu ve skor/zafer sartli acilmalar sessizce geciyordu.
        last = Capture();
        hasLast = true;
    }

    private static void Persist()
    {
        PlayerPrefs.SetString(Key, JsonUtility.ToJson(new Save { defeatedMask = defeatedMask, all = all }));
        PlayerPrefs.Save();
    }

    private static int Bit(BossType t) { return 1 << (int)t; }

    public static bool HasDefeated(BossType t)
    {
        EnsureLoaded();
        return all || (defeatedMask & Bit(t)) != 0;
    }

    // -------------------------------------------------------------- sorgular

    public static bool IsMet(Requirement r)
    {
        EnsureLoaded();
        if (all) return true;

        switch (r.kind)
        {
            case RequirementKind.Boss: return (defeatedMask & Bit(r.boss)) != 0;
            case RequirementKind.Score: return RunState.HighScore >= r.value;
            case RequirementKind.Wins: return RunState.Wins >= r.value;
            default: return true;
        }
    }

    public static string Describe(Requirement r)
    {
        switch (r.kind)
        {
            case RequirementKind.Boss: return $"{WhereIs(r.boss)}: {BossBrain.TitleOf(r.boss)}'i yen";
            case RequirementKind.Score: return $"Tek oyunda {r.value:N0} puana ulaş";
            case RequirementKind.Wins: return r.value <= 1 ? "Oyunu bir kez bitir" : $"Oyunu {r.value} kez bitir";
            default: return "";
        }
    }

    public static string WhereIs(BossType b)
    {
        for (int i = 0; i < Stages.All.Length; i++)
        {
            if (Stages.All[i].midBoss == b) return $"Aşama {i + 1} orta boss";
            if (Stages.All[i].boss == b) return $"Aşama {i + 1} ana boss";
        }
        return "";
    }

    public static bool OwnsAircraft(int model)
    {
        return IsMet(AircraftCatalog.Get(model).unlock);
    }

    /// <summary>Slot bu ucakta var mi (ornegin Atmaca'da taret slotu yok)?</summary>
    public static bool SlotExists(int model, SlotKind kind, int index)
    {
        return index >= 0 && index < AircraftCatalog.Get(model).SlotCount(kind);
    }

    /// <summary>Slot var ve acik mi (sarti saglanmis ya da sarti yok)?</summary>
    public static bool IsOpen(int model, SlotKind kind, int index)
    {
        if (!SlotExists(model, kind, index)) return false;
        SlotRule r = AircraftCatalog.Get(model).FindRule(kind, index);
        return r == null || IsMet(r.req);
    }

    public static string SlotRequirement(int model, SlotKind kind, int index)
    {
        SlotRule r = AircraftCatalog.Get(model).FindRule(kind, index);
        return r == null ? "" : Describe(r.req);
    }

    public static string AircraftRequirement(int model)
    {
        return Describe(AircraftCatalog.Get(model).unlock);
    }

    public static int OpenRuleCount(int model)
    {
        int n = 0;
        foreach (var r in AircraftCatalog.Get(model).rules)
            if (IsMet(r.req)) n++;
        return n;
    }

    public static int RuleCount(int model)
    {
        return AircraftCatalog.Get(model).rules.Length;
    }

    // ------------------------------------------------------------- degisiklik

    /// <summary>Bir boss yenildi: kayit + yeni acilanlar (ucak/slot) otomatik takilir ve mesaj uretilir.</summary>
    public static void RegisterBossDefeat(BossType boss)
    {
        EnsureLoaded();
        if ((defeatedMask & Bit(boss)) != 0) return;

        Evaluate(false);                // once mevcut durumu sabitle
        defeatedMask |= Bit(boss);
        Persist();
        Evaluate(true);
    }

    /// <summary>Oyun bitti (rekor skor / zafer guncellendi): skor ve zafer sartlari kontrol edilir.</summary>
    public static void OnProgressChanged()
    {
        EnsureLoaded();
        Evaluate(true);
    }

    /// <summary>Son degisiklikten biriken duyurulari alir ve temizler.</summary>
    public static List<string> TakeMessages()
    {
        var copy = new List<string>(messages);
        messages.Clear();
        return copy;
    }

    public static void UnlockAll()
    {
        EnsureLoaded();
        Evaluate(false);
        all = true;
        Persist();
        Evaluate(false);
        Changed?.Invoke();
    }

    public static void ResetAll()
    {
        EnsureLoaded();
        defeatedMask = 0;
        all = false;
        Persist();
        LoadoutStore.Resanitize();
        last = Capture();
        hasLast = true;
        messages.Clear();
        Changed?.Invoke();
    }

    // --------------------------------------------------------- durum karsilastirma

    private static State Capture()
    {
        var s = new State
        {
            owned = new bool[AircraftCatalog.All.Length],
            rules = new bool[AircraftCatalog.All.Length][]
        };

        for (int m = 0; m < AircraftCatalog.All.Length; m++)
        {
            AircraftDef a = AircraftCatalog.All[m];
            s.owned[m] = IsMet(a.unlock);
            s.rules[m] = new bool[a.rules.Length];
            for (int r = 0; r < a.rules.Length; r++) s.rules[m][r] = IsMet(a.rules[r].req);
        }
        return s;
    }

    // Onceki duruma gore yeni acilanlari bulur; announce ise mesaj ekler ve donanima otomatik takar
    private static void Evaluate(bool announce)
    {
        State now = Capture();
        if (!hasLast)
        {
            last = now;
            hasLast = true;
            return;
        }

        bool changed = false;
        for (int m = 0; m < now.owned.Length; m++)
        {
            AircraftDef a = AircraftCatalog.All[m];

            if (now.owned[m] && !last.owned[m])
            {
                changed = true;
                if (announce) messages.Add($"YENİ UÇAK AÇILDI: {a.name} (hangardan seç)");
            }

            for (int r = 0; r < a.rules.Length; r++)
            {
                if (!now.rules[m][r] || last.rules[m][r]) continue;
                changed = true;

                AutoEquip(m, a.rules[r]);
                if (announce && now.owned[m])
                    messages.Add($"{a.name}: {a.rules[r].Label} açıldı (otomatik takıldı)");
            }
        }

        last = now;
        if (changed) Changed?.Invoke();
    }

    // Yeni acilan slota o slotun ilk parcasini (guc butcesi yetiyorsa) takar
    private static void AutoEquip(int model, SlotRule rule)
    {
        PlaneLoadout l = LoadoutStore.Get(model);
        if (CurrentPart(l, rule) != 0) return;

        if (LoadoutMath.FitsPower(l, rule.kind, rule.index, 1))
        {
            LoadoutMath.Set(l, rule.kind, rule.index, 1);
            LoadoutStore.Save();
        }
    }

    private static int CurrentPart(PlaneLoadout l, SlotRule rule)
    {
        switch (rule.kind)
        {
            case SlotKind.Weapon: return l.weapons[rule.index];
            case SlotKind.Shield: return l.shields[rule.index];
            case SlotKind.Turret: return l.turrets[rule.index];
            case SlotKind.Missile: return l.missiles[rule.index];
            default: return 0;
        }
    }
}
