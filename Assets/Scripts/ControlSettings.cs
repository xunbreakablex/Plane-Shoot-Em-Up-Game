using UnityEngine;

public enum ControlScheme
{
    Arcade,   // A/D = otomatik yatisli donus, Q/E = yatis (roll), W/S = yunuslama
    Classic,  // A/D = yatis, Q/E = dumen (eski duzen), W/S = yunuslama
    Mouse     // Fare = yonlendirme (yukari-asagi + saga-sola), W/S = hizlan/yavasla, A/D = saga-sola kay, Space/Ctrl = yukari/asagi kay, Sag tik = zoom
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

    /// <summary>Fare semasinda fare ucagi yonettigi icin nisan her zaman otomatiktir.</summary>
    public static bool EffectiveAutoAim => AutoAim || MouseSteering;

    public static string AimLabel => MouseSteering ? "OTOMATİK (fareyle uçuş)" : (AutoAim ? "OTOMATİK" : "FARE");

    /// <summary>
    /// SPACE ates eder mi? FARE semasinda kayma acikken SPACE yukari kaymaya ayrilir (ates yalniz sol tik),
    /// yoksa yukari kayarken surekli ates edilirdi.
    /// </summary>
    public static bool SpaceFires => !(MouseSteering && GameSettings.Current.keysStrafe);

    public static string AimHint => EffectiveAutoAim
        ? (SpaceFires ? "SPACE / SOL TIK  ateş" : "SOL TIK  ateş") + "  •  imleç burnun önünde, öndeki en yakın düşmana nişan alır"
        : "FARE  nişan  •  SOL TIK / SPACE  ateş  •  imleç düşmana yakınsa kilitlenir";

    /// <summary>Ilk oyunlardaki egitim duyurusu: atesin nasil yapildigi (secili semaya gore).</summary>
    public static string FireTutorial => MouseSteering
        ? (SpaceFires
            ? "FARE ile yönlendir, W/S ile hızlan-yavaşla, SOL TIK ya da SPACE ile ateş et"
            : "FARE ile yönlendir, W/S ile hızlan-yavaşla, SOL TIK ile ateş et, SPACE / CTRL ile yukarı-aşağı kay")
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
                    return GameSettings.Current.keysStrafe
                        ? "FARE  yönlendir     W / S  hızlan / yavaşla     A / D  sağa-sola kay     SPACE / CTRL  yukarı-aşağı kay     SAĞ TIK  yakınlaştır"
                        : "FARE  yönlendir     W / S  hızlan / yavaşla     A / D  dön     SAĞ TIK  yakınlaştır";
                case ControlScheme.Arcade: return "A / D  sağa-sola dön     Q / E  yatış (tonoz)     W / S  yunuslama";
                default: return "A / D  yatış             Q / E  dümen     W / S  yunuslama";
            }
        }
    }
}
