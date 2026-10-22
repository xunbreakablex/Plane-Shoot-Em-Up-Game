using UnityEngine;
using UnityEngine.InputSystem;

// Ucaklar arasi gecis (1/2/3, Tab) ve ucus HUD'i (bilgi paneli, gaz, yapay ufuk, uyarilar, kontrol ipucu).
// Yerlesim Hud.cs'deki bolgelere gore: sol ust = ucus bilgisi, sag ust = ufuk, alt sol = ipucu.
public class FlightTestManager : MonoBehaviour
{
    public PlaneController[] planes;
    public FollowCamera followCamera;

    private int index;
    private bool helpToggled;

    // Savasin ilk saniyelerinde kontroller otomatik gosterilir
    private const float AutoHelpSeconds = 12f;

    public PlaneController Current => planes != null && planes.Length > 0 ? planes[index] : null;
    public int CurrentIndex => index;

    private void Start()
    {
        if (planes == null || planes.Length == 0) return;

        // Hangarda secilen model ile basla (acik degilse ilk ucak)
        index = Mathf.Clamp(LoadoutStore.SelectedModel, 0, planes.Length - 1);
        if (!Unlocks.OwnsAircraft(index)) index = 0;

        for (int i = 0; i < planes.Length; i++)
            planes[i].gameObject.SetActive(i == index);

        if (followCamera != null) followCamera.SetTarget(planes[index].transform);
    }

    private void Update()
    {
        if (GameState.Paused) return;

        var kb = Keyboard.current;
        if (kb == null || planes == null || planes.Length == 0) return;

        if (DevBuild.Enabled)   // savasta ucak degistirme test aracidir (yayinda ucak yalniz hangardan secilir)
        {
        if (kb.digit1Key.wasPressedThisFrame) SelectAircraft(0);
        if (kb.digit2Key.wasPressedThisFrame) SelectAircraft(1);
        if (kb.digit3Key.wasPressedThisFrame) SelectAircraft(2);
        if (kb.digit4Key.wasPressedThisFrame) SelectAircraft(3);
        if (kb.digit5Key.wasPressedThisFrame) SelectAircraft(4);
        if (kb.digit6Key.wasPressedThisFrame) SelectAircraft(5);
        if (kb.digit7Key.wasPressedThisFrame) SelectAircraft(6);
        if (kb.digit8Key.wasPressedThisFrame) SelectAircraft(7);
        if (kb.digit9Key.wasPressedThisFrame) SelectAircraft(8);
        if (kb.tabKey.wasPressedThisFrame)
        {
            // Sonraki acik ucak
            for (int step = 1; step <= planes.Length; step++)
            {
                int next = (index + step) % planes.Length;
                if (Unlocks.OwnsAircraft(next))
                {
                    Select(next);
                    break;
                }
            }
        }
        }
        if (kb.hKey.wasPressedThisFrame) helpToggled = !helpToggled;
    }

    /// <summary>Acik bir ucaga gecer; kilitliyse false doner.</summary>
    public bool SelectAircraft(int i)
    {
        if (i < 0 || i >= planes.Length || !Unlocks.OwnsAircraft(i)) return false;
        Select(i);
        return true;
    }

    private void Select(int i)
    {
        if (i < 0 || i >= planes.Length || i == index) return;

        PlaneController prev = planes[index];
        PlaneController next = planes[i];

        Vector3 pos = prev.IsCrashed ? prev.SpawnPosition : prev.transform.position;
        Quaternion rot = prev.IsCrashed ? prev.SpawnRotation : prev.transform.rotation;
        float speed = prev.IsCrashed ? next.cruiseSpeed : prev.Speed;

        next.transform.SetPositionAndRotation(pos, rot);
        next.InheritState(speed, prev.Throttle);

        prev.gameObject.SetActive(false);
        next.gameObject.SetActive(true);
        index = i;

        if (followCamera != null) followCamera.SetTarget(next.transform);
    }

    // ------------------------------------------------------------------ HUD

    private void OnGUI()
    {
        // Bu HUD yalniz cizim: Layout / fare / klavye olaylarinda calistirmak bos is ve cop uretirdi
        if (Event.current.type != EventType.Repaint) return;

        PlaneController p = Current;
        if (p == null) return;

        // Duraklat menusu, magaza ve secim kartlari acikken oyun ici HUD gizlenir
        if (GameState.Paused) return;

        Hud.Begin();
        DrawFlightPanel(p);
        DrawWarnings(p);
        DrawHelp(p);
        Hud.End();
    }

