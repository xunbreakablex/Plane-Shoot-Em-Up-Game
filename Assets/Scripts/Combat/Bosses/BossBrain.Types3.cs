using UnityEngine;

// BossBrain: Iblis, Melek, Ki Savascisi, Yildiz Buyucusu (BossBuilder.Humanoids.cs ile eslesir).
// Saldiri bilesenleri BossBrain.cs'deki ortak yardimcilardir: Fan, Cone, StartSpray, StartBeam, StartCharge.
public partial class BossBrain
{
    // ------------------------------------------------------------ ortak yardimcilar

    // Oyuncunun onunde gezinme: yan salinim + yukseklik salinimi
    private void HoverMove(float dt, float dist, float sideAmp, float height, float heightAmp, float speed, float freq)
    {
        float sp = 1f + phase * 0.2f;
        Vector3 right = Vector3.Cross(Vector3.up, anchorFwd);
        Vector3 target = Anchor(dist + Mathf.Cos(age * freq * 0.6f * sp) * 70f, Mathf.Sin(age * freq * sp) * sideAmp,
            height + Mathf.Sin(age * freq * 1.3f * sp + 1f) * heightAmp);
        Steer(target, speed + phase * 15f, speed * 0.8f, dt);
        FaceDir(player.position - pos, 120f, dt);
    }

    // Gokten yagan mermiler: yavas, genis alana yayilir (kacmak icin ogren-ve-kac)
    private void RainFromAbove(int count, float speed, float spread)
    {
        Vector3 right = Vector3.Cross(Vector3.up, anchorFwd);
        Vector3 tgt = player.position + mgr.PlayerVelocity * 1.1f;
        for (int i = 0; i < count; i++)
        {
            Vector3 origin = tgt + anchorFwd * Random.Range(120f, 320f) + Vector3.up * Random.Range(200f, 320f)
                             + right * Random.Range(-spread, spread);
            Vector3 aim = tgt + right * Random.Range(-45f, 45f) + Vector3.up * Random.Range(-20f, 20f);
            Shoot(origin, (aim - origin).normalized, speed, 1, 7f);
        }
    }

    // ================================================================== IBLIS

    private Transform demonWingL, demonWingR, demonOrbL, demonOrbR, demonMouth, demonMuzzleL, demonMuzzleR;

    private void DemonInit()
    {
        demonWingL = Get("WingL"); demonWingR = Get("WingR");
        demonOrbL = Get("OrbL"); demonOrbR = Get("OrbR");
        demonMouth = Get("MuzzleMouth");
        demonMuzzleL = Get("MuzzleOrbL"); demonMuzzleR = Get("MuzzleOrbR");
    }

    private void DemonMove(float dt) { HoverMove(dt, 330f, 170f, 20f, 40f, 95f, 0.55f); }

    private void DemonAttack()
    {
        switch (step++ % 4)
        {
            case 0:
            {
                // Iki elden ates topu yelpazesi
                Vector3 a = demonMuzzleL.position, b = demonMuzzleR.position;
                Fan(a, AimFrom(a, 200f, 4f), 5, 24f, 200f, 1);
                Fan(b, AimFrom(b, 200f, 4f), 5, 24f, 200f, 1);
                break;
            }
            case 1:
                StartSpray(demonMuzzleR, 2.2f, 2 + phase, 14f, 0.09f, 180f);
                break;
            case 2:
                RainFromAbove(8 + phase * 4, 130f, 140f);
                break;
            default:
                if (phase >= 1) StartBeam(demonMouth, 1.1f, 1.3f);
                else StartCharge(1.0f, 140f, 1.8f);
                break;
        }

        nextAttack = Mathf.Max(nextAttack, Time.time + Mathf.Lerp(2.6f, 1.6f, phase / 2f));
    }

    private void DemonAnimate()
    {
        float flap = Mathf.Sin(age * 4.6f) * 24f;
        if (demonWingL != null) demonWingL.localRotation = Quaternion.Euler(0f, 0f, -flap);
        if (demonWingR != null) demonWingR.localRotation = Quaternion.Euler(0f, 0f, flap);
        float pulse = 1f + 0.12f * Mathf.Sin(age * 9f);
        if (demonOrbL != null) demonOrbL.localScale = Vector3.one * 3.2f * pulse;
        if (demonOrbR != null) demonOrbR.localScale = Vector3.one * 3.2f * pulse;
    }

    // ================================================================== MELEK

    private Transform angelWingL, angelWingR, angelHalo, angelStaff, angelHaloMuzzle;

    private void AngelInit()
    {
        angelWingL = Get("WingL"); angelWingR = Get("WingR");
        angelHalo = Get("Halo");
        angelStaff = Get("MuzzleStaff");
        angelHaloMuzzle = Get("MuzzleHalo");
    }

    private void AngelMove(float dt) { HoverMove(dt, 380f, 220f, 40f, 50f, 85f, 0.4f); }

    private void AngelAttack()
    {
        switch (step++ % 4)
        {
            case 0:
                StartSpray(angelStaff, 3.0f, 3, 20f, 0.07f, 160f);   // tuy yagmuru
                break;
            case 1:
            {
                Vector3 h = angelHaloMuzzle.position;
                Cone(h, AimFrom(h, 170f, 3f), 14 + phase * 4, 10f, 170f, 1, Random.value * 90f);
                break;
            }
            case 2:
                if (phase >= 1) StartBeam(angelStaff, 1.3f, 1.4f);
                else Fan(angelStaff.position, AimFrom(angelStaff.position, 210f, 4f), 7, 40f, 210f, 1);
                break;
            default:
                StartCharge(1.1f, 165f, 1.8f);                       // mizrak dalisi
                break;
        }

        nextAttack = Mathf.Max(nextAttack, Time.time + Mathf.Lerp(2.8f, 1.7f, phase / 2f));
    }

