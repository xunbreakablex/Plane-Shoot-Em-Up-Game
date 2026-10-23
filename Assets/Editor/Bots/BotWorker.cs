using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.LowLevel;

// Paralel bot kosucusu (yalniz editor). Ana editor ve Multiplayer Play Mode sanal oyunculari ayni is kuyrugunu
// paylasir: Library/BotQueue/queue/*.json. Oyun moduna girildiginde kuyrukta "enabled" dosyasi varsa her editor
// siradaki isi kapar (dosyayi running/'e tasiyarak), senaryoyu kosar, sonucu results/'e yazar ve sonrakine gecer.
// Senaryo her karenin basinda (EarlyUpdate) calisir; duraklat-adimla pompasi gerekmez (MPPM duraklatmayi esler).
[InitializeOnLoad]
public static class BotWorker
{
    [Serializable]
    public class BotJob
    {
        public string id;
        public string scenario;      // "wave" | "boss"
        public int aircraft = -1;    // AircraftCatalog sirasi; -1 = degistirme
        public int stage;            // wave: DebugJumpToStage
        public string boss;          // boss: BossType adi
        public float duration = 140f; // wave: oyun suresi; boss: zaman asimi
        public int power = 2;
        public float timeScale = 1f;
        public bool chain;           // flow: asama sonu ekranindan sonraki asamaya gec (oyun sonuna kadar)
        public int endless;          // flow + chain: zaferden sonra Sonsuz Savas'ta bu kadar asama daha
        public float killAfter = 8f; // flow: hava dusmani bu kadar sn yasayinca vurulmus sayilir
        public float groundEvery = 2f; // flow: yer hedefleri arasi sn (5 sn ustu seriyi bozar)
        public bool rush;            // oyun Boss Rush olarak baslar (hangardaki MOD secimi)
        public string after;         // death: sonuc ekranindan "retry" (tekrar oyna) ya da "hangar" (hangar -> savasa cik)
        public int seed;             // 0 = rastgele; ayni tohumu ayarlar arasinda paylasmak gurultuyu azaltir
        public string[] set;         // "EnemyPlane.OrganicAimSpread=1.3", "GameSettings.enemyHitsScale=1.2"
    }

    public abstract class Scenario
    {
        public BotJob job;
        public abstract void Begin();
        /// <summary>Karenin basinda cagrilir; is bitince true.</summary>
        public abstract bool Tick();
        public abstract string Result();
    }

    private struct BotTick { }

    private static string root;
    private static string workerName;
    private static Scenario current;
    private static string currentFile;
    private static readonly List<KeyValuePair<FieldInfo, object>> overrides = new List<KeyValuePair<FieldInfo, object>>();
    private static object overrideTarget;
    private static float startAt, nextPoll, settleUntil;
    private static int pendingAircraft = -1, waitFrames;
    private static bool prevRunInBackground;
    private static int jobErrors;
    private static string firstError;

    static BotWorker()
    {
        EditorApplication.playModeStateChanged += OnPlayMode;
        if (EditorApplication.isPlaying) Start();
    }

    /// <summary>Ana proje kok klasoru (sanal oyuncuda Library/VP/... altindan geri cikilir).</summary>
    public static string ProjectRoot
    {
        get
        {
            string p = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            int i = p.Replace('\\', '/').IndexOf("/Library/VP/", StringComparison.OrdinalIgnoreCase);
            return i >= 0 ? p.Substring(0, i) : p;
        }
    }

    private static void OnPlayMode(PlayModeStateChange s)
    {
        if (s == PlayModeStateChange.EnteredPlayMode) Start();
        else if (s == PlayModeStateChange.ExitingPlayMode) Stop();
    }

