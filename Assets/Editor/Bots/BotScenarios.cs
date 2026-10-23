using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

// Dalga olcumu: oyuncu olumsuz, sabit irtifa / yavas donus, surekli ates. Asamaya atlar ve "duration" sn boyunca
// gelen ucaklari, indirilenleri, dusman mermilerini ve oyuncuya isabetleri sayar (boss'lar kapali).
public class WaveScenario : BotWorker.Scenario
{
    private static readonly FieldInfo fMid = BotWorker.F(typeof(CombatManager), "midBossSpawned");
    private static readonly FieldInfo fBoss = BotWorker.F(typeof(CombatManager), "bossSpawned");
    private static readonly FieldInfo fEnemies = BotWorker.F(typeof(CombatManager), "enemies");
    private static readonly FieldInfo fHeat = BotWorker.F(typeof(PlayerGun), "<Heat>k__BackingField");

    private float t0, lastHit;
    private int hits, fired, planes, killed, bulletHits;
    private Dictionary<int, Vector3> bulletPos = new Dictionary<int, Vector3>(), prevPos = new Dictionary<int, Vector3>();
    private Vector3 lockFwd;
    private readonly HashSet<int> seenB = new HashSet<int>(), deadE = new HashSet<int>();
    private readonly Dictionary<int, EnemyPlane> aliveE = new Dictionary<int, EnemyPlane>();

    public override void Begin()
    {
        CombatManager cm = CombatManager.Instance;
        cm.DebugJumpToStage(job.stage);
        t0 = Time.time;
        PlayerHealth ph = cm.CurrentPlayerHealth;
        lastHit = ph != null ? ph.LastHitTime : -1f;
    }

    public override bool Tick()
    {
        CombatManager cm = CombatManager.Instance;
        if (Time.time - t0 > job.duration) return true;
        PlaneController plane = cm.flight.Current;
        PlayerHealth ph = cm.CurrentPlayerHealth;
        fMid.SetValue(cm, true); fBoss.SetValue(cm, true);
        RunState.DebugSetPower(job.power);

        // Mermiler: yeni atilanlar sayilir; gecen karede oyuncunun yakininda kaybolan varsa isabet mermidendir
        // (yoksa carpma / patlama)
        var swap = prevPos; prevPos = bulletPos; bulletPos = swap;
        bulletPos.Clear();
        foreach (Bullet b in Bullet.Enemy)
        {
            if (b == null) continue;
            int id = b.GetInstanceID();
            bulletPos[id] = b.transform.position;
            if (!seenB.Contains(id)) fired++;
        }
        seenB.Clear(); seenB.UnionWith(bulletPos.Keys);
        bool bulletGone = false;
        Vector3 playerPos = plane.transform.position;
        foreach (var kv in prevPos)
            if (!bulletPos.ContainsKey(kv.Key) && (kv.Value - playerPos).sqrMagnitude < 30f * 30f) { bulletGone = true; break; }

        if (ph != null)
        {
            if (ph.LastHitTime != lastHit) { hits++; if (bulletGone) bulletHits++; lastHit = ph.LastHitTime; }
            ph.SetState(ph.maxHealth, ph.maxShield);
        }
        if (cm.gun != null) fHeat.SetValue(cm.gun, 0f);

        Transform t = plane.transform;
        Rigidbody prb = plane.GetComponent<Rigidbody>();
        if (lockFwd == Vector3.zero)
        {
            lockFwd = Vector3.ProjectOnPlane(t.forward, Vector3.up).normalized;
            if (lockFwd == Vector3.zero) lockFwd = Vector3.forward;
        }
        lockFwd = Quaternion.AngleAxis(6f * Time.deltaTime, Vector3.up) * lockFwd;
        Vector3 pp = prb.position;
        pp.y = Mathf.Max(500f, cm.GroundHeight(pp) + 300f);
        prb.position = pp; t.position = pp;
        prb.rotation = Quaternion.LookRotation(lockFwd, Vector3.up); t.rotation = prb.rotation;
        prb.linearVelocity = lockFwd * 110f; prb.angularVelocity = Vector3.zero;

        foreach (EnemyPlane e in (IList)fEnemies.GetValue(cm))
        {
            if (e == null || e.IsBoss || e.IsGround) continue;
            int id = e.GetInstanceID();
            if (!aliveE.ContainsKey(id)) { aliveE[id] = e; planes++; }
        }
        foreach (var kv in aliveE)
            if (!deadE.Contains(kv.Key) && (kv.Value == null || kv.Value.IsDead)) { deadE.Add(kv.Key); if (kv.Value != null) killed++; }
        BotInput.Fire = true;
        return false;
    }

