using UnityEngine;

// BossBrain: Yildiz Buyucusu'nun imza saldirisi "yildiz carki" (yalniz boss fazinda).
// Eski sarmal (spray) boss fazinda oyuncunun cevresinde ici bos bir silindir ciziyordu: yerinde duran hic vurulmuyordu.
// Cark: savas alaninin (cercevenin) ortasinda donen 3-4 kol; her kol gobekten kenara uzanan pembe yildiz dizisidir ve
// tum alani tarar, gobek de surekli vurulur. Kolun uzerinden atlanmaz: oyuncu iki kol arasindaki boslukta carkin donus
// yonunde kayarak kalmali (ice yakin kol daha yavas gelir, ama gobek yasak).
public partial class BossBrain
{
    private static float WheelWarning => 0.8f * Difficulty.BossWarningMul;       // uyari cizgileri; ilk yildizlar ardindan cikar
    private const float WheelInterval = 0.15f;     // kol atis araligi (sn)
    private const int WheelSlots = 5;              // kol basina yildiz
    private const float WheelSpacing = 40f;        // kol uzerinde yildizlar arasi (m); tek/cift atislarda yarim kayar
    private const float WheelSpeed = 190f;         // taban hiz (boss carpanlari ayrica uygulanir)
    private const float WheelBulletRadius = 3.5f;  // kolu duvar gibi yapar (normal mermi 1 m)
    private const float WheelRest = 0.7f;          // cark bitince son yildizlar gecene kadar yeni saldiri yok
    private const float WheelTail = 1.6f;          // cark bitince son kollar duzleme varana dek HUD boslugu gosterir

    private float wheelStart, wheelEnd, wheelNext, wheelAngle, wheelAngle0, wheelSpin;
    private int wheelArms, wheelTick;
    private Vector3 wheelHub;
    private bool wheelAnnounced;

    private bool Wheeling => Time.time < wheelEnd;
    private bool WheelBusy => Time.time < wheelEnd + WheelRest;

    // Gobek: oyuncunun kayabildigi alanin ortasi (cerceveyle birlikte ilerler)
    private Vector3 WheelHubNow => mgr.RushOrigin + Vector3.up * ((CombatManager.RushUp - CombatManager.RushDown) * 0.5f);

    private void StartWheel(Transform wand, int arms, float spinDeg, float duration)
    {
        Vector3 right = Vector3.Cross(Vector3.up, mgr.RushHeading).normalized;
        wheelHub = WheelHubNow;
        wheelArms = Mathf.Max(2, arms);
        wheelSpin = (Random.value < 0.5f ? -1f : 1f) * spinDeg;
        wheelAngle = Random.value * 360f;
        wheelAngle0 = wheelAngle;
        wheelTick = 0;
        wheelStart = Time.time + WheelWarning;
        wheelNext = wheelStart;
        wheelEnd = wheelStart + duration;

        // Uyari: degnekten kollarin ilk konumuna uzanan cizgiler
        for (int a = 0; a < wheelArms; a++)
        {
            float ang = (wheelAngle + 360f * a / wheelArms) * Mathf.Deg2Rad;
            Vector3 tip = wheelHub + (right * Mathf.Cos(ang) + Vector3.up * Mathf.Sin(ang)) * 120f;
            Vector3 d = tip - wand.position;
            if (d.sqrMagnitude > 1f) warns.Add(new WarnLine { origin = wand.position, dir = d.normalized, until = wheelStart });
        }
        Sfx.PlayAt(Sfx.Warning, player.position, 0.6f);
        if (!wheelAnnounced)
        {
            wheelAnnounced = true;
            mgr.Announce("YILDIZ ÇARKI: kollarla birlikte dön!", new Color(1f, 0.55f, 0.85f), 2.2f);
        }
        nextAttack = Mathf.Max(nextAttack, wheelEnd + WheelRest);
    }

