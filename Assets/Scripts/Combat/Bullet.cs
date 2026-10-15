using System.Collections.Generic;
using UnityEngine;

// Hizli mermi: her karede hareket yolu SphereCast ile taranir (tunelleme yok).
public class Bullet : MonoBehaviour
{
    // Canli dusman mermileri: bomba bunlari temizler
    private static readonly List<Bullet> EnemyBullets = new List<Bullet>();

    public static int EnemyBulletCount => EnemyBullets.Count;

    /// <summary>Tum dusman mermilerini yok eder (bomba, asama sonu). Silinen sayiyi doner.</summary>
    public static int ClearEnemyBullets()
    {
        int n = 0;
        // Despawn listeyi degistirir: sondan basa gidilir
        for (int i = EnemyBullets.Count - 1; i >= 0; i--)
        {
            if (i >= EnemyBullets.Count) continue;
            Bullet b = EnemyBullets[i];
            if (b != null)
            {
                b.Despawn();
                n++;
            }
        }
        EnemyBullets.Clear();
        return n;
    }

    private void OnDestroy()
    {
        EnemyBullets.Remove(this);
    }

    // ---- Havuz: mermiler yok edilmez, kapatilip yeniden kullanilir (Instantiate/Destroy ve GC yok)
    private static readonly Dictionary<Bullet, Stack<Bullet>> Pools = new Dictionary<Bullet, Stack<Bullet>>();
    private Bullet sourcePrefab;
    private bool pooled;                 // havuzda bekliyor (cift despawn korumasi)
    private float defaultLifetime;
    private EnemyBulletVisual visual;

    private static Bullet Acquire(Bullet prefab, Vector3 position, Quaternion rotation)
    {
        if (!Pools.TryGetValue(prefab, out Stack<Bullet> stack))
        {
            stack = new Stack<Bullet>(64);
            Pools[prefab] = stack;
        }

        while (stack.Count > 0)
        {
            Bullet b = stack.Pop();
            if (b == null) continue;     // sahne degisince yok edilmis olabilir

            b.transform.SetPositionAndRotation(position, rotation);
            b.gameObject.SetActive(true);
            b.pooled = false;
            return b;
        }

        Bullet created = Instantiate(prefab, position, rotation);
        created.sourcePrefab = prefab;
        created.defaultLifetime = created.lifetime;
        return created;
    }

    /// <summary>Mermiyi havuza geri verir (yok etmez).</summary>
    private void Despawn()
    {
        if (pooled) return;

        if (sourcePrefab == null || !Pools.TryGetValue(sourcePrefab, out Stack<Bullet> stack))
        {
            Destroy(gameObject);
            return;
        }

        pooled = true;
        if (!fromPlayer) EnemyBullets.Remove(this);
        owner = null;
        lastPierced = null;
        gameObject.SetActive(false);
        stack.Push(this);
    }

    public float lifetime = 2.8f;
    public float radius = 1.0f;

    private Vector3 velocity;
    private int damage = 1;
    private Transform owner;
    private float age;

    private static readonly RaycastHit[] Hits = new RaycastHit[16];

    private bool fromPlayer;

    private bool TryHitShield(Vector3 start, Vector3 delta)
    {
        CombatManager mgr = CombatManager.Instance;
        PlayerHealth ph = mgr != null ? mgr.CurrentPlayerHealth : null;
        if (ph == null || !ph.ShieldUp) return false;

        Vector3 center = ph.ShieldCenter;
        float r = ph.shieldRadius;

        Vector3 f = start - center;
        float c = f.sqrMagnitude - r * r;

        Vector3 point;
        if (c <= 0f)
        {
            // Zaten kalkanin icinde dogdu: en yakin yuzeye tasi
            point = center + (f.sqrMagnitude > 1e-6f ? f.normalized : Vector3.forward) * r;
            return ph.AbsorbBullet(point, damage);
        }

        float a = delta.sqrMagnitude;
        float b = 2f * Vector3.Dot(f, delta);
        float disc = b * b - 4f * a * c;
        if (disc < 0f) return false;

        float t = (-b - Mathf.Sqrt(disc)) / (2f * a);
        if (t < 0f || t > 1f) return false;

        point = start + delta * t;
        return ph.AbsorbBullet(point, damage);
    }

    private int pierceLeft;
    private IDamageable lastPierced;

