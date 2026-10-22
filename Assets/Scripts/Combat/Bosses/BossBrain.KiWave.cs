using UnityEngine;

// BossBrain: Ki Savascisi'nin imza saldirisi "ki dalgasi" (yalniz boss fazinda).
// Oyuncunun bulundugu yerde, kendi duzleminde kirmizi bir hedef cemberi belirir; boss ellerinde ki toplarken cember
// oyuncuya dogru suzulur (kararli kaymak gerekir), ates etmeden hemen once durur. Sonra cember genisliginde dev bir isin
// ateslenir: cemberin icinde kalan hasar alir. Cember kameranin hemen onundeki duzlemde cogu zaman ekrana sigmaz: HUD'da
// kirmizi "KAC" oku en yakin cikisi gosterir. Kopan her kol dalgayi kucultur; son fazda iki dalga art arda gelir.
public partial class BossBrain
{
    private const float KiWaveRadius = 42f;         // iki kol saglamken cember (isin) yaricapi (m)
    private static float KiWaveCharge => 2.0f * Difficulty.BossWarningMul;        // toplama suresi (sn)
    private static float KiWaveFreeze => 0.8f * Difficulty.BossWarningMul;        // ates etmeden bu kadar once cember durur (cikis ~40 m)
    private const float KiWaveFire = 0.9f;          // isin suresi (sn)
    private const float KiWaveCreep = 26f;          // toplarken cemberin oyuncuya kayma hizi (m/sn; her faz +8)
    private const float KiWaveTick = 0.3f;          // isinda kalinirsa vurus araligi (sn)
    private const float KiWaveMargin = 3f;          // oyuncunun govdesi: merkezi bu kadar icerdeyse vurulur
    private const int KiRingSegments = 48;

    private bool kiWaveActive;
    private bool kiWaveFired;
    private float kiWaveStart;
    private int kiWavesLeft;
    private Vector2 kiWaveCenter;                   // cemberin merkezi (cercevede: sag, yukari; RushOrigin'e gore)
    private float kiWaveNextHit;
    private LineRenderer kiWaveRing, kiWaveBeam;
    private Transform kiWaveOrb;
    private Transform kiWaveTube;                   // atesteki isin: ellerden cembere genisleyen saydam huni (ag)
    private Mesh kiWaveTubeMesh;
    private Material kiWaveTubeMat;
    private const int KiTubeSides = 28;
    private static readonly Vector3[] kiTubeVerts = new Vector3[KiTubeSides * 2];

    /// <summary>HUD: ki dalgasi ates ediyor mu (oyuncu icerdeyse ekran parlar).</summary>
    public bool KiWaveFiring => kiWaveActive && kiWaveFired;

    private bool KiWaveBusy => kiWaveActive;

    // Kollar koptukca dalga kuculur (uzuv kirmanin odulu)
    private float KiWaveSize => kiHandL != null && kiHandR != null ? KiWaveRadius
        : (kiHandL != null || kiHandR != null ? KiWaveRadius * 0.85f : KiWaveRadius * 0.68f);

    private Vector3 KiWaveOrigin
    {
        get
        {
            if (kiHandL != null && kiHandR != null) return (kiHandL.position + kiHandR.position) * 0.5f;
            Transform h = Pick(kiHandR, kiHandL);
            return h != null ? h.position : transform.position;
        }
    }

    /// <summary>HUD: ki dalgasi cemberi (dunyada merkez, yaricap) ve ates etmesine kalan sure; yalniz isin bitene kadar.</summary>
    public bool KiWaveDanger(out Vector3 center, out float radius, out float timeLeft)
    {
        center = Vector3.zero;
        radius = KiWaveSize;
        timeLeft = 0f;
        if (!kiWaveActive || mgr == null || !mgr.RushActive || enemy == null || enemy.IsDead) return false;
        center = KiWaveWorld(kiWaveCenter);
        timeLeft = KiWaveCharge - (Time.time - kiWaveStart);
        return true;
    }