    public override string Result()
    {
        return "planes=" + planes + "\tkilled=" + killed + "\tfired=" + fired + "\thits=" + hits + "\tbulletHits=" + bulletHits;
    }
}

// Boss olcumu: boss'u (ara boss olarak, asama gecisi olmasin) cagirir, oyuncuyu olumsuz tutar, boss'un nisan
// noktasina kilitlenip saga sola kayarak ates eder. Govde korumasinin dustugu an ve olum suresi (ttk).
public class BossScenario : BotWorker.Scenario
{
    private static readonly FieldInfo fMid = BotWorker.F(typeof(CombatManager), "midBossSpawned");
    private static readonly FieldInfo fBoss = BotWorker.F(typeof(CombatManager), "bossSpawned");
    private static readonly FieldInfo fWave = BotWorker.F(typeof(CombatManager), "waveIndex");
    private static readonly FieldInfo fAim = BotWorker.F(typeof(PlaneController), "rushAim");
    private static readonly FieldInfo fHeat = BotWorker.F(typeof(PlayerGun), "<Heat>k__BackingField");

    private int state;
    private float t0, waitUntil, guardAt = -1f, ttk = -1f;
    private bool dead;
    private EnemyPlane boss;
    private BossBrain brain;

    public override void Begin()
    {
        CombatManager.Instance.DebugJumpToStage(0);
        waitUntil = Time.time + 3f;
    }

    public override bool Tick()
    {
        CombatManager cm = CombatManager.Instance;
        PlaneController plane = cm.flight.Current;
        PlayerHealth ph = cm.CurrentPlayerHealth;
        fMid.SetValue(cm, true); fBoss.SetValue(cm, true); fWave.SetValue(cm, 9999);
        RunState.DebugSetPower(job.power);
        if (cm.gun != null) fHeat.SetValue(cm.gun, 0f);
        if (ph != null) ph.SetState(ph.maxHealth, ph.maxShield);

        if (state == 0)
        {
            if (Time.time < waitUntil) return false;
            boss = cm.SpawnBossEncounter((BossType)Enum.Parse(typeof(BossType), job.boss), EnemyKind.MidBoss);
            brain = boss.GetComponent<BossBrain>();
            t0 = Time.time;
            state = 1;
            return false;
        }
        float el = Time.time - t0;
        if (brain != null && guardAt < 0f && !brain.BodyProtected) guardAt = el;
        if (boss == null || boss.IsDead || el > job.duration)
        {
            dead = boss == null || boss.IsDead;
            ttk = el;
            return true; // sonraki is sahneyi yeniden yukler
        }
        if (cm.RushActive)
        {
            Vector3 to = boss.AimPoint - plane.transform.position;
            Vector3 local = Quaternion.Inverse(Quaternion.LookRotation(cm.RushHeading, Vector3.up)) * to;
            fAim.SetValue(plane, new Vector2(Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg,
                Mathf.Atan2(local.y, new Vector2(local.x, local.z).magnitude) * Mathf.Rad2Deg));
            BotInput.Fire = true;
            BotInput.Strafe = Mathf.Repeat(el, 2.2f) < 1.1f ? -1f : 1f;
        }
        return false;
    }

    public override string Result()
    {
        return "guard=" + (guardAt < 0f ? "-" : guardAt.ToString("0")) + "\tttk=" + ttk.ToString("0") + "\tdead=" + (dead ? 1 : 0);
    }
}
