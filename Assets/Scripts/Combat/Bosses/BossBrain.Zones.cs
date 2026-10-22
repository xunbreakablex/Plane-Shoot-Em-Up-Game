using System.Collections.Generic;
using UnityEngine;

// Boss fazi: ISARETLI BOLGELER. Kayma duzleminde (cercevede: sag, yukari) daire seklinde tehlike bolgeleri belirir;
// halka dolar, sure bitince bolge patlar ve icindeki oyuncu vurulur. Orta bosslarin imza saldirilari bununla kurulur:
//   Melek    "KUTSAL ISIK": senin ve cevrendeki noktalara dusen isik sutunlari (iki-uc dalga)
//   Zeplin   "BOMBA HALISI": once arti (+), sonra carpi (x) bicimli bomba isaretleri
//   Iblis    "CEHENNEM CUKURLARI": seni kovalayan uc cukur; kilitlenip patlar (durma, kac)
//   Yengec   "KISKAC": once iki yanin, hemen sonra ortan kapanir (en guvenlisi yukari / asagi kacmak)
// Halkalar oyuncunun onunde, uzakta cizilir (onundeki duzlemde cizilse kameranin hemen onunde kalip gorunmuyordu);
// HUD oyuncu bir bolgenin icindeyse en yakin cikisa "KAC" oku gosterir.
public partial class BossBrain
{
    private struct Zone
    {
        public Vector2 center;        // cercevede
        public float radius;
        public float born, fireAt;
        public float chaseSpeed;      // > 0: kilitlenene kadar oyuncuyu kovalar
        public float lockAt;
        public int style;             // 0 isik, 1 bomba, 2 ates, 3 kiskac
        public bool fired;
    }

    private const float ZoneAhead = 80f;          // halkalarin cizildigi derinlik (oyuncunun onunde)
    private const float ZoneLinger = 0.35f;       // patlamadan sonra gorsel kalma suresi
    private const int ZoneRingPoints = 40;

    private readonly List<Zone> zones = new List<Zone>();
    private readonly List<LineRenderer> zoneRings = new List<LineRenderer>();
    private readonly List<LineRenderer> zoneFills = new List<LineRenderer>();
    private float zoneBusyUntil;

    private bool ZonesBusy => Time.time < zoneBusyUntil;

    private static Color ZoneColor(int style)
    {
        switch (style)
        {
            case 0: return new Color(1f, 0.92f, 0.45f);
            case 1: return new Color(1f, 0.55f, 0.2f);
            case 2: return new Color(1f, 0.25f, 0.1f);
            default: return new Color(0.4f, 0.9f, 1f);
        }
    }

    private void AddZone(Vector2 center, float radius, float delay, int style, float chase = 0f, float lockBefore = 0.6f)
    {
        center.x = Mathf.Clamp(center.x, -CombatManager.RushHalfWidth, CombatManager.RushHalfWidth);
        center.y = Mathf.Clamp(center.y, -CombatManager.RushDown, CombatManager.RushUp);
        zones.Add(new Zone
        {
            center = center, radius = radius, born = Time.time, fireAt = Time.time + delay,
            chaseSpeed = chase, lockAt = Time.time + Mathf.Max(0f, delay - lockBefore), style = style
        });
        zoneBusyUntil = Mathf.Max(zoneBusyUntil, Time.time + delay + 0.5f);
    }

    private static Camera overlayCam;

    /// <summary>
    /// Kayma duzlemindeki (cercevede) bir noktanin, oyuncunun "depth" metre onune cizilecek karsiligi. Kamera oyuncunun
    /// arkasinda ve biraz ustunde oldugu icin duz ileri tasinan isaret oyuncunun ustunde gorunuyordu: kaydirma,
    /// oyuncunun kendi noktasini tam oyuncunun ekrandaki yerine dusurur; diger isaretler onun cevresinde ayni oranla
    /// kuculmus olarak gorunur (icinde misin / disinda misin ekranda dogru okunur).
    /// </summary>
    private Vector3 OverlayPoint(Vector2 frame, float depth)
    {
        Vector3 w = KiWaveWorld(frame) + mgr.RushHeading * depth;
        if (overlayCam == null) overlayCam = Camera.main;
        if (overlayCam == null || player == null) return w;
        Vector3 rel = player.position - overlayCam.transform.position;
        float dz = Vector3.Dot(rel, mgr.RushHeading);
        if (dz < 2f) return w;
        Vector3 lateral = rel - mgr.RushHeading * dz;
        return w + lateral * (depth / dz);
    }

