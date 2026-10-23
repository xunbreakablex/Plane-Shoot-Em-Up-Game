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

    // ---- Uzamsal gezinme: serbest yerlesimli ekranlar (hangar, magaza) satir sirasiyla gezilemez. Ekran OnGUI'nin her
    // gecisinin basinda BeginScreen cagirir, her dugmesini Item ile kaydeder; dpad / sol cubuk odagi o yondeki en
    // yakin dugmeye tasir (gecen karenin dugme konumlarina gore), A odaktaki dugmeye basar. Konumlar ekran
    // koordinatinda tutulur: kaydirma alanindaki ve disindaki dugmeler ayni duzlemde karsilastirilir.
    private static readonly System.Collections.Generic.List<Rect> spatialRects = new System.Collections.Generic.List<Rect>();
    private static readonly System.Collections.Generic.List<Rect> spatialPrev = new System.Collections.Generic.List<Rect>();
    private static int spatialFocus = -1, spatialIndex, spatialFrame = -1, spatialRectsFrame = -1;
    private static bool spatialAccept;

    /// <summary>Son Item cagrisindaki dugme odakta mi (magaza odaklanan satiri gorunur alana kaydirir).</summary>
    public static bool LastItemFocused { get; private set; }

    /// <summary>Uzamsal ekranin her OnGUI gecisinin basinda: karede bir kez girisi okur ve odagi tasir.</summary>
    public static void BeginScreen()
    {
        spatialIndex = 0;
        if (spatialFrame == Time.frameCount) return;
        spatialFrame = Time.frameCount;

        // Gecen karede cizilmediyse (baska ekrandan gelindi) eski konumlar gecersiz
        bool continuous = spatialRectsFrame == Time.frameCount - 1;
        spatialPrev.Clear();
        if (continuous) spatialPrev.AddRange(spatialRects);
        else spatialFocus = -1;
        spatialRects.Clear();

        spatialAccept = false;
        if (!Connected) return;
        Poll();
        int v = vertical, h = horizontal;
        bool a = accept;
        vertical = horizontal = 0;
        accept = false;
        if (spatialPrev.Count == 0) return;

        if (spatialFocus < 0 || spatialFocus >= spatialPrev.Count)
        {
            // Ilk dokunus yalniz odagi gosterir (bir seye basmaz)
            if (v != 0 || h != 0 || a) { spatialFocus = 0; GameAudio.PlayUi(Sfx.Click, 0.25f); }
            return;
        }
        spatialAccept = a;
        if (v == 0 && h == 0) return;
        int next = Nearest(spatialFocus, new Vector2(h, v));
        if (next != spatialFocus) { spatialFocus = next; GameAudio.PlayUi(Sfx.Click, 0.25f); }
    }

    /// <summary>Dugmeyi uzamsal gezinmeye kaydeder; odaktaysa cerceve cizer. A basildiysa ve dugme etkinse true.</summary>
    public static bool Item(Rect r)
    {
        int index = spatialIndex++;
        LastItemFocused = Connected && index == spatialFocus;
        if (Event.current.type == EventType.Repaint)
        {
            Vector2 min = GUIUtility.GUIToScreenPoint(r.min), max = GUIUtility.GUIToScreenPoint(r.max);
            spatialRects.Add(Rect.MinMaxRect(min.x, min.y, max.x, max.y));
            spatialRectsFrame = Time.frameCount;
            if (LastItemFocused) Highlight(r);
        }
        if (!LastItemFocused || !spatialAccept || !GUI.enabled) return false;
        spatialAccept = false;
        return true;
    }

    // Yon (dpad: x saga, y asagi) dogrultusundaki en yakin dugme; yan sapma iki buyuk kat sayilir
    private static int Nearest(int from, Vector2 dir)
    {
        Vector2 c = spatialPrev[from].center;
        int best = from;
        float bestScore = float.MaxValue;
        for (int i = 0; i < spatialPrev.Count; i++)
        {
            if (i == from) continue;
            Vector2 d = spatialPrev[i].center - c;
            float along = Vector2.Dot(d, dir);
            if (along <= 1f) continue;
            float across = Mathf.Abs(dir.x != 0f ? d.y : d.x);
            float score = along + across * 2.5f;
            if (score < bestScore) { bestScore = score; best = i; }
        }
        return best;
    }

    /// <summary>Odaklanan satirin cevresine cerceve cizer (yalniz gamepad bagliysa).</summary>
    public static void Highlight(Rect r)
    {
        if (!Connected) return;
        float pulse = 0.7f + 0.3f * Mathf.Sin(Time.unscaledTime * 6f);
        Hud.Outline(new Rect(r.x - 4f, r.y - 4f, r.width + 8f, r.height + 8f), new Color(1f, 0.85f, 0.3f, pulse), 3f);
    }
}
