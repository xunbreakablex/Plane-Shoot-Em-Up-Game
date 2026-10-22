using UnityEngine;

// Boss fazi imza saldirisi: SUPURME. Boss agzindan/gozlerinden cikan dev bir akim, oyuncunun bulundugu kayma
// duzleminde bir uctan obur uca suprulur.
//   Ejderha "ATES NEFESI": yatay supurme, oyuncunun o anki YUKSEKLIGINE kilitli -> yukari ya da asagi kacilir
//   Ahtapot "DOKUNAC TOKADI": dikey supurme, oyuncunun o anki YANAL konumuna kilitli -> saga ya da sola kacilir
// Once kilitli seridin iki kenari kirmizi cizgilerle gosterilir (uyari), sonra akim seridi tarar. Seridin icinde
// kalan oyuncu, akim ustunden gecerken vurulur. Koordinatlar cercevede (sag, yukari; RushOrigin'e gore) tutulur:
// cerceve kayip donse de serit oyuncuyla birlikte gider.
public partial class BossBrain
{
    private static float SweepWarn => 1.0f * Difficulty.BossWarningMul;        // uyari suresi (sn)
    private const float SweepTime = 1.6f;        // bir taramanin suresi
    private const float SweepBand = 15f;         // kilitli seridin yari kalinligi (m)
    private const float SweepHit = 13f;          // akimin vurus yari genisligi (tarama ekseninde)
    private const float SweepMargin = 25f;       // tarama cerceve kenarinin bu kadar disindan baslar
    private const float SweepEdgeAhead = 110f;   // serit kenar cizgilerinin oyuncunun onundeki derinligi (m)

    private bool sweepActive;
    private bool sweepVertical;                  // false: yatay tarama (y kilitli), true: dikey tarama (x kilitli)
    private float sweepStart;
    private float sweepLock;                     // kilitli eksendeki deger (cercevede)
    private int sweepPasses, sweepPass;
    private float sweepNextHit;
    private float sweepNextSpark;
    private Transform sweepMuzzle;
    private Color sweepCore, sweepGlow;
    private LineRenderer sweepEdgeA, sweepEdgeB, sweepBeam, sweepBeamGlow;

    private bool SweepBusy => sweepActive;

    private void StartSweep(Transform muzzle, bool vertical, int passes, Color core, Color glow, string announce)
    {
        if (muzzle == null || !mgr.RushActive) return;
        sweepActive = true;
        sweepVertical = vertical;
        sweepMuzzle = muzzle;
        sweepPasses = Mathf.Max(1, passes);
        sweepPass = 0;
        sweepStart = Time.time;
        sweepCore = core;
        sweepGlow = glow;
        Vector2 p = PlayerFrame();
        sweepLock = vertical ? p.x : p.y;
        nextAttack = Mathf.Max(nextAttack, Time.time + SweepWarn + SweepTime * sweepPasses + 0.9f);
        mgr.Announce(announce, new Color(1f, 0.6f, 0.3f), 2f);
        Sfx.PlayAt(Sfx.Warning, pos, 0.8f);
    }

    private void EndSweep()
    {
        sweepActive = false;
        if (sweepEdgeA != null) sweepEdgeA.enabled = false;
        if (sweepEdgeB != null) sweepEdgeB.enabled = false;
        if (sweepBeam != null) sweepBeam.enabled = false;
        if (sweepBeamGlow != null) sweepBeamGlow.enabled = false;
    }

    // Taramanin o anki ekseni boyunca konumu (cercevede); u: 0..1
    private float SweepAlong(float u)
    {
        float lo = sweepVertical ? -CombatManager.RushDown - SweepMargin : -CombatManager.RushHalfWidth - SweepMargin;
        float hi = sweepVertical ? CombatManager.RushUp + SweepMargin : CombatManager.RushHalfWidth + SweepMargin;
        bool reverse = (sweepPass & 1) == 1;
        return Mathf.Lerp(reverse ? hi : lo, reverse ? lo : hi, u);
    }

    private Vector2 SweepFramePoint(float along)
    {
        return sweepVertical ? new Vector2(sweepLock, along) : new Vector2(along, sweepLock);
    }

    // FixedUpdate: tarama ilerlemesi ve vurus
    private void TickSweep()
    {
        if (!sweepActive) return;
        if (!mgr.RushActive || sweepMuzzle == null) { EndSweep(); return; }

        float t = Time.time - sweepStart - SweepWarn;
        if (t < 0f) return;

        sweepPass = Mathf.Min(sweepPasses - 1, (int)(t / SweepTime));
        float u = Mathf.Clamp01((t - sweepPass * SweepTime) / SweepTime);
        if (t >= SweepTime * sweepPasses) { EndSweep(); return; }

        float along = SweepAlong(u);
        Vector2 p = PlayerFrame();
        float lockDist = sweepVertical ? Mathf.Abs(p.x - sweepLock) : Mathf.Abs(p.y - sweepLock);
        float alongDist = sweepVertical ? Mathf.Abs(p.y - along) : Mathf.Abs(p.x - along);
        if (lockDist < SweepBand && alongDist < SweepHit && Time.time >= sweepNextHit)
        {
            sweepNextHit = Time.time + 0.5f;
            PlayerHealth ph = mgr.CurrentPlayerHealth;
            if (ph != null) ph.TakeHit(1, player.position);
            FollowCamera.AddShake(0.7f, 0.35f);
        }
    }

