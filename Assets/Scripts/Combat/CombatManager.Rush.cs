using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Boss fazi ("rush"): boss gelince diger dusman ucaklar cekilir, yer birimleri susar. Oyuncu ve boss once acik denize,
// sonra haritanin cevresindeki genis bir cember uzerinde durmadan ilerler (adalar ve ussler sisin otesinde kalir).
// Ilerleyen bir cerceve (RushOrigin + RushHeading) her fizik adiminda kayar; oyuncu bu cercevede yalnizca sag-sol /
// yukari-asagi hareket eder (W/S yok), fare nisani cerceve yonune gore sinirlidir (PlaneController). Boss, mermiler,
// fuzeler ve guclendirmeler cerceveyle birlikte tasinir: goreli savas mantigi (onde tutma, mermi hizi, boss dumeni)
// duragan savastaki gibi calisir. Boss olunce cerceve durur, ekran kararir ve oyuncu boss'tan onceki yerine doner.
public partial class CombatManager
{
    private const float RushCircle = 9500f;      // harita merkezine uzaklik (m): ada ve ussler sisin otesinde kalir
    private const float RushAltitude = 170f;     // deniz ustu ucus yuksekligi (m)
    public const float RushHalfWidth = 170f;     // oyuncunun cercevede saga-sola kayma siniri (m)
    public const float RushDown = 90f;           // cerceve yuksekliginin altina inme siniri (m)
    public const float RushUp = 130f;            // ustune cikma siniri (m)

    /// <summary>Boss fazinda cercevenin hizi (m/s); mermi, fuze ve guclendirmeler bunu kendi hizlarina ekler.</summary>
    public static Vector3 FrameVelocity { get; private set; }

    public bool RushActive { get; private set; }
    public Vector3 RushHeading { get; private set; } = Vector3.forward;
    public Vector3 RushOrigin => rushOrigin;
    /// <summary>Boss fazi boyunca cercevenin toplam kaydigi yol (uyarili atislar icin).</summary>
    public Vector3 RushShift { get; private set; }
    public float RushAge => RushActive ? Time.time - rushStart : 0f;

    /// <summary>Boss savasi suruyor mu: diger dusmanlar ates etmez, takviye gelmez.</summary>
    public bool BossFightActive => RushActive || ActiveBoss != null;

    private Vector3 rushOrigin;
    private float rushSpeed, rushTargetSpeed, rushStart;
    private float rushStepTime = -1f;
    private Vector3 rushDelta;
    private float rushTangentSign = 1f;
    private bool rushEnding;
    private EnemyPlane rushBoss;
    private bool rushHasReturn;
    private Vector3 rushReturnPos;
    private Quaternion rushReturnRot;
    private Coroutine rushEndRoutine;
    private RushScenery rushScenery;
    private ScreenFader fader;

    // -------------------------------------------------------------- baslangic / bitis

    private void BeginRush(EnemyPlane bossEnemy)
    {
        PlaneController plane = flight != null ? flight.Current : null;
        if (plane == null || bossEnemy == null) return;
        if (RushActive)
        {
            // Donus surerken yeni bir boss geldiyse donus iptal: boss fazi yeni boss'la surer
            rushBoss = bossEnemy;
            if (rushEnding)
            {
                if (rushEndRoutine != null) StopCoroutine(rushEndRoutine);
                rushEndRoutine = null;
                rushEnding = false;
                if (fader != null) fader.Alpha = 0f;
            }
            return;
        }

        // Diger dusman ucaklar cekilir (boss'a odaklan); havadaki dusman mermileri temizlenir
        int withdrawn = 0;
        foreach (var e in new List<EnemyPlane>(enemies))
        {
            if (e == null || e.IsDead || e.IsBoss || e.IsGround) continue;
            e.Vanish();
            withdrawn++;
        }
        Bullet.ClearEnemyBullets();

        rushHasReturn = !plane.IsCrashed;
        rushReturnPos = plane.transform.position;
        rushReturnRot = plane.transform.rotation;

        Vector3 f = Vector3.ProjectOnPlane(plane.transform.forward, Vector3.up);
        if (f.sqrMagnitude < 0.01f) f = Vector3.forward;
        RushHeading = f.normalized;
        rushOrigin = plane.transform.position;

        // Cemberde hangi yone donulecegi: ucagin su anki yonune en yakin teget (gereksiz U donusu olmasin)
        Vector2 p = new Vector2(rushOrigin.x, rushOrigin.z);
        Vector2 outward = p.sqrMagnitude > 1f ? p.normalized : new Vector2(RushHeading.x, RushHeading.z);
        Vector2 tangentPlus = new Vector2(-outward.y, outward.x);
        rushTangentSign = Vector2.Dot(new Vector2(RushHeading.x, RushHeading.z), tangentPlus) >= 0f ? 1f : -1f;

        rushSpeed = Mathf.Clamp(plane.Speed, 40f, 170f);
        rushTargetSpeed = Mathf.Clamp(plane.cruiseSpeed, 85f, 125f);
        rushStart = Time.time;
        rushStepTime = -1f;
        rushDelta = Vector3.zero;
        RushShift = Vector3.zero;
        FrameVelocity = Vector3.zero;
        rushEnding = false;
        rushBoss = bossEnemy;
        RushActive = true;

        plane.BeginRush(rushOrigin, RushHeading);

        if (rushScenery == null) rushScenery = gameObject.AddComponent<RushScenery>();
        rushScenery.Begin(this);

        if (withdrawn > 0)
            Announce("Komutan: avcılar çekildi, boss'a odaklan!", new Color(0.75f, 0.95f, 1f), 2.6f);
        Announce("BOSS SAVAŞI: açık denize!", new Color(1f, 0.8f, 0.35f), 2.8f);
    }

