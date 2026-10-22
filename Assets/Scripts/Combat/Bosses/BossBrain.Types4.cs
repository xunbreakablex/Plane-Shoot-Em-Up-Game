using UnityEngine;

// BossBrain: Çelik Kartal ve Tas Dev (BossBuilder.Beasts.cs ile eslesir).
public partial class BossBrain
{
    // ================================================================== KARTAL

    private Transform eagleWingL, eagleWingR, eagleBeak, eagleMuzzleL, eagleMuzzleR;

    private void EagleInit()
    {
        eagleWingL = Get("WingL"); eagleWingR = Get("WingR");
        eagleBeak = Get("MuzzleBeak");
        eagleMuzzleL = Get("MuzzleWingL"); eagleMuzzleR = Get("MuzzleWingR");
    }

    private void EagleMove(float dt) { HoverMove(dt, 320f, 260f, 30f, 60f, 125f, 0.8f); }

    private void EagleAttack()
    {
        // Saldirilar: FIRTINA KANADI (imza), pence dalisi
        switch (step++ % 2)
        {
            case 0:
                if (mgr.RushActive) StartGale();                                                         // boss fazi imza: firtina kanadi
                else StartCharge(0.9f, 205f, 1.7f);
                break;
            default:
                StartCharge(0.9f, 205f, 1.7f);                                                           // pence dalisi
                break;
        }

        nextAttack = Mathf.Max(nextAttack, Time.time + Mathf.Lerp(2.3f, 1.4f, phase / 2f));
    }

    private void EagleAnimate()
    {
        float flap = Mathf.Sin(age * (5f + phase)) * 22f;
        // Firtina kanadi: once kanatlar yukari acilir, ruzgar boyunca hizli ve genis cirpar
        if (GaleBusy)
        {
            float since = Time.time - galeStart;
            flap = since < GaleWarn ? Mathf.Lerp(flap, -55f, Mathf.Clamp01(since / GaleWarn * 2f))
                                    : Mathf.Sin(age * 16f) * 48f;
        }
        if (eagleWingL != null) eagleWingL.localRotation = Quaternion.Euler(0f, 0f, -flap);
        if (eagleWingR != null) eagleWingR.localRotation = Quaternion.Euler(0f, 0f, flap);
    }

    // ==================================================================== DEV

    private Transform golemFistL, golemFistR, golemCore, golemMuzzleCore, golemMuzzleL, golemMuzzleR;
    private Vector3 golemFistLBase, golemFistRBase;

    private void GolemInit()
    {
        golemFistL = Get("FistL"); golemFistR = Get("FistR");
        golemCore = Get("Core"); golemMuzzleCore = Get("MuzzleCore");
        golemMuzzleL = Get("MuzzleFistL"); golemMuzzleR = Get("MuzzleFistR");
        if (golemFistL != null) golemFistLBase = golemFistL.localPosition;
        if (golemFistR != null) golemFistRBase = golemFistR.localPosition;
    }

    private void GolemMove(float dt) { HoverMove(dt, 300f, 150f, 10f, 25f, 80f, 0.45f); }

    private void GolemAttack()
    {
        // Saldirilar: YUMRUKLAR (imza; iki yumruk da koptuysa ezme), cekirdek isini, kaya yagmuru
        switch (step++ % 3)
        {
            case 0:
                if (FistL || FistR) StartFists();                                                        // yumruklar takip edip vurur
                else StartCharge(1.0f, 170f, 1.9f);                                                      // ezme
                break;
            case 1:
                StartBeam(golemMuzzleCore, 1.3f, 1.5f);                                                  // cekirdek isini
                break;
            default:
                RainFromAbove(10 + phase * 4, 120f, 150f);                                               // kaya yagmuru
                break;
        }

        nextAttack = Mathf.Max(nextAttack, Time.time + Mathf.Lerp(2.6f, 1.6f, phase / 2f));
    }

    private void GolemAnimate()
    {
        // Yumruklar havada salinir (saldirida TickFists yonetir), cekirdek atar
        if (FistBusy) { }
        else if (golemFistL != null) golemFistL.localPosition = golemFistLBase + new Vector3(0f, Mathf.Sin(age * 1.8f) * 1.6f, Mathf.Sin(age * 1.1f) * 1.2f);
        if (!FistBusy && golemFistR != null) golemFistR.localPosition = golemFistRBase + new Vector3(0f, Mathf.Sin(age * 1.8f + 2f) * 1.6f, Mathf.Sin(age * 1.1f + 1f) * 1.2f);
        if (golemCore != null)
        {
            float p = 1f + 0.15f * Mathf.Sin(age * 6f);
            golemCore.localScale = new Vector3(3.2f * p, 3.2f * p, 1.6f);
        }
    }
}
