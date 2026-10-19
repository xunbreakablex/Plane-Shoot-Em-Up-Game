using UnityEngine;
using UnityEngine.InputSystem;

// Gamepad ile menu gezinme (IMGUI menuler icin): odak satiri dpad / sol cubukla asagi-yukari gezer, A onaylar,
// B geri gider, sol-sag sliderlari ayarlar. Her menu kendi odak indeksini bu siniftan okur; cizim sirasinda
// odaklanan dugmenin cevresine cerceve cizilir. Fare kullanilinca odak gorunumu kalir ama tiklama fareyle olur.
public static class PadNav
{
    private const float RepeatFirst = 0.35f, RepeatNext = 0.12f;
    private static float nextVertical, nextHorizontal;
    private static int lastFrame = -1;
    private static int vertical, horizontal;
    private static bool accept, back;
    private static bool prevSouth, prevEast;   // kendi kenar algilamasi (wasPressedThisFrame kare adimlamada guvenilmez)

    // Okuyan tuketir: OnGUI karede birden cok kez (Layout + Repaint) cagrilir, giris bir kez islensin
    /// <summary>Bu karede dikey hareket (-1 yukari, +1 asagi), tekrar araligiyla.</summary>
    public static int Vertical { get { Poll(); int v = vertical; vertical = 0; return v; } }
    public static int Horizontal { get { Poll(); int v = horizontal; horizontal = 0; return v; } }
    public static bool Accept { get { Poll(); bool v = accept; accept = false; return v; } }
    public static bool Back { get { Poll(); bool v = back; back = false; return v; } }
    public static bool Connected => Gamepad.current != null;

    private static void Poll()
    {
        if (lastFrame == Time.frameCount) return;
        lastFrame = Time.frameCount;
        vertical = horizontal = 0;
        accept = back = false;

        Gamepad g = Gamepad.current;
        if (g == null) return;

        Vector2 stick = g.leftStick.ReadValue();
        float vy = -stick.y + (g.dpad.down.isPressed ? 1f : 0f) - (g.dpad.up.isPressed ? 1f : 0f);
        float hx = stick.x + (g.dpad.right.isPressed ? 1f : 0f) - (g.dpad.left.isPressed ? 1f : 0f);
        float t = Time.unscaledTime;

        if (Mathf.Abs(vy) > 0.5f)
        {
            if (t >= nextVertical) { vertical = vy > 0f ? 1 : -1; nextVertical = t + (nextVertical > 0f && t - nextVertical < RepeatNext * 2f ? RepeatNext : RepeatFirst); }
        }
        else nextVertical = 0f;

        if (Mathf.Abs(hx) > 0.5f)
        {
            if (t >= nextHorizontal) { horizontal = hx > 0f ? 1 : -1; nextHorizontal = t + (nextHorizontal > 0f && t - nextHorizontal < RepeatNext * 2f ? RepeatNext : RepeatFirst); }
        }
        else nextHorizontal = 0f;

        bool south = g.buttonSouth.isPressed, east = g.buttonEast.isPressed;
        accept = south && !prevSouth;
        back = east && !prevEast;
        prevSouth = south;
        prevEast = east;
    }

    /// <summary>Odak indeksini dikey girise gore ilerletir (kapali satirlar atlanir).</summary>
    public static int Move(int focus, int count, System.Func<int, bool> enabled = null)
    {
        int dir = Vertical;
        if (dir == 0 || count <= 0) return Mathf.Clamp(focus, 0, count - 1);
        for (int step = 0; step < count; step++)
        {
            focus = (focus + dir + count) % count;
            if (enabled == null || enabled(focus)) break;
        }
        GameAudio.PlayUi(Sfx.Click, 0.25f);
        return focus;
    }

    /// <summary>Odaklanan satirin cevresine cerceve cizer (yalniz gamepad bagliysa).</summary>
    public static void Highlight(Rect r)
    {
        if (!Connected) return;
        float pulse = 0.7f + 0.3f * Mathf.Sin(Time.unscaledTime * 6f);
        Hud.Outline(new Rect(r.x - 4f, r.y - 4f, r.width + 8f, r.height + 8f), new Color(1f, 0.85f, 0.3f, pulse), 3f);
    }
}
