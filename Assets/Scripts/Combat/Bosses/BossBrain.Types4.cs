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
                Fan(eagleBeak.position, AimFrom(eagleBeak.position, 215f, 4f), 7, 40f, 215f, 1);          // tuy yagmuru
                break;
            case 1:
            {
                Vector3 a = eagleMuzzleL.position, b = eagleMuzzleR.position;
                Cone(a, AimFrom(a, 180f, 3f), 8 + phase * 2, 10f, 180f, 1, Random.value * 90f);
                Cone(b, AimFrom(b, 180f, 3f), 8 + phase * 2, 10f, 180f, 1, Random.value * 90f);
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
                Vector3 a = golemMuzzleL.position, b = golemMuzzleR.position;
                Fan(a, AimFrom(a, 190f, 4f), 5, 30f, 190f, 1);
                Fan(b, AimFrom(b, 190f, 4f), 5, 30f, 190f, 1);
                break;
            }
            case 1:
                RainFromAbove(10 + phase * 4, 120f, 150f);                                               // kaya yagmuru
                break;
            case 2:
                StartBeam(golemMuzzleCore, 1.3f, 1.5f);
                break;
            case 3:
                StartCharge(1.0f, 170f, 1.9f);                                                           // ezme
                break;
            default:
                Cone(golemMuzzleCore.position, AimFrom(golemMuzzleCore.position, 150f, 0f), 16 + phase * 4, 12f, 150f, 2, Random.value * 90f);
                break;
        }

        nextAttack = Mathf.Max(nextAttack, Time.time + Mathf.Lerp(2.6f, 1.6f, phase / 2f));
    }

    private void GolemAnimate()
    {
        // Yumruklar havada salinir, cekirdek atar
        if (golemFistL != null) golemFistL.localPosition = golemFistLBase + new Vector3(0f, Mathf.Sin(age * 1.8f) * 1.6f, Mathf.Sin(age * 1.1f) * 1.2f);
        if (golemFistR != null) golemFistR.localPosition = golemFistRBase + new Vector3(0f, Mathf.Sin(age * 1.8f + 2f) * 1.6f, Mathf.Sin(age * 1.1f + 1f) * 1.2f);
        if (golemCore != null)
        {
            float p = 1f + 0.15f * Mathf.Sin(age * 6f);
            golemCore.localScale = new Vector3(3.2f * p, 3.2f * p, 1.6f);
        }
    }
}
