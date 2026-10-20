using UnityEngine;

// Guduml fuze: hedefi onde tutarak takip eder, yaklasinca veya bir seye carpinca patlar,
// patlama yaricapindaki tum dusmanlara hasar verir.
public class Missile : MonoBehaviour
{
    public float lifetime = 9f;
    public float radius = 1.2f;
    public float armTime = 0.35f;
    public float proximityFuse = 7f;

    private PartDef def;
    private EnemyPlane target;
    private Transform owner;
    private Vector3 direction;
    private float speed;
    private float age;
    private bool exploded;

    private static readonly RaycastHit[] Hits = new RaycastHit[16];
    private static readonly Collider[] Blast = new Collider[32];

    public static Missile Launch(Missile prefab, Vector3 position, Quaternion rotation, float launchSpeed,
        EnemyPlane target, PartDef def, Transform owner)
    {
        Missile m = Instantiate(prefab, position, rotation);
        m.def = def;
        m.target = target;
        m.owner = owner;
        m.direction = rotation * Vector3.forward;
        m.speed = launchSpeed;
        return m;
    }

    private void Update()
    {
        if (exploded || def == null) return;

        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        age += dt;
        if (age > lifetime)
        {
            Explode(transform.position);
            return;
        }

        // Motor: kalkis hizindan azami hiza hizlan
        speed = Mathf.MoveTowards(speed, def.speed, 140f * dt);

        // Hedef olduyse yeniden hedef bul
        if (target == null && age > 0.6f) target = Reacquire();

        if (target != null && age > 0.2f)
        {
            Vector3 toTarget = target.transform.position - transform.position;
            float t = Mathf.Clamp(toTarget.magnitude / Mathf.Max(speed, 1f), 0f, 3f);
            Vector3 aim = target.transform.position + target.Velocity * t;
            Vector3 desired = (aim - transform.position).normalized;
            direction = Vector3.RotateTowards(direction, desired, def.turn * Mathf.Deg2Rad * dt, 0f).normalized;
        }

        Vector3 start = transform.position;
        Vector3 delta = (direction * speed + CombatManager.FrameVelocity) * dt;   // boss fazinda cerceveyle tasinir
        float dist = delta.magnitude;

        // Yakinlik sigortasi
        if (target != null && age > armTime)
        {
            Vector3 after = start + delta;
            if (Vector3.Distance(after, target.transform.position) < proximityFuse)
            {
                Explode(after);
                return;
            }
        }

        // Cismlere carpma
        if (age > armTime && dist > 1e-4f)
        {
            int n = Physics.SphereCastNonAlloc(start, radius, delta / dist, Hits, dist, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            int bi = -1;
            for (int i = 0; i < n; i++)
            {
                RaycastHit h = Hits[i];
                if (owner != null && h.collider.transform.IsChildOf(owner)) continue;
                if (h.collider.GetComponentInParent<PlayerHealth>() != null) continue;
                if (h.distance < best) { best = h.distance; bi = i; }
            }
            if (bi >= 0)
            {
                Vector3 p = Hits[bi].distance <= 0f ? start : Hits[bi].point;
                Explode(p);
                return;
            }
        }

        transform.position = start + delta;
        transform.rotation = Quaternion.LookRotation(direction);
    }

    private EnemyPlane Reacquire()
    {
        if (CombatManager.Instance == null) return null;

        EnemyPlane best = null;
        float bestScore = float.MaxValue;
        foreach (var e in CombatManager.Instance.Enemies)
        {
            if (e == null) continue;
            Vector3 to = e.transform.position - transform.position;
            float d = to.magnitude;
            if (d > def.range) continue;
            float angle = Vector3.Angle(direction, to);
            if (angle > 60f) continue;

            float score = angle * 10f + d * 0.05f;
            if (score < bestScore)
            {
                bestScore = score;
                best = e;
            }
        }
        return best;
    }

    private void Explode(Vector3 point)
    {
        if (exploded) return;
        exploded = true;

        if (CombatManager.Instance != null) CombatManager.Instance.SpawnExplosion(point);

        int n = Physics.OverlapSphereNonAlloc(point, def.blast, Blast, ~0, QueryTriggerInteraction.Ignore);
        var done = new System.Collections.Generic.HashSet<EnemyPlane>();
        for (int i = 0; i < n; i++)
        {
            var enemy = Blast[i].GetComponentInParent<EnemyPlane>();
            if (enemy == null || !done.Add(enemy)) continue;
            enemy.TakeHit(Mathf.RoundToInt(def.damage), point);
        }

        Destroy(gameObject);
    }
}
