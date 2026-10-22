using System.Collections.Generic;
using UnityEngine;

// BossBrain: Ahtapot, Hava Zirhlisi (zeplin) ve Ejderha.
public partial class BossBrain
{
    // ================================================================== OCTOPUS

    private Transform octoEyes;
    private Transform[,] octoSeg;

    private void OctoInit()
    {
        octoEyes = Get("MuzzleEyes");
        octoSeg = new Transform[BossBuilder.OctoTentacles, BossBuilder.OctoSegments];
        for (int t = 0; t < BossBuilder.OctoTentacles; t++)
            for (int k = 0; k < BossBuilder.OctoSegments; k++)
                octoSeg[t, k] = Get($"T{t}_{k}");
    }

    private void OctoMove(float dt)
    {
        float side = Mathf.Sin(age * 0.28f) * 110f;
        float height = 35f + Mathf.Sin(age * 0.5f) * 45f;
        Steer(Anchor(380f, side, height), 70f + phase * 16f, 40f, dt);

        Vector3 toPlayer = player.position - pos;
        toPlayer.y *= 0.3f;
        FaceDir(toPlayer, 55f, dt);
    }

    private void OctoAttack()
    {
        Vector3 eyes = octoEyes.position;
        Vector3 aim = AimFrom(eyes, 200f, 4f);

        switch (step++ % 4)
        {
            case 0:
                // Boss fazinda imza saldiri: dikey dokunac tokadi (oyuncunun yanal konumuna kilitli; yana kac)
                if (mgr.RushActive)
                    StartSweep(octoEyes, true, phase >= 2 ? 2 : 1, new Color(0.95f, 0.75f, 1f, 1f), new Color(0.55f, 0.15f, 0.75f, 0.6f),
                        "DOKUNAÇ TOKADI: sağa ya da sola kaç!");
                else StartSpray(octoEyes, 3.2f, 3 + phase, 14f, 0.07f, 190f);
                break;
            case 1:
            {
                // Her dokuncak ucu hedefe birer mermi gonderir (kopan dokunac susar)
                int fired = 0;
                for (int t = 0; t < BossBuilder.OctoTentacles; t++)
                {
                    Transform tip = octoSeg[t, BossBuilder.OctoSegments - 1];
                    if (tip == null) continue;
                    Shoot(tip.position, AimFrom(tip.position, 230f, 7f), 230f, 1);
                    fired++;
                }
                if (fired == 0) Fan(eyes, aim, 5, 26f, 215f, 1);
                break;
            }
            case 2:
                Cone(eyes, aim, 14, 10f, 180f, 2, Random.value * 90f);
                Fan(eyes, aim, 5, 26f, 215f, 1);
                break;
            default:
                if (phase >= 1) StartBeam(octoEyes, 1.1f, 1.6f);
                else Fan(eyes, aim, 7, 36f, 215f, 1);
                break;
        }

        nextAttack = Mathf.Max(nextAttack, Time.time + Mathf.Lerp(2.5f, 1.6f, phase / 2f));
    }

    private void OctoAnimate()
    {
        if (octoSeg == null) return;

        float breath = 1f + Mathf.Sin(age * 1.4f) * 0.04f;
        for (int t = 0; t < BossBuilder.OctoTentacles; t++)
        {
            float a = t * Mathf.PI * 2f / BossBuilder.OctoTentacles;
            Vector3 outDir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            Vector3 tangent = new Vector3(-Mathf.Sin(a), 0f, Mathf.Cos(a));
            Vector3 baseP = outDir * 6.5f + new Vector3(0f, -2f, 0f);

            for (int k = 0; k < BossBuilder.OctoSegments; k++)
            {
                Transform seg = octoSeg[t, k];
                if (seg == null) continue;

                float along = (k + 1) * 4.4f;
                float amp = (k + 1) * 1.1f;
                Vector3 p = baseP
                            + outDir * (along * 0.75f) * breath
                            + Vector3.down * (along * 0.55f)
                            + tangent * Mathf.Sin(age * 1.7f + k * 0.6f + t) * amp
                            + Vector3.up * Mathf.Sin(age * 1.2f + k * 0.45f + t * 0.7f) * amp * 0.5f;
                seg.localPosition = p;
            }
        }
    }

    // ================================================================= ZEPPELIN

    private readonly Transform[] zepTurret = new Transform[BossBuilder.ZepTurrets];
    private readonly Transform[] zepMuzzle = new Transform[BossBuilder.ZepTurrets];
    private readonly int[] zepBurst = new int[BossBuilder.ZepTurrets];
    private readonly float[] zepNext = new float[BossBuilder.ZepTurrets];
    private Transform zepBelly, zepNose;

