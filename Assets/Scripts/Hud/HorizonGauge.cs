using System.Collections.Generic;
using UnityEngine;

// Yapay ufuk. Dondurulmus IMGUI icerigi grup kirpmasiyla guvenilir kirpilmadigi ve GL cizimi
// IMGUI baglaminda gorunmedigi icin: gokyuzu/zemin poligonlari kare gostergeye analitik olarak
// kirpilir ve yatay tarama seritleriyle (duz dikdortgenler) doldurulur. Her roll acisinda dogrudur.
public static class HorizonGauge
{
    private static readonly Color Sky = new Color(0.35f, 0.6f, 0.95f);
    private static readonly Color Ground = new Color(0.45f, 0.32f, 0.18f);
    private static readonly Color Gold = new Color(1f, 0.85f, 0.1f);

    private const float Step = 2f;   // tarama seridi yuksekligi (HUD birimi)

    private static readonly List<Vector2> Square = new List<Vector2>(4);

    /// <param name="r">HUD koordinatinda kare alan</param>
    /// <param name="pitchDeg">burun yukari = pozitif</param>
    /// <param name="rollDeg">sag kanat yukari = pozitif</param>
    public static void Draw(Rect r, float pitchDeg, float rollDeg)
    {
        float d = r.width;
        Vector2 center = r.center;
        float th = rollDeg * Mathf.Deg2Rad;

        // Dondurulmus eksenler (HUD'da y asagi): u = ufuk cizgisi yonu, v = "yukari"
        Vector2 u = new Vector2(Mathf.Cos(th), Mathf.Sin(th));
        Vector2 v = new Vector2(Mathf.Sin(th), -Mathf.Cos(th));
        float ppd = d / 60f;
        Vector2 h0 = center + new Vector2(-Mathf.Sin(th), Mathf.Cos(th)) * (pitchDeg * ppd);

        Square.Clear();
        Square.Add(new Vector2(r.xMin, r.yMin));
        Square.Add(new Vector2(r.xMax, r.yMin));
        Square.Add(new Vector2(r.xMax, r.yMax));
        Square.Add(new Vector2(r.xMin, r.yMax));

        // Cerceve
        Hud.Fill(new Rect(r.x - 3f, r.y - 3f, r.width + 6f, r.height + 6f), new Color(0f, 0f, 0f, 0.65f));

        // Gokyuzu ve zemin
        FillConvex(Clip(Square, h0, v), Sky);
        FillConvex(Clip(Square, h0, -v), Ground);

        // Ufuk cizgisi (ince bant)
        FillConvex(Clip(Clip(Square, h0 - v * 1.2f, v), h0 + v * 1.2f, -v), Color.white);

        // Pitch merdiveni (merkeze yakin kisimlar kare icinde kalir)
        float half = d * 0.12f;
        for (int deg = -30; deg <= 30; deg += 10)
        {
            if (deg == 0) continue;
            Vector2 c = h0 + v * (deg * ppd);
            if ((c - center).magnitude > d * 0.34f) continue;

            var quad = new List<Vector2>(4)
            {
                c - u * half - v, c + u * half - v, c + u * half + v, c - u * half + v
            };
            FillConvex(quad, Color.white);
        }

        // Sabit ucak isareti
        Hud.Fill(new Rect(center.x - d * 0.22f, center.y - 2f, d * 0.16f, 4f), Gold);
        Hud.Fill(new Rect(center.x + d * 0.06f, center.y - 2f, d * 0.16f, 4f), Gold);
        Hud.Fill(new Rect(center.x - 3f, center.y - 3f, 6f, 6f), Gold);
    }

    // ------------------------------------------------------------------ yardimcilar

    // Sutherland-Hodgman: noktanin normal tarafindaki kismi tutar
    private static List<Vector2> Clip(List<Vector2> poly, Vector2 point, Vector2 normal)
    {
        var result = new List<Vector2>(poly.Count + 2);
        int n = poly.Count;
        for (int i = 0; i < n; i++)
        {
            Vector2 a = poly[i];
            Vector2 b = poly[(i + 1) % n];
            float da = Vector2.Dot(a - point, normal);
            float db = Vector2.Dot(b - point, normal);

            if (da >= 0f) result.Add(a);
            if ((da >= 0f) != (db >= 0f))
            {
                float t = da / (da - db);
                result.Add(a + (b - a) * t);
            }
        }
        return result;
    }

    // Konveks poligonu yatay seritlerle doldurur
    private static void FillConvex(List<Vector2> poly, Color color)
    {
        if (poly == null || poly.Count < 3) return;

        float minY = float.MaxValue, maxY = float.MinValue;
        foreach (var p in poly)
        {
            if (p.y < minY) minY = p.y;
            if (p.y > maxY) maxY = p.y;
        }

        int n = poly.Count;
        for (float y = minY; y < maxY; y += Step)
        {
            float yc = Mathf.Min(y + Step * 0.5f, maxY - 0.001f);
            float lo = float.MaxValue, hi = float.MinValue;

            for (int i = 0; i < n; i++)
            {
                Vector2 a = poly[i];
                Vector2 b = poly[(i + 1) % n];
                if ((a.y <= yc && b.y > yc) || (b.y <= yc && a.y > yc))
                {
                    float t = (yc - a.y) / (b.y - a.y);
                    float x = a.x + (b.x - a.x) * t;
                    if (x < lo) lo = x;
                    if (x > hi) hi = x;
                }
            }

            if (hi > lo)
                Hud.Fill(new Rect(lo, y, hi - lo, Mathf.Min(Step, maxY - y) + 0.5f), color);
        }
    }
}
