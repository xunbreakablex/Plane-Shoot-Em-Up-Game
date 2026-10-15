using UnityEngine;

public enum ControlScheme
{
    Arcade,   // A/D = otomatik yatisli donus, Q/E = yatis (roll)
    Classic   // A/D = yatis, Q/E = dumen (eski duzen)
}

// Kontrol semasi tercihi (duraklat menusunden degisir, kalici). Her karede PlayerPrefs okunmasin diye onbellekli.
public static class ControlSettings
{
    private const string Key = "PlaneShootEmUp.ControlScheme";
    private static int cached = -1;

    public static ControlScheme Scheme
    {
        get
        {
            if (cached < 0) cached = Mathf.Clamp(PlayerPrefs.GetInt(Key, (int)ControlScheme.Arcade), 0, 1);
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

    public static string Label => Arcade ? "ARCADE" : "KLASİK";

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

    public static string AimLabel => AutoAim ? "OTOMATİK" : "FARE";

    public static string AimHint => AutoAim
        ? "SPACE / SOL TIK  ateş  •  imleç burnun önünde, öndeki en yakın düşmana nişan alır"
        : "FARE  nişan  •  SOL TIK / SPACE  ateş  •  imleç düşmana yakınsa kilitlenir";

    /// <summary>Ilk oyunlardaki egitim duyurusu: atesin nasil yapildigi (secili nisan moduna gore).</summary>
    public static string FireTutorial => AutoAim
        ? "SPACE basılı tut: öndeki en yakın düşmana ateş edersin"
        : "FARE ile nişan al, SOL TIK ya da SPACE ile ateş et";

    /// <summary>Kontrol ipucu satiri (A/D ve Q/E'nin ne yaptigi).</summary>
    public static string SteerHint => Arcade
        ? "A / D  sağa-sola dön     Q / E  yatış (tonoz)"
        : "A / D  yatış             Q / E  dümen";
}
