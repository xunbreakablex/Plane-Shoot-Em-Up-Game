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
        if (kb == null || !kb.escapeKey.wasPressedThisFrame) return;

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

    private void OnGUI()
    {
        if (!GameState.Paused) return;
        if (shop != null && shop.IsOpen) return;
        if (stageClear != null && stageClear.IsOpen) return;
        if (results != null && results.IsOpen) return;

        if (button == null)
        {
            button = new GUIStyle(GUI.skin.button) { fontStyle = FontStyle.Bold, fontSize = 22 };
        }

        // Diger ekranlarla ayni sanal 1280x720 olcek
        Hud.Begin();
        float W = Hud.W, H = Hud.H;

        Hud.Fill(new Rect(0f, 0f, W, H), new Color(0f, 0f, 0f, 0.62f));

        float w = 400f, h = 56f, gap = 16f;
        float x = (W - w) * 0.5f;
        float y = H * 0.22f;   // 5 dugme + ipuclari sigsin

        Hud.Text(new Rect(0f, y - 80f, W, 60f), "DURAKLATILDI", Color.white, 44, TextAnchor.MiddleCenter);

        if (GUI.Button(new Rect(x, y, w, h), "DEVAM ET   (ESC)", button))
            GameState.SetPaused(false);

        // Savas ortasinda magaza (sinirsiz onarim) GameSettings.shopInPauseMenu ile acilir; varsayilan:
        // yalniz asama aralarinda (Asama Tamam ekrani)
        if (shop != null && GameSettings.Current.shopInPauseMenu)
        {
            if (GUI.Button(new Rect(x, y + (h + gap), w, h), $"MAĞAZA   •   {RunUpgrades.Scrap} hurda", button))
                shop.Open();
        }
        else
        {
            GUI.enabled = false;
            GUI.Button(new Rect(x, y + (h + gap), w, h), "MAĞAZA  (aşama aralarında)", button);
            GUI.enabled = true;
        }

        if (GUI.Button(new Rect(x, y + (h + gap) * 2f, w, h), "HANGARA DÖN", button))
        {
            // Savasin ortasinda hangara donmek oyunu bastan baslatir
            GameState.SetPaused(false);
            SceneManager.LoadScene(GameScenes.Hangar);
        }

        // Kontrol semasi: Arcade (A/D don, Q/E yatis) / Klasik (A/D yatis, Q/E dumen); tercih kalicidir
        if (GUI.Button(new Rect(x, y + (h + gap) * 3f, w, h), $"KONTROL:  {ControlSettings.Label}", button))
            ControlSettings.Scheme = ControlSettings.Arcade ? ControlScheme.Classic : ControlScheme.Arcade;

        // Nisan: Otomatik (imlec burun onunde, en yakin dusman) / Fare; tercih kalicidir
        if (GUI.Button(new Rect(x, y + (h + gap) * 4f, w, h), $"NİŞAN:  {ControlSettings.AimLabel}", button))
            ControlSettings.AutoAim = !ControlSettings.AutoAim;

        Hud.Text(new Rect(0f, y + (h + gap) * 5f - 4f, W, 22f), ControlSettings.SteerHint,
            new Color(1f, 0.9f, 0.55f), 15, TextAnchor.MiddleCenter);
        Hud.Text(new Rect(0f, y + (h + gap) * 5f + 20f, W, 22f), ControlSettings.AimHint,
            new Color(1f, 0.9f, 0.55f), 15, TextAnchor.MiddleCenter);

        Hud.Text(new Rect(0f, y + (h + gap) * 5f + 50f, W, 24f),
            "Savaş sırasında hangara dönersen oyun baştan başlar. Mağaza yükseltmeleri bu oyuna özeldir.",
            new Color(0.8f, 0.85f, 0.9f), 14, TextAnchor.MiddleCenter, false);

        Hud.End();
    }
}