    /// <summary>
    /// HUD: oyuncu cemberin icindeyse en kisa yoldaki cikis noktasi (dunyada). Kayma alaninin kenarlari hesaba katilir:
    /// kenara sikismis oyuncuya (merkezden uzaga = duvara degil) kenar boyunca yukari/asagi ya da yana cikis gosterilir.
    /// </summary>
    public bool KiWaveExit(out Vector3 exit)
    {
        exit = Vector3.zero;
        if (!kiWaveActive || mgr == null || !mgr.RushActive || player == null) return false;
        Vector2 p = PlayerFrame();
        float r = KiWaveSize;
        if ((p - kiWaveCenter).magnitude >= r) return false;

        const float edge = 8f;
        float minX = -CombatManager.RushHalfWidth + edge, maxX = CombatManager.RushHalfWidth - edge;
        float minY = -CombatManager.RushDown + edge, maxY = CombatManager.RushUp - edge;
        float best = float.MaxValue;
        Vector2 bestPoint = kiWaveCenter + Vector2.right * (r + 10f);
        for (int i = 0; i < 24; i++)
        {
            float a = i * Mathf.PI * 2f / 24f;
            Vector2 e = kiWaveCenter + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (r + 10f);
            if (e.x < minX || e.x > maxX || e.y < minY || e.y > maxY) continue;
            float cost = (e - p).sqrMagnitude;
            if (cost < best) { best = cost; bestPoint = e; }
        }
        exit = KiWaveWorld(bestPoint);
        return true;
    }

    private void StartKiWave(int waves)
    {
        kiWavesLeft = Mathf.Max(1, waves);
        BeginKiWave();
        nextAttack = Mathf.Max(nextAttack, Time.time + (KiWaveCharge + KiWaveFire) * kiWavesLeft + 0.8f);
        if (!kiWaveAnnounced)
        {
            kiWaveAnnounced = true;
            mgr.AnnounceNow("Kİ DALGASI: kırmızı çemberden çık!", new Color(1f, 0.45f, 0.35f), 2.2f);
        }
    }

    private bool kiWaveAnnounced;

    private void BeginKiWave()
    {
        kiWaveActive = true;
        kiWaveFired = false;
        kiWaveStart = Time.time;
        kiWaveNextHit = 0f;
        kiWaveCenter = PlayerFrame();
        Sfx.PlayAt(Sfx.BeamCharge, transform.position, 1f);
        Sfx.PlayAt(Sfx.Warning, player.position, 0.6f);
    }

    private Vector2 PlayerFrame()
    {
        Vector3 right = Vector3.Cross(Vector3.up, mgr.RushHeading).normalized;
        Vector3 rel = player.position - mgr.RushOrigin;
        return new Vector2(Vector3.Dot(rel, right), rel.y);
    }

    private Vector3 KiWaveWorld(Vector2 frame)
    {
        Vector3 right = Vector3.Cross(Vector3.up, mgr.RushHeading).normalized;
        return mgr.RushOrigin + right * frame.x + Vector3.up * frame.y;
    }

    // FixedUpdate: cemberin kaymasi, ates ve vurus
    private void TickKiWave(float dt)
    {
        if (!kiWaveActive) return;
        if (!mgr.RushActive || enemy == null || enemy.IsDead) { EndKiWave(); return; }

        float t = Time.time - kiWaveStart;
        if (t < KiWaveCharge - KiWaveFreeze)
        {
            kiWaveCenter = Vector2.MoveTowards(kiWaveCenter, PlayerFrame(), (KiWaveCreep + phase * 8f) * dt);
        }
        else if (t >= KiWaveCharge && t < KiWaveCharge + KiWaveFire)
        {
            if (!kiWaveFired)
            {
                kiWaveFired = true;
                FollowCamera.AddShake(0.6f, 0.5f);
                Sfx.PlayAt(Sfx.BossAttack, transform.position, 1f);
                Sfx.PlayAt(Sfx.Rail, player.position, 0.7f);
            }
            if (Time.time >= kiWaveNextHit && (PlayerFrame() - kiWaveCenter).magnitude < KiWaveSize - KiWaveMargin)
            {
                kiWaveNextHit = float.MaxValue;   // dalga basina tek vurus (eskiden 0,3 sn'de bir, 3'e kadar)
                PlayerHealth ph = mgr.CurrentPlayerHealth;
                if (ph != null) ph.TakeHit(1, player.position);
            }
        }
        else if (t >= KiWaveCharge + KiWaveFire)
        {
            if (--kiWavesLeft > 0) BeginKiWave();
            else EndKiWave();
        }
    }