    // Her satirin metni yalniz gosterilen deger degisince yeniden kurulur (cop azaltma)
    private static readonly Hud.TextCache nameText = new Hud.TextCache();
    private static readonly Hud.TextCache speedText = new Hud.TextCache();
    private static readonly Hud.TextCache dynText = new Hud.TextCache();
    private static readonly Hud.TextCache stallText = new Hud.TextCache();
    private static readonly Hud.TextCache throttleText = new Hud.TextCache();
    private static string nameSource;

    private static void DrawFlightPanel(PlaneController p)
    {
        // Boss fazinda gaz, stall ve dikey hiz anlamsiz (W/S kapali, cerceve ucurur): yalniz ad, hiz ve irtifa
        CombatManager cm = CombatManager.Instance;
        bool rush = cm != null && cm.RushActive;
        // Kahraman (insan): AoA, stall ve G anlamsiz; yalniz ad, hiz/irtifa ve ucus gucu
        bool hero = cm != null && cm.flight != null && PlaneBuilder.IsHero(cm.flight.CurrentIndex);
        float x = Hud.Margin, y = Hud.TopY, w = 330f, h = rush ? 52f : (hero ? (ControlSettings.WsStrafe ? 52f : 72f) : 104f);
        Hud.Panel(new Rect(x, y, w, h));

        float tx = x + 10f;
        if (!ReferenceEquals(nameSource, p.displayName))
        {
            nameSource = p.displayName;
            nameText.Text = p.displayName.ToUpper();
        }
        Hud.Text(new Rect(tx, y + 5f, w - 20f, 22f), nameText.Text, new Color(1f, 0.88f, 0.4f), 16);

        float kmh = p.Speed * 3.6f;
        float alt = p.transform.position.y;
        int kmhI = Mathf.RoundToInt(kmh), altI = Mathf.RoundToInt(alt);
        if (speedText.Update(kmhI, altI)) speedText.Text = $"HIZ {kmhI} km/s     İRTİFA {altI} m";
        Hud.Text(new Rect(tx, y + 27f, w - 20f, 20f), speedText.Text, Color.white, 15);
        if (rush) return;
        if (hero)
        {
            if (ControlSettings.WsStrafe) return;
            Rect hb = new Rect(tx, y + 54f, 200f, 9f);
            Hud.Bar(hb, p.Throttle, Color.Lerp(new Color(0.6f, 0.5f, 1f), new Color(1f, 0.5f, 0.9f), p.Throttle));
            int hpI = Mathf.RoundToInt(p.Throttle * 100f);
            if (throttleText.Update(hpI)) throttleText.Text = $"UÇUŞ GÜCÜ %{hpI}";
            Hud.Text(new Rect(hb.xMax + 10f, y + 49f, 110f, 18f), throttleText.Text, Color.white, 13);
            return;
        }

        int vsI = Mathf.RoundToInt(p.VerticalSpeed * 10f), gI = Mathf.RoundToInt(p.GForce * 10f), aoaI = Mathf.RoundToInt(p.AoADeg);
        if (dynText.Update(vsI, gI, aoaI))
            dynText.Text = $"DİKEY {vsI / 10f:0.0} m/s     G {gI / 10f:0.0}     AoA {aoaI}°";
        Hud.Text(new Rect(tx, y + 48f, w - 20f, 18f), dynText.Text, new Color(0.82f, 0.88f, 0.95f), 13, TextAnchor.UpperLeft, false);

        Color state = p.IsGrounded ? new Color(0.6f, 1f, 0.6f) : new Color(0.7f, 0.85f, 1f);
        int stallI = Mathf.RoundToInt(p.StallSpeed * 3.6f);
        int groundedI = p.IsGrounded ? 1 : 0;
        if (stallText.Update(stallI, groundedI))
            stallText.Text = $"Stall hızı {stallI} km/s     {(groundedI == 1 ? "YERDE" : "HAVADA")}";
        Hud.Text(new Rect(tx, y + 66f, w - 20f, 18f), stallText.Text, state, 13, TextAnchor.UpperLeft, false);

        // Gaz (FARE semasinda kayma modunda gaz mekanigi yok: cubuk gosterilmez)
        if (ControlSettings.WsStrafe) return;
        Rect bar = new Rect(tx, y + 88f, 200f, 9f);
        Hud.Bar(bar, p.Throttle, Color.Lerp(new Color(0.3f, 0.9f, 0.35f), new Color(1f, 0.6f, 0.15f), p.Throttle));
        int thrI = Mathf.RoundToInt(p.Throttle * 100f);
        if (throttleText.Update(thrI)) throttleText.Text = $"GAZ %{thrI}";
        Hud.Text(new Rect(bar.xMax + 10f, y + 83f, 100f, 18f), throttleText.Text, Color.white, 13);
    }


