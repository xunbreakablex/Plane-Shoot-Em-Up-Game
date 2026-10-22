using UnityEngine;

// BossBrain: tur dagitimi + Jet, Kopekbaligi ve Yengec.
public partial class BossBrain
{
    private void InitType()
    {
        switch (type)
        {
            case BossType.Jet: JetInit(); break;
            case BossType.Shark: SharkInit(); break;
            case BossType.Crab: CrabInit(); break;
            case BossType.Octopus: OctoInit(); break;
            case BossType.Zeppelin: ZepInit(); break;
            case BossType.Demon: DemonInit(); break;
            case BossType.Angel: AngelInit(); break;
            case BossType.KiWarrior: KiInit(); break;
            case BossType.MagicalGirl: MagicInit(); break;
            case BossType.Eagle: EagleInit(); break;
            case BossType.Golem: GolemInit(); break;
            default: DragonInit(); break;
        }
    }

    private void Move(float dt)
    {
        switch (type)
        {
            case BossType.Jet: JetMove(dt); break;
            case BossType.Shark: SharkMove(dt); break;
            case BossType.Crab: CrabMove(dt); break;
            case BossType.Octopus: OctoMove(dt); break;
            case BossType.Zeppelin: ZepMove(dt); break;
            case BossType.Demon: DemonMove(dt); break;
            case BossType.Angel: AngelMove(dt); break;
            case BossType.KiWarrior: KiMove(dt); break;
            case BossType.MagicalGirl: MagicMove(dt); break;
            case BossType.Eagle: EagleMove(dt); break;
            case BossType.Golem: GolemMove(dt); break;
            default: DragonMove(dt); break;
        }
    }

    private void Attack()
    {
        switch (type)
        {
            case BossType.Jet: JetAttack(); break;
            case BossType.Shark: SharkAttack(); break;
            case BossType.Crab: CrabAttack(); break;
            case BossType.Octopus: OctoAttack(); break;
            case BossType.Zeppelin: ZepAttack(); break;
            case BossType.Demon: DemonAttack(); break;
            case BossType.Angel: AngelAttack(); break;
            case BossType.KiWarrior: KiAttack(); break;
            case BossType.MagicalGirl: MagicAttack(); break;
            case BossType.Eagle: EagleAttack(); break;
            case BossType.Golem: GolemAttack(); break;
            default: DragonAttack(); break;
        }
    }

    private void TickType(float dt)
    {
        switch (type)
        {
            case BossType.Shark: SharkTick(); break;
            case BossType.Crab: CrabTick(); break;
            case BossType.Zeppelin: ZepTick(dt); break;
            case BossType.Golem: TickFists(dt); break;
        }
    }

    private void AnimateType()
    {
        switch (type)
        {
            case BossType.Jet: JetAnimate(); break;
            case BossType.Shark: SharkAnimate(); break;
            case BossType.Crab: CrabAnimate(); break;
            case BossType.Octopus: OctoAnimate(); break;
            case BossType.Zeppelin: ZepAnimate(); break;
            case BossType.Demon: DemonAnimate(); break;
            case BossType.Angel: AngelAnimate(); break;
            case BossType.KiWarrior: KiAnimate(); break;
            case BossType.MagicalGirl: MagicAnimate(); break;
            case BossType.Eagle: EagleAnimate(); break;
            case BossType.Golem: GolemAnimate(); break;
            default: DragonAnimate(); break;
        }
    }

    // ====================================================================== JET

    private Transform jetNose, jetPodL, jetPodR, jetFlameL, jetFlameR;
    private float jetDashNext;

    private void JetInit()
    {
        jetNose = Get("MuzzleNose");
        jetPodL = Get("MuzzlePodL");
        jetPodR = Get("MuzzlePodR");
        jetFlameL = Get("FlameL");
        jetFlameR = Get("FlameR");
        jetDashNext = Time.time + 10f;
    }

