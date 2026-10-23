using System.Collections.Generic;
using UnityEngine;

// PlayerGun'in lazer kismi. Lazer silahlari (PartDef.beam > 0) mermi atmaz: ates tusu basili tutuldukca bir isin cizilir,
// isin aninda vurur ve yolundaki TUM dusmanlari deler (arazi / pist / ada gibi hasar almayan seyler isini durdurur).
//   beam 1: kesintisiz isin (namlu ucundan)
//   beam 2: darbeli isin (beamOn sn acik, beamOff sn kapali; namlu ucundan)
//   beam 3: portal lazeri: burnun onunde bir halka (portal) acilir, darbeli isin ondan cikar
// Hasar vurusu: acikken saniyede (rate / aciklik orani) kez; her vurusta isin boyunca her dusmana bir kez.
public partial class PlayerGun
{
    private const float PortalDistance = 28f;     // portalin burnun onundeki mesafesi (m)
    private const int PortalSegments = 28;
    private const float BeamRadius = 1.1f;        // isinin isabet kalinligi (m)

    private class BeamVisual
    {
        public LineRenderer core, glow, ring;
        public AudioSource hum;
        public float clock;                       // tus basili kaldigi sure (darbe fazi icin)
        public float nextTick;
        public bool active;
    }

    private readonly BeamVisual[] beams = new BeamVisual[PartCatalog.WeaponSlots];
    private readonly RaycastHit[] beamHits = new RaycastHit[24];
    private readonly IDamageable[] beamSeen = new IDamageable[24];

    private static readonly Color CoreColor = new Color(0.9f, 1f, 1f, 1f);
    private static readonly Color GlowColor = new Color(0.25f, 0.8f, 1f, 0.45f);
    private static readonly Color PortalColor = new Color(0.55f, 0.4f, 1f, 0.95f);