    // Update: uyari cizgileri ve akim
    private void DrawSweep()
    {
        if (!sweepActive || sweepMuzzle == null) return;
        EnsureSweepLines();

        float t = Time.time - sweepStart;
        bool warn = t < SweepWarn;

        // Seridin iki kenari: kayma alani boyunca uzanan kirmizi cizgiler (uyarida yanip soner, taramada sabit)
        float lo = sweepVertical ? -CombatManager.RushDown - 10f : -CombatManager.RushHalfWidth - 10f;
        float hi = sweepVertical ? CombatManager.RushUp + 10f : CombatManager.RushHalfWidth + 10f;
        float blink = warn ? 0.45f + 0.55f * Mathf.Abs(Mathf.Sin(Time.time * 14f)) : 0.55f;
        Color edge = new Color(1f, 0.25f, 0.15f, blink);
        for (int k = 0; k < 2; k++)
        {
            LineRenderer lr = k == 0 ? sweepEdgeA : sweepEdgeB;
            float off = k == 0 ? -SweepBand : SweepBand;
            Vector2 a = sweepVertical ? new Vector2(sweepLock + off, lo) : new Vector2(lo, sweepLock + off);
            Vector2 b = sweepVertical ? new Vector2(sweepLock + off, hi) : new Vector2(hi, sweepLock + off);
            // Oyuncunun duzleminde cizilse kameranin hemen onunde kalip ekran disina tasiyordu: ileride ciz
            lr.SetPosition(0, OverlayPoint(a, SweepEdgeAhead));
            lr.SetPosition(1, OverlayPoint(b, SweepEdgeAhead));
            SetLineColor(lr, edge);
            lr.enabled = true;
        }

        if (warn)
        {
            sweepBeam.enabled = sweepBeamGlow.enabled = false;
            return;
        }

        float ft = t - SweepWarn;
        int pass = Mathf.Min(sweepPasses - 1, (int)(ft / SweepTime));
        float u = Mathf.Clamp01((ft - pass * SweepTime) / SweepTime);
        int keep = sweepPass;
        sweepPass = pass;
        Vector3 end = KiWaveWorld(SweepFramePoint(SweepAlong(u)));
        sweepPass = keep;

        Vector3 from = sweepMuzzle.position;
        Vector3 dir = (end - from).normalized;
        Vector3 far = end + dir * 60f;   // akim oyuncunun duzlemini gecip devam eder
        float flicker = 1f + 0.12f * Mathf.Sin(Time.time * 47f);
        sweepBeam.startWidth = 3f * flicker; sweepBeam.endWidth = 9f * flicker;
        sweepBeamGlow.startWidth = 7f * flicker; sweepBeamGlow.endWidth = 2f * SweepHit * flicker;
        sweepBeam.SetPosition(0, from); sweepBeam.SetPosition(1, far);
        sweepBeamGlow.SetPosition(0, from); sweepBeamGlow.SetPosition(1, far);
        SetLineColor(sweepBeam, sweepCore);
        SetLineColor(sweepBeamGlow, sweepGlow);
        sweepBeam.enabled = sweepBeamGlow.enabled = true;

        if (Time.time >= sweepNextSpark)
        {
            sweepNextSpark = Time.time + 0.07f;
            mgr.SpawnHitSpark(end, 2.2f);
        }
    }

    private void EnsureSweepLines()
    {
        if (sweepEdgeA != null) return;
        sweepEdgeA = MakeSweepLine("SweepEdgeA", 2.6f);
        sweepEdgeB = MakeSweepLine("SweepEdgeB", 2.6f);
        sweepBeamGlow = MakeSweepLine("SweepGlow", 8f);
        sweepBeam = MakeSweepLine("SweepBeam", 4f);
    }

    private LineRenderer MakeSweepLine(string name, float width)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.positionCount = 2;
        lr.numCapVertices = 4;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        lr.sharedMaterial = new Material(mgr.fxMaterial != null ? mgr.fxMaterial : mgr.bossGlow);
        lr.startWidth = width;
        lr.endWidth = width;
        lr.enabled = false;
        return lr;
    }

    /// <summary>HUD: tarama suruyorsa kilitli serit (dunyada, oyuncunun hizasindaki merkez) ve kacis ekseni.</summary>
    public bool SweepDanger(out Vector3 bandCenter, out bool vertical, out bool inside)
    {
        bandCenter = Vector3.zero;
        vertical = sweepVertical;
        inside = false;
        if (!sweepActive || !mgr.RushActive) return false;
        Vector2 p = PlayerFrame();
        inside = sweepVertical ? Mathf.Abs(p.x - sweepLock) < SweepBand : Mathf.Abs(p.y - sweepLock) < SweepBand;
        bandCenter = KiWaveWorld(sweepVertical ? new Vector2(sweepLock, p.y) : new Vector2(p.x, sweepLock));
        return true;
    }
}