    private void JetMove(float dt)
    {
        // Oyuncunun onunde genis, hizli 8 cizer; burnu hareket yonune bakar
        float side = Mathf.Sin(age * 0.55f) * 190f;
        float height = 25f + Mathf.Sin(age * 0.9f) * 40f;
        Steer(Anchor(340f, side, height), 115f + phase * 20f, 85f, dt);

        Vector3 right = Vector3.Cross(Vector3.up, anchorFwd);
        float lateral = Vector3.Dot(vel, right);

        Vector3 face = vel.sqrMagnitude > 64f ? vel.normalized : (player.position - pos).normalized;
        face = Vector3.Lerp(face, (player.position - pos).normalized, 0.35f);
        FaceDir(face, 150f, dt, Mathf.Clamp(-lateral * 0.35f, -55f, 55f));
    }

    private void JetAttack()
    {
        Vector3 nose = jetNose.position;
        Vector3 aim = AimFrom(nose, 240f, 6f);

        switch (step++ % 4)
        {
            case 0:
                Fan(nose, aim, phase >= 1 ? 7 : 5, 28f, 240f, 1);
                break;
            case 1:
                // Kanat ucu toplari: kopan topun atisi susar; ikisi de koptuysa burundan dar yelpaze
                if (jetPodL == null && jetPodR == null)
                {
                    Fan(nose, aim, 5, 16f, 260f, 1);
                    break;
                }
                for (int i = 0; i < 3; i++)
                {
                    Vector3 spread = Random.insideUnitSphere * 0.04f;
                    if (jetPodL != null) Shoot(jetPodL.position, (AimFrom(jetPodL.position, 260f, 4f) + spread).normalized, 260f, 1);
                    if (jetPodR != null) Shoot(jetPodR.position, (AimFrom(jetPodR.position, 260f, 4f) + spread).normalized, 260f, 1);
                }
                break;
            case 2:
                Cone(nose, aim, 8, 9f, 200f, 1, step * 17f);
                break;
            default:
                if (phase >= 1 && Time.time >= jetDashNext)
                {
                    jetDashNext = Time.time + 10f;
                    StartCharge(0.9f, 175f, 1.7f);
                }
                else
                {
                    Fan(nose, aim, 3, 10f, 290f, 2);
                }
                break;
        }

        nextAttack = Mathf.Max(nextAttack, Time.time + Mathf.Lerp(1.9f, 1.15f, phase / 2f));
    }

    private void JetAnimate()
    {
        float boost = Charging ? 1.9f : 1f;
        float f = (1f + 0.18f * Mathf.Sin(age * 32f)) * boost;
        if (jetFlameL != null) jetFlameL.localScale = new Vector3(2f, 2f, 3.4f * f);
        if (jetFlameR != null) jetFlameR.localScale = new Vector3(2f, 2f, 3.4f * f);
    }

    // =================================================================== SHARK

    private Transform sharkJaw, sharkTail, sharkMouth;
    private float sharkJawOpen;
    private bool sharkJawBroken;       // cene koptu: isin yok, yelpaze daralir
    private int sharkPending;          // 0 yok, 1 yelpaze, 2 halka
    private float sharkFireAt;

    private void SharkInit()
    {
        sharkJaw = Get("Jaw");
        sharkTail = Get("Tail");
        sharkMouth = Get("MuzzleMouth");
    }

    private void SharkMove(float dt)
    {
        float side = Mathf.Sin(age * 0.4f) * 130f;
        float height = 10f + Mathf.Sin(age * 0.7f) * 30f;
        Steer(Anchor(370f, side, height), 85f + phase * 18f, 45f, dt);

        Vector3 toPlayer = player.position - pos;
        toPlayer.y *= 0.35f;
        // Isirip gectikten sonra hizla oyuncuya doner (isirik alip geri donmus gibi)
        FaceDir(toPlayer, ChargeReturning ? 360f : 70f, dt);
    }

    private float sharkBiteAt = -10f;

