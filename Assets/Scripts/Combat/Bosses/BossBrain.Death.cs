using System.Collections.Generic;
using UnityEngine;

// Boss olumu (R-Type / Darius tarzi): govde hemen yok olmaz. Once en uctaki parcalardan (kanat uclari, kuyruk,
// uzuvlar) baslayarak art arda patlar ve parcalar kopup savrulur; patlamalar hizlanarak merkeze yaklasir, govde
// titrer ve beyaz yanip soner; sonunda cekirdekte buyuk bir patlamayla dagilir. Boss fazinda govde cerceveyle
// birlikte tasinir, boyle olunca patlamalar hep gorus alaninda kalir.
public partial class BossBrain
{
    private bool dying;
    private float dieStart, dieDuration;
    private float dieNextBlast;
    private int dieIndex, dieBlasts;
    private Vector3 dieVel;
    private bool dieFlash;
    private readonly List<Renderer> dieOrder = new List<Renderer>();

    public bool Dying => dying;
    /// <summary>Olum gosterisinin kalan suresi (sn); boss fazi bunun bitmesini bekler.</summary>
    public float DeathTimeLeft => dying ? Mathf.Max(0f, dieStart + dieDuration - Time.time) : 0f;

    /// <summary>EnemyPlane.Die cagirir: govdeyi yok etmek yerine asamali patlama baslar. false: beyin hazir degil.</summary>
    public bool BeginDeath()
    {
        if (dying || mgr == null || renderers == null) return false;
        dying = true;
        dieStart = Time.time;
        dieIndex = 0;
        dieBlasts = 0;
        dieNextBlast = Time.time + 0.12f;
        dieVel = vel * 0.25f;
        chargeActive = false;
        ClearWarns();
        if (beamLine != null) beamLine.enabled = false;
        beamActive = false;

        // Artik hedef degil, carpmaz
        foreach (var c in GetComponentsInChildren<Collider>(true)) if (c != null) c.enabled = false;

        // Patlama sirasi: govde merkezinden en uzak parca once (uclardan iceri dogru)
        dieOrder.Clear();
        Vector3 c0 = transform.position;
        foreach (var r in renderers)
            if (r != null && !(r is LineRenderer) && !(r is TrailRenderer) && !(r is ParticleSystemRenderer)) dieOrder.Add(r);
        dieOrder.Sort((a, b) => (b.bounds.center - c0).sqrMagnitude.CompareTo((a.bounds.center - c0).sqrMagnitude));

        dieDuration = Mathf.Clamp(2.2f + dieOrder.Count * 0.04f, 2.8f, 4.2f);
        Sfx.PlayAt(Sfx.BossRoar, c0, 0.9f);
        return true;
    }

    private void StepDeath(float dt)
    {
        // Boss fazinda cerceveyle birlikte kayar ve doner
        mgr.RushStepDelta();
        if (mgr.RushActive)
        {
            pos = mgr.RushMapPoint(rb.position);
            rot = mgr.RushStepRotation * rb.rotation;
            dieVel = mgr.RushMapDir(dieVel);
        }
        else
        {
            pos = rb.position;
            rot = rb.rotation;
        }

        float t = Mathf.Clamp01((Time.time - dieStart) / dieDuration);

        // Suzulerek alcalir, hizi soner; govde gittikce daha sert sarsilir
        dieVel = Vector3.MoveTowards(dieVel, Vector3.down * 9f, 10f * dt);
        pos += dieVel * dt;
        float jitter = (0.3f + 2.2f * t) * transform.lossyScale.x * 0.12f;
        pos += Random.insideUnitSphere * jitter * dt * 12f;
        rot = rot * Quaternion.Euler(Random.insideUnitSphere * (1.5f + 6f * t) * dt * 10f);

        float ground = mgr.GroundHeight(pos);
        if (pos.y < ground + 40f) pos.y = ground + 40f;

        // Beyaz yanip sonme: sona dogru sikilasir
        bool want = Mathf.Repeat(Time.time * (3f + 12f * t), 1f) < 0.35f + 0.3f * t;
        if (want != dieFlash)
        {
            dieFlash = want;
            ApplyFlash(want);
        }

        // Uclardan iceri patlamalar: aralik kisalir
        if (t < 1f && Time.time >= dieNextBlast)
        {
            dieNextBlast = Time.time + Mathf.Lerp(0.34f, 0.07f, t * t);
            DeathBlast(t);
        }

        if (t >= 1f) FinishDeath();

        rb.MovePosition(pos);
        rb.MoveRotation(rot);
        enemy.SetVelocity(dieVel + CombatManager.FrameVelocityAt(pos));
    }