    private void EndKiWave()
    {
        kiWaveActive = false;
        if (kiWaveRing != null) kiWaveRing.enabled = false;
        if (kiWaveBeam != null) kiWaveBeam.enabled = false;
        if (kiWaveOrb != null) kiWaveOrb.gameObject.SetActive(false);
        if (kiWaveTube != null) kiWaveTube.gameObject.SetActive(false);
    }

    // Update: cember, ellerdeki ki topu ve isin
    private void DrawKiWave()
    {
        if (!kiWaveActive) return;
        if (!mgr.RushActive || enemy == null || enemy.IsDead) { EndKiWave(); return; }
        EnsureKiWaveVisuals();

        float t = Time.time - kiWaveStart;
        float size = KiWaveSize;
        Vector3 center = KiWaveWorld(kiWaveCenter);
        Vector3 right = Vector3.Cross(Vector3.up, mgr.RushHeading).normalized;
        Vector3 origin = KiWaveOrigin;
        bool firing = t >= KiWaveCharge;

        // Cember: toplarken atesli kirmizi, ates yaklastikca hizli yanip soner; isin sirasinda gizli
        kiWaveRing.enabled = !firing;
        if (!firing)
        {
            float urgency = Mathf.Clamp01(t / KiWaveCharge);
            float blink = 0.6f + 0.4f * Mathf.Sin(Time.time * Mathf.Lerp(8f, 30f, urgency));
            SetLineColor(kiWaveRing, new Color(1f, 0.2f, 0.15f, blink));
            kiWaveRing.startWidth = kiWaveRing.endWidth = Mathf.Lerp(1.2f, 2.6f, urgency);
            for (int i = 0; i < KiRingSegments; i++)
            {
                float a = i * Mathf.PI * 2f / KiRingSegments;
                kiWaveRing.SetPosition(i, center + (right * Mathf.Cos(a) + Vector3.up * Mathf.Sin(a)) * size);
            }
        }

        // Ki topu: ellerde buyur, atesle birlikte soner
        float orb = firing ? Mathf.Lerp(1f, 0f, (t - KiWaveCharge) / KiWaveFire) : Mathf.Clamp01(t / KiWaveCharge);
        kiWaveOrb.gameObject.SetActive(orb > 0.02f);
        kiWaveOrb.position = origin;
        float parentScale = Mathf.Max(0.01f, transform.lossyScale.x);   // boss govdesi olcekli olabilir: dunyada metre
        kiWaveOrb.localScale = Vector3.one * (4f + 14f * orb) * (1f + 0.08f * Mathf.Sin(Time.time * 25f)) / parentScale;

        // Toplarken ellerden cemberin merkezine ince sari nisan cizgisi; atesle parlak beyaz cekirdek
        kiWaveBeam.enabled = true;
        kiWaveBeam.SetPosition(0, origin);
        kiWaveBeam.SetPosition(1, center);
        float fade = firing ? 1f - Mathf.Clamp01((t - KiWaveCharge) / KiWaveFire) : 0f;
        kiWaveBeam.startWidth = firing ? 5f + 5f * fade : 0.6f;
        kiWaveBeam.endWidth = firing ? 8f + 8f * fade : 0.6f;
        SetLineColor(kiWaveBeam, firing ? new Color(1f, 1f, 1f, 0.9f * fade + 0.1f) : new Color(1f, 0.85f, 0.3f, 0.5f));

        // Isin: ellerden cembere genisleyen saydam huni; oyuncunun duzleminde biter (icerdeysen tunel gibi gorunur)
        kiWaveTube.gameObject.SetActive(firing);
        if (firing)
        {
            Vector3 axis = center - origin;
            float len = axis.magnitude;
            if (len > 1f)
            {
                kiWaveTube.SetPositionAndRotation(origin, Quaternion.LookRotation(axis / len, Vector3.up));
                float r0 = size * 0.3f, wobble = 0.92f + 0.08f * Mathf.Sin(Time.time * 40f);
                for (int i = 0; i < KiTubeSides; i++)
                {
                    float a = i * Mathf.PI * 2f / KiTubeSides;
                    Vector2 ring = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                    kiTubeVerts[i] = new Vector3(ring.x * r0, ring.y * r0, 0f);
                    kiTubeVerts[i + KiTubeSides] = new Vector3(ring.x * size * wobble, ring.y * size * wobble, len);
                }
                kiWaveTubeMesh.vertices = kiTubeVerts;
                kiWaveTubeMesh.RecalculateBounds();
            }
            // Gokle karismasin: doygun elektrik mavisi
            Color tc = new Color(0.25f, 0.68f, 1f, 0.38f + 0.42f * fade);
            kiWaveTubeMat.color = tc;
            if (kiWaveTubeMat.HasProperty("_BaseColor")) kiWaveTubeMat.SetColor("_BaseColor", tc);
        }
    }