    public static Bullet Spawn(Bullet prefab, Vector3 position, Vector3 direction, float speed, int damage,
        Transform owner, bool fromPlayer, int pierce = 0)
    {
        Bullet b = Acquire(prefab, position, Quaternion.LookRotation(direction));

        // Havuzdan gelen mermi onceki kullanimdan kalan durumlari tasimasin
        b.velocity = direction * speed;
        b.damage = damage;
        b.owner = owner;
        b.fromPlayer = fromPlayer;
        b.pierceLeft = pierce;
        b.lastPierced = null;
        b.age = 0f;
        b.lifetime = b.defaultLifetime > 0f ? b.defaultLifetime : b.lifetime;

        if (fromPlayer)
        {
            // Iz merminin arkasina uzanir ve namlu ucundan baslar (eskiden merkezliydi: govdenin icinden gecerdi)
            if (b.tracer == null)
            {
                b.tracer = b.transform.Find("Tracer");
                if (b.tracer != null)
                {
                    b.tracerThickness = b.tracer.localScale.x;
                    b.tracerFullLength = b.tracer.localScale.z;
                    b.tracerRenderer = b.tracer.GetComponent<Renderer>();
                }
            }

            // Namludan cikan mermi ilk birkac milisaniye gorunmez: parlama namluyu orter, iz namlunun
            // onunde belirir (namlunun icinden/ucak govdesinden cikiyormus gibi gorunmez)
            if (b.tracerRenderer != null) b.tracerRenderer.enabled = false;
        }
        else
        {
            EnemyBullets.Add(b);
            // Ince cubuk yerine okunabilir parlak top; bir kez eklenir, havuzda kalir
            if (b.visual == null)
            {
                b.visual = b.GetComponent<EnemyBulletVisual>();
                if (b.visual == null)
                {
                    EnemyBulletVisual.Attach(b);
                    b.visual = b.GetComponent<EnemyBulletVisual>();
                }
            }
        }
        return b;
    }

    /// <summary>Merminin anlik hizi (m/s): yaklasan tehdit uyarisi icin.</summary>
    public Vector3 Velocity => velocity;

    /// <summary>Canli dusman mermileri (salt okunur).</summary>
    public static IReadOnlyList<Bullet> Enemy => EnemyBullets;

    private Transform tracer;
    private Renderer tracerRenderer;
    private float tracerThickness, tracerFullLength;

    /// <summary>Oyuncu mermisi namludan ciktiktan sonra bu kadar sure (sn) gorunmez kalir.</summary>
    public const float HiddenAfterMuzzle = 0.02f;

    private void Update()
    {
        float dt = Time.deltaTime;
        age += dt;

        if (tracerRenderer != null && !tracerRenderer.enabled && age >= HiddenAfterMuzzle)
            tracerRenderer.enabled = true;

        if (tracer != null)
        {
            // Izin kuyrugu dogdugu noktada sabit: uzunluk gidilen yola esit (en fazla tam boy)
            float len = Mathf.Clamp(age * velocity.magnitude, 0.3f, tracerFullLength);
            tracer.localScale = new Vector3(tracerThickness, tracerThickness, len);
            tracer.localPosition = new Vector3(0f, 0f, -len * 0.5f);
        }
        if (age > lifetime)
        {
            Despawn();
            return;
        }

        Vector3 start = transform.position;
        Vector3 delta = velocity * dt;
        float dist = delta.magnitude;
        if (dist < 1e-4f) return;

        // Dusman mermisi oyuncunun kalkan kuresine girerse yuzeyde parcalanir
        if (!fromPlayer && TryHitShield(start, delta))
        {
            Despawn();
            return;
        }

        int n = Physics.SphereCastNonAlloc(start, radius, delta / dist, Hits, dist, ~0, QueryTriggerInteraction.Ignore);

        int best = -1;
        float bestDist = float.MaxValue;
        for (int i = 0; i < n; i++)
        {
            RaycastHit h = Hits[i];
            if (owner != null && h.collider.transform.IsChildOf(owner)) continue;

            // Dost ateşi yok: oyuncu mermisi oyuncuya, dusman mermisi dusmana islemez
            var dmg = h.collider.GetComponentInParent<IDamageable>();
            if (dmg != null && ((fromPlayer && dmg is PlayerHealth) || (!fromPlayer && dmg is EnemyPlane))) continue;

            // Delinen dusmanin diger parcalarini tekrar sayma
            if (dmg != null && ReferenceEquals(dmg, lastPierced)) continue;

            if (h.distance < bestDist)
            {
                bestDist = h.distance;
                best = i;
            }
        }

        if (best < 0)
        {
            transform.position = start + delta;
            return;
        }

        RaycastHit hit = Hits[best];
        // Baslangicta ic ice gecmis colliderlarda point sifir doner
        Vector3 point = hit.distance <= 0f ? start : hit.point;

        var target = hit.collider.GetComponentInParent<IDamageable>();
        if (target != null)
        {
            target.TakeHit(damage, point);

            // Delici mermi: dusmani gecip devam eder
            if (pierceLeft > 0)
            {
                pierceLeft--;
                lastPierced = target;
                transform.position = start + delta;
                return;
            }
        }
        else if (CombatManager.Instance != null)
        {
            CombatManager.Instance.SpawnHitSpark(point);
        }

        Despawn();
    }
}
