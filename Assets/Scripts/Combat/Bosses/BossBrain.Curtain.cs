using UnityEngine;

// BossBrain: boss fazina ozgu "perde" saldirisi.
// Boss fazinda oyuncu yalniz kayabildigi (sag-sol, yukari-asagi) icin tek duzlemli yelpaze ve seyrek halkalar capraz
// kaymayla kolayca atlatiliyordu. Perde: oyuncunun bulundugu yere ayni anda varan genis bir mermi duvari; duvarda tek
// bir bosluk vardir. Duvar oyuncunun kayabildigi alanin tamamini kaplar (uzaga kacarak kurtulunmaz). Bosluk once yesil
// bir cizgi ve HUD'da yesil okla gosterilir: oyuncu ates ederken bosluga kaymak ya da mermilerin arasindan dikkatle
// siyrilmak zorundadir. Bosluk her zaman oyuncunun su anki yerinin disinda ve ulasilabilir mesafede acilir.
public partial class BossBrain
{
    private const int CurtainCols = 12;          // duvar sutunu: cercevenin tum genisligi (±165 m)
    private const int CurtainRows = 8;           // duvar satiri: cercevenin tum yuksekligi (-90..+120 m)
    private const float CurtainSpacing = 30f;    // mermiler arasi (m)
    private const float CurtainSpeed = 190f;     // taban mermi hizi (boss carpanlari ayrica uygulanir)
    private const float CurtainBulletRadius = 7f; // perde kuresinin vurus yaricapi (gorseli de bu boyda); normal mermi 1 m
    private const int CurtainEvery = 4;          // boss fazinda her bu kadar saldiridan biri perde (ara boss)
    private const int CurtainEveryStage = 3;     // asama sonu bosslarinda daha sik

    private int attackCount;
    private Vector3 curtainGap;                  // boslugun merkezi (cerceveyle tasinir)
    private float curtainGapUntil;
    private LineRenderer gapLine;

    private bool CurtainTurn
    {
        get
        {
            int every = enemy != null && enemy.IsStageBoss ? CurtainEveryStage : CurtainEvery;
            return mgr.RushActive && attackCount % every == every - 1;
        }
    }

    /// <summary>HUD: perdenin boslugu (uyari ve mermiler gelene kadar).</summary>
    public bool CurtainGap(out Vector3 position)
    {
        position = curtainGap;
        return Time.time < curtainGapUntil && enemy != null && !enemy.IsDead;
    }

    private void Curtain()
    {
        Vector3 f = anchorFwd;
        Vector3 right = Vector3.Cross(Vector3.up, f).normalized;
        Vector3 o = mgr.RushOrigin;
        float x0 = -CombatManager.RushHalfWidth + 5f;   // en soldaki sutun
        float y0 = -CombatManager.RushDown;             // en alttaki satir

        // Oyuncunun duvardaki hucresi; bosluk (2x2) bu hucreyi icermez, 1-2 hucre yanda ya da yukarida/asagida acilir
        Vector3 rel = player.position - o;
        int pc = Mathf.Clamp(Mathf.RoundToInt((Vector3.Dot(rel, right) - x0) / CurtainSpacing), 0, CurtainCols - 1);
        int pr = Mathf.Clamp(Mathf.RoundToInt((rel.y - y0) / CurtainSpacing), 0, CurtainRows - 1);
        int hc, hr;
        if (Random.value < 0.6f)
        {
            int step = Random.Range(1, 3);
            bool left = Random.value < 0.5f;
            hc = left ? pc - 1 - step : pc + step;
            if (hc < 0 || hc > CurtainCols - 2) hc = left ? pc + step : pc - 1 - step;   // kenardaysa obur yana
            hr = pr - Random.Range(0, 2);
        }
        else
        {
            int step = Random.Range(1, 3);
            bool down = Random.value < 0.5f;
            hr = down ? pr - 1 - step : pr + step;
            if (hr < 0 || hr > CurtainRows - 2) hr = down ? pr + step : pr - 1 - step;
            hc = pc - Random.Range(0, 2);
        }
        hc = Mathf.Clamp(hc, 0, CurtainCols - 2);
        hr = Mathf.Clamp(hr, 0, CurtainRows - 2);

        var targets = new System.Collections.Generic.List<Vector3>(CurtainCols * CurtainRows);
        for (int i = 0; i < CurtainCols; i++)
        {
            for (int j = 0; j < CurtainRows; j++)
            {
                bool hole = (i == hc || i == hc + 1) && (j == hr || j == hr + 1);
                if (hole) continue;
                targets.Add(o + right * (x0 + i * CurtainSpacing) + Vector3.up * (y0 + j * CurtainSpacing));
            }
        }

        curtainGap = o + right * (x0 + (hc + 0.5f) * CurtainSpacing) + Vector3.up * (y0 + (hr + 0.5f) * CurtainSpacing);
        float effSpeed = CurtainSpeed * Mathf.Max(0.05f, GameSettings.Current.bossBulletSpeedMultiplier) * RushBulletSpeed;
        float flight = Vector3.Distance(transform.position, player.position) / Mathf.Max(effSpeed, 1f);
        curtainGapUntil = Time.time + ShotWarning + flight + 0.25f;

        Sfx.PlayAt(Sfx.Warning, player.position, 0.6f);
        pendingShots.Add(new PendingShot
        {
            at = Time.time + ShotWarning,
            snap = mgr.TakeRushSnap(),
            fire = m =>
            {
                // Duvar bossun onunde kucuk bir ag olarak dogar ve oyuncuya yaklastikca acilir (kureler ciktiginda ust
                // uste binmesin); hedef noktalar cerceveyle tasinmis olarak atilir
                Vector3 from = transform.position;
                Vector3 center = m.Point(o);
                foreach (Vector3 t in targets)
                {
                    Vector3 tp = m.Point(t);
                    Vector3 start = from + (tp - center) * 0.45f;
                    Vector3 to = tp - start;
                    if (to.sqrMagnitude < 1f) continue;
                    Bullet b = Shoot(start, to.normalized, CurtainSpeed, 1);
                    if (b != null) b.radius = CurtainBulletRadius;
                }
                Sfx.PlayAt(Sfx.BossAttack, from, 0.8f);
            }
        });

        nextAttack = Mathf.Max(nextAttack, Time.time + 2.6f);
    }

    // Uyari: bosstan bosluga uzanan yesil cizgi (DrawWarns ile her karede)
    private void DrawGapLine()
    {
        bool on = Time.time < curtainGapUntil && enemy != null && !enemy.IsDead;
        if (!on)
        {
            if (gapLine != null) gapLine.enabled = false;
            return;
        }

        if (gapLine == null)
        {
            gapLine = MakeWarnLine();
            gapLine.name = "CurtainGap";
            Color c = new Color(0.3f, 1f, 0.45f);
            gapLine.sharedMaterial.color = c;
            if (gapLine.sharedMaterial.HasProperty("_BaseColor")) gapLine.sharedMaterial.SetColor("_BaseColor", c);
        }
        float width = 1.1f * (0.75f + 0.25f * Mathf.Sin(Time.time * 20f));
        gapLine.enabled = true;
        gapLine.SetPosition(0, transform.position);
        gapLine.SetPosition(1, curtainGap);
        gapLine.startWidth = width;
        gapLine.endWidth = width;
    }
}
