using UnityEngine;

// Dusman ussundaki yer birimlerinin davranisi (ates, donus, patlama). EnemyPlane (can, puan, hasar) ile ayni
// nesnede durur; BaseBuilder kurar. Rol: Flak (hizli ucaksavar), Cannon (yavas agir top), Radar (doner; yasadikca
// ussun nisan hatasi dusuktur), Core (komuta merkezi), Tank (yakit deposu: patlayinca civari yakar), Building.
public class GroundUnit : MonoBehaviour
{
    public enum Role { Flak, Cannon, Radar, Core, Tank, Building }

    /// <summary>Yasayan radar sayisi: sifirsa uc aksavarlarin nisan hatasi artar.</summary>
    public static int RadarAlive { get; private set; }

    private Role role;
    private Transform head, muzzle, beacon;
    private EnemyPlane unit;
    private bool counted;

    private float nextBurst;
    private float nextShot;
    private int burstLeft;
    private float spin;
    private float born;

    public Role UnitRole => role;

    public void Setup(Role r, Transform headT, Transform muzzleT, Transform beaconT = null)
    {
        role = r;
        head = headT;
        muzzle = muzzleT;
        beacon = beaconT;
        unit = GetComponent<EnemyPlane>();
        born = Time.time;
        nextBurst = Time.time + Random.Range(1.5f, 4f);   // hepsi ayni anda ates etmesin

        if (role == Role.Radar && !counted)
        {
            counted = true;
            RadarAlive++;
        }
    }

    private void OnDestroy()
    {
        if (counted)
        {
            counted = false;
            RadarAlive = Mathf.Max(0, RadarAlive - 1);
        }
    }

    private void Update()
    {
        if (GameState.Paused || unit == null || unit.IsDead) return;
        float dt = Time.deltaTime;

        switch (role)
        {
            case Role.Radar:
                if (head != null)
                {
                    spin += 70f * dt;
                    head.localRotation = Quaternion.Euler(0f, spin, 0f);
                }
                break;

            case Role.Core:
                if (beacon != null)
                {
                    float s = 4.5f + Mathf.Sin((Time.time - born) * 5f) * 1.2f;
                    beacon.localScale = Vector3.one * s;
                }
                break;

            case Role.Flak:
            case Role.Cannon:
                Fight(dt);
                break;
        }
    }

    private void Fight(float dt)
    {
        CombatManager mgr = CombatManager.Instance;
        if (mgr == null || head == null || muzzle == null) return;

        Transform player = mgr.PlayerTransform;
        if (player == null) return;

        // Komuta merkezi yikilinca uc aksavarlar susar; asama bitince / savas disinda ates yok
        if (!RunState.Active || mgr.BaseCleared || !mgr.enemiesShoot || mgr.EnemyBulletPrefab == null) return;

        GameSettings gs = GameSettings.Current;
        // Ileri asamalarda uc aksavarlar daha sik atar (%15 / asama)
        float stageRate = Mathf.Max(0.55f, 1f - 0.15f * RunState.Stage);
        Vector3 from = muzzle.position;
        float dist = Vector3.Distance(from, player.position);
        float range = role == Role.Cannon ? gs.baseFlakRange * 1.1f : gs.baseFlakRange;
        if (dist > range) return;

        float speed = gs.baseBulletSpeed * (role == Role.Cannon ? 0.8f : 1f);
        Vector3 aim = player.position + mgr.PlayerVelocity * (dist / speed) * 0.85f;

        // Namlu hedefe doner (ufkun altina inmez: yer topu yukari bakar)
        Vector3 want = (aim - head.position).normalized;
        want.y = Mathf.Max(want.y, 0.08f);
        want.Normalize();
        head.rotation = Quaternion.Slerp(head.rotation, Quaternion.LookRotation(want), 1f - Mathf.Exp(-5f * dt));

        // Radar yoksa nisan hatasi iki kati
        float err = mgr.enemyAimError * (RadarAlive > 0 ? 1f : 2.2f);

        if (role == Role.Flak)
        {
            if (burstLeft > 0 && Time.time >= nextShot)
            {
                burstLeft--;
                nextShot = Time.time + 0.2f;
                Vector3 dir = (aim + Random.insideUnitSphere * err - from).normalized;
                Bullet.Spawn(mgr.EnemyBulletPrefab, from, dir, speed, 1, transform, false);
                Sfx.PlayAt(Sfx.Shot, from, 0.28f);
            }
            else if (burstLeft <= 0 && Time.time >= nextBurst && Vector3.Angle(head.forward, want) < 12f)
            {
                burstLeft = gs.baseFlakBurst;
                nextBurst = Time.time + gs.baseFlakCooldown * stageRate * Random.Range(0.8f, 1.3f);
            }
        }
        else
        {
            // Agir top: tek salvo, hedefe yelpaze (kacmasi icin araliklar var)
            if (Time.time >= nextBurst && Vector3.Angle(head.forward, want) < 12f)
            {
                nextBurst = Time.time + gs.baseFlakCooldown * stageRate * Random.Range(1.7f, 2.2f);
                Vector3 dir = (aim + Random.insideUnitSphere * (err * 0.4f) - from).normalized;
                const int n = 7;
                for (int i = 0; i < n; i++)
                {
                    float off = -16f + 32f * i / (n - 1);
                    Bullet.Spawn(mgr.EnemyBulletPrefab, from, Quaternion.AngleAxis(off, Vector3.up) * dir, speed, 2, transform, false);
                }
                Sfx.PlayAt(Sfx.Shot, from, 0.45f);
            }
        }
    }

    /// <summary>Yok edilince CombatManager cagirir: ek patlamalar, enkaz, yakit deposunda zincirleme patlama.</summary>
    public void OnKilled(CombatManager mgr)
    {
        if (mgr == null) return;

        float r = unit != null ? Mathf.Max(unit.HitRadius, 3f) : 5f;
        int extra = role == Role.Core ? 14 : (role == Role.Tank ? 5 : (role == Role.Building ? 3 : 1));
        Vector3 pos = transform.position;
        for (int i = 0; i < extra; i++)
            mgr.SpawnExplosion(pos + new Vector3(Random.Range(-r, r), Random.Range(1f, r * 0.7f), Random.Range(-r, r)) * 0.6f);

        mgr.SpawnRubble(pos, r, role == Role.Core || role == Role.Tank);

        if (role == Role.Tank)
        {
            // Civardaki yer birimleri yanar
            foreach (var e in new System.Collections.Generic.List<EnemyPlane>(mgr.Enemies))
            {
                if (e == null || e.IsDead || e == unit || !e.IsGround) continue;
                if ((e.transform.position - pos).sqrMagnitude < 55f * 55f) e.TakeHit(6, e.transform.position);
            }
        }
    }
}