    private static void Start()
    {
        string dir = Path.Combine(ProjectRoot, "Library", "BotQueue");
        if (!File.Exists(Path.Combine(dir, "enabled"))) return;
        root = dir;
        Directory.CreateDirectory(Path.Combine(root, "queue"));
        Directory.CreateDirectory(Path.Combine(root, "running"));
        Directory.CreateDirectory(Path.Combine(root, "results"));
        workerName = "w" + System.Diagnostics.Process.GetCurrentProcess().Id;
        startAt = Time.realtimeSinceStartup + 2f; // sahne kurulsun
        // Odakta olmayan editorde oyun karesi ilerlemez; kosu boyunca arka planda calis (ayar cikista geri alinir)
        prevRunInBackground = PlayerSettings.runInBackground;
        Application.runInBackground = true;
        Install(true);
        Application.logMessageReceived += OnLog;
        Log("basladi " + Application.dataPath);
    }

    private static void Stop()
    {
        if (root == null) return;
        if (current != null) Finish("iptal");
        root = null;
        Application.logMessageReceived -= OnLog;
        Install(false);
        PlayerSettings.runInBackground = prevRunInBackground;
        BotInput.Clear();
        Time.captureFramerate = 0;
        Time.timeScale = 1f;
    }

    private static void Install(bool on)
    {
        PlayerLoopSystem loop = PlayerLoop.GetCurrentPlayerLoop();
        for (int i = 0; i < loop.subSystemList.Length; i++)
        {
            if (loop.subSystemList[i].type != typeof(UnityEngine.PlayerLoop.EarlyUpdate)) continue;
            var list = new List<PlayerLoopSystem>(loop.subSystemList[i].subSystemList);
            list.RemoveAll(x => x.type == typeof(BotTick));
            if (on) list.Insert(0, new PlayerLoopSystem { type = typeof(BotTick), updateDelegate = Frame });
            loop.subSystemList[i].subSystemList = list.ToArray();
        }
        PlayerLoop.SetPlayerLoop(loop);
    }