    // Hamle oyuncuyu gecti: cene SAK diye kapanir
    private void OnChargePassed()
    {
        if (type != BossType.Shark) return;
        sharkBiteAt = Time.time;
        Sfx.PlayAt(Sfx.Crack, pos, 1f);
        FollowCamera.AddShake(0.5f, 0.3f);
    }

    private void SharkAttack()
    {
        switch (step++ % 4)
        {
            case 0:
                sharkPending = 1;
                sharkFireAt = Time.time + 0.55f;
                break;
            case 1:
                sharkPending = 2;
                sharkFireAt = Time.time + 0.55f;
                break;
            case 2:
                if (phase >= 1 && !sharkJawBroken) StartBeam(sharkMouth, 1.0f, 1.4f);
                else
                {
                    sharkPending = 1;
                    sharkFireAt = Time.time + 0.55f;
                }
                break;
            default:
                StartCharge(1.0f, 150f, 1.8f);
                break;
        }

        nextAttack = Mathf.Max(nextAttack, Time.time + Mathf.Lerp(2.4f, 1.5f, phase / 2f));
    }

    private void SharkTick()
    {
        if (sharkPending != 0 && Time.time >= sharkFireAt)
        {
            Vector3 mouth = sharkMouth.position;
            Vector3 aim = AimFrom(mouth, 220f, 4f);

            if (sharkPending == 1) Fan(mouth, aim, sharkJawBroken ? 4 : 7, sharkJawBroken ? 24f : 38f, 220f, 1);
            else Cone(mouth, aim, 12, 10f, 190f, 2, Random.value * 90f);

            sharkPending = 0;
        }
    }

    private void SharkAnimate()
    {
        bool open = sharkPending != 0 || beamActive || (Charging && !ChargeReturning);
        bool bite = Time.time - sharkBiteAt < 0.35f;
        sharkJawOpen = Mathf.MoveTowards(sharkJawOpen, open ? 1f : 0f, (bite ? 14f : 3f) * Time.deltaTime);

        if (sharkJaw != null) sharkJaw.localRotation = Quaternion.Euler(40f * sharkJawOpen, 0f, 0f);
        if (sharkTail != null) sharkTail.localRotation = Quaternion.Euler(0f, Mathf.Sin(age * 3.2f) * 24f, 0f);
    }

    // ==================================================================== CRAB

    private Transform crabMuzzleL, crabMuzzleR, crabMuzzleBody;
    private Transform crabArmL, crabArmR;
    private Transform crabTopL, crabBotL, crabTopR, crabBotR;
    private readonly Transform[] crabLegs = new Transform[6];
    private float crabSweepEnd, crabSweepStart, crabSweepNext;
    private Vector3 crabSweepBase;

    private bool CrabSweeping => Time.time < crabSweepEnd;

    private void CrabInit()
    {
        crabMuzzleL = Get("MuzzleClawL");
        crabMuzzleR = Get("MuzzleClawR");
        crabMuzzleBody = Get("MuzzleBody");
        crabArmL = Get("ArmL");
        crabArmR = Get("ArmR");
        crabTopL = Get("PincerTopL");
        crabBotL = Get("PincerBotL");
        crabTopR = Get("PincerTopR");
        crabBotR = Get("PincerBotR");
        for (int i = 0; i < 3; i++)
        {
            crabLegs[i] = Get("LegL" + i);
            crabLegs[3 + i] = Get("LegR" + i);
        }
    }

    private void CrabMove(float dt)
    {
        float side = Mathf.Sin(age * 0.33f) * 215f;
        float height = Mathf.Sin(age * 0.6f) * 25f;
        Steer(Anchor(330f, side, height), 95f + phase * 18f, 55f, dt);

        Vector3 toPlayer = player.position - pos;
        toPlayer.y *= 0.2f;
        FaceDir(toPlayer, 80f, dt);
    }

