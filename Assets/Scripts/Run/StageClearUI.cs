using UnityEngine;
using UnityEngine.SceneManagement;

// Asama tamam ekrani: puan dokumu, sonraki asama, magaza ya da hangar. Dokum odul ani olarak oynar: satirlar sirayla
// gelir, sayilar tikirtiyla sayilir, en sonda derece harfi (S/A/B/C/D) gumleyerek basilir. Herhangi bir tus / tik /
// kumanda dugmesi sayimi atlatir; dugmeler sayim bitince cikar.
public class StageClearUI : MonoBehaviour
{
    public bool IsOpen { get; private set; }

    private const float RowDelay = 0.42f, RowCount = 0.38f, FirstRow = 0.35f;
    private const int Rows = 6;
    private static float StampAt => FirstRow + RowDelay * Rows + 0.3f;

    private float openedAt;
    private bool stamped;
    private float nextTick;
    private int skipFrame = -1;

    /// <summary>Asama derecesi 0-100: isabet (45), govde hasari (35, vurus basina -4), en iyi kombo (20, x25'te tam).</summary>
    public static float RankPoints(out float accPts, out float hullPts, out float comboPts)
    {
        float ratio = RunState.StageSpawned > 0 ? Mathf.Clamp01((float)RunState.StageKills / RunState.StageSpawned) : 1f;
        accPts = 45f * ratio;
        hullPts = Mathf.Max(0f, 35f - 4f * RunState.StageHullHits);
        comboPts = 20f * Mathf.Clamp01(RunState.StageBestCombo / 25f);
        return accPts + hullPts + comboPts;
    }

    public static string RankLetter(float pts)
    {
        if (pts >= 88f) return "S";
        if (pts >= 75f) return "A";
        if (pts >= 60f) return "B";
        if (pts >= 45f) return "C";
        return "D";
    }

    private static Color RankColor(string r)
    {
        switch (r)
        {
            case "S": return new Color(1f, 0.85f, 0.25f);
            case "A": return new Color(0.45f, 1f, 0.55f);
            case "B": return new Color(0.45f, 0.85f, 1f);
            case "C": return new Color(0.9f, 0.9f, 0.95f);
            default: return new Color(1f, 0.5f, 0.45f);
        }
    }

    private ShopUI shop;
    private int focus;
    private bool padAccept;

    // Gamepad: odaklanan dugme cerceveli; A onaylar
    private bool Btn(Rect r, string text, GUIStyle style, int index)
    {
        bool c = GUI.Button(r, text, style);
        if (index == focus) { PadNav.Highlight(r); if (padAccept) c = true; }
        if (c) GameAudio.PlayUi(Sfx.Click, 0.5f);
        return c;
    }

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
        if (cm != null && Time.unscaledTime < cm.StageClearNotBefore) return;   // boss patlamasi ve donus bitsin

