using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

// Build duman testi (yalniz development build): "-smoke <klasor>" ile acilirsa hangardan ucusa gecer, asama temalarini
// ve 12 boss'u sirayla cagirip her birinin ekran goruntusunu klasore yazar, hatalari smoke.txt'ye sayar ve kapanir.
// Editorde calisan ama build'de kirilan seyleri (shader ayiklama, eksik varlik) yakalamak icin.
public class BuildSmoke : MonoBehaviour
{
    private string dir;
    private int errors;
    private string firstError;

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
        yield return new WaitForSecondsRealtime(6f);
        PlayerHealth.God = true;
        yield return Shot("1_ucus");

        CombatManager cm = CombatManager.Instance;
        for (int stage = 1; stage < Stages.All.Length; stage++)
        {
            cm.DebugJumpToStage(stage);
            yield return new WaitForSecondsRealtime(5f);
            yield return Shot("2_asama" + (stage + 1));
        }

        cm.DebugJumpToStage(0);
        yield return new WaitForSecondsRealtime(2f);
        foreach (BossType b in System.Enum.GetValues(typeof(BossType)))
        {
            EnemyPlane boss = cm.SpawnBossEncounter(b, EnemyKind.MidBoss);
            yield return new WaitForSecondsRealtime(4.5f);
            yield return Shot("3_boss_" + b);
            if (boss != null && !boss.IsDead) boss.Vanish();
            cm.DebugJumpToStage(0);
            yield return new WaitForSecondsRealtime(2.5f);
        }

        File.WriteAllText(Path.Combine(dir, "smoke.txt"), "errors=" + errors + "\nfirstError=" + firstError + "\n");
        yield return new WaitForSecondsRealtime(1f);
        Application.Quit();
    }
}
