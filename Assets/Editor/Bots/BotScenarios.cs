using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

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

// Akis testi: asamayi bastan sona oynar (dalgalar, orta boss, us, boss, asama sonu). Oyuncu olumsuz; hava dusmani
// "killAfter" sn yasayinca vurulmus sayilir (iyi oyuncu taklidi), us hedefleri 25 sn'den sonra sirayla yikilir;
// boss fazinda BossScenario'nun nisan botu oynar, 200 sn'de bitmezse boss zorla oldurulur (forced). Asama sonu
// gelmezse "duration" sonunda takilma durumu yazilir. Hatalar BotWorker'da sayilir.
public class FlowScenario : BotWorker.Scenario
{
    private static readonly FieldInfo fEnemies = BotWorker.F(typeof(CombatManager), "enemies");
    private static readonly FieldInfo fWave = BotWorker.F(typeof(CombatManager), "waveIndex");
    private static readonly FieldInfo fMid = BotWorker.F(typeof(CombatManager), "midBossSpawned");
    private static readonly FieldInfo fBossSp = BotWorker.F(typeof(CombatManager), "bossSpawned");
    private static readonly FieldInfo fAim = BotWorker.F(typeof(PlaneController), "rushAim");
    private static readonly FieldInfo fHeat = BotWorker.F(typeof(PlayerGun), "<Heat>k__BackingField");
    private static readonly FieldInfo fGuard = BotWorker.F(typeof(BossBrain), "limbGuardOff");

    private static readonly MethodInfo mClose = typeof(StageClearUI).GetMethod("Close", BindingFlags.NonPublic | BindingFlags.Instance);

    private float t0, stageT0, lastGroundKill, clearAt = -1f, bossT0;
    private readonly System.Text.StringBuilder stages = new System.Text.StringBuilder();
    private float tMid = -1f, midTtk = -1f, tBoss = -1f, bossTtk = -1f;
    private bool midForced, bossForced, uiOpen;
    private int airKills, groundKills;
    private EnemyPlane fighting;
    private Vector3 lockFwd;
    private string timeoutState;
    private readonly Dictionary<int, float> born = new Dictionary<int, float>();

    public override void Begin()
    {
        CombatManager.Instance.DebugJumpToStage(job.stage);
        t0 = stageT0 = Time.time;
    }