        IsOpen = true;
        GameState.UiOpen = true;
        GameState.SetPaused(true);
        openedAt = Time.unscaledTime;
        stamped = false;
        nextTick = openedAt + FirstRow;
    }

    private void LateUpdate()
    {
        if (!IsOpen || (shop != null && shop.IsOpen)) return;
        float t = Time.unscaledTime - openedAt;

        // Atla: tus, tik ya da kumanda onayi sayimi bitirir (dugmeye basmis sayilmaz)
        if (t < StampAt)
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            var mouse = UnityEngine.InputSystem.Mouse.current;
            bool skip = (kb != null && kb.anyKey.wasPressedThisFrame) || (mouse != null && mouse.leftButton.wasPressedThisFrame) || PadNav.Accept;
            if (skip)
            {
                openedAt -= StampAt - t;
                t = StampAt;
                skipFrame = Time.frameCount;
            }
        }

        // Sayim tikirtisi
        if (t >= FirstRow && t < FirstRow + RowDelay * Rows && Time.unscaledTime >= nextTick)
        {
            float inRow = (t - FirstRow) % RowDelay;
            if (inRow < RowCount) GameAudio.PlayUi(Sfx.Click, 0.22f);
            nextTick = Time.unscaledTime + 0.055f;
        }

        // Derece damgasi
        if (!stamped && t >= StampAt)
        {
            stamped = true;
            string r = RankLetter(RankPoints(out _, out _, out _));
            GameAudio.PlayUi(Sfx.Crack, 0.9f);
            GameAudio.PlayUi(Sfx.Explosion, 0.35f);
            if (r == "S" || r == "A") GameAudio.PlayUi(Sfx.LevelUp, 0.7f);
        }
    }

    private void OnGUI()
    {
        if (!IsOpen) return;
        if (shop != null && shop.IsOpen) return;   // magaza kendi ekranini cizer

        Hud.Begin();
        float W = Hud.W, H = Hud.H;

        Hud.Fill(new Rect(0f, 0f, W, H), new Color(0f, 0f, 0f, 0.8f));

        int stageNo = RunState.Stage + 1;
        StageDef def = Stages.Get(RunState.Stage);
        bool hasNext = RunState.Stage < RunState.LastStage;

        Color accent = new Color(0.5f, 1f, 0.6f);
        Hud.Text(new Rect(0f, H * 0.08f, W, 60f), $"AŞAMA {stageNo} TAMAM", accent, 48, TextAnchor.MiddleCenter);
        Hud.Text(new Rect(0f, H * 0.08f + 56f, W, 26f), def.title, new Color(0.85f, 0.9f, 1f), 18, TextAnchor.MiddleCenter, false);

        // Puan dokumu: satirlar sirayla gelir, sayilar sayilir
        float t = Time.unscaledTime - openedAt;
        float pw = 460f, ph = Rows * 40f + 20f;
        float rankW = 170f;
        float px = (W - pw - rankW - 24f) * 0.5f;
        Rect panel = new Rect(px, H * 0.26f, pw, ph);
        Hud.Fill(panel, new Color(0.06f, 0.09f, 0.14f, 0.96f));
        Hud.Fill(new Rect(panel.x, panel.y, pw, 5f), accent);

        int stagePoints = RunState.Score - RunState.StageStartScore - RunState.ClearBonusAccuracy - RunState.ClearBonusNoDamage;
        float ratio = RunState.StageSpawned > 0 ? (float)RunState.StageKills / RunState.StageSpawned : 1f;
        int hull = RunState.StageHullHits;

        string[] names =
        {
            "Aşama puanı", $"İsabet ({RunState.StageKills}/{RunState.StageSpawned})", "Gövde hasarı", "En iyi kombo", "Toplam skor", "Hurda"
        };
        for (int i = 0; i < Rows; i++)
        {
            float rowT = t - (FirstRow + i * RowDelay);
            if (rowT < 0f) break;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(rowT / RowCount));
            float slide = (1f - Mathf.Clamp01(rowT / 0.15f)) * 30f;
            string value;
            switch (i)
            {
                case 0: value = "+" + Mathf.RoundToInt(stagePoints * k).ToString("N0"); break;
                case 1: value = $"%{ratio * 100f * k:0}   +{Mathf.RoundToInt(RunState.ClearBonusAccuracy * k):N0}"; break;
                case 2: value = hull == 0 ? $"HASARSIZ!  +{Mathf.RoundToInt(RunState.ClearBonusNoDamage * k):N0}" : $"{Mathf.RoundToInt(hull * k)} vuruş"; break;
                case 3: value = "x" + Mathf.RoundToInt(RunState.StageBestCombo * k); break;
                case 4: value = Mathf.RoundToInt(Mathf.Lerp(RunState.StageStartScore, RunState.Score, k)).ToString("N0"); break;
                default: value = Mathf.RoundToInt(RunUpgrades.Scrap * k).ToString(); break;
            }
            float ry = panel.y + 20f + i * 40f;
            bool total = i == 4;
            Color vc = total ? new Color(1f, 0.9f, 0.4f) : (i == 2 && hull == 0 ? new Color(0.5f, 1f, 0.6f) : Color.white);
            Hud.Text(new Rect(panel.x + 28f, ry, 240f, 30f), names[i], total ? Color.white : new Color(0.8f, 0.86f, 0.92f),
                total ? 20 : 17, TextAnchor.MiddleLeft, total);
            Hud.Text(new Rect(panel.xMax - 248f + slide, ry, 220f, 30f), value, vc, total ? 24 : 19, TextAnchor.MiddleRight);
        }

        // Derece: sayim bitince damga gibi buyukten kuculerek basilir
        DrawRank(new Rect(panel.xMax + 24f, panel.y, rankW, ph), t);

        // Dugmeler: sayim bitince
        if (t < StampAt + 0.25f)
        {
            Hud.Text(new Rect(0f, panel.yMax + 30f, W, 22f), "atlamak için bir tuşa bas", new Color(0.6f, 0.66f, 0.74f), 14, TextAnchor.MiddleCenter, false);
            Hud.End();
            return;
        }
        float bw = 360f, bh = 52f;
        float by = panel.yMax + 26f;
        float bx = (W - bw) * 0.5f;

        var big = new GUIStyle(GUI.skin.button) { fontSize = 19, fontStyle = FontStyle.Bold };

        // Gamepad: 0 sonraki asama, 1 magaza, 2 hangar (olmayanlar atlanir)
        int count = (hasNext ? 1 : 0) + (shop != null ? 1 : 0) + 1;
        focus = PadNav.Move(focus, count);
        padAccept = PadNav.Accept && Time.frameCount != skipFrame;
        int slot = 0;

        if (hasNext)
        {
            StageDef next = Stages.Get(RunState.Stage + 1);
            if (Btn(new Rect(bx, by, bw, bh), $"SONRAKİ AŞAMA:  {next.title}", big, slot++))
            {
                Close();
                if (CombatManager.Instance != null) CombatManager.Instance.NextStage();
            }
            by += bh + 12f;
        }

        if (shop != null)
        {
            if (Btn(new Rect(bx, by, bw, 44f), $"MAĞAZA   •   {RunUpgrades.Scrap} hurda", big, slot++))
                shop.Open();
            by += 44f + 12f;
        }

        if (Btn(new Rect(bx, by, bw, 44f), "HANGARA DÖN  (donanımı değiştir)", big, slot))
        {
            // Kampanya (skor, guc, bomba, hurda, upgrade) hangardan sonra kaldigi yerden surer
            PlayerHealth leaving = CombatManager.Instance != null ? CombatManager.Instance.CurrentPlayerHealth : null;
            RunState.PrepareContinue(Mathf.Min(RunState.Stage + 1, RunState.LastStage),
                leaving != null ? leaving.HealthFraction : 1f);
            Close();
            SceneManager.LoadScene(GameScenes.Hangar);
        }

        Hud.Text(new Rect(0f, by + 58f, W, 22f),
            $"Sonraki aşamada (hangardan dönsen de) %{GameSettings.Current.stageClearHeal * 100f:0} onarım yapılır, kalkan ve füzeler dolar.",
            new Color(0.7f, 0.76f, 0.84f), 14, TextAnchor.MiddleCenter, false);

        Hud.End();
    }

    private void DrawRank(Rect r, float t)
    {
        Hud.Fill(r, new Color(0.06f, 0.09f, 0.14f, 0.96f));
        Hud.Text(new Rect(r.x, r.y + 10f, r.width, 22f), "DERECE", new Color(0.75f, 0.8f, 0.88f), 15, TextAnchor.MiddleCenter);
        float st = t - StampAt;
        if (st < 0f)
        {
            Hud.Text(new Rect(r.x, r.y + 60f, r.width, 100f), "?", new Color(1f, 1f, 1f, 0.25f), 80, TextAnchor.MiddleCenter);
            return;
        }

        float pts = RankPoints(out float acc, out float hullPts, out float combo);
        string letter = RankLetter(pts);
        Color c = RankColor(letter);
        Hud.Fill(new Rect(r.x, r.y, r.width, 5f), c);

        // Damga: 2,6x'ten 1x'e 0,16 sn'de; S'de parilti
        float k = Mathf.Clamp01(st / 0.16f);
        float scale = Mathf.Lerp(2.6f, 1f, k * k);
        int size = Mathf.RoundToInt(110f * scale);
        float alpha = Mathf.Clamp01(st / 0.08f);
        if (letter == "S")
        {
            float glow = 0.5f + 0.5f * Mathf.Sin(t * 6f);
            Hud.Fill(new Rect(r.x + 10f, r.y + 44f, r.width - 20f, 130f), new Color(c.r, c.g, c.b, 0.12f + 0.1f * glow));
        }
        Hud.Text(new Rect(r.x - 200f, r.y + 110f - size * 0.65f, r.width + 400f, size * 1.3f), letter, new Color(c.r, c.g, c.b, alpha), size, TextAnchor.MiddleCenter);

        // Altta: puan ve bir sonraki derece icin ipucu
        Hud.Text(new Rect(r.x, r.y + 180f, r.width, 20f), $"{pts:0} / 100", Color.white, 15, TextAnchor.MiddleCenter);
        string tip;
        if (letter == "S") tip = "Kusursuz uçuş!";
        else if (hullPts < 20f) tip = "Daha az hasar al";
        else if (acc < 38f) tip = "Daha çok düşman indir";
        else tip = "Kombonu uzat";
        Hud.Text(new Rect(r.x + 6f, r.y + 204f, r.width - 12f, 40f), tip, new Color(0.75f, 0.82f, 0.9f), 13, TextAnchor.UpperCenter, false);
    }

    private void Close()
    {
        IsOpen = false;
        GameState.UiOpen = false;
        GameState.SetPaused(false);
    }
}