    private void ZepInit()
    {
        for (int i = 0; i < BossBuilder.ZepTurrets; i++)
        {
            zepTurret[i] = Get("Turret" + i);
            zepMuzzle[i] = Get("MuzzleTurret" + i);
            zepNext[i] = Time.time + 2f + i * 0.7f;
        }
        zepBelly = Get("MuzzleBelly");
        zepNose = Get("MuzzleNose");
    }

    private void ZepMove(float dt)
    {
        // Yavas, agir: oyuncunun onunden yanlamasina gecer; burnu gidis yonune bakar
        float side = Mathf.Sin(age * 0.17f) * 290f;
        float height = 45f + Mathf.Sin(age * 0.4f) * 15f;
        Steer(Anchor(430f, side, height), 55f + phase * 10f, 14f, dt);

        Vector3 flat = new Vector3(vel.x, 0f, vel.z);
        Vector3 dir = flat.sqrMagnitude > 25f ? flat : (player.position - pos);
        dir.y = 0f;
        FaceDir(dir, 22f, dt);
    }

    private void ZepTick(float dt)
    {
        PlaneController plane = mgr.flight != null ? mgr.flight.Current : null;
        bool alive = plane != null && !plane.IsCrashed;

        for (int i = 0; i < BossBuilder.ZepTurrets; i++)
        {
            Transform tr = zepTurret[i];
            if (tr == null) continue;

            // Kule oyuncuya doner
            Vector3 to = player.position - tr.position;
            if (to.sqrMagnitude > 1f)
                tr.rotation = Quaternion.Slerp(tr.rotation, Quaternion.LookRotation(to.normalized, Vector3.up),
                    1f - Mathf.Exp(-5f * dt));

            if (!alive) continue;

            if (zepBurst[i] > 0)
            {
                // Seri: 4 mermi, 0.14 sn arayla; sonra faza gore kisalan bekleme
                if (Time.time >= zepNext[i])
                {
                    zepBurst[i]--;
                    Vector3 m = zepMuzzle[i].position;
                    Shoot(m, AimFrom(m, 240f, 9f), 240f, 1);

                    zepNext[i] = zepBurst[i] > 0
                        ? Time.time + 0.14f
                        : Time.time + Random.Range(2.6f, 3.8f) * (1f - 0.2f * phase);
                }
            }
            else if (Time.time >= zepNext[i])
            {
                zepBurst[i] = 4;
                zepNext[i] = Time.time;
            }
        }
    }

    private void ZepAttack()
    {
        Vector3 nose = zepNose.position;

        switch (step++ % 3)
        {
            case 0:
            {
                // Bomba yagmuru: karin altindan yavas, genis bir salvo
                Vector3 belly = zepBelly.position;
                Vector3 baseDir = AimFrom(belly, 90f, 0f);
                for (int i = 0; i < 12; i++)
                {
                    Vector3 d = Quaternion.Euler(Random.Range(-14f, 14f), Random.Range(-26f, 26f), 0f) * baseDir;
                    Shoot(belly, d.normalized, 80f, 1, 9f);
                }
                break;
            }
            case 1:
                Fan(nose, AimFrom(nose, 200f, 4f), 7, 40f, 200f, 1);
                break;
            default:
                if (phase >= 1) Cone(nose, AimFrom(nose, 190f, 4f), 12, 10f, 190f, 2, Random.value * 90f);
                else Fan(nose, AimFrom(nose, 200f, 4f), 5, 28f, 200f, 1);
                break;
        }

        nextAttack = Mathf.Max(nextAttack, Time.time + Mathf.Lerp(3.2f, 2f, phase / 2f));
    }

    private void ZepAnimate()
    {
        // Govde sabit; kuleler ZepTick'te donuyor
    }

    // ================================================================== DRAGON

    private Transform dragonJaw, dragonMouth, dragonWingL, dragonWingR;
    private bool dragonJawBroken;      // cene koptu: alev isini yok
    private Transform[] dragonSeg;
    private readonly List<Vector3> trail = new List<Vector3>();
    private const float SegSpacingLocal = 6.6f;     // govde olcegiyle carpilir
    private float SegSpacing => SegSpacingLocal * transform.lossyScale.x;

    private void DragonInit()
    {
        dragonJaw = Get("Jaw");
        dragonMouth = Get("MuzzleMouth");
        dragonWingL = Get("WingL");
        dragonWingR = Get("WingR");

        dragonSeg = new Transform[BossBuilder.DragonSegments];
        for (int k = 0; k < dragonSeg.Length; k++) dragonSeg[k] = Get("Seg" + k);

        // Iz: basin arkasina duz bir hat, segmentler ilk karede gerilmis dursun
        Vector3 back = -transform.forward;
        for (int i = 0; i < 220; i++)
            trail.Add(transform.position + back * (i * 1.5f));
    }

