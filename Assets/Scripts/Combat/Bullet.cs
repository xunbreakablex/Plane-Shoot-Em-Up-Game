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

    /// <summary>Plazma: carpinca bu yaricapta (m) patlar ve civardaki dusmanlara hasarin %60'ini verir. 0 = patlamaz.</summary>
    public float splash;
    private static readonly Collider[] SplashHits = new Collider[24];
    private static float lastSplashFx;
    private static readonly System.Collections.Generic.HashSet<EnemyPlane> SplashDone = new System.Collections.Generic.HashSet<EnemyPlane>();

    private void Splash(Vector3 point, IDamageable direct)
    {
        CombatManager mgr = CombatManager.Instance;
        if (mgr != null && Time.time - lastSplashFx > 0.12f) { lastSplashFx = Time.time; mgr.SpawnExplosion(point); }   // efekt seyreltilir
        int n = Physics.OverlapSphereNonAlloc(point, splash, SplashHits, ~0, QueryTriggerInteraction.Ignore);
        int dmg = Mathf.Max(1, Mathf.RoundToInt(damage * 0.6f));
        SplashDone.Clear();
        var done = SplashDone;
        for (int i = 0; i < n; i++)
        {
            var e = SplashHits[i].GetComponentInParent<EnemyPlane>();
            if (e == null || ReferenceEquals(e, direct) || !done.Add(e)) continue;
            e.TakeHit(dmg, point);
        }
    }

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
        b.splash = 0f;
        b.lastPierced = null;
        b.age = 0f;
        b.lifetime = b.defaultLifetime > 0f ? b.defaultLifetime : b.lifetime;
        b.easing = false;

        if (fromPlayer)
        {
            // Mermi sert bir cubuk: ucu (nesnenin konumu) onde, tam boy iz arkasina uzanir. Yalniz bir kez kurulur.
            if (b.tracer == null)
            {
                b.tracer = b.transform.Find("Tracer");
                if (b.tracer != null)
                {
                    b.tracerFullLength = b.tracer.localScale.z;
                    b.tracerRenderer = b.tracer.GetComponent<Renderer>();
                    b.tracer.localPosition = new Vector3(0f, 0f, -b.tracerFullLength * 0.5f);
                }
            }

            // Namludan cikan mermi KIPI namludan cikana kadar gorunmez: ucu namlu ucunun TipClearance onunde
            // dogar, kipi tam boy gerisindedir; kip namlu ucunu gectiginde (gidilen yol >= boy - TipClearance)
            // gorunur olur. Gizli kalma suresi bu yuzden merminin hizina gore degisir (hizli mermi daha kisa).
            // Zaman sabiti yok; mesafe karsilastirmasi yalniz gizliyken yapilir (fizik/trigger gerekmez).
            b.spawnPos = position;
            b.rearClearDist = Mathf.Max(0f, b.tracerFullLength - PlayerShot.TipClearance + GameSettings.Current.bulletRearMargin);
            if (b.tracerRenderer != null) b.tracerRenderer.enabled = b.rearClearDist <= 0f;
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
    private float tracerFullLength;
    private Vector3 spawnPos;        // dogus noktasi (namlu ucunun TipClearance onu)
    private float rearClearDist;     // kipin namluyu gecmesi icin gidilmesi gereken yol (m); gorunurluk bu mesafede acilir

    /// <summary>Merminin kipi namluyu gecene kadar gizli kaldigi yol (m). Hiza bolunce gizli kalma suresi.</summary>
    public float HiddenDistance => rearClearDist;

    // Gizliyken her karede bir kez: gidilen yol esigi gectiyse mermiyi gorunur yapar
    private void RevealIfRearClear()
    {
        if (tracerRenderer == null || tracerRenderer.enabled) return;
        if ((transform.position - spawnPos).sqrMagnitude >= rearClearDist * rearClearDist)
            tracerRenderer.enabled = true;
    }

    // ---- Nisan noktasina yumusak kivrilma (ease-in). Hedef SABIT bir noktadir (atis anindaki onde
    // tutma noktasi): mermi dusmani takip etmez, yalnizca namludan dumduz cikip o noktaya yonelir.
    private const float EaseMaxTurn = 6f;        // tam gucte donus hizi (rad/sn, ~340 derece/sn)
    private bool easing;
    private Vector3 easePoint;
    private float easeTime;

    /// <summary>Mermi namlu yonunde cikar ve verilen noktaya ease-in ile kivrilir (yalniz oyuncu mermisi).</summary>
    public void EaseTowards(Vector3 point, float time)
    {
        easing = true;
        easePoint = point;
        easeTime = Mathf.Max(0.01f, time);
    }

    private void UpdateEase(float dt)
    {
        Vector3 to = easePoint - transform.position;
        float dist = to.magnitude;

        // Noktaya vardi ya da gecti: duz devam eder
        if (dist < 3f || Vector3.Dot(to, velocity) <= 0f)
        {
            easing = false;
            return;
        }

        float k = Mathf.Clamp01(age / easeTime);
        k *= k;                                                 // ease-in: yavas baslar, hizlanir
        Vector3 desired = to / dist * velocity.magnitude;
        velocity = Vector3.RotateTowards(velocity, desired, k * EaseMaxTurn * dt, 0f);
        transform.rotation = Quaternion.LookRotation(velocity);

        // Tam hizalandiysa kivrilma biter (gereksiz hesap yapilmasin)
        if (k >= 1f && Vector3.Angle(velocity, to) < 0.2f) easing = false;
    }


    private void Update()
    {
        float dt = Time.deltaTime;
        age += dt;

        if (age > lifetime)
        {
            Despawn();
            return;
        }

        if (easing) UpdateEase(dt);

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
            RevealIfRearClear();
            return;
        }

        RaycastHit hit = Hits[best];
        // Baslangicta ic ice gecmis colliderlarda point sifir doner
        Vector3 point = hit.distance <= 0f ? start : hit.point;

        var target = hit.collider.GetComponentInParent<IDamageable>();
        if (splash > 0f && fromPlayer) Splash(point, target);
        if (target != null)
        {
            target.TakeHit(damage, point);

            // Delici mermi: dusmani gecip devam eder
            if (pierceLeft > 0)
            {
                pierceLeft--;
                lastPierced = target;
                transform.position = start + delta;
                RevealIfRearClear();
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