    // Yapay ufuk: GL ile cizilir (HorizonGauge); roll'a gore doner, pitch'e gore kayar
    private static void DrawHorizon(PlaneController p)
    {
        float d = Hud.HorizonSize;
        var rect = new Rect(Hud.W - Hud.Margin - d, Hud.TopY, d, d);
        Transform t = p.transform;

        float pitch = Mathf.Asin(Mathf.Clamp(t.forward.y, -1f, 1f)) * Mathf.Rad2Deg;
        float roll = Mathf.Atan2(t.right.y, t.up.y) * Mathf.Rad2Deg;

        HorizonGauge.Draw(rect, pitch, roll);
    }

    private static void DrawWarnings(PlaneController p)
    {
        float cx = Hud.W * 0.5f;
        float y = Hud.H * 0.33f;

        if (p.IsCrashed)
            Hud.Text(new Rect(cx - 300f, y, 600f, 60f), "ÇARPTI!", new Color(1f, 0.3f, 0.2f), 44, TextAnchor.MiddleCenter);
        else if (p.IsStalling && !p.InRush)   // boss fazinda cerceve durunca stall uyarisi anlamsiz
        {
            CombatManager cm = CombatManager.Instance;
            bool hero = cm != null && cm.flight != null && PlaneBuilder.IsHero(cm.flight.CurrentIndex);
            Hud.Text(new Rect(cx - 300f, y, 600f, 60f), hero ? "DÜŞÜYORSUN, HIZLAN!" : "STALL!", new Color(1f, 0.3f, 0.2f), 44, TextAnchor.MiddleCenter);
        }
    }

    // Nisan moduna gore 1. satir (ESC menusunden Otomatik/Fare)
    private const string HelpAimAuto = "SPACE / SOL TIK  ateş (öndeki en yakın düşmana)     F  güdümlü füze";
    private const string HelpAimMouse = "FARE  nişan alma        SOL TIK / SPACE  ateş        F  güdümlü füze";

    // Kontrol semasina gore 2. satir (ESC menusunden Arcade/Klasik)
    private const string HelpSteerMouse = "FARE  yönlendir        W / S  ileri / geri        A / D  sağa-sola kay";
    private const string HelpSteerMouseGas = "FARE  yönlendir        W / S  hızlan / yavaşla        A / D  sağa-sola kay";
    private const string HelpKeysMouse = "SHIFT  yukarı kay       CTRL  aşağı kay        C  kamera mesafesi";
    private const string HelpKeysDefault = "SHIFT / CTRL  gaz       SPACE (yerde)  fren   C  kamera mesafesi";
    private const string HelpSteerMouseClassic = "FARE  yönlendir        W / S  hızlan / yavaşla        A / D  dön";
    // Sanal cubuk (varsayilan): imlec ortada degilken ucak doner
    private const string HelpSteerStick = "FARE  imleci it: ortada değilse döner     W / S  ileri / geri     A / D  kay";
    private const string HelpSteerStickGas = "FARE  imleci it: ortada değilse döner     W / S  hızlan / yavaşla     A / D  kay";
    private const string HelpKeysStick = "SHIFT / CTRL  yukarı-aşağı kay     ORTA TIK  imleci ortala     C  kamera";
    private const string HelpSteerArcade = "W / S  yunuslama        A / D  sağa-sola dön     Q / E  yatış";
    private const string HelpSteerClassic = "W / S  yunuslama        A / D  yatış        Q / E  dümen";

    // R/P ve 1-8/TAB test araclaridir: yalniz editor ve development build yardimda gorunur
    private static readonly string HelpAutoOn = (DevBuild.Enabled ? "R  havada başla         P  pistte başla     " : "") + "T  oto-düzeltme: AÇIK";
    private static readonly string HelpAutoOff = (DevBuild.Enabled ? "R  havada başla         P  pistte başla     " : "") + "T  oto-düzeltme: KAPALI";

    private static readonly string[] helpLines =
    {
        HelpAimAuto,
        HelpSteerArcade,
        HelpKeysDefault,
        DevBuild.Enabled ? "SAĞ TIK  yakınlaştır     1-9 / TAB  uçak değiştir (açık olanlar)" : "SAĞ TIK  yakınlaştır     B  bomba     F  füze",
        HelpAutoOn,
    };

