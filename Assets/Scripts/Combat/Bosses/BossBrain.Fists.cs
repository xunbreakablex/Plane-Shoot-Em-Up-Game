using UnityEngine;

// Tas Dev imza saldirisi: "YUMRUKLAR". Iki yumruk govdeden ayrilip oyuncunun onunde birkac saniye onu takip eder
// (son anda kilit cizgisi kizarir), sonra kilitlenen noktaya hizla vurur ve govdeye geri doner. Oyuncu vurus aninda
// yana kayarak kacar; yerinde kalan yumrugu yer. Kopan yumruk saldiriya katilmaz.
public partial class BossBrain
{
    private int fistStage;                       // 0 yok, 1 cikis/takip, 2 vurus, 3 donus
    private float fistStageEnd;
    private Vector3 fistPosL, fistPosR;          // yumruklarin dunya konumu (saldirida biz yonetiriz)
    private Vector3 fistDirL, fistDirR;          // vurus yonu
    private Vector3 fistAimL, fistAimR;          // vurus hedefi
    private bool fistHitL, fistHitR, fistPassL, fistPassR;
    private LineRenderer fistLineL, fistLineR;

    private const float FistTrack = 2.6f;        // takip suresi (sn)
    private const float FistHover = 70f;         // oyuncunun onunde durulan uzaklik (m)
    private const float FistSide = 22f;          // iki yumrugun yana acikligi (m)
    private const float FistTrackSpeed = 170f;   // takip hizi (m/s)
    private const float FistStrikeSpeed = 300f;  // vurus hizi (m/s)
    private const float FistReturnSpeed = 240f;
    private const float FistLockWindow = 0.7f;   // vurustan once kilit cizgisinin kizardigi sure

    private bool FistBusy => fistStage != 0;
    private bool FistL => golemMuzzleL != null && golemFistL != null;
    private bool FistR => golemMuzzleR != null && golemFistR != null;

    private void StartFists()
    {
        if (!FistL && !FistR) return;
        fistStage = 1;
        fistStageEnd = Time.time + FistTrack;
        fistHitL = fistHitR = fistPassL = fistPassR = false;
        if (FistL) fistPosL = golemFistL.position;
        if (FistR) fistPosR = golemFistR.position;
        nextAttack = Mathf.Max(nextAttack, Time.time + FistTrack + 3.6f);
        mgr.Announce("TAŞ DEV: yumruklar geliyor, vuruş anında YANA KAY!", new Color(1f, 0.6f, 0.3f), 2.2f);
        Sfx.PlayAt(Sfx.Warning, pos, 0.8f);
    }

    private void TickFists(float dt)
    {
        if (fistStage == 0) return;
        if (!FistL && !FistR) { EndFists(); return; }

        float s = transform.lossyScale.x;
        Vector3 pf = Vector3.ProjectOnPlane(mgr.PlayerVelocity.sqrMagnitude > 25f ? mgr.PlayerVelocity : player.forward, Vector3.up).normalized;
        if (pf.sqrMagnitude < 0.01f) pf = anchorFwd;
        Vector3 right = Vector3.Cross(Vector3.up, pf);

        if (fistStage == 1)
        {
            // Takip: oyuncunun onunde iki yanda durur, ona bakar
            Vector3 hub = player.position + pf * FistHover + Vector3.up * 4f;
            if (FistL) fistPosL = Vector3.MoveTowards(fistPosL, hub - right * FistSide, FistTrackSpeed * dt);
            if (FistR) fistPosR = Vector3.MoveTowards(fistPosR, hub + right * FistSide, FistTrackSpeed * dt);

            if (Time.time >= fistStageEnd)
            {
                // Kilit: oyuncunun su anki yeri (az onde tutma); yana kayan kacar
                Vector3 aim = player.position + mgr.PlayerVelocity * 0.2f;
                fistAimL = aim; fistAimR = aim;
                fistDirL = (aim - fistPosL).normalized;
                fistDirR = (aim - fistPosR).normalized;
                fistStage = 2;
                fistStageEnd = Time.time + 1.0f;
                Sfx.PlayAt(Sfx.BossAttack, aim, 1f);
                FollowCamera.AddShake(0.4f, 0.25f);
                SetFistColliders(false);   // vurus hasari tek kaynaktan (temas hasariyla ikilenmesin)
            }
        }
        else if (fistStage == 2)
        {
            if (FistL && !fistPassL) StrikeFist(ref fistPosL, fistDirL, fistAimL, ref fistHitL, ref fistPassL, s, dt);
            if (FistR && !fistPassR) StrikeFist(ref fistPosR, fistDirR, fistAimR, ref fistHitR, ref fistPassR, s, dt);
            bool done = (!FistL || fistPassL) && (!FistR || fistPassR);
            if (done || Time.time >= fistStageEnd)
            {
                fistStage = 3;
                fistStageEnd = Time.time + 2.6f;
                SetFistColliders(true);
            }
        }
        else
        {
            // Donus: govdedeki yuvasina
            bool homeL = true, homeR = true;
            if (FistL)
            {
                Vector3 home = transform.TransformPoint(golemFistLBase);
                fistPosL = Vector3.MoveTowards(fistPosL, home, FistReturnSpeed * dt);
                homeL = (fistPosL - home).sqrMagnitude < 1f;
            }
            if (FistR)
            {
                Vector3 home = transform.TransformPoint(golemFistRBase);
                fistPosR = Vector3.MoveTowards(fistPosR, home, FistReturnSpeed * dt);
                homeR = (fistPosR - home).sqrMagnitude < 1f;
            }
            if ((homeL && homeR) || Time.time >= fistStageEnd) { EndFists(); return; }
        }

        // Yumruklar oyuncuya bakar; takipte nabiz gibi buyur kuculur
        float pulse = fistStage == 1 ? 1f + 0.18f * Mathf.Abs(Mathf.Sin(Time.time * 9f)) : 1f;
        if (FistL) PlaceFist(golemFistL, fistPosL, pulse);
        if (FistR) PlaceFist(golemFistR, fistPosR, pulse);
    }