    private void AngelAnimate()
    {
        float flap = Mathf.Sin(age * 2.6f) * 16f;
        if (angelWingL != null) angelWingL.localRotation = Quaternion.Euler(0f, 0f, -flap);
        if (angelWingR != null) angelWingR.localRotation = Quaternion.Euler(0f, 0f, flap);
        if (angelHalo != null) angelHalo.localRotation = Quaternion.Euler(0f, age * 70f, 0f);
    }

    // ============================================================ KI SAVASCISI

    private Transform kiHandL, kiHandR, kiAura, kiHair, kiFistL, kiFistR;

    private void KiInit()
    {
        kiHandL = Get("MuzzleHandL"); kiHandR = Get("MuzzleHandR");
        kiAura = Get("Aura"); kiHair = Get("Hair");
        kiFistL = Get("FistL"); kiFistR = Get("FistR");
    }

    private void KiMove(float dt) { HoverMove(dt, 300f, 220f, 15f, 35f, 135f, 0.9f); }

    private void KiAttack()
    {
        switch (step++ % 5)
        {
            case 0:
                StartSpray(kiHandR, 1.7f, 1, 3f, 0.05f, 230f);         // hizli ki atesi akisi
                break;
            case 1:
            {
                Vector3 a = kiHandL.position, b = kiHandR.position;
                Fan(a, AimFrom(a, 210f, 4f), 7, 30f, 210f, 1);
                Fan(b, AimFrom(b, 210f, 4f), 7, 30f, 210f, 1);
                break;
            }
            case 2:
                StartBeam(kiHandR, 1.2f, 1.6f);                        // ki isini
                break;
            case 3:
                StartCharge(0.9f, 195f, 1.7f);                         // atilma
                break;
            default:
                Cone(transform.position, AimFrom(transform.position, 120f, 0f), 18 + phase * 6, 12f, 110f, 2, Random.value * 90f);   // ki patlamasi
                break;
        }

        nextAttack = Mathf.Max(nextAttack, Time.time + Mathf.Lerp(2.0f, 1.2f, phase / 2f));
    }

    private void KiAnimate()
    {
        float pulse = 1f + 0.18f * Mathf.Sin(age * 12f);
        if (kiAura != null)
        {
            kiAura.localScale = new Vector3(pulse, pulse * (1f + phase * 0.15f), 1f);
            kiAura.localRotation = Quaternion.Euler(0f, 0f, age * 40f);
        }
        if (kiHair != null) kiHair.localScale = new Vector3(1f, 1f + 0.08f * Mathf.Sin(age * 16f), 1f);
        float fist = (2.6f + phase * 0.4f) * (1f + 0.1f * Mathf.Sin(age * 10f));
        if (kiFistL != null) kiFistL.localScale = Vector3.one * fist;
        if (kiFistR != null) kiFistR.localScale = Vector3.one * fist;
    }

    // ======================================================= YILDIZ BUYUCUSU

    private Transform starPivot, starMuzzle, magicTailL, magicTailR, magicWingL, magicWingR;

    private void MagicInit()
    {
        starPivot = Get("Star"); starMuzzle = Get("MuzzleStar");
        magicTailL = Get("TailL"); magicTailR = Get("TailR");
        magicWingL = Get("WingL"); magicWingR = Get("WingR");
    }

    private void MagicMove(float dt) { HoverMove(dt, 340f, 200f, 25f, 45f, 110f, 0.65f); }

    private void MagicAttack()
    {
        Vector3 m = starMuzzle.position;

        switch (step++ % 6)
        {
            case 0:
                Cone(m, AimFrom(m, 180f, 3f), 14, 9f, 180f, 1, Random.value * 90f);               // yildiz halkasi
                break;
            case 1:
                StartSpray(starMuzzle, 3.0f, 4 + phase, 16f, 0.1f, 160f);                          // kalp/yildiz sarmali
                break;
            case 2:
                StartBeam(starMuzzle, 1.2f, 1.6f);                                                // gokkusagi isini
                break;
            case 3:
                StartCharge(1.0f, 165f, 1.8f);                                                     // kuyruklu yildiz
                break;
            case 4:
                RainFromAbove(10 + phase * 5, 125f, 160f);                                         // yildiz yagmuru
                break;
            default:
                Fan(m, AimFrom(m, 220f, 4f), 9, 50f, 220f, 1);
                if (phase >= 1) Fan(m, AimFrom(m, 180f, 4f), 7, 34f, 180f, 1);
                break;
        }

        nextAttack = Mathf.Max(nextAttack, Time.time + Mathf.Lerp(2.1f, 1.2f, phase / 2f));
    }

    private void MagicAnimate()
    {
        if (starPivot != null) starPivot.localRotation = Quaternion.Euler(0f, 0f, age * 120f);
        float sway = Mathf.Sin(age * 3f) * 14f;
        if (magicTailL != null) magicTailL.localRotation = Quaternion.Euler(0f, 0f, -sway);
        if (magicTailR != null) magicTailR.localRotation = Quaternion.Euler(0f, 0f, sway);
        float flap = Mathf.Sin(age * 7f) * 18f;
        if (magicWingL != null) magicWingL.localRotation = Quaternion.Euler(0f, 0f, -flap);
        if (magicWingR != null) magicWingR.localRotation = Quaternion.Euler(0f, 0f, flap);
    }
}