    private LineRenderer MakeLine(string name, Transform parent, Material mat, bool loop, int points)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var lr = go.AddComponent<LineRenderer>();
        lr.sharedMaterial = mat;
        lr.useWorldSpace = true;
        lr.loop = loop;
        lr.positionCount = points;
        lr.numCapVertices = loop ? 0 : 4;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        lr.enabled = false;
        return lr;
    }

    private BeamVisual EnsureBeam(int slot)
    {
        BeamVisual b = beams[slot];
        if (b != null && b.core != null) return b;

        CombatManager mgr = CombatManager.Instance;
        Material mat = mgr != null ? mgr.fxMaterial : null;
        if (mat == null) return null;

        b = new BeamVisual();
        var root = new GameObject("Beam" + slot).transform;
        root.SetParent(transform, false);
        b.glow = MakeLine("Glow", root, mat, false, 2);
        b.core = MakeLine("Core", root, mat, false, 2);
        b.ring = MakeLine("Portal", root, mat, true, PortalSegments);
        b.hum = root.gameObject.AddComponent<AudioSource>();
        b.hum.clip = Sfx.BeamHum;
        b.hum.loop = true;
        b.hum.spatialBlend = 0f;
        b.hum.playOnAwake = false;
        b.hum.volume = 0.18f;
        beams[slot] = b;
        return b;
    }

    private static void SetLine(LineRenderer lr, float width, Color c)
    {
        lr.startWidth = width;
        lr.endWidth = width;
        lr.startColor = c;
        lr.endColor = c;
    }

    /// <summary>Ates birakilinca tum isinlar ve portallar kapanir; darbe fazi sifirlanir.</summary>
    private void StopBeams()
    {
        for (int i = 0; i < beams.Length; i++)
        {
            BeamVisual b = beams[i];
            if (b == null || !b.active) continue;
            b.active = false;
            b.clock = 0f;
            if (b.core != null) b.core.enabled = false;
            if (b.glow != null) b.glow.enabled = false;
            if (b.ring != null) b.ring.enabled = false;
            if (b.hum != null) b.hum.Stop();
        }
    }

    // Bir lazer slotunun bir kare isi: kaynak + yon, darbe fazi, isin cizimi, vurus
    private void BeamStep(int slot, PartDef def, PlaneController plane, float span)
    {
        BeamVisual b = EnsureBeam(slot);
        if (b == null) return;
        if (def.beam == 4) { LightningStep(slot, def, plane, span, b); return; }
        if (b.core.positionCount != 2) { b.core.positionCount = 2; b.glow.positionCount = 2; }

        Transform t = plane.transform;
        GameSettings gs = GameSettings.Current;
        UpgradeMods mods = RunUpgrades.Mods;
        float rateMul = mods.fireRateMul * (1f + PowerRateStep * RunState.PowerLevel) * AircraftCatalog.Get(flight.CurrentIndex).fireRateMul;

        // Kaynak: portal ya da namlu ucu
        Vector3 origin;
        if (def.beam == 3)
        {
            float lateral = PlaneBuilder.WeaponOffset(flight.CurrentIndex, slot, span).x;
            origin = t.position + t.forward * PortalDistance + t.right * lateral * 0.6f + t.up * 0.4f;
        }
        else
        {
            MuzzleFlash mp = MuzzleFor(t, slot);
            origin = mp != null
                ? PlayerShot.MuzzlePoint(mp)
                : t.TransformPoint(PlaneBuilder.WeaponOffset(flight.CurrentIndex, slot, span) + new Vector3(0f, 0f, PlayerShot.TipClearance));
        }

        // Yon: nisan noktasina, burundan en fazla fireCone sapar (mermilerle ayni)
        Vector3 dir = (AimPoint - origin).normalized;
        if (Vector3.Angle(t.forward, dir) > gs.fireCone)
            dir = Vector3.RotateTowards(t.forward, dir, gs.fireCone * Mathf.Deg2Rad, 0f).normalized;

        // Darbe fazi: kesintisizde hep acik; darbelide tus basildigindan beri (on, off) dongusu (acik fazla baslar)
        float on = def.beam == 1 ? 1f : Mathf.Max(def.beamOn, 0.05f);
        float off = def.beam == 1 ? 0f : Mathf.Max(def.beamOff, 0f);
        float cycle = on + off;
        b.clock += Time.deltaTime;
        bool beamOn = off <= 0f || (b.clock % cycle) < on;
        float duty = on / cycle;
        float tickRate = def.rate * rateMul / Mathf.Max(duty, 0.05f);

        // Portal: tus basili oldukca acik halka (isin kapaliyken bile); acilirken buyur
        b.active = true;
        if (def.beam == 3)
        {
            float open = Mathf.Clamp01(b.clock / 0.12f);
            float pulse = 1f + 0.08f * Mathf.Sin(Time.time * 18f);
            float radius = 3.4f * open * pulse;
            Vector3 up = Mathf.Abs(Vector3.Dot(dir, Vector3.up)) > 0.95f ? Vector3.right : Vector3.up;
            Vector3 u = Vector3.Cross(dir, up).normalized;
            Vector3 v = Vector3.Cross(dir, u).normalized;
            for (int i = 0; i < PortalSegments; i++)
            {
                float a = Mathf.PI * 2f * i / PortalSegments;
                b.ring.SetPosition(i, origin + (u * Mathf.Cos(a) + v * Mathf.Sin(a)) * radius);
            }
            SetLine(b.ring, 0.45f, PortalColor);
            b.ring.enabled = true;
        }

        if (!beamOn)
        {
            b.core.enabled = false;
            b.glow.enabled = false;
            if (b.hum != null && b.hum.isPlaying) b.hum.Pause();
            return;
        }
        if (b.hum != null && !b.hum.isPlaying) b.hum.Play();

        // Isin boyunca tara: en yakindan uzaga; hasar almayan ilk sey isini durdurur, dusmanlar delinir
        float range = def.range;
        int n = Physics.SphereCastNonAlloc(origin, BeamRadius, dir, beamHits, range, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 1; i < n; i++)   // ekleme siralamasi (n kucuk)
        {
            RaycastHit key = beamHits[i];
            int j = i - 1;
            while (j >= 0 && beamHits[j].distance > key.distance) { beamHits[j + 1] = beamHits[j]; j--; }
            beamHits[j + 1] = key;
        }

        bool tick = Time.time >= b.nextTick;
        if (tick) b.nextTick = Time.time + 1f / Mathf.Max(tickRate, 0.1f);
        int dmg = Mathf.RoundToInt(def.damage) + (tick && Random.value < mods.critChance ? 1 : 0);

        float end = range;
        int seen = 0;
        for (int k = 0; k < n; k++)
        {
            RaycastHit h = beamHits[k];
            if (h.collider.transform.IsChildOf(t)) continue;

            IDamageable target = h.collider.GetComponentInParent<IDamageable>();
            if (target == null || target is PlayerHealth)
            {
                // Arazi / pist / ada / deniz: isin burada biter
                if (h.distance > 0.5f) { end = h.distance; break; }
                continue;
            }

            if (!tick) continue;

            // Ayni dusmanin birden cok parcasi bir vurusta tek sayilir
            bool dup = false;
            for (int s = 0; s < seen; s++) if (ReferenceEquals(beamSeen[s], target)) { dup = true; break; }
            if (dup) continue;
            if (seen < beamSeen.Length) beamSeen[seen++] = target;

            if (target is EnemyPlane ep) ep.TakeHitLocal(CombatManager.PlayerDamage(ep, dmg), h.point, h.collider);
            else target.TakeHit(dmg, h.point);
        }

        // Cizim: ince parlak cekirdek + genis yari saydam hale
        Vector3 endPoint = origin + dir * end;
        float w = def.beam == 1 ? 0.55f : 0.8f;
        SetLine(b.core, w, CoreColor);
        SetLine(b.glow, w * 3.2f, GlowColor);
        b.core.SetPosition(0, origin);
        b.core.SetPosition(1, endPoint);
        b.glow.SetPosition(0, origin);
        b.glow.SetPosition(1, endPoint);
        b.core.enabled = true;
        b.glow.enabled = true;
    }

    // ------------------------------------------------------------------ yildirim (beam 4)
    // Imparator'un silahi: parmak ucundan nisan konisindeki en yakin dusmana yildirim atlar, oradan en yakin iki
    // dusmana daha sicrar. Kirik cizgi her kare yeniden cizilir; vurus def.rate/sn, zincirdeki herkese.

    private const float LightningCone = 32f;
    private const float LightningHop = 170f;
    private const int LightningSegments = 9;
    private readonly List<EnemyPlane> zapChain = new List<EnemyPlane>(3);
    private static readonly Color ZapCore = new Color(0.92f, 0.85f, 1f, 1f);
    private static readonly Color ZapGlow = new Color(0.6f, 0.25f, 1f, 0.7f);

    private void LightningStep(int slot, PartDef def, PlaneController plane, float span, BeamVisual b)
    {
        Transform t = plane.transform;
        CombatManager mgr = CombatManager.Instance;
        if (mgr == null) return;

        MuzzleFlash mp = MuzzleFor(t, slot);
        Vector3 origin = mp != null
            ? PlayerShot.MuzzlePoint(mp)
            : t.TransformPoint(PlaneBuilder.WeaponOffset(flight.CurrentIndex, slot, span));
        Vector3 aimDir = (AimPoint - origin).normalized;

        b.active = true;
        b.clock += Time.deltaTime;
        if (b.ring != null) b.ring.enabled = false;

        // Zincir: koni icindeki en yakin dusman, sonra ona en yakin dusmanlar
        zapChain.Clear();
        EnemyPlane first = null;
        float best = def.range;
        foreach (var e in mgr.Enemies)
        {
            if (e == null || e.IsDead) continue;
            Vector3 to = e.transform.position - origin;
            float d = to.magnitude;
            if (d > best || d < 1f || Vector3.Angle(aimDir, to) > LightningCone) continue;
            best = d;
            first = e;
        }
        if (first == null)
        {
            // Hedef yok: parmak ucunda kisa kivilcim
            b.core.positionCount = 2;
            b.glow.positionCount = 2;
            Vector3 tip = origin + aimDir * (2.5f + Random.value * 2f) + Random.insideUnitSphere * 0.6f;
            SetLine(b.core, 0.12f, ZapCore);
            SetLine(b.glow, 0.4f, ZapGlow);
            b.core.SetPosition(0, origin); b.core.SetPosition(1, tip);
            b.glow.SetPosition(0, origin); b.glow.SetPosition(1, tip);
            b.core.enabled = b.glow.enabled = true;
            if (b.hum != null && b.hum.isPlaying) b.hum.Pause();
            return;
        }
        zapChain.Add(first);
        for (int hop = 0; hop < 2; hop++)
        {
            EnemyPlane last = zapChain[zapChain.Count - 1];
            EnemyPlane next = null;
            float nd = LightningHop;
            foreach (var e in mgr.Enemies)
            {
                if (e == null || e.IsDead || zapChain.Contains(e)) continue;
                float d = Vector3.Distance(last.transform.position, e.transform.position);
                if (d < nd) { nd = d; next = e; }
            }
            if (next == null) break;
            zapChain.Add(next);
        }
        if (b.hum != null && !b.hum.isPlaying) b.hum.Play();

        // Vurus
        UpgradeMods mods = RunUpgrades.Mods;
        float rateMul = mods.fireRateMul * (1f + PowerRateStep * RunState.PowerLevel) * AircraftCatalog.Get(flight.CurrentIndex).fireRateMul;
        if (Time.time >= b.nextTick)
        {
            b.nextTick = Time.time + 1f / Mathf.Max(def.rate * rateMul, 0.1f);
            int dmg = Mathf.RoundToInt(def.damage) + (Random.value < mods.critChance ? 1 : 0);
            foreach (var e in zapChain)
            {
                e.TakeHit(CombatManager.PlayerDamage(e, dmg), e.transform.position);
                mgr.SpawnHitSpark(e.transform.position + Random.insideUnitSphere * 1.5f, 1.2f);
            }
            if (Random.value < 0.5f) Sfx.PlayAt(Sfx.Zap, origin, 0.22f);
        }

        // Cizim: her hop icin kirik cizgi parcalari (her kare yeni titreme)
        int points = zapChain.Count * LightningSegments + 1;
        b.core.positionCount = points;
        b.glow.positionCount = points;
        Vector3 from = origin;
        int idx = 0;
        b.core.SetPosition(idx, from); b.glow.SetPosition(idx, from); idx++;
        for (int h = 0; h < zapChain.Count; h++)
        {
            Vector3 to = zapChain[h].AimPoint;   // korunan boss'ta hedef uzuv; merkezdeki cekirdegi ortmesin
            float len = Vector3.Distance(from, to);
            for (int i = 1; i <= LightningSegments; i++)
            {
                float f = (float)i / LightningSegments;
                Vector3 p = Vector3.Lerp(from, to, f);
                // Titreme ortada en buyuk, uclarda sifir: hedefin ustunde mor karalama olmasin (eskiden uca kadar %5)
                if (i < LightningSegments) p += Random.insideUnitSphere * len * 0.05f * Mathf.Sin(f * Mathf.PI);
                b.core.SetPosition(idx, p);
                b.glow.SetPosition(idx, p);
                idx++;
            }
            from = to;
        }
        SetLine(b.core, 0.55f, ZapCore);
        SetLine(b.glow, 2.2f, ZapGlow);
        b.core.endWidth = 0.3f;   // hedefe dogru incelir
        b.glow.endWidth = 0.8f;
        b.core.enabled = true;
        b.glow.enabled = true;
    }
}