    // Boss fazi (rush): ucus kontrolleri yerine bu satirlar; ilk satir altin renkte baslik
    private const float RushHelpSeconds = 9f;
    private static readonly string[] rushHelpKeys =
    {
        "BOSS SAVAŞI  •  W / S kapalı, boss hep önünde",
        "A / D  sağa-sola kay        SHIFT / CTRL  yukarı-aşağı kay",
        "FARE  nişan (±45°)     SPACE / SOL TIK  ateş     F  füze     B  bomba",
        "Yeşil / pembe ok: duvardaki ya da çarktaki BOŞLUK, oraya kay",
    };
    private const string HelpHeroKeys = "B  özel yetenek     Q / E  yana kaç (kısa dokunulmazlık)     SAĞ TIK  yakınlaştır";
    private static readonly string[] rushHelpHero =
    {
        "BOSS SAVAŞI  •  W / S kapalı, boss hep önünde",
        "A / D  sağa-sola kay     SHIFT / CTRL  yukarı-aşağı     Q / E  yana kaç",
        "FARE  nişan (±45°)     SPACE / SOL TIK  ateş     B  özel yetenek",
        "Yeşil / pembe ok: duvardaki ya da çarktaki BOŞLUK, oraya kay",
    };
    private static readonly string[] rushHelpPad =
    {
        "BOSS SAVAŞI  •  boss hep önünde",
        "SOL ÇUBUK  hareket        SAĞ ÇUBUK  nişan (±45°)",
        "SAĞ TETİK  ateş     X  bomba     (klavye: A/D, SHIFT/CTRL, fare)",
        "Yeşil / pembe ok: duvardaki ya da çarktaki BOŞLUK, oraya kay",
    };

    private void DrawHelp(PlaneController p)
    {
        // Boss fazina girince yardim kendiliginden acilir ve o fazin kontrollerini gosterir
        CombatManager cm = CombatManager.Instance;
        bool rush = cm != null && cm.RushActive;
        bool auto = Time.timeSinceLevelLoad < AutoHelpSeconds || (rush && cm.RushAge < RushHelpSeconds);
        bool open = helpToggled || auto;

        float baseY = Hud.H - Hud.Margin - 20f;
        Hud.Text(new Rect(Hud.Margin, baseY, 500f, 20f),
            open ? "H: kontrolleri gizle" : "H: kontroller   •   ESC: duraklat / mağaza",
            new Color(0.8f, 0.85f, 0.9f), 12, TextAnchor.UpperLeft, false);

        if (!open) return;

        string[] lines;
        bool hero = cm != null && cm.flight != null && PlaneBuilder.IsHero(cm.flight.CurrentIndex);
        if (rush)
        {
            lines = PadNav.Connected ? rushHelpPad : (hero ? rushHelpHero : rushHelpKeys);
        }
        else
        {
            // Dizi ve metinler tek sefer kurulur; yalniz kontrol semasi ve oto-duzeltme satirlari degisir
            bool strafeScheme = ControlSettings.MouseSteering && GameSettings.Current.keysStrafe;
            bool stick = ControlSettings.MouseSteering && ControlSettings.MouseStick;
            helpLines[0] = ControlSettings.EffectiveAutoAim ? HelpAimAuto : HelpAimMouse;
            helpLines[1] = ControlSettings.MouseSteering
                ? (strafeScheme
                    ? (stick ? (ControlSettings.WsStrafe ? HelpSteerStick : HelpSteerStickGas)
                             : (ControlSettings.WsStrafe ? HelpSteerMouse : HelpSteerMouseGas))
                    : HelpSteerMouseClassic)
                : (ControlSettings.Arcade ? HelpSteerArcade : HelpSteerClassic);
            helpLines[2] = strafeScheme ? (stick ? HelpKeysStick : HelpKeysMouse) : HelpKeysDefault;
            helpLines[4] = p.autoLevel ? HelpAutoOn : HelpAutoOff;
            helpLines[3] = hero ? HelpHeroKeys : (DevBuild.Enabled ? "SAĞ TIK  yakınlaştır     1-9 / TAB  uçak değiştir (açık olanlar)" : "SAĞ TIK  yakınlaştır     B  bomba     F  füze");
            lines = helpLines;
        }

        float w = 520f;
        float h = lines.Length * 19f + 14f;
        Rect panel = new Rect(Hud.Margin, baseY - h - 6f, w, h);
        Hud.Panel(panel, 0.5f);
        if (rush) Hud.Fill(new Rect(panel.x, panel.y, w, 3f), new Color(1f, 0.8f, 0.35f));

        for (int i = 0; i < lines.Length; i++)
        {
            Color c = rush && i == 0 ? new Color(1f, 0.85f, 0.45f) : Color.white;
            Hud.Text(new Rect(panel.x + 10f, panel.y + 7f + i * 19f, w - 20f, 19f), lines[i], c, 13, TextAnchor.UpperLeft, rush && i == 0);
        }
    }
}
