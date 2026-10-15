using UnityEngine;
using UnityEngine.SceneManagement;

// Asama tamam ekrani: puan dokumu, sonraki asama, magaza ya da hangar.
public class StageClearUI : MonoBehaviour
{
    public bool IsOpen { get; private set; }

    private ShopUI shop;

    private void Start()
    {
        shop = GetComponent<ShopUI>();
        if (shop == null) shop = FindFirstObjectByType<ShopUI>();
    }

    private void OnDestroy()
    {
        IsOpen = false;
    }

    private void Update()
    {
        // Faz disaridan degistiyse (ornegin konsoldan "stage" komutu) panel acik kalmasin
        if (IsOpen && !RunState.StageIsClear)
        {
            Close();
            return;
        }

        if (IsOpen || !RunState.StageIsClear) return;

        CombatManager cm = CombatManager.Instance;
        float delay = cm != null ? cm.stageClearDelay : 2.8f;
        if (Time.unscaledTime - RunState.EndedAt < delay) return;

        IsOpen = true;
        GameState.UiOpen = true;
        GameState.SetPaused(true);
    }

    private void OnGUI()
    {
        if (!IsOpen) return;
        if (shop != null && shop.IsOpen) return;   // magaza kendi ekranini cizer

        Hud.Begin();
        float W = Hud.W, H = Hud.H;

        Hud.Fill(new Rect(0f, 0f, W, H), new Color(0f, 0f, 0f, 0.8f));

        int stageNo = RunState.Stage + 1;
        StageDef def = Stages.All[Mathf.Clamp(RunState.Stage, 0, Stages.All.Length - 1)];
        bool hasNext = RunState.Stage < Stages.All.Length - 1;

        Color accent = new Color(0.5f, 1f, 0.6f);
        Hud.Text(new Rect(0f, H * 0.08f, W, 60f), $"AŞAMA {stageNo} TAMAM", accent, 48, TextAnchor.MiddleCenter);
        Hud.Text(new Rect(0f, H * 0.08f + 56f, W, 26f), def.title, new Color(0.85f, 0.9f, 1f), 18, TextAnchor.MiddleCenter, false);

        // Puan dokumu
        float pw = 460f, ph = 240f;
        Rect panel = new Rect((W - pw) * 0.5f, H * 0.28f, pw, ph);
        Hud.Fill(panel, new Color(0.06f, 0.09f, 0.14f, 0.96f));
        Hud.Fill(new Rect(panel.x, panel.y, pw, 5f), accent);

        int stagePoints = RunState.Score - RunState.StageStartScore - RunState.ClearBonusAccuracy - RunState.ClearBonusNoDamage;
        float ratio = RunState.StageSpawned > 0 ? (float)RunState.StageKills / RunState.StageSpawned : 1f;

        string[] names =
        {
            "Aşama puanı", $"İsabet ({RunState.StageKills}/{RunState.StageSpawned})", "Hasarsız bonus", "Toplam skor", "Hurda"
        };
        string[] values =
        {
            "+" + stagePoints.ToString("N0"),
            $"%{ratio * 100f:0}   +{RunState.ClearBonusAccuracy:N0}",
            RunState.ClearBonusNoDamage > 0 ? "+" + RunState.ClearBonusNoDamage.ToString("N0") : "yok",
            RunState.Score.ToString("N0"),
            RunUpgrades.Scrap.ToString()
        };

        for (int i = 0; i < names.Length; i++)
        {
            float ry = panel.y + 20f + i * 40f;
            bool total = i == 3;
            Hud.Text(new Rect(panel.x + 28f, ry, 240f, 30f), names[i], total ? Color.white : new Color(0.8f, 0.86f, 0.92f),
                total ? 20 : 17, TextAnchor.MiddleLeft, total);
            Hud.Text(new Rect(panel.xMax - 218f, ry, 190f, 30f), values[i],
                total ? new Color(1f, 0.9f, 0.4f) : Color.white, total ? 24 : 19, TextAnchor.MiddleRight);
        }

        // Dugmeler
        float bw = 360f, bh = 52f;
        float by = panel.yMax + 26f;
        float bx = (W - bw) * 0.5f;

        var big = new GUIStyle(GUI.skin.button) { fontSize = 19, fontStyle = FontStyle.Bold };

        if (hasNext)
        {
            StageDef next = Stages.All[RunState.Stage + 1];
            if (GUI.Button(new Rect(bx, by, bw, bh), $"SONRAKİ AŞAMA:  {next.title}", big))
            {
                Close();
                if (CombatManager.Instance != null) CombatManager.Instance.NextStage();
            }
            by += bh + 12f;
        }

        if (shop != null)
        {
            if (GUI.Button(new Rect(bx, by, bw, 44f), $"MAĞAZA   •   {RunUpgrades.Scrap} hurda", big))
                shop.Open();
            by += 44f + 12f;
        }

        if (GUI.Button(new Rect(bx, by, bw, 44f), "HANGARA DÖN  (donanımı değiştir)", big))
        {
            // Kampanya (skor, guc, bomba, hurda, upgrade) hangardan sonra kaldigi yerden surer
            PlayerHealth leaving = CombatManager.Instance != null ? CombatManager.Instance.CurrentPlayerHealth : null;
            RunState.PrepareContinue(Mathf.Min(RunState.Stage + 1, Stages.All.Length - 1),
                leaving != null ? leaving.HealthFraction : 1f);
            Close();
            SceneManager.LoadScene(GameScenes.Hangar);
        }

        Hud.Text(new Rect(0f, by + 58f, W, 22f),
            $"Sonraki aşamada (hangardan dönsen de) %{GameSettings.Current.stageClearHeal * 100f:0} onarım yapılır, kalkan ve füzeler dolar.",
            new Color(0.7f, 0.76f, 0.84f), 14, TextAnchor.MiddleCenter, false);

        Hud.End();
    }

    private void Close()
    {
        IsOpen = false;
        GameState.UiOpen = false;
        GameState.SetPaused(false);
    }
}
