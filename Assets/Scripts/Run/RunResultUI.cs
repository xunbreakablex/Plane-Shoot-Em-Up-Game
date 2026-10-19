using UnityEngine;
using UnityEngine.SceneManagement;

// Oyun bitince (zafer ya da yenilgi) sonuc ekrani: skor, rekor, istatistikler; tekrar oyna / hangara don.
public class RunResultUI : MonoBehaviour
{
    public bool IsOpen { get; private set; }

    private void Update()
    {
        if (IsOpen || !RunState.Ended) return;

        // Patlamayi gorsun diye kisa bir bekleme (zaferde daha uzun)
        float wait = RunState.Phase == RunPhase.Won ? 3f : 1.8f;
        if (Time.unscaledTime - RunState.EndedAt < wait) return;

        IsOpen = true;
        GameState.UiOpen = true;
        GameState.SetPaused(true);

        // Rekor/zafer sartiyla acilan ucak ya da slotlar
        unlockLines = Unlocks.TakeMessages();
    }

    private System.Collections.Generic.List<string> unlockLines = new System.Collections.Generic.List<string>();

    private void OnDestroy()
    {
        IsOpen = false;
    }

    private void OnGUI()
    {
        if (!IsOpen) return;

        bool won = RunState.Phase == RunPhase.Won;
        Hud.Begin();
        float W = Hud.W, H = Hud.H;

        Hud.Fill(new Rect(0f, 0f, W, H), new Color(0f, 0f, 0f, 0.8f));

        Color accent = won ? new Color(1f, 0.88f, 0.3f) : new Color(1f, 0.35f, 0.3f);
        Hud.Text(new Rect(0f, H * 0.08f, W, 70f), won ? "ZAFER!" : "UÇAĞIN DÜŞTÜ", accent, 56, TextAnchor.MiddleCenter);

        string sub = won
            ? (RunState.Rush ? "Tüm bosslar yenildi: Boss Rush tamam." : "Kızıl Komutan'ı yendin ve operasyonu tamamladın.")
            : (RunState.Endless ? $"Sonsuz Savaş'ta {RunState.Stage + 1}. aşamada düştün." : $"Aşama {RunState.Stage + 1} / {RunState.StageTotal} içinde düştün.");
        Hud.Text(new Rect(0f, H * 0.08f + 64f, W, 28f), sub, new Color(0.85f, 0.9f, 0.95f), 18, TextAnchor.MiddleCenter, false);

        if (RunState.NewRecord)
            Hud.Text(new Rect(0f, H * 0.08f + 94f, W, 26f), "YENİ REKOR!", new Color(0.5f, 1f, 0.6f), 20, TextAnchor.MiddleCenter);

        // Istatistik paneli
        float pw = 460f, ph = 270f;
        Rect panel = new Rect((W - pw) * 0.5f, H * 0.34f, pw, ph);
        Hud.Fill(panel, new Color(0.06f, 0.09f, 0.14f, 0.96f));
        Hud.Fill(new Rect(panel.x, panel.y, pw, 5f), accent);

        string[] names =
        {
            "Skor", "Rekor", "Ulaşılan aşama", "Öldürme", "En iyi kombo", "Yenilen boss", "Süre", "Kazanılan hurda"
        };
        string[] values =
        {
            RunState.Score.ToString("N0"),
            RunState.HighScore.ToString("N0"),
            RunState.Endless ? $"{RunState.Stage + 1} (sonsuz)" : $"{RunState.Stage + 1} / {RunState.StageTotal}",
            RunState.Kills.ToString(),
            "x" + RunState.BestCombo,
            RunState.BossKills.ToString(),
            RunState.FormatTime(RunState.Elapsed),
            RunUpgrades.TotalEarned.ToString()
        };

        for (int i = 0; i < names.Length; i++)
        {
            float ry = panel.y + 20f + i * 30f;
            Hud.Text(new Rect(panel.x + 28f, ry, 240f, 26f), names[i], new Color(0.8f, 0.86f, 0.92f), 17, TextAnchor.MiddleLeft, false);
            Hud.Text(new Rect(panel.xMax - 218f, ry, 190f, 26f), values[i], i == 0 ? new Color(1f, 0.9f, 0.4f) : Color.white,
                i == 0 ? 22 : 19, TextAnchor.MiddleRight);
        }

        // Yeni acilanlar ve bu oyunda kazanilan madalyalar
        float ly = panel.yMax + 10f;
        if (Medals.EarnedThisRun.Count > 0)
        {
            Hud.Text(new Rect(0f, ly, W, 22f), "MADALYA: " + string.Join("  •  ", Medals.EarnedThisRun), new Color(1f, 0.85f, 0.3f), 16, TextAnchor.MiddleCenter);
            ly += 24f;
        }
        foreach (string line in unlockLines)
        {
            Hud.Text(new Rect(0f, ly, W, 22f), line, new Color(0.5f, 1f, 0.7f), 16, TextAnchor.MiddleCenter);
            ly += 22f;
        }

        // Dugmeler
        float bw = 240f, bh = 52f, by = ly + 16f;
        var big = new GUIStyle(GUI.skin.button) { fontSize = 20, fontStyle = FontStyle.Bold };

        if (GUI.Button(new Rect(W * 0.5f - bw - 12f, by, bw, bh), "TEKRAR OYNA", big))
            Leave(GameScenes.Flight);

        if (GUI.Button(new Rect(W * 0.5f + 12f, by, bw, bh), "HANGARA DÖN", big))
            Leave(GameScenes.Hangar);

        if (won && !RunState.Rush)
        {
            // Zafer: kampanya bitti ama ayni oyuna Sonsuz Savas'ta devam edilebilir (skor ve donanim korunur)
            by += bh + 12f;
            if (GUI.Button(new Rect(W * 0.5f - bw - 12f, by, bw * 2f + 24f, bh), "SONSUZ SAVAŞA DEVAM ET", big))
            {
                IsOpen = false;
                GameState.UiOpen = false;
                GameState.SetPaused(false);
                RunState.ResumeAsEndless();
                return;
            }
        }

        Hud.Text(new Rect(0f, by + bh + 14f, W, 22f),
            won && !RunState.Rush ? "Sonsuz Savaş: aşamalar tekrar eder, düşmanlar ve bosslar her turda güçlenir." : "Hangarda donanımını değiştirip daha güçlü bir uçakla tekrar dene.",
            new Color(0.7f, 0.76f, 0.84f), 14, TextAnchor.MiddleCenter, false);

        Hud.End();
    }

    private void Leave(string scene)
    {
        IsOpen = false;
        GameState.UiOpen = false;
        GameState.SetPaused(false);
        SceneManager.LoadScene(scene);
    }
}