    /// <summary>Boss oldu ya da kayboldu: cerceve yavaslayip durur, patlamalar izlenir, ekran kararip oyuncu doner.</summary>
    private void RequestRushEnd()
    {
        if (!RushActive || rushEnding) return;
        rushEnding = true;
        if (rushEndRoutine != null) StopCoroutine(rushEndRoutine);
        rushEndRoutine = StartCoroutine(RushEndRoutine());
    }

    private IEnumerator RushEndRoutine()
    {
        yield return new WaitForSeconds(1.8f);
        EnsureFader();
        yield return StartCoroutine(fader.Fade(1f, 0.45f));
        ReturnFromRush();
        yield return new WaitForSecondsRealtime(0.15f);
        yield return StartCoroutine(fader.Fade(0f, 0.5f));
        rushEndRoutine = null;
    }

    // Oyuncu boss'tan onceki yerine doner (orta boss sonrasi us ve dalgalar oradadir); yakindaki guclendirmeler de
    // birlikte tasinir ki boss'un dusurdugu odul kaybolmasin
    private void ReturnFromRush()
    {
        PlaneController plane = flight != null ? flight.Current : null;
        EndRushNow();

        if (plane != null && rushHasReturn && !plane.IsCrashed && !RunState.Ended)
        {
            Vector3 fwd = Vector3.ProjectOnPlane(rushReturnRot * Vector3.forward, Vector3.up);
            if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;
            fwd.Normalize();
            Vector3 at = rushReturnPos;
            at.y = Mathf.Max(at.y, GroundHeight(at) + 120f);

            // Boss'un dusurdugu guclendirmeler oyuncunun hemen onune dizilir (uzakta kalip kaybolmasin)
            Vector3 from = plane.transform.position;
            int k = 0;
            foreach (var pu in PowerUp.All.ToArray())
            {
                if (pu == null || (pu.transform.position - from).sqrMagnitude > 2500f * 2500f) continue;
                pu.transform.position = at + fwd * (140f + 30f * k) + Vector3.Cross(Vector3.up, fwd) * ((k % 2 == 0 ? -1f : 1f) * 8f * k);
                k++;
            }
            plane.Teleport(at, Quaternion.LookRotation(fwd.normalized, Vector3.up));

            FollowCamera fc = cam != null ? cam.GetComponent<FollowCamera>() : null;
            if (fc != null) fc.Snap();
        }
        rushHasReturn = false;
    }

    /// <summary>Boss fazini hemen kapatir (asama degisimi, oyun sonu, donus).</summary>
    private void EndRushNow()
    {
        if (!RushActive) return;
        RushActive = false;
        rushEnding = false;
        rushDelta = Vector3.zero;
        FrameVelocity = Vector3.zero;
        rushBoss = null;

        if (flight != null && flight.planes != null)
            foreach (var p in flight.planes)
                if (p != null && p.InRush) p.EndRush();
        if (rushScenery != null) rushScenery.End();
    }

    private void AbortRush()
    {
        if (rushEndRoutine != null)
        {
            StopCoroutine(rushEndRoutine);
            rushEndRoutine = null;
        }
        EndRushNow();
        rushHasReturn = false;
        if (fader != null) fader.Alpha = 0f;
    }

    private void EnsureFader()
    {
        if (fader == null) fader = gameObject.AddComponent<ScreenFader>();
    }

    // Her karede: boss oldu / kayboldu mu
    private void UpdateRush()
    {
        if (RushActive && !rushEnding && (rushBoss == null || rushBoss.IsDead)) RequestRushEnd();
    }

    private void FixedUpdate()
    {
        RushStepDelta();
    }

    // -------------------------------------------------------------- cerceve adimi

    /// <summary>
    /// Bu fizik adiminda cercevenin kaydigi yol (boss fazi yoksa sifir). Ilk cagri adimi ilerletir: oyuncu, boss ve
    /// CombatManager hangi sirayla calisirsa calissin ayni adim bir kez islenir.
    /// </summary>
    public Vector3 RushStepDelta()
    {
        if (!RushActive) return Vector3.zero;
        if (rushStepTime != Time.fixedTime)
        {
            rushStepTime = Time.fixedTime;
            StepRush(Time.fixedDeltaTime);
        }
        return rushDelta;
    }