    private void StrikeFist(ref Vector3 p, Vector3 dir, Vector3 aim, ref bool hit, ref bool passed, float s, float dt)
    {
        p += dir * FistStrikeSpeed * dt;
        if (!hit && (p - player.position).magnitude < 2.7f * s + 3f)
        {
            hit = true;
            PlayerHealth ph = mgr.CurrentPlayerHealth;
            if (ph != null) ph.TakeHit(1, player.position);
            mgr.SpawnExplosion(player.position, 1.2f);
            FollowCamera.AddShake(1.3f, 0.6f);
            Sfx.PlayAt(Sfx.Crack, player.position, 1f);
        }
        if (Vector3.Dot(aim - p, dir) <= -10f) passed = true;
    }

    private void PlaceFist(Transform fist, Vector3 worldPos, float pulse)
    {
        fist.position = worldPos;
        Vector3 to = player.position - worldPos;
        if (to.sqrMagnitude > 0.01f) fist.rotation = Quaternion.LookRotation(to, Vector3.up);
        fist.localScale = Vector3.one * pulse;
    }

    private void SetFistColliders(bool on)
    {
        if (FistL) foreach (var c in golemFistL.GetComponentsInChildren<Collider>(true)) c.enabled = on;
        if (FistR) foreach (var c in golemFistR.GetComponentsInChildren<Collider>(true)) c.enabled = on;
    }

    private void EndFists()
    {
        if (fistStage == 2) SetFistColliders(true);
        fistStage = 0;
        if (golemFistL != null) { golemFistL.localScale = Vector3.one; golemFistL.localRotation = Quaternion.identity; }
        if (golemFistR != null) { golemFistR.localScale = Vector3.one; golemFistR.localRotation = Quaternion.identity; }
        if (fistLineL != null) fistLineL.enabled = false;
        if (fistLineR != null) fistLineR.enabled = false;
    }

    // Takibin son aninda yumruktan oyuncuya kizaran kilit cizgisi (Update'te)
    private void DrawFistLines()
    {
        if (fistStage == 0) return;
        bool show = fistStage == 1 && fistStageEnd - Time.time < FistLockWindow;
        if (FistL) DrawFistLine(ref fistLineL, show, fistPosL);
        if (FistR) DrawFistLine(ref fistLineR, show, fistPosR);
    }

    private void DrawFistLine(ref LineRenderer lr, bool show, Vector3 from)
    {
        if (!show) { if (lr != null) lr.enabled = false; return; }
        if (lr == null)
        {
            var go = new GameObject("FistLock");
            go.transform.SetParent(transform, false);
            lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.positionCount = 2;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.sharedMaterial = new Material(mgr.bossGlow != null ? mgr.bossGlow : mgr.fxMaterial);
            lr.startWidth = 1.4f;
            lr.endWidth = 0.4f;
        }
        float blink = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(Time.time * 22f));
        SetLineColor(lr, new Color(1f, 0.2f, 0.1f, blink));
        lr.SetPosition(0, from);
        lr.SetPosition(1, player.position);
        lr.enabled = true;
    }
}
