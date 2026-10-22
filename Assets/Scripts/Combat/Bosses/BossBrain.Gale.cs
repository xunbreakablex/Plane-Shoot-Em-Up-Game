using System.Collections.Generic;
using UnityEngine;

// Boss fazi: Celik Kartal "FIRTINA KANADI". Kartal kanatlarini acip sert cirpar; guclu bir yan ruzgar oyuncuyu
// cercevede bir yana iter. Ruzgar kaymadan yavastir (52-68 m/s, kayma 85): karsi kayan oyuncu yerinde kalir, hatta
// ilerler. Ruzgarin ittigi kenarda tuy firtinasi bolgeleri art arda patlar: birakan oyuncu kenara suruklenip vurulur.
// Ekranda ruzgar cizgileri akar; HUD ruzgarin tersine ok gosterir.
public partial class BossBrain
{
    private const float GaleWarn = 0.7f;          // kanat acilisi: ruzgar baslamadan once
    private const float GaleRamp = 0.4f;          // ruzgarin guclenme / dinme suresi
    private const int GaleStreaks = 24;
    private const float GaleStreakLen = 12f;      // cercevede (m)
    private const float GaleStreakSpanX = 95f;    // cizgiler oyuncunun cevresinde akar (o derinlikte ekran ~+-60 m gosterir)
    private const float GaleStreakSpanY = 55f;

    private float galeStart = -10f, galeEnd = -10f;
    private float galeDir;                        // +1 saga, -1 sola iter
    private float galeSpeed;
    private readonly List<LineRenderer> galeLines = new List<LineRenderer>();
    private readonly Vector2[] galeStreak = new Vector2[GaleStreaks];   // oyuncuya gore (cercevede)

    private bool GaleBusy => Time.time < galeEnd + 0.3f;

    /// <summary>Ruzgar gucu 0-1 (uyari sirasinda 0, sonra yumusakca 1, bitiste dinerek 0).</summary>
    private float GaleStrength
    {
        get
        {
            float t = Time.time;
            if (t < galeStart + GaleWarn || t >= galeEnd) return 0f;
            float up = Mathf.Clamp01((t - galeStart - GaleWarn) / GaleRamp);
            float down = Mathf.Clamp01((galeEnd - t) / GaleRamp);
            return Mathf.Min(up, down);
        }
    }

    private void StartGale()
    {
        Vector2 p = PlayerFrame();
        // Oyuncu hangi yandaysa ruzgar o yana iter (kenar yakin; kacis ruzgara karsi, ortaya dogru)
        galeDir = Mathf.Abs(p.x) > 30f ? Mathf.Sign(p.x) : (Random.value < 0.5f ? -1f : 1f);
        galeSpeed = 52f + 8f * phase;
        float dur = 4.2f + 0.3f * phase;
        galeStart = Time.time;
        galeEnd = Time.time + GaleWarn + dur;

        // Kenarda tuy firtinasi: ruzgar yonundeki kenar boyunca dikey bir dizi bolge, iki-uc dalga. Ilk dalga, ortadan
        // birakilan oyuncunun ruzgarla kenara vardigi ana (~2,4 sn) denk gelir.
        float edge = galeDir * (CombatManager.RushHalfWidth - 22f);
        int waves = 2 + Mathf.Min(phase, 1);
        for (int w = 0; w < waves; w++)
        {
            float delay = GaleWarn + 2.4f + w * 0.8f;
            float off = (w & 1) * 22f;
            for (float y = -CombatManager.RushDown + 20f; y <= CombatManager.RushUp; y += 45f)
                AddZone(new Vector2(edge, y + off), 38f, delay, 4);
        }

        for (int i = 0; i < GaleStreaks; i++)
            galeStreak[i] = new Vector2(Random.Range(-GaleStreakSpanX, GaleStreakSpanX), Random.Range(-GaleStreakSpanY, GaleStreakSpanY));

        nextAttack = Mathf.Max(nextAttack, galeEnd + 0.6f);
        mgr.Announce("FIRTINA KANADI: rüzgâra karşı kay, kenara sürüklenme!", new Color(0.75f, 0.95f, 1f), 2.2f);
        Sfx.PlayAt(Sfx.BossRoar, pos, 0.7f);
        Sfx.PlayAt(Sfx.Wind, player.position, 0.9f);
    }

