using UnityEngine;

// Ucaga takili, en yakin dusmana kendiliginden donup ates eden taret.
public class AutoTurret : MonoBehaviour
{
    public PartDef def;
    public Transform head;
    public Transform owner;
    public MuzzleFlash muzzle;   // namlu ucu + agiz parlamasi (PlaneBuilder kurar)

    private EnemyPlane target;
    private float nextScan;
    private float nextFire;

    private void Update()
    {
        if (GameState.Paused || def == null || head == null) return;

        CombatManager mgr = CombatManager.Instance;
        if (mgr == null) return;

        // Hedef tara
        if (Time.time >= nextScan)
        {
            nextScan = Time.time + 0.25f;
            target = FindTarget(mgr);
        }

        if (target == null) return;

        float dist = Vector3.Distance(head.position, target.transform.position);
        float shotSpeed = def.speed * GameSettings.Current.playerBulletSpeedMultiplier;
        Vector3 aim = target.transform.position + target.Velocity * (dist / Mathf.Max(shotSpeed, 1f));
        Vector3 dir = (aim - head.position).normalized;

        head.rotation = Quaternion.Slerp(head.rotation, Quaternion.LookRotation(dir), 1f - Mathf.Exp(-12f * Time.deltaTime));

        bool aligned = Vector3.Angle(head.forward, dir) < 6f;
        if (aligned && Time.time >= nextFire && mgr.playerBulletPrefab != null)
        {
            nextFire = Time.time + 1f / Mathf.Max(def.rate * RunUpgrades.Mods.turretRateMul, 0.1f);

            Vector3 shotDir = (dir + Random.insideUnitSphere * Mathf.Tan(def.spread * Mathf.Deg2Rad)).normalized;
            // Standart oyuncu atisi: namlu ucundan mermi + parlama + kisa gorunmezlik
            Vector3 fallback = head.position + head.forward * 0.9f;
            PlayerShot.Fire(mgr.playerBulletPrefab, muzzle, fallback, shotDir, shotSpeed, Mathf.RoundToInt(def.damage), owner);
            Sfx.PlayAt(Sfx.Shot, fallback, 0.12f);
        }
    }

    private EnemyPlane FindTarget(CombatManager mgr)
    {
        EnemyPlane best = null;
        float bestDist = def.range * RunUpgrades.Mods.turretRangeMul;
        foreach (var e in mgr.Enemies)
        {
            if (e == null) continue;
            // Hava dusmani oncelikli: yer birimleri sanki 350 m daha uzaktaymis gibi puanlanir
            float d = Vector3.Distance(head.position, e.transform.position) + (e.IsGround ? 350f : 0f);
            if (d < bestDist)
            {
                bestDist = d;
                best = e;
            }
        }
        return best;
    }
}