    private void CrabAttack()
    {
        Vector3 bodyPos = crabMuzzleBody.position;

        switch (step++ % 4)
        {
            case 0:
                // Kiskaclar: kopan kiskac susar; ikisi de koptuysa govdeden halka
                if (crabMuzzleL != null) Fan(crabMuzzleL.position, AimFrom(crabMuzzleL.position, 230f, 5f), 3, 12f, 230f, 1);
                if (crabMuzzleR != null) Fan(crabMuzzleR.position, AimFrom(crabMuzzleR.position, 230f, 5f), 3, 12f, 230f, 1);
                if (crabMuzzleL == null && crabMuzzleR == null) Cone(bodyPos, AimFrom(bodyPos, 190f, 4f), 10, 10f, 190f, 1, Random.value * 90f);
                break;
            case 1:
                if (mgr.RushActive) StartPincer();       // boss fazi imza: kiskac (once ortada kal, sonra yana kac)
                else StartCrabSweep(2.4f);
                break;
            case 2:
                Cone(bodyPos, AimFrom(bodyPos, 190f, 4f), 12, 10f, 190f, 2, Random.value * 90f);
                break;
            default:
                if (phase >= 1)
                {
                    StartCrabSweep(2.8f);
                    Fan(bodyPos, AimFrom(bodyPos, 220f, 4f), 5, 30f, 220f, 1);
                }
                else
                {
                    Fan(bodyPos, AimFrom(bodyPos, 220f, 4f), 5, 30f, 220f, 1);
                }
                break;
        }

        nextAttack = Mathf.Max(nextAttack, Time.time + Mathf.Lerp(2.4f, 1.5f, phase / 2f));
    }

    private void StartCrabSweep(float duration)
    {
        if (crabMuzzleL == null && crabMuzzleR == null) return;   // iki kiskac da koptu: supurme yok
        crabSweepStart = Time.time;
        crabSweepEnd = Time.time + duration;
        crabSweepNext = Time.time;
        crabSweepBase = (player.position - crabMuzzleBody.position).normalized;
        nextAttack = Mathf.Max(nextAttack, crabSweepEnd + 0.3f);
    }

    private void CrabTick()
    {
        if (!CrabSweeping) return;

        while (Time.time >= crabSweepNext)
        {
            crabSweepNext += 0.09f;

            // Her kiskac farkli fazda yatay suprulur: oyuncu aralardan gecmeli
            float t = Time.time - crabSweepStart;
            float angL = Mathf.Sin(t * 2.6f) * 34f;
            float angR = Mathf.Sin(t * 2.6f + Mathf.PI) * 34f;
            if (crabMuzzleL != null) Shoot(crabMuzzleL.position, Quaternion.AngleAxis(angL, Vector3.up) * crabSweepBase, 215f, 1);
            if (crabMuzzleR != null) Shoot(crabMuzzleR.position, Quaternion.AngleAxis(angR, Vector3.up) * crabSweepBase, 215f, 1);
        }
    }

    private void CrabAnimate()
    {
        float open = 0.3f + 0.25f * Mathf.Sin(age * 1.3f) + (CrabSweeping ? 0.35f : 0f);
        if (crabTopL != null) crabTopL.localRotation = Quaternion.Euler(0f, 0f, open * 30f);
        if (crabBotL != null) crabBotL.localRotation = Quaternion.Euler(0f, 0f, -open * 30f);
        if (crabTopR != null) crabTopR.localRotation = Quaternion.Euler(0f, 0f, open * 30f);
        if (crabBotR != null) crabBotR.localRotation = Quaternion.Euler(0f, 0f, -open * 30f);

        float sway = Mathf.Sin(age * 1.5f) * 6f;
        if (crabArmR != null) crabArmR.localRotation = Quaternion.Euler(0f, -48f + sway, 0f);
        if (crabArmL != null) crabArmL.localRotation = Quaternion.Euler(0f, 228f - sway, 0f);

        for (int i = 0; i < crabLegs.Length; i++)
        {
            if (crabLegs[i] == null) continue;
            float s = i < 3 ? -1f : 1f;
            float wob = Mathf.Sin(age * 3f + i * 1.3f) * 9f;
            crabLegs[i].localRotation = Quaternion.Euler(0f, 0f, s * -55f + wob);
        }
    }
}