    public override bool Tick()
    {
        CombatManager cm = CombatManager.Instance;
        float total = Time.time - t0;
        float el = Time.time - stageT0;
        PlaneController plane = cm.flight.Current;
        PlayerHealth ph = cm.CurrentPlayerHealth;
        RunState.DebugSetPower(job.power);
        if (ph != null) ph.SetState(ph.maxHealth, ph.maxShield);
        if (cm.gun != null) fHeat.SetValue(cm.gun, 0f);

        // Asama sonu: ekran acilinca (ya da 60 sn) biter; chain ise sonraki asamaya gecer
        if (RunState.StageIsClear || RunState.Ended)
        {
            BotInput.Clear();
            if (clearAt < 0f)
            {
                clearAt = el;
                if (fighting != null) { if (fighting.Kind == EnemyKind.MidBoss) midTtk = el - bossT0; else bossTtk = el - bossT0; fighting = null; }
            }
            var sc = UnityEngine.Object.FindFirstObjectByType<StageClearUI>();
            var rr = UnityEngine.Object.FindFirstObjectByType<RunResultUI>();
            uiOpen = (sc != null && sc.IsOpen) || (rr != null && rr.IsOpen);
            if (!uiOpen && el - clearAt <= 60f) return false;
            // Zafer ekranindan Sonsuz Savas'a devam (dugmenin yaptigi): asama tamam ekrani acilir, zincir surer
            if (job.chain && job.endless > 0 && rr != null && rr.IsOpen && RunState.Phase == RunPhase.Won && !RunState.Rush)
            {
                typeof(RunResultUI).GetProperty("IsOpen").SetValue(rr, false);
                GameState.UiOpen = false;
                GameState.SetPaused(false);
                RunState.ResumeAsEndless();
                return false;
            }
            bool endlessDone = RunState.Endless && RunState.Stage - (Stages.All.Length - 1) >= job.endless;
            if (job.chain && sc != null && sc.IsOpen && RunState.StageIsClear && !endlessDone)
            {
                stages.Append(StageSummary()).Append(" | ");
                mClose.Invoke(sc, null);
                cm.NextStage();
                stageT0 = Time.time;
                clearAt = tMid = midTtk = tBoss = bossTtk = -1f;
                lastGroundKill = 0f;   // asama saati sifirlandi: us hedefleri yine 25 sn'den sonra
                midForced = bossForced = uiOpen = false;
                lockFwd = Vector3.zero;
                born.Clear();
                return false;
            }
            return true;
        }
        if (total > job.duration)
        {
            IList list = (IList)fEnemies.GetValue(cm);
            int air = 0, ground = 0;
            foreach (EnemyPlane e in list) if (e != null && !e.IsDead) { if (e.IsGround) ground++; else if (!e.IsBoss) air++; }
            timeoutState = "wave=" + fWave.GetValue(cm) + "/" + cm.WaveCount + " mid=" + fMid.GetValue(cm) + " boss=" + fBossSp.GetValue(cm)
                + " base=" + cm.BaseCleared + " rush=" + cm.RushActive + " active=" + (cm.ActiveBoss != null) + " air=" + air + " ground=" + ground
                + " phase=" + RunState.Phase + " paused=" + GameState.Paused;
            return true;
        }

        EnemyPlane boss = cm.ActiveBoss;
        if (boss != null)
        {
            if (boss != fighting)
            {
                fighting = boss;
                bossT0 = el;
                if (boss.Kind == EnemyKind.MidBoss) tMid = el; else tBoss = el;
            }
            if (el - bossT0 > 200f)
            {
                BossBrain br = boss.GetComponent<BossBrain>();
                if (boss.Kind == EnemyKind.MidBoss) midForced = true; else bossForced = true;
                if (br != null) fGuard.SetValue(br, true);
                boss.TakeHit(999999, boss.transform.position);
                if (br != null) fGuard.SetValue(br, false);
            }
            else if (cm.RushActive)
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
        if (fighting != null)
        {
            if (fighting.Kind == EnemyKind.MidBoss) midTtk = el - bossT0; else bossTtk = el - bossT0;
            fighting = null;
        }
        BotInput.Clear();
        if (cm.RushActive) return false;   // boss fazindan donus

        // Ucus: sabit irtifa, yavas donus (arenada kalir), ates
        Transform t = plane.transform;
        Rigidbody prb = plane.GetComponent<Rigidbody>();
        if (lockFwd == Vector3.zero) lockFwd = Vector3.ProjectOnPlane(t.forward, Vector3.up).normalized;
        if (lockFwd == Vector3.zero) lockFwd = Vector3.forward;
        lockFwd = Quaternion.AngleAxis(6f * Time.deltaTime, Vector3.up) * lockFwd;
        Vector3 pp = prb.position;
        pp.y = Mathf.Max(pp.y, cm.GroundHeight(pp) + 150f);
        prb.position = pp; t.position = pp;
        prb.rotation = Quaternion.LookRotation(lockFwd, Vector3.up); t.rotation = prb.rotation;
        prb.linearVelocity = lockFwd * plane.cruiseSpeed; prb.angularVelocity = Vector3.zero;
        BotInput.Fire = true;

        // Dusmanlar: hava "killAfter" sn sonra, yer hedefleri 25 sn'den sonra 2 sn arayla (cekirdek en son)
        EnemyPlane groundPick = null;
        int groundAlive = 0;
        foreach (EnemyPlane e in new List<EnemyPlane>((List<EnemyPlane>)fEnemies.GetValue(cm)))
        {
            if (e == null || e.IsDead || e.IsBoss) continue;
            int id = e.GetInstanceID();
            if (!born.ContainsKey(id)) born[id] = el;
            if (e.IsGround)
            {
                groundAlive++;
                if (groundPick == null || (groundPick.IsCore && !e.IsCore)) groundPick = e;
            }
            else if (el - born[id] > job.killAfter) { e.TakeHit(999999, e.transform.position); airKills++; }
        }
        if (groundPick != null && el > 25f && el - lastGroundKill > job.groundEvery && (!groundPick.IsCore || groundAlive == 1 || el > 120f))
        {
            groundPick.TakeHit(999999, groundPick.transform.position);
            groundKills++;
            lastGroundKill = el;
        }
        return false;
    }

    private string StageSummary()
    {
        return "s" + (RunState.Stage + 1) + " mid@" + tMid.ToString("0") + "/" + midTtk.ToString("0") + (midForced ? "!" : "")
            + " boss@" + tBoss.ToString("0") + "/" + bossTtk.ToString("0") + (bossForced ? "!" : "")
            + " clear@" + clearAt.ToString("0") + " ui=" + (uiOpen ? 1 : 0) + " skor=" + RunState.Score + " hurda=" + RunUpgrades.TotalEarned;
    }

    public override string Result()
    {
        return "stages=" + stages + StageSummary() + "\tphase=" + RunState.Phase + (RunState.Rush ? "\trush" : "") + (RunState.Endless ? "\tendless" : "") + "\ttotal=" + (Time.time - t0).ToString("0")
            + "\tair=" + airKills + "\tground=" + groundKills + (timeoutState != null ? "\tTAKILDI=" + timeoutState : "");
    }
}


// Olum yolu: oyuncu olumlu (pasif, sabit irtifa, ates eder). Olunce sonuc ekrani acilmali; sonra "retry" (TEKRAR OYNA)
// yeni oyunu asama 1 / skor 0'dan baslatmali, "hangar" (HANGARA DON) hangari acmali ve hangarin savasa cik dugmesi
// ucusa donmeli. Sureler ve her adimin sonucu yazilir; hatalar BotWorker'da sayilir.
public class DeathScenario : BotWorker.Scenario
{
    private static readonly MethodInfo mLeave = typeof(RunResultUI).GetMethod("Leave", BindingFlags.NonPublic | BindingFlags.Instance);
    private static readonly FieldInfo FlowEnemies = BotWorker.F(typeof(CombatManager), "enemies");

