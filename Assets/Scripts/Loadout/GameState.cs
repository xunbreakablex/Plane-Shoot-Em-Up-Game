using UnityEngine;

// Sahneler arasi paylasilan durum ve sahne isimleri.
public static class GameState
{
    public static bool Paused;

    // Magaza veya secim karti acikken (oyun durur); ESC ile duraklat menusu cakismasin
    public static bool UiOpen;

    // Oyunu durdur/surdur: zaman, ses ve imlec birlikte yonetilir
    public static void SetPaused(bool paused)
    {
        Paused = paused;
        Time.timeScale = paused ? 0f : 1f;
        AudioListener.pause = paused;

        Cursor.visible = paused;
        Cursor.lockState = paused ? CursorLockMode.None : CursorLockMode.Confined;
    }
}

public static class GameScenes
{
    public const string Hangar = "Hangar";
    public const string ClassicFlight = "SampleScene";
    public const string SpaceFlight = "SpacePrototype";

    /// <summary>Savas sahnesi: GameSettings.useSpacePrototype acikken uzay prototipi.</summary>
    public static string Flight => GameSettings.Current.useSpacePrototype ? SpaceFlight : ClassicFlight;
}
