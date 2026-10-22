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
        switch (step++ % 5)
        {
            case 0:
                if (mgr.RushActive) StartGale();                                                         // boss fazi imza: firtina kanadi
                else Fan(eagleBeak.position, AimFrom(eagleBeak.position, 215f, 4f), 7, 40f, 215f, 1);     // tuy yagmuru
                break;
            case 1:
            {
                // Kanat ucu toplari: kopan top susar; ikisi de koptuysa gagadan
                if (eagleMuzzleL != null) Cone(eagleMuzzleL.position, AimFrom(eagleMuzzleL.position, 180f, 3f), 8 + phase * 2, 10f, 180f, 1, Random.value * 90f);
                if (eagleMuzzleR != null) Cone(eagleMuzzleR.position, AimFrom(eagleMuzzleR.position, 180f, 3f), 8 + phase * 2, 10f, 180f, 1, Random.value * 90f);
                if (eagleMuzzleL == null && eagleMuzzleR == null)
                    Cone(eagleBeak.position, AimFrom(eagleBeak.position, 180f, 3f), 10 + phase * 2, 10f, 180f, 1, Random.value * 90f);
                break;
            }
            case 2:
                StartCharge(0.9f, 205f, 1.7f);                                                           // dalis
                break;
            case 3:
                StartSpray(eagleBeak, 2.2f, 2 + phase, 14f, 0.08f, 190f);
                break;
            default:
                if (phase >= 1) StartBeam(eagleBeak, 1.1f, 1.3f);
                else RainFromAbove(8, 135f, 140f);
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
        switch (step++ % 5)
        {
            case 0:
            {
                // Yumruklar: kopan yumruk susar; ikisi de koptuysa cekirdekten
                if (golemMuzzleL != null) Fan(golemMuzzleL.position, AimFrom(golemMuzzleL.position, 190f, 4f), 5, 30f, 190f, 1);
                if (golemMuzzleR != null) Fan(golemMuzzleR.position, AimFrom(golemMuzzleR.position, 190f, 4f), 5, 30f, 190f, 1);
                if (golemMuzzleL == null && golemMuzzleR == null)
                    Fan(golemMuzzleCore.position, AimFrom(golemMuzzleCore.position, 190f, 4f), 7, 36f, 190f, 1);
                break;
            }
            case 1:
                RainFromAbove(10 + phase * 4, 120f, 150f);                                               // kaya yagmuru
                break;
            case 2:
                StartBeam(golemMuzzleCore, 1.3f, 1.5f);
                break;
            case 3:
                if (FistL || FistR) StartFists();                                                        // yumruklar takip edip vurur
                else StartCharge(1.0f, 170f, 1.9f);                                                      // ezme
                break;
            default:
                Cone(golemMuzzleCore.position, AimFrom(golemMuzzleCore.position, 150f, 0f), 16 + phase * 4, 12f, 150f, 2, Random.value * 90f);
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
