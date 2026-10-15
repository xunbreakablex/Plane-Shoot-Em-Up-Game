using UnityEngine;

// Ortak HUD yerlesimi: tum oyun ici arayuz sanal 1280x720 izgarasinda cizilir ve ekrana olceklenir.
// Boylece yazi boyutlari/aralik her cozunurlukte tutarli kalir ve bolgeler birbirine girmez.
//
// Bolgeler:
//   Sol ust   : ucus bilgisi + gaz        (FlightTestManager)
//   Ust orta  : can / kalkan / oldurme    (CombatManager)
//   Sag ust   : yapay ufuk + bilgi paneli (FlightTestManager / CombatManager)
//   Alt sol   : kontroller ipucu          (FlightTestManager)
//   Merkez    : uyarilar, nisangah, yuzen yazilar
public static class Hud
{
    public const float RefW = 1280f;
    public const float RefH = 720f;
    public const float Margin = 14f;
    public const float HorizonSize = 120f;
    public const float RightColumnWidth = 250f;

    // En ustteki XP cubugunun yuksekligi; diger paneller bunun altindan baslar
    public const float TopBar = 16f;
    public const float TopY = 26f;

    // Yuzen yazilarin ve oklarin icinde kalacagi guvenli alan (panellerden uzak)
    public const float SafeTop = 170f;
    // Yuzen kelimeler duyuru seridinin (y~176-230) altinda kalir
    public const float FloatTop = 240f;
    public const float SafeBottom = 70f;
    public const float SafeSide = 40f;

    public static float Scale
    {
        get { return Mathf.Clamp(Mathf.Min(Screen.width / RefW, Screen.height / RefH), 0.55f, 2.4f); }
    }

    public static float W { get { return Screen.width / Scale; } }
    public static float H { get { return Screen.height / Scale; } }

    /// <summary>
    /// Her karede bicimlendirilen HUD metinleri (string interpolasyonu) cop uretir. Anahtar degerler
    /// degismedikce onceki dizgi kullanilir: <c>if (c.Update(a, b)) c.Text = $"...";</c>
    /// </summary>
    public sealed class TextCache
    {
        private int a, b, c, d;
        private bool init;
        public string Text = "";

        /// <summary>Anahtarlar degistiyse true doner (metin yeniden kurulmali).</summary>
        public bool Update(int a, int b = 0, int c = 0, int d = 0)
        {
            if (init && a == this.a && b == this.b && c == this.c && d == this.d) return false;
            this.a = a; this.b = b; this.c = c; this.d = d;
            init = true;
            return true;
        }
    }

    // "123 m" gibi mesafe etiketleri: ilk kullanimda bir kez uretilir
    private static readonly string[] meterLabels = new string[5001];

    public static string Meters(float distance)
    {
        int m = Mathf.Clamp(Mathf.RoundToInt(distance), 0, meterLabels.Length - 1);
        string s = meterLabels[m];
        if (s == null)
        {
            s = m + " m";
            meterLabels[m] = s;
        }
        return s;
    }

    private static GUIStyle style;
    private static Matrix4x4 saved;

