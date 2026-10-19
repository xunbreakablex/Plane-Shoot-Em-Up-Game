using System.Collections.Generic;
using UnityEngine;

// Madalyalar: kalici basarimlar (PlayerPrefs). Savasta olaylar (oldurme, asama sonu, oyun sonu) buraya bildirilir;
// kosul saglaninca madalya verilir, savasta duyurulur, sonuc ekraninda ve hangardaki panelde listelenir.
public static class Medals
{
    public struct Def
    {
        public string id, name, desc;
        public Def(string id, string name, string desc) { this.id = id; this.name = name; this.desc = desc; }
    }

    public static readonly Def[] All =
    {
        new Def("first",    "İlk Zafer",          "Kampanyayı bir kez bitir"),
        new Def("flawless", "Kusursuz Uçuş",      "Bir aşamayı gövdene hiç hasar almadan bitir"),
        new Def("combo20",  "Kombo Ustası",       "20'lik öldürme serisi yap"),
        new Def("score100", "Yüz Bin",            "Tek oyunda 100.000 puana ulaş"),
        new Def("score250", "Çeyrek Milyon",      "Tek oyunda 250.000 puana ulaş"),
        new Def("hard",     "Çelik Sinir",        "Zor modda kampanyayı bitir"),
        new Def("rush",     "Boss Avcısı",        "Boss Rush'ı bitir"),
        new Def("endless3", "Sonsuz Yolcu",       "Sonsuz Savaş'ta 3 tur dayan"),
        new Def("kills500", "Hava Hâkimi",        "Toplamda 500 düşman indir"),
        new Def("kamikaze", "Kamikaze Avcısı",    "Toplamda 40 kamikaze indir"),
        new Def("sniper",   "Nişancı Avcısı",     "Toplamda 25 nişancı indir"),
        new Def("ships",    "Gemi Batırıcı",      "Toplamda 10 savaş gemisi batır"),
        new Def("bosses",   "Dev Avcısı",         "Toplamda 25 boss yen"),
    };

    private const string Prefix = "PlaneShootEmUp.Medal.";
    private const string CountPrefix = "PlaneShootEmUp.MedalCount.";

    public static readonly List<string> EarnedThisRun = new List<string>();   // sonuc ekrani
    private static readonly List<string> toAnnounce = new List<string>();     // savasta duyurulacaklar

    public static bool Has(string id) { return PlayerPrefs.GetInt(Prefix + id, 0) == 1; }

    public static int EarnedCount
    {
        get { int n = 0; foreach (var d in All) if (Has(d.id)) n++; return n; }
    }

    public static int Count(string key) { return PlayerPrefs.GetInt(CountPrefix + key, 0); }

    private static int Add(string key, int amount = 1)
    {
        int v = Count(key) + amount;
        PlayerPrefs.SetInt(CountPrefix + key, v);
        return v;
    }

    private static void Award(string id)
    {
        if (Has(id)) return;
        PlayerPrefs.SetInt(Prefix + id, 1);
        PlayerPrefs.Save();
        foreach (var d in All)
            if (d.id == id) { EarnedThisRun.Add(d.name); toAnnounce.Add(d.name); }
    }

    /// <summary>Savasta duyurulacak yeni madalya adlari (alinca temizlenir).</summary>
    public static List<string> TakeAnnouncements()
    {
        if (toAnnounce.Count == 0) return null;
        var copy = new List<string>(toAnnounce);
        toAnnounce.Clear();
        return copy;
    }

    public static void NewRun()
    {
        EarnedThisRun.Clear();
        toAnnounce.Clear();
    }

    // ------------------------------------------------------------------ olaylar

    public static void OnKill(EnemyPlane e, int streak)
    {
        if (e == null) return;
        int total = Add("kills");
        if (total >= 500) Award("kills500");
        if (streak >= 20) Award("combo20");

        if (e.IsStageBoss || e.Kind == EnemyKind.MidBoss)
        {
            if (Add("bosses") >= 25) Award("bosses");
        }
        else if (e.Kind == EnemyKind.Kamikaze)
        {
            if (Add("kamikaze") >= 40) Award("kamikaze");
        }
        else if (e.Kind == EnemyKind.Sniper)
        {
            if (Add("sniper") >= 25) Award("sniper");
        }
        else if (e.Title == "Savaş Gemisi")
        {
            if (Add("ships") >= 10) Award("ships");
        }
    }

    /// <summary>Asama tamam (ClearStage sonrasi): hasarsiz bonus ve skor esikleri.</summary>
    public static void OnStageClear()
    {
        if (RunState.ClearBonusNoDamage > 0) Award("flawless");
        CheckScore();
    }

    /// <summary>Oyun bitti ya da sonsuz modda yeni asamaya gecildi.</summary>
    public static void OnRunEnd(bool won)
    {
        CheckScore();
        if (RunState.Endless && RunState.Stage >= Stages.All.Length + 2) Award("endless3");
        if (!won) return;

        if (RunState.Rush) Award("rush");
        else
        {
            Award("first");
            if (Difficulty.Level == 2) Award("hard");
        }
    }

    public static void OnEndlessStage()
    {
        if (RunState.Endless && RunState.Stage >= Stages.All.Length + 2) Award("endless3");
    }

    private static void CheckScore()
    {
        if (RunState.Score >= 100000) Award("score100");
        if (RunState.Score >= 250000) Award("score250");
    }
}