    private void TickWheel()
    {
        if (!Wheeling || Time.time < wheelStart) return;
        if (!mgr.RushActive) { wheelEnd = 0f; return; }   // boss fazi bittiyse cark durur

        Transform wand = Pick(starMuzzle, Get("Brooch"));
        if (wand == null) return;

        while (Time.time >= wheelNext)
        {
            wheelNext += WheelInterval;
            wheelHub = WheelHubNow;
            Vector3 origin = wand.position;
            Vector3 right = Vector3.Cross(Vector3.up, mgr.RushHeading).normalized;
            float effSpeed = WheelSpeed * BulletSpeedMul * RushBulletSpeed;

            // Tek/cift atislarda yildizlar kol boyunca yarim aralik kayar: kolda arasindan sizilacak bosluk kalmaz
            bool odd = (wheelTick++ & 1) == 1;
            float first = odd ? WheelSpacing * 0.875f : WheelSpacing * 0.375f;

            if (!odd) ShootWheel(origin, wheelHub, effSpeed);
            for (int a = 0; a < wheelArms; a++)
            {
                float ang = (wheelAngle + 360f * a / wheelArms) * Mathf.Deg2Rad;
                Vector3 arm = right * Mathf.Cos(ang) + Vector3.up * Mathf.Sin(ang);
                for (int j = 0; j < WheelSlots; j++)
                    ShootWheel(origin, wheelHub + arm * (first + j * WheelSpacing), effSpeed);
            }
            wheelAngle += wheelSpin * WheelInterval;
        }
    }

    /// <summary>
    /// HUD: carkta oyuncunun gidecegi bosluk (kendi duzleminde, en yakin iki kolun ortasi). Kollar duzleme mermilerin
    /// ucus suresi kadar gec vardigi icin aci o kadar onceki atistan hesaplanir; bosluk carkla birlikte doner.
    /// </summary>
    public bool WheelGap(out Vector3 gap)
    {
        gap = Vector3.zero;
        if (mgr == null || !mgr.RushActive || enemy == null || enemy.IsDead || player == null) return false;
        if (wheelEnd <= 0f || Time.time < wheelStart - WheelWarning || Time.time > wheelEnd + WheelTail) return false;

        Vector3 o = mgr.RushOrigin;
        Vector3 right = Vector3.Cross(Vector3.up, mgr.RushHeading).normalized;
        float hubY = (CombatManager.RushUp - CombatManager.RushDown) * 0.5f;
        float effSpeed = WheelSpeed * BulletSpeedMul * RushBulletSpeed;
        float flight = Vector3.Distance(transform.position, player.position) / Mathf.Max(effSpeed, 1f);
        float shotAt = Mathf.Clamp(Time.time - flight, wheelStart, wheelEnd);
        float armAngle = wheelAngle0 + wheelSpin * (shotAt - wheelStart);

        Vector3 rel = player.position - o;
        float px = Vector3.Dot(rel, right), py = rel.y - hubY;
        float sector = 360f / Mathf.Max(1, wheelArms);
        float pAng = Mathf.Atan2(py, px) * Mathf.Rad2Deg;
        float k = Mathf.Round((pAng - armAngle - sector * 0.5f) / sector);
        float gAng = (armAngle + sector * (0.5f + k)) * Mathf.Deg2Rad;
        // Gobekten uzak (gobek yasak) ama kolun oyuncudan hizli donmeyecegi kadar yakin bir yaricap
        float r = Mathf.Clamp(new Vector2(px, py).magnitude, 45f, 85f);
        float gx = Mathf.Clamp(Mathf.Cos(gAng) * r, -CombatManager.RushHalfWidth + 15f, CombatManager.RushHalfWidth - 15f);
        float gy = Mathf.Clamp(hubY + Mathf.Sin(gAng) * r, -CombatManager.RushDown + 15f, CombatManager.RushUp - 15f);
        gap = o + right * gx + Vector3.up * gy;
        return true;
    }

    private void ShootWheel(Vector3 origin, Vector3 target, float effSpeed)
    {
        Vector3 d = target - origin;
        float dist = d.magnitude;
        if (dist < 1f) return;
        Bullet b = Shoot(origin, d / dist, WheelSpeed, 1);
        if (b == null) return;
        b.radius = WheelBulletRadius;
        b.look = 1;
        // Oyuncunun duzlemini gecer gecmez soner: kameranin onunden dev diskler gibi gecmesin, yuzlercesi birikmesin
        b.lifetime = (dist + PlanePassMargin) / Mathf.Max(effSpeed, 1f);
    }
}
