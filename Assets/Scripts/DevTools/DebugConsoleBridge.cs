using AtomicConsole.Engine;
using UnityEngine;

// Atomic Console'u oyuna baglar:
//  - Konsol prefab'ini (Resources/Atomic_Console) editorde ve development build'lerde bir kez olusturur.
//  - Konsol savasta acikken oyunu durdurur ki yazarken ucak ucmasin / ates etmesin; kapaninca devam eder.
public class DebugConsoleBridge : MonoBehaviour
{
    private AtomicConsoleEngine engine;
    private bool wasVisible;
    private bool prevPaused;
    private float prevScale = 1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        if (!Application.isEditor && !Debug.isDebugBuild) return;

        // Sahneye elle konmus bir konsol varsa ona sadece kopru ekle
        AtomicConsoleEngine existing = FindFirstObjectByType<AtomicConsoleEngine>();
        if (existing != null)
        {
            if (existing.GetComponent<DebugConsoleBridge>() == null) existing.gameObject.AddComponent<DebugConsoleBridge>();
            return;
        }

        var prefab = Resources.Load<GameObject>("Atomic_Console");
        if (prefab == null)
        {
            Debug.LogWarning("Atomic_Console prefab'i Resources altinda bulunamadi.");
            return;
        }

        GameObject go = Instantiate(prefab);
        go.name = "Atomic_Console";
        go.AddComponent<DebugConsoleBridge>();
    }

    // Paketin varsayilan penceresi kucuk (320x240); okunur bir boyuta getirir. windowRect ozeldir, yansima ile.
    private void SizeWindow()
    {
        var field = typeof(AtomicConsoleEngine).GetField("windowRect",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (field == null || engine.skin == null) return;

        float w = Mathf.Clamp(Screen.width * 0.55f, engine.skin.MinWinSize.x, engine.skin.MaxWinSize.x);
        float h = Mathf.Clamp(Screen.height * 0.5f, engine.skin.MinWinSize.y, engine.skin.MaxWinSize.y);
        field.SetValue(engine, new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h));
    }

    private void Update()
    {
        if (engine == null)
        {
            engine = GetComponent<AtomicConsoleEngine>();
            if (engine == null) return;
        }

        bool visible = engine.isVisible;
        if (visible == wasVisible) return;
        wasVisible = visible;

        if (visible) SizeWindow();

        // Hangarda durdurmaya gerek yok (fare tiklamali arayuz)
        if (CombatManager.Instance == null) return;

        if (visible)
        {
            prevPaused = GameState.Paused;
            prevScale = Time.timeScale;
            GameState.Paused = true;
            Time.timeScale = 0f;
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }
        else if (!prevPaused)
        {
            // "timescale" komutu acikken degistirildiyse onu koru
            float s = Time.timeScale > 0f ? Time.timeScale : (prevScale > 0f ? prevScale : 1f);
            GameState.Paused = false;
            Time.timeScale = s;
        }
    }
}