    private static void Frame()
    {
        if (!Application.isPlaying || Time.realtimeSinceStartup < startAt) return;
        try
        {
            if (current == null)
            {
                if (Time.realtimeSinceStartup < nextPoll) return;
                if (!Claim()) { nextPoll = Time.realtimeSinceStartup + 1f; return; }
            }
            if (waitFrames > 0) { waitFrames--; return; } // sahne yeniden yukleniyor
            if (pendingAircraft >= 0)
            {
                if (CombatManager.Instance.flight.CurrentIndex != pendingAircraft)
                {
                    var ftm = UnityEngine.Object.FindFirstObjectByType<FlightTestManager>();
                    typeof(FlightTestManager).GetMethod("Select", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(ftm, new object[] { pendingAircraft });
                    settleUntil = Time.time + 1.5f;
                }
                pendingAircraft = -1;
            }
            if (Time.time < settleUntil) return;
            if (!GameState.Paused) Time.timeScale = current.job.timeScale; // oyun duraklat / devam'da 1'e cekiyor
            if (!started)
            {
                if (current.job.seed != 0) UnityEngine.Random.InitState(current.job.seed);
                current.Begin();
                started = true;
            }
            if (current.Tick()) Finish(current.Result());
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            if (current != null) Finish("hata=" + e.GetType().Name + ":" + e.Message.Replace('\n', ' '));
        }
    }

    private static bool started;

    private static bool Claim()
    {
        string[] files = Directory.GetFiles(Path.Combine(root, "queue"), "*.json");
        Array.Sort(files, StringComparer.Ordinal);
        foreach (string f in files)
        {
            string dst = Path.Combine(root, "running", Path.GetFileNameWithoutExtension(f) + "." + workerName + ".json");
            try { File.Move(f, dst); }
            catch (IOException) { continue; } // baskasi kapti
            var job = JsonUtility.FromJson<BotJob>(File.ReadAllText(dst));
            if (string.IsNullOrEmpty(job.id)) job.id = Path.GetFileNameWithoutExtension(f);
            currentFile = dst;
            current = Create(job);
            ApplyOverrides(job.set);
            Time.captureFramerate = 60;
            Time.timeScale = job.timeScale;
            jobErrors = 0;
            firstError = null;
            pendingAircraft = job.aircraft;
            settleUntil = 0f;
            // Her is temiz sahneyle baslar (oyunun yeniden deneme yolu da Flight sahnesini yukler); onceki isin
            // acik biraktigi asama sonu ekrani duraklatmayi tasimasin
            GameState.UiOpen = false;
            GameState.SetPaused(false);
            RunState.NextRush = job.rush;   // sahne yuklenince NewCampaign uygular
            UnityEngine.SceneManagement.SceneManager.LoadScene(GameScenes.Flight);
            waitFrames = 90;
            started = false;
            Log("is " + job.id);
            return true;
        }
        return false;
    }

    private static Scenario Create(BotJob job)
    {
        Scenario s;
        switch (job.scenario)
        {
            case "wave": s = new WaveScenario(); break;
            case "boss": s = new BossScenario(); break;
            case "flow": s = new FlowScenario(); break;
            case "death": s = new DeathScenario(); break;
            default: throw new ArgumentException("bilinmeyen senaryo " + job.scenario);
        }
        s.job = job;
        return s;
    }

    private static void Finish(string result)
    {
        string id = current.job.id;
        if (jobErrors > 0) result += "\terrors=" + jobErrors + "\tfirstError=" + firstError;
        File.WriteAllText(Path.Combine(root, "results", id + ".txt"), id + "\t" + workerName + "\t" + result + "\n");
        try { File.Delete(currentFile); } catch (IOException) { }
        RevertOverrides();
        BotInput.Clear();
        RunState.NextRush = false;
        Time.timeScale = 1f;
        current = null;
        Log("bitti " + id + " " + result);
    }

    // Is sirasinda konsola dusen hata / istisna sayisi ve ilki (akis testi icin)
    private static void OnLog(string msg, string stack, LogType type)
    {
        if (current == null || (type != LogType.Error && type != LogType.Exception && type != LogType.Assert)) return;
        jobErrors++;
        if (firstError == null)
        {
            string line = stack.Split('\n')[0];
            firstError = (msg + " @ " + line).Replace('\t', ' ').Replace('\n', ' ').Replace('\r', ' ');
            if (firstError.Length > 220) firstError = firstError.Substring(0, 220);
        }
    }

    // "Tip.alan=deger": statik alan (const olmayan) ya da GameSettings.Current uzerindeki alan
    private static void ApplyOverrides(string[] set)
    {
        overrides.Clear();
        if (set == null) return;
        Assembly game = typeof(CombatManager).Assembly;
        foreach (string s in set)
        {
            int dot = s.IndexOf('.'), eq = s.IndexOf('=');
            Type t = game.GetType(s.Substring(0, dot), true);
            string name = s.Substring(dot + 1, eq - dot - 1);
            const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
            FieldInfo f = t.GetField(name, all);
            if (f == null) throw new MissingFieldException(t.Name, name);
            object target = f.IsStatic ? null : (object)GameSettings.Current;
            object value = Convert.ChangeType(s.Substring(eq + 1), f.FieldType, System.Globalization.CultureInfo.InvariantCulture);
            overrides.Add(new KeyValuePair<FieldInfo, object>(f, f.GetValue(target)));
            f.SetValue(target, value);
        }
    }

    private static void RevertOverrides()
    {
        for (int i = overrides.Count - 1; i >= 0; i--)
            overrides[i].Key.SetValue(overrides[i].Key.IsStatic ? null : (object)GameSettings.Current, overrides[i].Value);
        overrides.Clear();
    }

    private static void Log(string msg)
    {
        Debug.Log("[Bot " + workerName + "] " + msg);
        try { File.AppendAllText(Path.Combine(root, "log.txt"), DateTime.Now.ToString("HH:mm:ss") + " " + workerName + " " + msg + "\n"); }
        catch (IOException) { }
    }

    internal static readonly BindingFlags BF = BindingFlags.NonPublic | BindingFlags.Instance;
    internal static FieldInfo F(Type t, string name) { return t.GetField(name, BF); }
}
