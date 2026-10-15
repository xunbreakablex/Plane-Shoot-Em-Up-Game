using UnityEngine;

// Mağaza: hurda ile upgrade ve tek seferlik tuketim al. Duraklat menusunden acilir.
public class ShopUI : MonoBehaviour
{
    public bool IsOpen { get; private set; }

    private Vector2 scroll;
    private string message = "";
    private float messageUntil;

    private GUIStyle title, section, label, small, button, pips;

    public void Open()
    {
        IsOpen = true;
        GameState.UiOpen = true;
        scroll = Vector2.zero;
        message = "";
    }

    public void Close()
    {
        IsOpen = false;
        GameState.UiOpen = false;
    }

    private void OnDisable()
    {
        if (IsOpen) Close();
    }

    private static float UiScale()
    {
        return Mathf.Clamp(Mathf.Min(Screen.width / 1280f, Screen.height / 720f), 0.6f, 2.2f);
    }

    private void EnsureStyles()
    {
        if (title != null) return;

        title = new GUIStyle(GUI.skin.label) { fontSize = 28, fontStyle = FontStyle.Bold };
        title.normal.textColor = Color.white;

        section = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold };
        section.normal.textColor = new Color(1f, 0.82f, 0.3f);

        label = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold };
        label.normal.textColor = Color.white;

        small = new GUIStyle(GUI.skin.label) { fontSize = 13, wordWrap = true };
        small.normal.textColor = new Color(0.8f, 0.85f, 0.9f);

        pips = new GUIStyle(label) { alignment = TextAnchor.MiddleCenter, fontSize = 14 };
        pips.normal.textColor = new Color(0.6f, 0.9f, 1f);

        button = new GUIStyle(GUI.skin.button) { fontSize = 15, fontStyle = FontStyle.Bold };
    }

    private void OnGUI()
    {
        if (!IsOpen) return;
        EnsureStyles();

        float s = UiScale();
        GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1f));
        float W = Screen.width / s;
        float H = Screen.height / s;

        GUI.color = new Color(0f, 0f, 0f, 0.7f);
        GUI.DrawTexture(new Rect(0, 0, W, H), Texture2D.whiteTexture);

        Rect panel = new Rect(W * 0.5f - 470f, 24f, 940f, H - 48f);
        GUI.color = new Color(0.06f, 0.08f, 0.12f, 0.96f);
        GUI.DrawTexture(panel, Texture2D.whiteTexture);
        GUI.color = Color.white;

        GUILayout.BeginArea(new Rect(panel.x + 18f, panel.y + 12f, panel.width - 36f, panel.height - 24f));

        GUILayout.BeginHorizontal();
        GUILayout.Label("MAĞAZA", title);
        GUILayout.FlexibleSpace();
        var scrapStyle = new GUIStyle(title) { alignment = TextAnchor.MiddleRight };
        scrapStyle.normal.textColor = new Color(1f, 0.85f, 0.3f);
        GUILayout.Label($"HURDA: {RunUpgrades.Scrap}", scrapStyle);
        GUILayout.EndHorizontal();

        GUILayout.Label("Düşman uçaklardan kazandığın hurda ile bu savaşın upgrade'lerini al. Savaş bitince sıfırlanır.", small);
        GUILayout.Space(6f);

        scroll = GUILayout.BeginScrollView(scroll, false, false, GUIStyle.none, GUI.skin.verticalScrollbar,
            GUILayout.Height(panel.height - 170f));

        for (int c = 0; c < UpgradeCatalog.CategoryNames.Length; c++)
        {
            GUILayout.Label(UpgradeCatalog.CategoryNames[c], section);
            foreach (var def in UpgradeCatalog.All)
            {
                if ((int)def.category != c) continue;
                DrawUpgradeRow(def);
            }
            GUILayout.Space(8f);
        }

        GUILayout.Label("TEK SEFERLİK", section);
        foreach (Consumable item in new[] { Consumable.Repair, Consumable.ShieldCharge, Consumable.MissileRefill })
            DrawConsumableRow(item);

        GUILayout.EndScrollView();

        GUILayout.FlexibleSpace();

        if (!string.IsNullOrEmpty(message) && Time.unscaledTime < messageUntil)
        {
            var msg = new GUIStyle(label) { alignment = TextAnchor.MiddleCenter };
            msg.normal.textColor = new Color(0.6f, 1f, 0.6f);
            GUILayout.Label(message, msg);
        }

        if (GUILayout.Button("GERİ  (ESC)", button, GUILayout.Height(40f)))
            Close();

        GUILayout.EndArea();
        GUI.matrix = Matrix4x4.identity;
    }

    private bool IsRelevant(UpgradeDef def)
    {
        FlightTestManager flight = CombatManager.Instance != null ? CombatManager.Instance.flight : null;
        if (flight == null) return true;

        LoadoutStats st = LoadoutMath.Compute(LoadoutStore.Get(flight.CurrentIndex));
        if (def.requires == UpgradeRequires.Missile) return st.missileLaunchers > 0;
        if (def.requires == UpgradeRequires.Turret) return st.turretCount > 0;
        return true;
    }

    private void DrawUpgradeRow(UpgradeDef def)
    {
        int level = RunUpgrades.Level(def.id);
        bool max = level >= def.maxLevel;
        int cost = RunUpgrades.Cost(def.id);
        bool relevant = IsRelevant(def);

        GUILayout.BeginHorizontal();
        GUILayout.Label(def.name, label, GUILayout.Width(210f));
        GUILayout.Label($"Sv {level}/{def.maxLevel}", pips, GUILayout.Width(80f));
        GUILayout.Label(def.description + (relevant ? "" : "  (bu uçakta takılı değil)"), small, GUILayout.Width(450f));

        GUI.enabled = !max && RunUpgrades.Scrap >= cost;
        string text = max ? "MAKS" : $"AL  {cost}";
        if (GUILayout.Button(text, button, GUILayout.Width(110f), GUILayout.Height(30f)))
        {
            if (RunUpgrades.TryBuy(def.id)) Say($"{def.name} Sv {RunUpgrades.Level(def.id)} alındı.");
        }
        GUI.enabled = true;
        GUILayout.EndHorizontal();
    }

    private void DrawConsumableRow(Consumable item)
    {
        int cost = RunUpgrades.ConsumableCost(item);

        GUILayout.BeginHorizontal();
        GUILayout.Label(UpgradeCatalog.ConsumableName(item), label, GUILayout.Width(210f));
        GUILayout.Label("", GUILayout.Width(80f));
        GUILayout.Label(UpgradeCatalog.ConsumableDescription(item), small, GUILayout.Width(450f));

        GUI.enabled = RunUpgrades.Scrap >= cost;
        if (GUILayout.Button($"AL  {cost}", button, GUILayout.Width(110f), GUILayout.Height(30f)))
        {
            RunUpgrades.TryBuyConsumable(item, out string msg);
            Say(msg);
        }
        GUI.enabled = true;
        GUILayout.EndHorizontal();
    }

    private void Say(string text)
    {
        message = text;
        messageUntil = Time.unscaledTime + 2.2f;
    }
}