    private void EnsureKiWaveVisuals()
    {
        if (kiWaveRing == null)
        {
            kiWaveRing = MakeWarnLine();
            kiWaveRing.name = "KiWaveRing";
            kiWaveRing.loop = true;
            kiWaveRing.positionCount = KiRingSegments;
        }
        if (kiWaveBeam == null)
        {
            // Saydam malzeme (Sprites/Default): dev sutun arkasindaki boss ve gok gorunmeye devam etsin
            kiWaveBeam = MakeWarnLine();
            kiWaveBeam.name = "KiWaveBeam";
            kiWaveBeam.numCapVertices = 6;
            if (mgr.fxMaterial != null)
            {
                Destroy(kiWaveBeam.sharedMaterial);
                kiWaveBeam.sharedMaterial = new Material(mgr.fxMaterial);
            }
        }
        if (kiWaveTube == null)
        {
            // Huni: iki halka arasi seritler (uclari acik); Sprites/Default iki yuzu de cizer
            var go = new GameObject("KiWaveTube");
            kiWaveTube = go.transform;   // boss'a bagli degil: olcekten etkilenmesin (OnDestroy'da silinir)
            kiWaveTubeMesh = new Mesh { name = "KiWaveTube" };
            kiWaveTubeMesh.vertices = kiTubeVerts;
            var tris = new int[KiTubeSides * 6];
            for (int i = 0; i < KiTubeSides; i++)
            {
                int j = (i + 1) % KiTubeSides;
                int k = i * 6;
                tris[k] = i; tris[k + 1] = i + KiTubeSides; tris[k + 2] = j;
                tris[k + 3] = j; tris[k + 4] = i + KiTubeSides; tris[k + 5] = j + KiTubeSides;
            }
            kiWaveTubeMesh.triangles = tris;
            go.AddComponent<MeshFilter>().sharedMesh = kiWaveTubeMesh;
            var mr = go.AddComponent<MeshRenderer>();
            // Duz beyaz doku: agin UV'si yok, efekt malzemesinin yumusak parcacik dokusu koselerde saydamdir
            kiWaveTubeMat = new Material(mgr.fxMaterial != null ? mgr.fxMaterial : mgr.bossGlow) { mainTexture = Texture2D.whiteTexture };
            mr.sharedMaterial = kiWaveTubeMat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            go.SetActive(false);
        }
        if (kiWaveOrb == null)
        {
            var go = new GameObject("KiWaveOrb");
            go.transform.SetParent(transform, false);
            var tmp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.AddComponent<MeshFilter>().sharedMesh = tmp.GetComponent<MeshFilter>().sharedMesh;
            DestroyImmediate(tmp);   // carpistirici bir kare bile kalmasin
            var r = go.AddComponent<MeshRenderer>();
            Material m = new Material(mgr.bossGlow != null ? mgr.bossGlow : mgr.fxMaterial);
            Color c = new Color(0.6f, 0.95f, 1f);
            m.color = c;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            r.sharedMaterial = m;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            kiWaveOrb = go.transform;
        }
    }

    private static void SetLineColor(LineRenderer lr, Color c)
    {
        if (lr == null) return;
        Material m = lr.sharedMaterial;
        if (m == null) return;
        m.color = c;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
    }
}
