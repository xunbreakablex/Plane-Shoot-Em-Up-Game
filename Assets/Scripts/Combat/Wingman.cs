using UnityEngine;

// Yardimci IHA (W guclendirmesi): oyuncunun iki yaninda ucar, en yakin dusmana kendiliginden ates eder.
// Iki IHA bir cift olarak yasar; tekrar alinca sure uzar. Suresi bitmeden once yanip soner.
public class Wingman : MonoBehaviour
{
    public const float Seconds = 28f;

    private static Wingman[] pair;

    private Transform player;
    private Vector3 offset;
    private Vector3 localOffset;
    private float until;
    private float nextScan, nextFire;
    private EnemyPlane target;
    private Renderer[] renderers;
    private float seed;

    /// <summary>Cifti verir ya da suresini uzatir.</summary>
    public static void Grant(CombatManager mgr)
    {
        if (mgr == null || mgr.PlayerTransform == null) return;

        if (pair != null && pair[0] != null && pair[1] != null)
        {
            pair[0].until = pair[1].until = Time.time + Seconds;
            return;
        }

        pair = new Wingman[2];
        for (int i = 0; i < 2; i++)
        {
            float side = i == 0 ? -1f : 1f;
            GameObject go = Build(mgr.bossLit, mgr.bossGlow);
            go.name = "Wingman" + i;
            Wingman w = go.AddComponent<Wingman>();
            w.player = mgr.PlayerTransform;
            w.offset = new Vector3(side * 11.5f, 2f, 5f);
            w.localOffset = w.offset + new Vector3(side * 12f, 6f, -10f);   // ucagin yanina suzulerek gelir
            w.until = Time.time + Seconds;
            w.seed = i * 3.1f;
            w.renderers = go.GetComponentsInChildren<Renderer>();
            go.transform.position = mgr.PlayerTransform.TransformPoint(w.offset);
            go.transform.rotation = mgr.PlayerTransform.rotation;
            pair[i] = w;
        }
    }

    /// <summary>Oyun sonu / sahne degisimi: IHA'lari kaldirir.</summary>
    public static void Clear()
    {
        if (pair == null) return;
        foreach (var w in pair) if (w != null) Destroy(w.gameObject);
        pair = null;
    }

    // Kucuk savas dronu: govde, kanatlar, kuyruk ve parlayan motor
    private static GameObject Build(Material lit, Material glow)
    {
        var root = new GameObject("Wingman");
        root.transform.localScale = Vector3.one * 1.0f;
        Material body = lit != null ? new Material(lit) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
        SetColor(body, new Color(0.25f, 0.55f, 0.62f));
        Material trim = new Material(body);
        SetColor(trim, new Color(0.9f, 0.92f, 0.95f));
        Material eng = glow != null ? new Material(glow) : new Material(body);
        SetColor(eng, new Color(0.4f, 0.95f, 1f));

        Part(root, PrimitiveType.Capsule, new Vector3(0f, 0f, 0f), new Vector3(0.9f, 1.8f, 0.9f), new Vector3(90f, 0f, 0f), body);
        Part(root, PrimitiveType.Cube, new Vector3(0f, 0f, -0.2f), new Vector3(4.2f, 0.12f, 1.1f), Vector3.zero, trim);
        Part(root, PrimitiveType.Cube, new Vector3(0f, 0.55f, -1.4f), new Vector3(0.12f, 0.9f, 0.8f), Vector3.zero, body);
        Part(root, PrimitiveType.Sphere, new Vector3(0f, 0f, -2.0f), new Vector3(0.55f, 0.55f, 0.55f), Vector3.zero, eng);
        return root;
    }

    private static void Part(GameObject root, PrimitiveType type, Vector3 pos, Vector3 scale, Vector3 euler, Material m)
    {
        GameObject g = GameObject.CreatePrimitive(type);
        Destroy(g.GetComponent<Collider>());
        g.transform.SetParent(root.transform, false);
        g.transform.localPosition = pos;
        g.transform.localScale = scale;
        g.transform.localEulerAngles = euler;
        g.GetComponent<Renderer>().sharedMaterial = m;
    }

    private static void SetColor(Material m, Color c)
    {
        m.color = c;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
    }

    private void Update()
    {
        if (GameState.Paused) return;

        CombatManager mgr = CombatManager.Instance;
        player = mgr != null ? mgr.PlayerTransform : null;
        if (player == null || RunState.Ended || Time.time > until)
        {
            Clear();
            if (this != null) Destroy(gameObject);
            return;
        }

        float dt = Time.deltaTime;
        // Konum ucagin yerel uzayinda sabit tutulur (hizli ucakta geride kalmasin); yalniz yerel ofset yumusar ve salinir
        Vector3 sway = new Vector3(Mathf.Sin(Time.time * 1.3f + seed) * 1.6f, Mathf.Sin(Time.time * 2.2f + seed) * 1.4f, 0f);
        localOffset = Vector3.Lerp(localOffset, offset + sway, 1f - Mathf.Exp(-5f * dt));
        transform.position = player.TransformPoint(localOffset);
        transform.rotation = Quaternion.Slerp(transform.rotation, player.rotation, 1f - Mathf.Exp(-8f * dt));

        // Suresi bitmeden yanip soner
        bool blinkOff = until - Time.time < 4f && Mathf.Sin(Time.time * 18f) > 0.3f;
        foreach (var r in renderers) if (r != null) r.enabled = !blinkOff;

        // Hedef tara ve ates et
        if (Time.time >= nextScan)
        {
            nextScan = Time.time + 0.3f + Random.value * 0.1f;
            target = FindTarget(mgr);
        }

        if (target == null || target.IsDead || mgr.playerBulletPrefab == null || Time.time < nextFire) return;

        const float speed = 420f;
        float dist = Vector3.Distance(transform.position, target.transform.position);
        Vector3 aim = target.transform.position + target.Velocity * (dist / speed);
        Vector3 dir = (aim - transform.position).normalized;
        nextFire = Time.time + 0.2f;
        Vector3 spawn = transform.position + dir * 2.2f;
        Bullet b = Bullet.Spawn(mgr.playerBulletPrefab, spawn, dir, speed, 1, transform, true);
        if (b != null) Sfx.PlayAt(Sfx.Shot, spawn, 0.07f);
    }

    private EnemyPlane FindTarget(CombatManager mgr)
    {
        EnemyPlane best = null;
        float bestScore = 650f;
        foreach (var e in mgr.Enemies)
        {
            if (e == null || e.IsDead) continue;
            Vector3 to = e.transform.position - transform.position;
            // Yalniz onde ve yakin cevredeki hedefler; hava hedefleri onceliklidir
            if (to.sqrMagnitude > 120f * 120f && Vector3.Angle(player.forward, to) > 80f) continue;
            float score = to.magnitude + (e.IsGround ? 300f : 0f);
            if (score < bestScore) { bestScore = score; best = e; }
        }
        return best;
    }
}