    private void ClearZones()
    {
        zones.Clear();
        zoneBusyUntil = 0f;
        foreach (var lr in zoneRings) if (lr != null) lr.enabled = false;
        foreach (var lr in zoneFills) if (lr != null) lr.enabled = false;
    }

    // ------------------------------------------------------------------ desenler

    // Melek: isik sutunlari; her dalgada biri tam sende, digerleri cevrende
    private void StartLightPillars()
    {
        int waves = phase >= 2 ? 3 : 2;
        int count = 4 + phase;
        Vector2 p = PlayerFrame();
        for (int w = 0; w < waves; w++)
        {
            float delay = 1.3f + w * 0.9f;
            if (w == 0) AddZone(p, 24f, delay, 0);
            else AddZone(p + Random.insideUnitCircle * 30f, 24f, delay, 0, 55f, 0.7f);   // sonraki dalgalarin ilki seni izler
            for (int i = 1; i < count; i++)
                AddZone(p + Random.insideUnitCircle.normalized * Random.Range(55f, 130f), 24f, delay, 0);
        }
        nextAttack = Mathf.Max(nextAttack, zoneBusyUntil + 0.5f);
        mgr.Announce("KUTSAL IŞIK: halkaların dışına çık!", new Color(1f, 0.9f, 0.5f), 2f);
        Sfx.PlayAt(Sfx.BeamCharge, pos, 0.8f);
    }

    // Zeplin: arti, sonra carpi (bosluklar capraz / duz; once capraza, sonra duze kac)
    private void StartCarpetBomb()
    {
        Vector2 p = PlayerFrame();
        const float step = 58f, r = 30f;
        AddZone(p, r, 1.3f, 1);
        AddZone(p + new Vector2(step, 0f), r, 1.3f, 1);
        AddZone(p + new Vector2(-step, 0f), r, 1.3f, 1);
        AddZone(p + new Vector2(0f, step), r, 1.3f, 1);
        AddZone(p + new Vector2(0f, -step), r, 1.3f, 1);
        float d = step * 0.75f;
        float t2 = phase >= 1 ? 2.1f : 2.4f;
        AddZone(p + new Vector2(d, d), r, t2, 1);
        AddZone(p + new Vector2(-d, d), r, t2, 1);
        AddZone(p + new Vector2(d, -d), r, t2, 1);
        AddZone(p + new Vector2(-d, -d), r, t2, 1);
        if (phase >= 2) AddZone(p, r, t2, 1);
        nextAttack = Mathf.Max(nextAttack, zoneBusyUntil + 0.6f);
        mgr.Announce("BOMBA HALISI: önce çapraza, sonra düze kaç!", new Color(1f, 0.6f, 0.3f), 2f);
        Sfx.PlayAt(Sfx.Warning, pos, 0.7f);
    }

    // Iblis: oyuncuyu kovalayan cukurlar; patlamadan 0,6 sn once yerine kilitlenir
    private void StartHellPits()
    {
        Vector2 p = PlayerFrame();
        int n = 2 + Mathf.Min(phase, 1);
        for (int i = 0; i < n; i++)
        {
            Vector2 start = p + new Vector2(Mathf.Cos(i * 2.1f), Mathf.Sin(i * 2.1f)) * 110f;
            AddZone(start, 26f, 2.2f + i * 0.55f, 2, 60f + 10f * phase, 0.6f);
        }
        nextAttack = Mathf.Max(nextAttack, zoneBusyUntil + 0.6f);
        mgr.Announce("CEHENNEM ÇUKURLARI peşinde: durma, kaç!", new Color(1f, 0.4f, 0.25f), 2f);
        Sfx.PlayAt(Sfx.BossRoar, pos, 0.6f);
    }