    private int state, frames;
    private float t0, deathAt = -1f, uiAt = -1f;
    private string phaseAtDeath = "-", after = "-", hpInfo = "-";
    private Vector3 lockFwd;
    private int hits, kills;
    private float lastHit;
    private readonly HashSet<int> seenDead = new HashSet<int>();

    public override void Begin()
    {
        CombatManager.Instance.DebugJumpToStage(job.stage);
        t0 = Time.time;
    }

    private static string Scene => UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

    public override bool Tick()
    {
        float el = Time.time - t0;
        frames++;
        switch (state)
        {
            case 0:   // ucus: olene kadar
            {
                if (el > job.duration) { after = "olmedi"; return true; }
                CombatManager cm = CombatManager.Instance;
                PlayerHealth ph = cm.CurrentPlayerHealth;
                if (ph != null)
                {
                    if (hpInfo == "-") { hpInfo = ph.maxHealth + "/" + ph.maxShield.ToString("0"); lastHit = ph.LastHitTime; }
                    if (ph.LastHitTime != lastHit) { hits++; lastHit = ph.LastHitTime; }
                }
                foreach (EnemyPlane e in (List<EnemyPlane>)FlowEnemies.GetValue(cm))
                    if (e != null && e.IsDead && !e.IsBoss && seenDead.Add(e.GetInstanceID())) kills++;
                if (RunState.Ended)
                {
                    deathAt = el;
                    phaseAtDeath = RunState.Phase.ToString();
                    BotInput.Clear();
                    state = 1;
                    return false;
                }
                PlaneController plane = cm.flight.Current;
                if (plane == null || plane.IsCrashed || cm.RushActive) { BotInput.Clear(); return false; }
                Transform t = plane.transform;
                Rigidbody prb = plane.GetComponent<Rigidbody>();
                if (lockFwd == Vector3.zero) lockFwd = Vector3.ProjectOnPlane(t.forward, Vector3.up).normalized;
                if (lockFwd == Vector3.zero) lockFwd = Vector3.forward;
                lockFwd = Quaternion.AngleAxis(6f * Time.deltaTime, Vector3.up) * lockFwd;
                Vector3 pp = prb.position;
                pp.y = Mathf.Max(pp.y, cm.GroundHeight(pp) + 150f);
                prb.position = pp; t.position = pp;
                prb.rotation = Quaternion.LookRotation(lockFwd, Vector3.up); t.rotation = prb.rotation;
                prb.linearVelocity = lockFwd * plane.cruiseSpeed; prb.angularVelocity = Vector3.zero;
                BotInput.Fire = true;
                return false;
            }
            case 1:   // sonuc ekrani
            {
                var rr = UnityEngine.Object.FindFirstObjectByType<RunResultUI>();
                if (rr == null || !rr.IsOpen)
                {
                    if (el - deathAt > 60f) { after = "ekran-acilmadi"; return true; }
                    return false;
                }
                uiAt = el;
                mLeave.Invoke(rr, new object[] { job.after == "hangar" ? GameScenes.Hangar : GameScenes.Flight });
                frames = 0;
                state = job.after == "hangar" ? 3 : 2;
                return false;
            }
            case 2:   // TEKRAR OYNA: yeni oyun
            {
                if (frames < 120) return false;
                bool ok = Scene == GameScenes.Flight && CombatManager.Instance != null && RunState.Phase == RunPhase.Playing
                          && RunState.Stage == 0 && RunState.Score == 0 && !GameState.Paused;
                after = (ok ? "retry-ok" : "retry-HATA") + " sahne=" + Scene + " faz=" + RunState.Phase + " asama=" + RunState.Stage
                        + " skor=" + RunState.Score + " durak=" + GameState.Paused;
                return true;
            }
            case 3:   // HANGARA DON: hangar acilir, savasa cik
            {
                if (frames < 120) return false;
                if (Scene != GameScenes.Hangar || UnityEngine.Object.FindFirstObjectByType<HangarController>() == null)
                {
                    after = "hangar-HATA sahne=" + Scene;
                    return true;
                }
                LoadoutStore.SelectedModel = job.aircraft >= 0 ? job.aircraft : LoadoutStore.SelectedModel;
                LoadoutStore.Save();
                UnityEngine.SceneManagement.SceneManager.LoadScene(GameScenes.Flight);
                frames = 0;
                state = 4;
                return false;
            }
            default:  // hangardan ucusa donus
            {
                if (frames < 120) return false;
                bool ok = Scene == GameScenes.Flight && CombatManager.Instance != null && RunState.Phase == RunPhase.Playing
                          && RunState.Stage == 0 && !GameState.Paused
                          && (job.aircraft < 0 || CombatManager.Instance.flight.CurrentIndex == job.aircraft);
                after = (ok ? "hangar-ok" : "hangar-HATA") + " sahne=" + Scene + " faz=" + RunState.Phase + " ucak="
                        + (CombatManager.Instance != null ? CombatManager.Instance.flight.CurrentIndex.ToString() : "-") + " durak=" + GameState.Paused;
                return true;
            }
        }
    }