    // Siradaki parcada patlama; uctaki parcalarin bir kismi kopup savrulur
    private void DeathBlast(float t)
    {
        if (dieOrder.Count == 0) return;
        Renderer r = dieOrder[dieIndex % dieOrder.Count];
        dieIndex++;
        dieBlasts++;
        if (r == null) return;

        Vector3 c = r.bounds.center;
        float s = transform.lossyScale.x;
        mgr.SpawnExplosion(c, Mathf.Lerp(1.1f, 1.9f, t));
        SpawnDebrisAt(c, r.sharedMaterial, 3);
        FollowCamera.AddShake(0.25f + 0.35f * t, 0.25f);

        // Ilk ucte ikideki (dis) parcalardan her ikincisi kopar; cekirdek sona kalir
        bool outer = dieIndex <= dieOrder.Count * 2 / 3;
        if (outer && (dieBlasts & 1) == 0 && r.transform != transform)
            FlingPart(r.transform, c);
    }

    // Parca govdeden ayrilir: fizikle savrulup duser, duman izi birakir
    private void FlingPart(Transform part, Vector3 center)
    {
        if (part == null || part == transform) return;
        var holder = new GameObject("BossParca");
        holder.transform.SetPositionAndRotation(center, part.rotation);
        part.SetParent(holder.transform, true);
        foreach (var col in part.GetComponentsInChildren<Collider>(true)) col.enabled = false;
        foreach (var rr in part.GetComponentsInChildren<Renderer>(true)) rr.SetPropertyBlock(null);

        var prb = holder.AddComponent<Rigidbody>();
        prb.mass = 3f;
        prb.useGravity = true;
        prb.linearDamping = 0.2f;
        prb.angularDamping = 0.15f;
        Vector3 outward = center - transform.position;
        outward = outward.sqrMagnitude > 0.01f ? outward.normalized : Random.onUnitSphere;
        prb.linearVelocity = dieVel + CombatManager.FrameVelocityAt(center) + outward * Random.Range(18f, 34f) + Vector3.up * Random.Range(4f, 14f);
        prb.angularVelocity = Random.onUnitSphere * Random.Range(2f, 6f);

        if (mgr.SmokeTrailPrefab != null)
        {
            var smoke = Instantiate(mgr.SmokeTrailPrefab, holder.transform);
            smoke.transform.localPosition = Vector3.zero;
        }
        Destroy(holder, 7f);
        RefreshRenderers();
    }

    // Son: cekirdekte buyuk patlama, parca yagmuru, govde yok olur
    private void FinishDeath()
    {
        Vector3 c = transform.position;
        float s = transform.lossyScale.x;
        for (int i = 0; i < 7; i++)
            mgr.SpawnExplosion(c + Random.insideUnitSphere * 3.5f * s, i == 0 ? 3.2f : Random.Range(1.6f, 2.6f));
        mgr.SpawnExplosionBurst(c, 6f * s, 5, 0.12f);
        Material mat = null;
        foreach (var r in renderers) if (r != null) { mat = r.sharedMaterial; break; }
        SpawnDebrisAt(c, mat, 16);
        FollowCamera.AddShake(2.2f, 1.2f);
        Sfx.PlayAt(Sfx.Explosion, c, 1f);
        Sfx.PlayAt(Sfx.Crack, c, 0.8f);

        // Kalan govde parcalari savrulur (gorunur kalmasin)
        var rest = new List<Renderer>(GetComponentsInChildren<Renderer>(true));
        int k = 0;
        foreach (var r in rest)
        {
            if (r == null || r is LineRenderer || r is TrailRenderer || r is ParticleSystemRenderer) continue;
            if (k++ < 10 && r.transform != transform) FlingPart(r.transform, r.bounds.center);
            else r.enabled = false;
        }
        dying = false;
        Destroy(gameObject);
    }
}