    public static void Begin()
    {
        saved = GUI.matrix;
        float s = Scale;
        GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1f));
        if (style == null)
        {
            style = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold,
                wordWrap = false,
                clipping = TextClipping.Overflow
            };
        }
    }

    public static void End()
    {
        GUI.matrix = saved;
        GUI.color = Color.white;
    }

    // HUD koordinatinda verilen pivot etrafinda dondurur. (GUIUtility.RotateAroundPivot olcek matrisiyle
    // pivotu yanlis yorumladigi icin kullanilmaz.) Isin bitince ClearRotation cagir.
    public static void RotateAround(float angleDegrees, Vector2 pivot)
    {
        float s = Scale;
        Matrix4x4 rot = Matrix4x4.TRS(new Vector3(pivot.x, pivot.y, 0f), Quaternion.Euler(0f, 0f, angleDegrees), Vector3.one)
                        * Matrix4x4.TRS(new Vector3(-pivot.x, -pivot.y, 0f), Quaternion.identity, Vector3.one);
        GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1f)) * rot;
    }

    public static void ClearRotation()
    {
        float s = Scale;
        GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1f));
    }

    // Ekran pikselini (y yukari) HUD koordinatina cevirir (y asagi)
    public static Vector2 ToHud(Vector3 screenPoint)
    {
        float s = Scale;
        return new Vector2(screenPoint.x / s, (Screen.height - screenPoint.y) / s);
    }

    public static Vector2 ToHud(Vector2 screenPoint)
    {
        float s = Scale;
        return new Vector2(screenPoint.x / s, (Screen.height - screenPoint.y) / s);
    }

    // Golgeli (buyuk yazilarda cerceveli) metin
    public static void Text(Rect r, string text, Color color, int size = 15,
        TextAnchor anchor = TextAnchor.UpperLeft, bool bold = true)
    {
        if (string.IsNullOrEmpty(text)) return;

        style.fontSize = size;
        style.alignment = anchor;
        style.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;

        Color shadow = new Color(0f, 0f, 0f, 0.85f * color.a);
        style.normal.textColor = shadow;

        if (size >= 24)
        {
            float o = Mathf.Max(1.5f, size * 0.05f);
            for (int dx = -1; dx <= 1; dx += 2)
            for (int dy = -1; dy <= 1; dy += 2)
                GUI.Label(new Rect(r.x + dx * o, r.y + dy * o, r.width, r.height), text, style);
        }
        else
        {
            GUI.Label(new Rect(r.x + 1f, r.y + 1f, r.width, r.height), text, style);
        }

        style.normal.textColor = color;
        GUI.Label(r, text, style);
    }

    public static void Fill(Rect r, Color c)
    {
        GUI.color = c;
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = Color.white;
    }

    public static void Panel(Rect r, float alpha = 0.38f)
    {
        Fill(r, new Color(0.02f, 0.04f, 0.07f, alpha));
    }

    public static void Bar(Rect r, float fraction, Color fill)
    {
        Fill(r, new Color(0f, 0f, 0f, 0.55f));
        Fill(new Rect(r.x, r.y, r.width * Mathf.Clamp01(fraction), r.height), fill);
    }

    private static Texture2D discTexture, ringTexture;

    private static Texture2D MakeCircle(bool ringOnly)
    {
        const int size = 128;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx = (x + 0.5f) / size * 2f - 1f;
            float dy = (y + 0.5f) / size * 2f - 1f;
            float r = Mathf.Sqrt(dx * dx + dy * dy);
            float outer = Mathf.Clamp01((1f - r) * 64f * 0.5f);
            float a = outer;
            if (ringOnly)
            {
                float inner = Mathf.Clamp01((r - 0.93f) * 64f * 0.5f);
                a = outer * inner;
            }
            tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
        }
        tex.Apply();
        return tex;
    }

    // Dolu daire (radar noktalari, arka plan)
    public static void Disc(Vector2 center, float radius, Color c)
    {
        if (discTexture == null) discTexture = MakeCircle(false);
        GUI.color = c;
        GUI.DrawTexture(new Rect(center.x - radius, center.y - radius, radius * 2f, radius * 2f), discTexture);
        GUI.color = Color.white;
    }

    // Ince halka (radar mesafe halkalari, cerceve)
    public static void Ring(Vector2 center, float radius, Color c)
    {
        if (ringTexture == null) ringTexture = MakeCircle(true);
        GUI.color = c;
        GUI.DrawTexture(new Rect(center.x - radius, center.y - radius, radius * 2f, radius * 2f), ringTexture);
        GUI.color = Color.white;
    }

    public static void Outline(Rect r, Color c, float thickness = 2f)
    {
        GUI.color = c;
        GUI.DrawTexture(new Rect(r.x, r.y, r.width, thickness), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(r.x, r.yMax - thickness, r.width, thickness), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(r.x, r.y, thickness, r.height), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(r.xMax - thickness, r.y, thickness, r.height), Texture2D.whiteTexture);
        GUI.color = Color.white;
    }
}