    public override string Result()
    {
        return "death=" + deathAt.ToString("0") + "\tcan=" + hpInfo + "\tisabet=" + hits + "\tindirilen=" + kills + "\tfaz=" + phaseAtDeath
            + "\tui=" + uiAt.ToString("0") + "\tsonra=" + after;
    }
}

// Kahraman kacisi tus testi: Arcade ve Fare semalarinda Q ve Z'ye basar, kacisin tetiklenip tetiklenmedigini yazar
// (klavye olayi enjekte eder: yalniz klonlarda ulasir). Beklenen: Arcade Q yok / Z var, Fare Q var / Z yok.
public class DashKeysScenario : BotWorker.Scenario
{
    private static readonly FieldInfo fNext = typeof(CombatManager).GetField("nextDash", BindingFlags.NonPublic | BindingFlags.Instance);
    private static readonly ControlScheme[] Schemes = { ControlScheme.Arcade, ControlScheme.Mouse };
    private static readonly Key[] Keys = { Key.Q, Key.Z };

    private int step, phase, frames;
    private float before;
    private ControlScheme prev;
    private readonly System.Text.StringBuilder log = new System.Text.StringBuilder();

    public override void Begin()
    {
        prev = ControlSettings.Scheme;
        PlayerHealth.God = true;
    }

    public override bool Tick()
    {
        CombatManager cm = CombatManager.Instance;
        frames++;
        if (step >= Schemes.Length * Keys.Length)
        {
            ControlSettings.Scheme = prev;
            PlayerHealth.God = false;
            return true;
        }
        ControlScheme scheme = Schemes[step / Keys.Length];
        Key key = Keys[step % Keys.Length];
        switch (phase)
        {
            case 0:   // bekleme suresi dolsun
                ControlSettings.Scheme = scheme;
                if (frames < 150) return false;
                before = (float)fNext.GetValue(cm);
                InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(key));
                phase = 1; frames = 0;
                return false;
            case 1:
                if (frames < 3) return false;
                InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
                phase = 2; frames = 0;
                return false;
            default:
                if (frames < 10) return false;
                bool dashed = !Mathf.Approximately((float)fNext.GetValue(cm), before);
                log.Append(scheme + "-" + key + "=" + (dashed ? "kacti" : "yok") + " ");
                step++; phase = 0; frames = 0;
                return false;
        }
    }

    public override string Result() { return log.ToString().Trim(); }
}
