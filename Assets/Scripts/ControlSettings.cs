using UnityEngine;

public enum ControlScheme
{
    Arcade,   // A/D = otomatik yatisli donus, Q/E = yatis (roll), W/S = yunuslama
    Classic,  // A/D = yatis, Q/E = dumen (eski duzen), W/S = yunuslama
    Mouse     // Fare = yonlendirme (yukari-asagi + saga-sola), W/S = hizlan/yavasla (gaz), A/D = saga-sola kay, Shift/Ctrl = yukari/asagi kay, Sag tik = zoom
}

// Kontrol semasi tercihi (duraklat menusunden degisir, kalici). Her karede PlayerPrefs okunmasin diye onbellekli.
public static class ControlSettings
{
    private const string Key = "PlaneShootEmUp.ControlScheme";
    private static int cached = -1;

    // Not: enum degerleri PlayerPrefs'e sayi olarak yazilir; yeni sema sona eklenir (eski kayitlar bozulmaz)
    public static ControlScheme Scheme
    {
        get
        {
            if (cached < 0) cached = Mathf.Clamp(PlayerPrefs.GetInt(Key, (int)ControlScheme.Mouse), 0, 2);
            return (ControlScheme)cached;
        }
        set
        {
            cached = (int)value;
            PlayerPrefs.SetInt(Key, cached);
            PlayerPrefs.Save();
        }
    }

    public static bool Arcade => Scheme == ControlScheme.Arcade;
    public static bool MouseSteering => Scheme == ControlScheme.Mouse;

    /// <summary>A/D otomatik yatisli donus, Q/E yatis mi (Arcade ve Fare semalarinda)?</summary>
    public static bool ArcadeSteering => Scheme != ControlScheme.Classic;

    /// <summary>FARE semasinda W/S ileri-geri KAYAR ve gaz mekanigi yok (GameSettings.wsForwardStrafe; varsayilan kapali).</summary>
    public static bool WsStrafe => MouseSteering && GameSettings.Current.keysStrafe && GameSettings.Current.wsForwardStrafe;

    /// <summary>Duraklat menusunde siradaki sema: Fare -> Arcade -> Klasik -> Fare.</summary>
    public static ControlScheme Next(ControlScheme s)
    {
        switch (s)
        {
            case ControlScheme.Mouse: return ControlScheme.Arcade;
            case ControlScheme.Arcade: return ControlScheme.Classic;
            default: return ControlScheme.Mouse;
        }
    }

    public static string Label
    {
        get
        {
            switch (Scheme)
            {
                case ControlScheme.Mouse: return "FARE";
                case ControlScheme.Arcade: return "ARCADE";
                default: return "KLASİK";
            }
        }
    }

    // ---- Nisan: Otomatik (imlec burnun onunde, en yakin dusmana ates) / Fare (imlecle nisan)
    private const string AimKey = "PlaneShootEmUp.AimMode";
    private static int aimCached = -1;

    /// <summary>true: imlec burnun onunde sabit, ates edince ondeki en yakin dusmana nisan alinir.</summary>
    public static bool AutoAim
    {
        get
        {
            if (aimCached < 0) aimCached = PlayerPrefs.GetInt(AimKey, 1) != 0 ? 1 : 0;
            return aimCached == 1;
        }
        set
        {
            aimCached = value ? 1 : 0;
            PlayerPrefs.SetInt(AimKey, aimCached);
            PlayerPrefs.Save();
        }
    }

    // ---- FARE semasinda fare: sanal cubuk (Battlefront 2: imlec ortalanmadikca ucak doner) / dogrudan (FPS gibi)
    private const string MouseModeKey = "PlaneShootEmUp.MouseMode";
    private static int mouseModeCached = -1;