    private void DragonMove(float dt)
    {
        // Oyuncunun onunde gezinen Lissajous egrisi: govde S seklinde kivrilir
        float sp = 1f + phase * 0.25f;
        Vector3 right = Vector3.Cross(Vector3.up, anchorFwd);
        Vector3 center = Anchor(360f, 0f, 25f);
        float amp = mgr.RushActive ? RushSideScale : 1f;   // boss fazinda ekranin ortasinda kalir
        Vector3 target = center
                         + right * Mathf.Sin(age * 0.5f * sp) * 200f * amp
                         + Vector3.up * Mathf.Sin(age * 0.9f * sp + 1f) * 75f * amp
                         + anchorFwd * Mathf.Cos(age * 0.35f * sp) * 130f * amp;

        Steer(target, 105f + phase * 20f, 75f, dt);

        Vector3 face = vel.sqrMagnitude > 25f ? vel : (player.position - pos);
        FaceDir(face, 110f, dt);
    }

    private void DragonAttack()
    {
        Vector3 mouth = dragonMouth.position;

        switch (step++ % 4)
        {
            case 0:
                Fan(mouth, AimFrom(mouth, 240f, 4f), 5, 22f, 240f, 1);
                break;
            case 1:
                // Boss fazinda imza saldiri: yatay ates nefesi (oyuncunun yuksekligine kilitli; yukari/asagi kac)
                if (mgr.RushActive)
                    StartSweep(dragonMouth, false, phase >= 2 ? 2 : 1, new Color(1f, 0.92f, 0.6f, 1f), new Color(1f, 0.38f, 0.08f, 0.6f),
                        "ATEŞ NEFESİ: yukarı ya da aşağı kaç!");
                else StartSpray(dragonMouth, 2.6f, 2 + phase, 12f, 0.08f, 200f);
                break;
            case 2:
                if (phase >= 1 && !dragonJawBroken) StartBeam(dragonMouth, 1.2f, 1.5f);
                else Cone(mouth, AimFrom(mouth, 190f, 4f), 12, 10f, 190f, 2, Random.value * 90f);
                break;
            default:
                StartCharge(1.2f, 150f, 2.0f);
                break;
        }

        nextAttack = Mathf.Max(nextAttack, Time.time + Mathf.Lerp(2.2f, 1.3f, phase / 2f));
    }

    private void DragonAnimate()
    {
        if (dragonSeg == null) return;

        // Isi/kafa konumunu iza ekle (render karesindeki yumusatilmis konum)
        Vector3 head = transform.position;
        if (trail.Count == 0 || (trail[0] - head).sqrMagnitude > 1f)
            trail.Insert(0, head);
        if (trail.Count > 420) trail.RemoveRange(420, trail.Count - 420);

        for (int k = 0; k < dragonSeg.Length; k++)
        {
            Transform seg = dragonSeg[k];
            if (seg == null) continue;

            float d = (k + 1) * SegSpacing;
            PointOnTrail(head, d, out Vector3 p, out Vector3 dir);
            seg.position = p;
            if (dir.sqrMagnitude > 0.0001f) seg.rotation = Quaternion.LookRotation(dir, Vector3.up);
        }

        Transform blade = Get("TailBlade");
        if (blade != null)
        {
            PointOnTrail(head, (dragonSeg.Length + 1.2f) * SegSpacing, out Vector3 bp, out Vector3 bd);
            blade.position = bp;
            if (bd.sqrMagnitude > 0.0001f) blade.rotation = Quaternion.LookRotation(bd, Vector3.up);
        }

        // Kanat cirpma, cene
        float flap = Mathf.Sin(age * 4.2f) * 26f;
        if (dragonWingL != null) dragonWingL.localRotation = Quaternion.Euler(0f, 0f, -flap);
        if (dragonWingR != null) dragonWingR.localRotation = Quaternion.Euler(0f, 0f, flap);

        bool open = beamActive || Spraying || Charging;
        if (dragonJaw != null)
            dragonJaw.localRotation = Quaternion.Euler(open ? 34f : 6f + Mathf.Sin(age * 2f) * 3f, 0f, 0f);
    }

    // Iz uzerinde bastan verilen mesafedeki nokta
    private void PointOnTrail(Vector3 head, float distance, out Vector3 point, out Vector3 dir)
    {
        float acc = 0f;
        Vector3 prev = head;
        point = head;
        dir = transform.forward;

        for (int i = 0; i < trail.Count; i++)
        {
            Vector3 p = trail[i];
            float len = (prev - p).magnitude;
            if (len < 0.001f) continue;

            if (acc + len >= distance)
            {
                float t = (distance - acc) / len;
                point = Vector3.Lerp(prev, p, t);
                dir = (prev - p) / len;
                return;
            }

            acc += len;
            prev = p;
        }

        point = prev;
    }
}