    // Sahin-X: kanat toplarindan fuze yagmuru; her fuzenin kilidi seni izler, patlamadan 0,7 sn once yerine sabitlenir.
    // Fuzeler arka arkaya duser: durursan hepsini yersin, surekli kayarsan hicbiri tutmaz.
    private void StartMissileRain()
    {
        Vector2 p = PlayerFrame();
        int n = 3 + phase;
        for (int i = 0; i < n; i++)
            AddZone(p, 18f, 1.4f + i * 0.45f, 1, 220f, 0.7f);
        nextAttack = Mathf.Max(nextAttack, zoneBusyUntil + 0.5f);
        mgr.Announce("FÜZE KİLİDİ: durma, sürekli kay!", new Color(1f, 0.6f, 0.3f), 2f);
        Sfx.PlayAt(Sfx.Lock, pos, 0.9f);
        Sfx.PlayAt(Sfx.MissileLaunch, pos, 0.7f);
    }

    // Yengec: kiskac once iki yana kapanir (ortada kal), sonra ortaya (yana kac)
    private void StartPincer()
    {
        Vector2 p = PlayerFrame();
        const float side = 62f, r = 34f;
        AddZone(p + new Vector2(-side, 0f), r, 1.2f, 3);
        AddZone(p + new Vector2(side, 0f), r, 1.2f, 3);
        AddZone(p, r, phase >= 1 ? 1.9f : 2.2f, 3);
        nextAttack = Mathf.Max(nextAttack, zoneBusyUntil + 0.5f);
        mgr.Announce("KISKAÇ: önce yanlar, sonra orta kapanır; yukarı ya da aşağı kaç!", new Color(0.5f, 0.9f, 1f), 2f);
        Sfx.PlayAt(Sfx.Warning, pos, 0.7f);
    }

    // ------------------------------------------------------------------ dongu

    // FixedUpdate: kovalama, patlama ve vurus
    private void TickZones(float dt)
    {
        if (zones.Count == 0) return;
        if (!mgr.RushActive) { ClearZones(); return; }

        Vector2 pf = PlayerFrame();
        bool hitThisStep = false;
        for (int i = zones.Count - 1; i >= 0; i--)
        {
            Zone z = zones[i];
            if (!z.fired && z.chaseSpeed > 0f && Time.time < z.lockAt)
                z.center = Vector2.MoveTowards(z.center, pf, z.chaseSpeed * dt);

            if (!z.fired && Time.time >= z.fireAt)
            {
                z.fired = true;
                Vector3 world = KiWaveWorld(z.center);
                if (z.style == 0)
                {
                    mgr.SpawnHitSpark(world, 3f);
                    Sfx.PlayAt(Sfx.Zap, world, 0.6f);
                }
                else
                {
                    mgr.SpawnExplosion(world, z.style == 2 ? 2.2f : 1.8f);
                }
                if (!hitThisStep && (pf - z.center).magnitude < z.radius - 3f)
                {
                    hitThisStep = true;   // ust uste binen bolgeler tek vurus
                    PlayerHealth ph = mgr.CurrentPlayerHealth;
                    if (ph != null) ph.TakeHit(1, player.position);
                    FollowCamera.AddShake(0.8f, 0.4f);
                }
            }

            if (z.fired && Time.time >= z.fireAt + ZoneLinger) zones.RemoveAt(i);
            else zones[i] = z;
        }
    }

    // Update: halkalar (dolan ic halka gerisayim), patlama aninda isik sutunu / alev
    private void DrawZones()
    {
        int used = 0;
        Vector3 right = Vector3.Cross(Vector3.up, mgr.RushHeading).normalized;
        for (int i = 0; i < zones.Count; i++)
        {
            Zone z = zones[i];
            if (used >= zoneRings.Count) { zoneRings.Add(MakeZoneLine("ZoneRing", true)); zoneFills.Add(MakeZoneLine("ZoneFill", true)); }
            LineRenderer ring = zoneRings[used], fill = zoneFills[used];
            used++;

            Vector3 c = OverlayPoint(z.center, ZoneAhead);
            Color col = ZoneColor(z.style);
            float u = Mathf.Clamp01((Time.time - z.born) / Mathf.Max(z.fireAt - z.born, 0.01f));

            if (!z.fired)
            {
                float blink = u > 0.7f ? 0.5f + 0.5f * Mathf.Abs(Mathf.Sin(Time.time * 26f)) : 0.85f;
                SetCircle(ring, c, right, z.radius, 0.9f + 0.6f * u, new Color(col.r, col.g, col.b, blink));
                SetCircle(fill, c, right, z.radius * u, 1.4f, new Color(col.r, col.g, col.b, 0.5f));
            }
            else
            {
                // Patlama: isikta dikey sutun, digerlerinde genisleyen parlak halka
                float k = Mathf.Clamp01((Time.time - z.fireAt) / ZoneLinger);
                if (z.style == 0)
                {
                    fill.loop = false;
                    fill.positionCount = 2;
                    fill.SetPosition(0, c - Vector3.up * 400f);
                    fill.SetPosition(1, c + Vector3.up * 400f);
                    fill.startWidth = fill.endWidth = z.radius * 2f * (1f - k);
                    SetLineColor(fill, new Color(1f, 0.97f, 0.75f, 0.75f * (1f - k)));
                    fill.enabled = true;
                    ring.enabled = false;
                }
                else
                {
                    SetCircle(ring, c, right, z.radius * (1f + 0.6f * k), 3f * (1f - k), new Color(col.r, col.g, col.b, 1f - k));
                    fill.enabled = false;
                }
            }
        }
        for (int i = used; i < zoneRings.Count; i++)
        {
            if (zoneRings[i] != null) zoneRings[i].enabled = false;
            if (zoneFills[i] != null) zoneFills[i].enabled = false;
        }
    }

