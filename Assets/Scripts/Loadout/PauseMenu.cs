using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// ESC: oyunu durdurur; devam et, magaza veya hangara don.
// Magaza aciksa ESC magazayi kapatir (duraklat menusune doner). Asama/sonuc ekranlarinda ESC calismaz.
public class PauseMenu : MonoBehaviour
{
    private GUIStyle button;
    private ShopUI shop;
    private StageClearUI stageClear;
    private RunResultUI results;

    private void Start()
    {
        shop = GetComponent<ShopUI>();
        if (shop == null) shop = FindFirstObjectByType<ShopUI>();
        stageClear = GetComponent<StageClearUI>();
        if (stageClear == null) stageClear = FindFirstObjectByType<StageClearUI>();
        results = GetComponent<RunResultUI>();
        if (results == null) results = FindFirstObjectByType<RunResultUI>();

        GameState.SetPaused(false);
    }

    private void OnDestroy()
    {
        GameState.Paused = false;
        GameState.UiOpen = false;
        Time.timeScale = 1f;
        AudioListener.pause = false;
    }

    private void Update()
    {
        var kb = Keyboard.current;
        var pad = Gamepad.current;
        bool pressed = (kb != null && kb.escapeKey.wasPressedThisFrame) || (pad != null && pad.startButton.wasPressedThisFrame);
        if (!pressed) return;

        // Asama tamam ve sonuc ekranlarinda kendi dugmeleri vardir
        if (results != null && results.IsOpen) return;

        if (shop != null && shop.IsOpen)
        {
            shop.Close();
            return;
        }

        if (stageClear != null && stageClear.IsOpen) return;
        if (RunState.Ended || RunState.StageIsClear) return;

        GameState.SetPaused(!GameState.Paused);
    }

    private bool showSettings;
    private int focus;          // gamepad odagi: 0 devam, 1 magaza, 2 ayarlar, 3 hangar
    private bool padAccept;     // bu karede A basildi (odaklanan dugme tiklanmis sayilir)

    private bool Click(Rect r, string text, int index = -1)
    {
        bool c = GUI.Button(r, text, button);
        if (index >= 0 && index == focus)
        {
            PadNav.Highlight(r);
            if (padAccept) c = true;
        }
        if (c) GameAudio.PlayUi(Sfx.Click, 0.5f);
        return c;
    }

    private void OnGUI()
    {
        if (!GameState.Paused) { showSettings = false; return; }
        if (shop != null && shop.IsOpen) return;
        if (stageClear != null && stageClear.IsOpen) return;
        if (results != null && results.IsOpen) return;

        if (button == null)
        {
            button = new GUIStyle(GUI.skin.button) { fontStyle = FontStyle.Bold, fontSize = 22 };
        }

        // Diger ekranlarla ayni sanal 1280x720 olcek
        Hud.Begin();

        if (showSettings)
        {
            if (SettingsPanel.Draw()) showSettings = false;
            Hud.End();
            return;
        }

        float W = Hud.W, H = Hud.H;
        Hud.Fill(new Rect(0f, 0f, W, H), new Color(0f, 0f, 0f, 0.62f));

        // Gamepad: dpad/cubuk odak, A onay, B devam
        bool shopOpen = shop != null && GameSettings.Current.shopInPauseMenu;
        focus = PadNav.Move(focus, 4, i => i != 1 || shopOpen);
        padAccept = PadNav.Accept;
        if (PadNav.Back) { GameState.SetPaused(false); Hud.End(); return; }

        float w = 400f, h = 56f, gap = 16f;
        float x = (W - w) * 0.5f;
        float y = H * 0.24f;

        Hud.Text(new Rect(0f, y - 80f, W, 60f), "DURAKLATILDI", Color.white, 44, TextAnchor.MiddleCenter);

        if (Click(new Rect(x, y, w, h), "DEVAM ET   (ESC)", 0))
            GameState.SetPaused(false);

        // Savas ortasinda magaza (sinirsiz onarim) GameSettings.shopInPauseMenu ile acilir; varsayilan:
        // yalniz asama aralarinda (Asama Tamam ekrani)
        if (shop != null && GameSettings.Current.shopInPauseMenu)
        {
            if (Click(new Rect(x, y + (h + gap), w, h), $"MAĞAZA   •   {RunUpgrades.Scrap} hurda", 1))
                shop.Open();
        }
        else
        {
            GUI.enabled = false;
            GUI.Button(new Rect(x, y + (h + gap), w, h), "MAĞAZA  (aşama aralarında)", button);
            GUI.enabled = true;
        }

        if (Click(new Rect(x, y + (h + gap) * 2f, w, h), "AYARLAR", 2))
            showSettings = true;

        if (Click(new Rect(x, y + (h + gap) * 3f, w, h), "HANGARA DÖN", 3))
        {
            // Savasin ortasinda hangara donmek oyunu bastan baslatir
            GameState.SetPaused(false);
            SceneManager.LoadScene(GameScenes.Hangar);
        }

        Hud.Text(new Rect(0f, y + (h + gap) * 4f - 4f, W, 22f), ControlSettings.SteerHint,
            new Color(1f, 0.9f, 0.55f), 15, TextAnchor.MiddleCenter);
        Hud.Text(new Rect(0f, y + (h + gap) * 4f + 20f, W, 22f), ControlSettings.AimHint,
            new Color(1f, 0.9f, 0.55f), 15, TextAnchor.MiddleCenter);

        Hud.Text(new Rect(0f, y + (h + gap) * 4f + 50f, W, 24f),
            "Savaş sırasında hangara dönersen oyun baştan başlar. Mağaza yükseltmeleri bu oyuna özeldir.",
            new Color(0.8f, 0.85f, 0.9f), 14, TextAnchor.MiddleCenter, false);

        Hud.End();
    }
}