    /// <summary>true (varsayilan): fare ekrandaki imleci iter, imlec merkezde degilse ucak o yone surekli doner.
    /// false: fare burnu dogrudan cevirir (FPS gibi).</summary>
    public static bool MouseStick
    {
        get
        {
            if (mouseModeCached < 0) mouseModeCached = PlayerPrefs.GetInt(MouseModeKey, 1) != 0 ? 1 : 0;
            return mouseModeCached == 1;
        }
        set
        {
            mouseModeCached = value ? 1 : 0;
            PlayerPrefs.SetInt(MouseModeKey, mouseModeCached);
            PlayerPrefs.Save();
        }
    }

    public static string MouseModeLabel => MouseStick ? "SANAL ÇUBUK" : "DOĞRUDAN (FPS)";

    /// <summary>Fare semasinda fare ucagi yonettigi icin nisan her zaman otomatiktir.</summary>
    public static bool EffectiveAutoAim => AutoAim || MouseSteering;

    public static string AimLabel => MouseSteering ? "OTOMATİK (fareyle uçuş)" : (AutoAim ? "OTOMATİK" : "FARE");

    public static string AimHint => EffectiveAutoAim
        ? "SPACE / SOL TIK  ateş  •  imleç burnun önünde, öndeki en yakın düşmana nişan alır"
        : "FARE  nişan  •  SOL TIK / SPACE  ateş  •  imleç düşmana yakınsa kilitlenir";

    /// <summary>Ilk oyunlardaki egitim duyurusu: atesin nasil yapildigi (secili semaya gore).</summary>
    public static string FireTutorial => MouseSteering
        ? (MouseStick
            ? "FAREYİ İT: imleç ortada değilken uçak o yöne döner (ortala = düz uç)  •  SHIFT hızlan, CTRL yavaşla  •  A/D W/S kay  •  SOL TIK / SPACE ateş"
            : "FARE ile yönlendir, SHIFT ile hızlan, CTRL ile yavaşla, A/D W/S ile kay, SOL TIK ya da SPACE ile ateş et")
        : AutoAim
            ? "SPACE basılı tut: öndeki en yakın düşmana ateş edersin"
            : "FARE ile nişan al, SOL TIK ya da SPACE ile ateş et";

    /// <summary>Kontrol ipucu satiri (secili semada tuslarin ne yaptigi).</summary>
    public static string SteerHint
    {
        get
        {
            switch (Scheme)
            {
                case ControlScheme.Mouse:
                {
                    string mouse = MouseStick ? "FARE  imleci it (ortada değilse döner)" : "FARE  yönlendir";
                    if (!GameSettings.Current.keysStrafe)
                        return mouse + "     SHIFT / CTRL  hızlan / yavaşla     A / D  dön     SAĞ TIK  yakınlaştır";
                    return mouse + (GameSettings.Current.wsForwardStrafe ? "     SHIFT / CTRL  ileri / geri" : "     SHIFT / CTRL  hızlan / yavaşla")
                        + "     A / D  sağa-sola kay     W / S  yukarı-aşağı kay     SAĞ TIK  yakınlaştır";
                }
                case ControlScheme.Arcade: return "A / D  sağa-sola dön     Q / E  yatış (tonoz)     W / S  yunuslama";
                default: return "A / D  yatış             Q / E  dümen     W / S  yunuslama";
            }
        }
    }

    /// <summary>Bomba tusu: klavyede B; kol bagliysa sol yuz dugmesi (Xbox X) de yazilir.</summary>
    public static string BombKey => PadNav.Connected ? "B / X" : "B";
    public static string BombLabel => PadNav.Connected ? "BOMBA [B/X]" : "BOMBA [B]";

    /// <summary>Kahraman kacisi: FARE semasinda Q / E; Arcade ve Klasik'te Q/E yatis / dumen oldugu icin Z / X.</summary>
    public static string DashKeys => MouseSteering ? "Q / E" : "Z / X";
    public static string DashKeysShort => MouseSteering ? "[Q/E]" : "[Z/X]";
}