    private void EndGale()
    {
        galeEnd = -10f;
        foreach (var lr in galeLines) if (lr != null) lr.enabled = false;
    }

    // FixedUpdate: ruzgar oyuncuyu iter (ucak her adimda yenilenmezse ruzgar kendiliginden durur)
    private void TickGale()
    {
        if (!GaleBusy) return;
        if (!mgr.RushActive) { EndGale(); return; }
        float k = GaleStrength;
        if (k <= 0f) return;
        PlaneController pc = mgr.flight != null ? mgr.flight.Current : null;
        if (pc != null) pc.SetRushWind(new Vector2(galeDir * galeSpeed * k, 0f));
    }

    // Update: ekranda akan ruzgar cizgileri (oyuncunun onundeki duzlemde, bolge halkalariyla ayni derinlikte)
    private void DrawGale()
    {
        bool on = GaleBusy && mgr.RushActive && Time.time >= galeStart;
        if (!on)
        {
            foreach (var lr in galeLines) if (lr != null && lr.enabled) lr.enabled = false;
            return;
        }
        while (galeLines.Count < GaleStreaks) galeLines.Add(MakeZoneLine("GaleStreak", false));

        float k = GaleStrength;
        float warm = Mathf.Clamp01((Time.time - galeStart) / GaleWarn);   // uyari sirasinda soluk, seyrek
        float vis = Mathf.Max(k, 0.25f * warm);
        float dt = Time.deltaTime;
        Vector2 pf = PlayerFrame();
        for (int i = 0; i < GaleStreaks; i++)
        {
            Vector2 s = galeStreak[i];
            s.x += galeDir * (galeSpeed * 1.6f + 30f) * Mathf.Max(vis, 0.2f) * dt * (0.8f + 0.4f * ((i * 37) % 10) / 10f);
            if (s.x * galeDir > GaleStreakSpanX)
            {
                s.x = -galeDir * GaleStreakSpanX;
                s.y = Random.Range(-GaleStreakSpanY, GaleStreakSpanY);
            }
            galeStreak[i] = s;
            Vector2 at = pf + s;

            LineRenderer lr = galeLines[i];
            lr.loop = false;
            lr.positionCount = 2;
            lr.SetPosition(0, OverlayPoint(at - new Vector2(galeDir * GaleStreakLen * (0.5f + vis), 0f), ZoneAhead));
            lr.SetPosition(1, OverlayPoint(at, ZoneAhead));
            lr.startWidth = 0.05f;
            lr.endWidth = 0.35f;
            SetLineColor(lr, new Color(0.88f, 0.96f, 1f, 0.55f * vis));
            lr.enabled = vis > 0.02f;
        }
    }

    /// <summary>
    /// HUD: Kartal ruzgari esiyorsa oyuncunun ruzgara karsi tarafindaki nokta (oraya kay) ve ruzgar gucu (0-1).
    /// Uyari sirasinda da doner (guc kucuk): oyuncu ruzgar baslamadan yonu gorur.
    /// </summary>
    public bool GaleDanger(out Vector3 upwind, out float strength)
    {
        upwind = Vector3.zero;
        strength = 0f;
        if (!GaleBusy || !mgr.RushActive || Time.time >= galeEnd) return false;
        strength = Mathf.Max(GaleStrength, 0.3f);
        upwind = KiWaveWorld(PlayerFrame() - new Vector2(galeDir * 45f, 0f));
        return true;
    }
}