    private void StepRush(float dt)
    {
        Vector3 prev = rushOrigin;

        // Yon: once disari (denize), sonra RushCircle yaricapli cemberin tegeti; onde kara ya da ada varsa kacar
        Vector2 p = new Vector2(rushOrigin.x, rushOrigin.z);
        float r = p.magnitude;
        Vector2 outward = r > 1f ? p / r : new Vector2(RushHeading.x, RushHeading.z);
        Vector2 tangent = new Vector2(-outward.y, outward.x) * rushTangentSign;
        float radial = Mathf.Clamp((RushCircle - r) / 2500f, -0.9f, 1f);
        Vector2 d2 = tangent * Mathf.Sqrt(Mathf.Max(0f, 1f - radial * radial)) + outward * radial;
        Vector3 desired = AvoidLand(new Vector3(d2.x, 0f, d2.y).normalized, new Vector3(outward.x, 0f, outward.y));

        // Ilk saniyelerde yon hizla denize donulur; sonra cember boyunca yavas yavas
        float err = Vector3.SignedAngle(RushHeading, desired, Vector3.up);
        float maxTurn = (Time.time - rushStart < 8f ? 24f : 6f) * dt;
        RushHeading = (Quaternion.AngleAxis(Mathf.Clamp(err, -maxTurn, maxTurn), Vector3.up) * RushHeading).normalized;

        // Hiz: seyir hizina yaklasir; boss olunce hizla durur (patlamalar gorus alaninda kalsin)
        float target = rushEnding ? 0f : rushTargetSpeed;
        rushSpeed = Mathf.MoveTowards(rushSpeed, target, (rushEnding ? 110f : 25f) * dt);

        rushOrigin += RushHeading * rushSpeed * dt;

        // Yukseklik: denizde sabit; karada (baslangicta) arazinin ve ondeki tepelerin ustunde
        float ground = Mathf.Max(GroundHeight(rushOrigin),
            Mathf.Max(GroundHeight(rushOrigin + RushHeading * 450f), GroundHeight(rushOrigin + RushHeading * 1000f)));
        float wantY = Mathf.Max(SeaLevel + RushAltitude, ground + 150f);
        rushOrigin.y = Mathf.MoveTowards(rushOrigin.y, wantY, 28f * dt);

        rushDelta = rushOrigin - prev;
        RushShift += rushDelta;
        FrameVelocity = dt > 0f ? rushDelta / dt : Vector3.zero;
    }

    // Onde kara / ada varsa acik denize bakan en yakin yonu secer (disari donmek oncelikli)
    private Vector3 AvoidLand(Vector3 desired, Vector3 outward)
    {
        if (OpenSeaAhead(desired)) return desired;

        float prefer = Vector3.SignedAngle(desired, outward, Vector3.up) >= 0f ? 1f : -1f;
        for (int k = 1; k <= 6; k++)
        {
            float a = 20f * k;
            Vector3 d1 = Quaternion.AngleAxis(a * prefer, Vector3.up) * desired;
            if (OpenSeaAhead(d1)) return d1;
            Vector3 d2 = Quaternion.AngleAxis(-a * prefer, Vector3.up) * desired;
            if (OpenSeaAhead(d2)) return d2;
        }
        return desired;
    }

    private bool OpenSeaAhead(Vector3 dir)
    {
        for (int i = 1; i <= 4; i++)
        {
            Vector3 q = rushOrigin + dir * (650f * i);
            if (GroundHeight(q) > SeaLevel + 3f) return false;
            if (baseDef.enabled && baseDef.island
                && Vector2.Distance(new Vector2(q.x, q.z), baseDef.center) < baseDef.radius + 900f) return false;
        }
        return true;
    }

    // -------------------------------------------------------------- uzuvlar

    /// <summary>Boss uzvu koptu (silah) ya da yandi (kanat): duyuru, odul, kisa donma.</summary>
    public void OnBossLimbBroken(EnemyPlane bossEnemy, string label, bool weapon, Vector3 point)
    {
        string text = weapon ? label + " KOPTU!" : label + " PARÇALANDI!";
        Announce(text, weapon ? new Color(1f, 0.6f, 0.25f) : new Color(1f, 0.85f, 0.45f), 1.8f);
        RunState.AddScore(weapon ? 600 : 350);
        RunUpgrades.AddScrap(weapon ? 25 : 15);
        killMarkerTime = Time.unscaledTime;
        HitStop(weapon ? 0.09f : 0.05f, 0.25f);
        if (uiAudio != null)
        {
            uiAudio.pitch = 1f;
            uiAudio.PlayOneShot(Sfx.Explosion, weapon ? 0.5f : 0.35f);
            uiAudio.PlayOneShot(Sfx.KillDing, 0.3f);
        }
        texts.Add(new FloatingText
        {
            position = point + Vector3.up * 6f,
            text = (weapon ? "+600  " : "+350  ") + label,
            born = Time.time,
            color = new Color(1f, 0.75f, 0.3f),
            scale = 0.7f
        });
    }
}
