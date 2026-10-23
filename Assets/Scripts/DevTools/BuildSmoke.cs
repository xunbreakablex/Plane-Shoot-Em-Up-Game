using System.Collections;
using System.Collections.Generic;
using System.IO;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.SceneManagement;

// Build duman testi (yalniz development build): "-smoke <klasor>" ile acilirsa hangardan ucusa gecer, asama temalarini
// ve 12 boss'u sirayla cagirip her birinin ekran goruntusunu klasore yazar, hatalari smoke.txt'ye sayar ve kapanir.
// Her adimin kare suresi (ortalama / en kotu / 33 ms ustu kare) ve kare basina GC ayirmasi da yazilir.
// Editorde calisan ama build'de kirilan seyleri (shader ayiklama, eksik varlik) yakalamak icin.
public class BuildSmoke : MonoBehaviour
{
    private string dir;
    private int errors;
    private string firstError;

    private class Seg { public string name; public int frames, slow; public double sumMs, maxMs, gcBytes; public int gcFrames; }
    private readonly List<Seg> segs = new List<Seg>();
    private Seg seg;
    private ProfilerRecorder gc;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Init()
    {
        if (!DevBuild.Enabled || Application.isEditor) return;
        string[] args = System.Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] != "-smoke") continue;
            var go = new GameObject("BuildSmoke");
            DontDestroyOnLoad(go);
            go.AddComponent<BuildSmoke>().dir = args[i + 1];
            return;
        }
    }

    private void Awake()
    {
        Application.logMessageReceived += OnLog;
        Application.runInBackground = true;   // pencere odakta degilken de ilerlesin (yalniz bu calisma icin)
        gc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
    }

    private void Begin(string name)
    {
        seg = new Seg { name = name };
        segs.Add(seg);
    }

    private void Update()
    {
        if (seg == null) return;
        double ms = Time.unscaledDeltaTime * 1000.0;
        seg.frames++;
        seg.sumMs += ms;
        if (ms > seg.maxMs) seg.maxMs = ms;
        if (ms > 33.4) seg.slow++;
        long b = gc.Valid ? gc.LastValue : 0;
        seg.gcBytes += b;
        if (b > 0) seg.gcFrames++;
    }

    private void OnLog(string msg, string stack, LogType type)
    {
        if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
        errors++;
        if (firstError == null) firstError = msg.Replace('\n', ' ');
    }

    private IEnumerator Shot(string name)
    {
        yield return new WaitForEndOfFrame();
        ScreenCapture.CaptureScreenshot(Path.Combine(dir, name + ".png"));
        yield return null;
    }

    private IEnumerator Start()
    {
        Directory.CreateDirectory(dir);
        yield return new WaitForSecondsRealtime(3f);
        yield return Shot("0_hangar");

        SceneManager.LoadScene(GameScenes.Flight);
        yield return new WaitForSecondsRealtime(4f);
        PlayerHealth.God = true;
        Begin("asama1");
        yield return new WaitForSecondsRealtime(12f);
        yield return Shot("1_ucus");

        CombatManager cm = CombatManager.Instance;
        for (int stage = 1; stage < Stages.All.Length; stage++)
        {
            cm.DebugJumpToStage(stage);
            yield return new WaitForSecondsRealtime(2f);
            Begin("asama" + (stage + 1));
            yield return new WaitForSecondsRealtime(12f);
            yield return Shot("2_asama" + (stage + 1));
        }

        cm.DebugJumpToStage(0);
        yield return new WaitForSecondsRealtime(2f);
        foreach (BossType b in System.Enum.GetValues(typeof(BossType)))
        {
            EnemyPlane boss = cm.SpawnBossEncounter(b, EnemyKind.MidBoss);
            Begin("boss_" + b);
            yield return new WaitForSecondsRealtime(9f);
            yield return Shot("3_boss_" + b);
            seg = null;
            if (boss != null && !boss.IsDead) boss.Vanish();
            cm.DebugJumpToStage(0);
            yield return new WaitForSecondsRealtime(2.5f);
        }

        seg = null;
        var sb = new System.Text.StringBuilder();
        sb.Append("errors=" + errors + "\nfirstError=" + firstError + "\n");
        sb.Append("adim\tkare\tort_ms\ten_kotu_ms\t33ms_ustu\tGC_KB_kare\tGC_kare_orani\n");
        foreach (Seg x in segs)
            sb.Append(x.name + "\t" + x.frames + "\t" + (x.sumMs / Mathf.Max(1, x.frames)).ToString("0.0") + "\t" + x.maxMs.ToString("0")
                + "\t" + x.slow + "\t" + (x.gcBytes / 1024.0 / Mathf.Max(1, x.frames)).ToString("0.0")
                + "\t" + ((double)x.gcFrames / Mathf.Max(1, x.frames)).ToString("0.00") + "\n");
        File.WriteAllText(Path.Combine(dir, "smoke.txt"), sb.ToString());
        yield return new WaitForSecondsRealtime(1f);
        Application.Quit();
    }
}
