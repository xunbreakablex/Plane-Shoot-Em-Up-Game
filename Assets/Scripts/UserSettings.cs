using UnityEngine;

// Oyuncunun kalici tercihleri (PlayerPrefs). GameSettings (denge) ile karismasin: bunlar oyuncuya aittir
// ve ayarlar panelinden degisir. Ses seviyeleri GameAudio'dadir.
public static class UserSettings
{
    private const string SensKey = "PlaneShootEmUp.Pref.MouseSens";
    private const string InvertKey = "PlaneShootEmUp.Pref.InvertY";
    private const string ShakeKey = "PlaneShootEmUp.Pref.Shake";
    private const string DistKey = "PlaneShootEmUp.Pref.CamDist";
    private const string SpeedFxKey = "PlaneShootEmUp.Pref.SpeedFx";

    private static float sens = -1f, shake = -1f, dist = -1f;
    private static int invert = -1, speedFx = -1;

    /// <summary>Hiz cizgileri ve hareket bulanikligi (hiz hissi) acik mi.</summary>
    public static bool SpeedFx
    {
        get { if (speedFx < 0) speedFx = PlayerPrefs.GetInt(SpeedFxKey, 1); return speedFx == 1; }
        set { speedFx = value ? 1 : 0; PlayerPrefs.SetInt(SpeedFxKey, speedFx); }
    }

    /// <summary>Fare hassasiyeti carpani (GameSettings.mouseSensitivity ile carpilir).</summary>
    public static float MouseSens
    {
        get { if (sens < 0f) sens = PlayerPrefs.GetFloat(SensKey, 1f); return sens; }
        set { sens = Mathf.Clamp(value, 0.3f, 3f); PlayerPrefs.SetFloat(SensKey, sens); }
    }

    /// <summary>Oyuncunun tercihi: fare yukari = burun asagi (GameSettings.invertMouseY ile birlikte tersine cevirir).</summary>
    public static bool InvertY
    {
        get { if (invert < 0) invert = PlayerPrefs.GetInt(InvertKey, 0); return invert == 1; }
        set { invert = value ? 1 : 0; PlayerPrefs.SetInt(InvertKey, invert); }
    }

    /// <summary>Kamera sarsintisi siddeti 0..1.</summary>
    public static float Shake
    {
        get { if (shake < 0f) shake = PlayerPrefs.GetFloat(ShakeKey, 1f); return shake; }
        set { shake = Mathf.Clamp01(value); PlayerPrefs.SetFloat(ShakeKey, shake); }
    }

    /// <summary>Takip kamerasi mesafe carpani (1 = eski mesafe).</summary>
    public static float CameraDistance
    {
        get { if (dist < 0f) dist = PlayerPrefs.GetFloat(DistKey, 1.15f); return dist; }
        set { dist = Mathf.Clamp(value, 0.8f, 1.7f); PlayerPrefs.SetFloat(DistKey, dist); }
    }

    public static void Save() { PlayerPrefs.Save(); }
}