    private void SetCircle(LineRenderer lr, Vector3 center, Vector3 right, float radius, float width, Color color)
    {
        lr.loop = true;
        if (lr.positionCount != ZoneRingPoints) lr.positionCount = ZoneRingPoints;
        for (int k = 0; k < ZoneRingPoints; k++)
        {
            float a = Mathf.PI * 2f * k / ZoneRingPoints;
            lr.SetPosition(k, center + (right * Mathf.Cos(a) + Vector3.up * Mathf.Sin(a)) * Mathf.Max(radius, 0.1f));
        }
        lr.startWidth = lr.endWidth = width;
        SetLineColor(lr, color);
        lr.enabled = radius > 0.5f;
    }

    private LineRenderer MakeZoneLine(string name, bool loop)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.loop = loop;
        lr.positionCount = ZoneRingPoints;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        var m = new Material(mgr.fxMaterial != null ? mgr.fxMaterial : mgr.bossGlow);
        m.mainTexture = Texture2D.whiteTexture;
        lr.sharedMaterial = m;
        lr.enabled = false;
        return lr;
    }

    /// <summary>
    /// HUD: oyuncu henuz patlamamis bir bolgenin icindeyse en kisa cikis noktasi (dunyada, oyuncunun duzleminde) ve
    /// o bolgenin kalan suresi. Cikis noktasi baska bir bolgenin icindeyse diger yonler denenir.
    /// </summary>
    public bool ZoneDanger(out Vector3 exit, out float timeLeft)
    {
        exit = Vector3.zero;
        timeLeft = 0f;
        if (zones.Count == 0 || !mgr.RushActive) return false;
        Vector2 p = PlayerFrame();
        int worst = -1;
        float soonest = float.MaxValue;
        for (int i = 0; i < zones.Count; i++)
        {
            Zone z = zones[i];
            if (z.fired || (p - z.center).magnitude >= z.radius) continue;
            if (z.fireAt < soonest) { soonest = z.fireAt; worst = i; }
        }
        if (worst < 0) return false;

        Zone w = zones[worst];
        Vector2 best = p;
        float bestCost = float.MaxValue;
        for (int k = 0; k < 16; k++)
        {
            float a = Mathf.PI * 2f * k / 16f;
            Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            Vector2 cand = w.center + dir * (w.radius + 10f);
            if (Mathf.Abs(cand.x) > CombatManager.RushHalfWidth - 6f || cand.y > CombatManager.RushUp - 6f || cand.y < -CombatManager.RushDown + 6f) continue;
            float cost = (cand - p).magnitude;
            for (int j = 0; j < zones.Count; j++)
            {
                Zone o = zones[j];
                if (j == worst || o.fired) continue;
                // Yakinda patlayacak baska bir bolgenin icine kacma
                if ((cand - o.center).magnitude < o.radius + 4f && o.fireAt < w.fireAt + 1f) cost += 500f;
            }
            if (cost < bestCost) { bestCost = cost; best = cand; }
        }
        exit = KiWaveWorld(best);
        timeLeft = w.fireAt - Time.time;
        return true;
    }
}
