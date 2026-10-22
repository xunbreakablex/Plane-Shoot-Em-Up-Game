using System.Collections.Generic;
using UnityEngine;

// BossBrain: uzuvlar.
//  Silah uzuvlari (top, kol, kule, dokunac, degnek, cene): kendi canlari vardir; bitince patlayarak kopar ve o uzvun
//  silahi susar (tur kodu namlu referansini birakir, saldiri kalan namlulardan ya da govdeden atilir).
//  Tasiyici uzuvlar (kanat, yuzgec, motor, kuyruk): kopmaz. Hasar aldikca kucuk parcalar doker; cani bitince yanar,
//  boss agirlasir (yavaslar).
//  Uzva gelen hasar bossun toplam canindan da duser: uzuv avlamak hep ilerlemedir. Asama sonu bosslarinda silah
//  uzuvlarinin kopmasi faz atlatir (LimbPhase).
//  Govde korumasi: gerekli silah uzuvlari (2 ya da daha azsa hepsi, coksa yarisi) kopana kadar govde ve kanatlar hasar
//  almaz; mermi seker, govde camgobegi parlar, HUD uyarir. Alan hasari (fuze, bomba, plazma, yildirim) hedef silah
//  uzvuna aktarilir; seken merminin de %30'u ona islenir (zarfin arkasindaki kule, govdenin arkasindaki degnek gibi
//  vurulamayan uzuvda kilitlenme olmaz). Hedef uzuv: gorus hatti acik olanlardan en cok hasar almisi.
public partial class BossBrain
{
    private enum LimbKind { Weapon, Wing }

    private class Limb
    {
        public string label;
        public LimbKind kind;
        public readonly List<Transform> parts = new List<Transform>();
        public readonly List<Renderer> rends = new List<Renderer>();
        public int maxHp, hp;
        public float lastHit = -10f;
        public bool broken;
        public bool visible = true;         // oyuncudan gorus hatti acik (gizli uzuv hedef secilmez)
        public int shed;                    // kanat: dokulen parca kademesi (her %25'te bir)
        public System.Action onBreak;       // tur kodu: namlu / animasyon referanslarini birakir
    }

    private readonly List<Limb> limbs = new List<Limb>();
    private readonly Dictionary<Collider, Limb> limbByCollider = new Dictionary<Collider, Limb>();
    private int weaponLimbs, brokenWeapons;
    private float wingSpeedMul = 1f;

    private const float LimbBarShowTime = 1.6f;   // vurulduktan sonra can cubugunun gorundugu sure (sn)
    private const float LimbLowFraction = 0.3f;   // bu oranin altindaki uzvun cubugu hep acik

    // Asama sonu bosslarinda kopan silah uzuvlari faz atlatir: iki uzuvluda 1 kopunca faz 1, ikisi kopunca faz 2;
    // cok uzuvluda (ahtapot, zeplin) uzuvlarin ucte biri / ucte ikisi kopunca.
    private int LimbPhase => enemy != null && enemy.IsStageBoss && weaponLimbs > 0
        ? Mathf.Min(2, brokenWeapons * 3 / (weaponLimbs + 1))
        : 0;

    // ------------------------------------------------------- govde korumasi

    private static readonly Color ShieldTint = new Color(0.35f, 0.95f, 1f);
    private int RequiredLimbs => weaponLimbs <= 2 ? weaponLimbs : Mathf.CeilToInt(weaponLimbs * 0.5f);
    private bool limbGuardOff;          // uzuv kopunca govdeye verilen pay korumaya takilmasin
    private float deflectUntil, lastDeflectFx;
    private const float DeflectShare = 0.3f;   // seken mermi hasarinin hedef uzva islenen payi
    private float deflectCarry;
    private float visCheckAt;
    private readonly HashSet<Renderer> limbRenderers = new HashSet<Renderer>();

    /// <summary>Gerekli silah uzuvlari kopmadi: govde hasar almaz.</summary>
    public bool BodyProtected => !dying && !limbGuardOff && enemy != null && !enemy.IsDead
                                 && weaponLimbs > 0 && brokenWeapons < RequiredLimbs;

    /// <summary>Korumanin kalkmasi icin daha kac silah uzvu kopmali.</summary>
    public int GuardLimbsLeft => Mathf.Max(0, RequiredLimbs - brokenWeapons);

    /// <summary>Mermi korunan govdeden / kanattan sekti: kivilcim, ses, govde parlamasi; hasarin bir payi hedef uzva.</summary>
    public void Deflect(Vector3 point, int damage)
    {
        deflectUntil = Time.time + 0.2f;
        deflectCarry += damage * DeflectShare;
        int share = Mathf.FloorToInt(deflectCarry);
        if (share > 0)
        {
            deflectCarry -= share;
            Limb l = GuardTarget();
            if (l != null)
            {
                Vector3 c = LimbCenter(l, point);
                l.hp -= share;
                l.lastHit = Time.time;
                if (l.hp <= 0) BreakLimb(l, c);
                limbGuardOff = true;   // uzva islenen hasar bossun toplam canindan da duser (uzuv vurusu gibi)
                if (!enemy.IsDead) enemy.TakeHit(share, c);
                limbGuardOff = false;
            }
        }

        if (Time.time - lastDeflectFx < 0.08f) return;
        lastDeflectFx = Time.time;
        Vector3 n = point - transform.position;
        mgr.SpawnShieldHit(point, n.sqrMagnitude > 1e-4f ? n.normalized : Vector3.up, transform);
    }

    /// <summary>Korunurken alan hasari: en cok hasar almis saglam silah uzvuna aktarilir (uzuv cani duser).</summary>
    public bool RedirectToLimb(int damage, Vector3 point)
    {
        Limb l = GuardTarget();
        if (l == null) return false;
        l.hp -= damage;
        l.lastHit = Time.time;
        if (l.hp <= 0) BreakLimb(l, LimbCenter(l, point));
        return true;
    }

    /// <summary>Nisan noktasi: korunurken hedef silah uzvunun merkezi, degilse boss merkezi.</summary>
    public Vector3 GuardAimPoint()
    {
        if (!BodyProtected) return transform.position;
        Limb l = GuardTarget();
        return l != null ? LimbCenter(l, transform.position) : transform.position;
    }

    // Saglam silah uzuvlarindan gorus hatti acik olanlarin en cok hasar almisi (hic gorunen yoksa hepsinden):
    // ates ve aktarilan hasar tek uzva toplanir. Eskiden esitlikte listedeki ilk uzuv seciliyordu: zeplinin zarf
    // arkasindaki kulesine nisan alinip sonsuza dek zarf vuruluyordu.
    private Limb GuardTarget()
    {
        if (Time.time >= visCheckAt)
        {
            visCheckAt = Time.time + 0.4f;
            foreach (Limb l in limbs)
                if (!l.broken && l.kind == LimbKind.Weapon) l.visible = LineOfSight(l);
        }

        Limb best = null;
        float bestScore = float.MaxValue;
        foreach (Limb l in limbs)
        {
            if (l.broken || l.kind != LimbKind.Weapon) continue;
            float score = (float)l.hp / Mathf.Max(1, l.maxHp) + (l.visible ? 0f : 10f);
            if (score < bestScore - 0.001f) { bestScore = score; best = l; }
        }
        return best;
    }

    // Oyuncudan uzvun merkezine isin: ilk carptigi boss parcasi bu uzva aitse gorunur
    private bool LineOfSight(Limb l)
    {
        Transform pt = mgr != null ? mgr.PlayerTransform : null;
        if (pt == null) return true;
        Vector3 c = LimbCenter(l, transform.position);
        Vector3 d = c - pt.position;
        float dist = d.magnitude;
        if (dist < 1f) return true;
        // Oyuncunun kendi parcalarindan kacinmak icin 4 m onden; bossa ait olmayan engel (bulut, baska ucak) sayilmaz
        Vector3 dir = d / dist;
        var hits = Physics.RaycastAll(pt.position + dir * 4f, dir, dist + 5f, ~0, QueryTriggerInteraction.Ignore);
        float nearest = float.MaxValue;
        Collider first = null;
        foreach (var h in hits)
            if (h.collider.transform.IsChildOf(transform) && h.distance < nearest) { nearest = h.distance; first = h.collider; }
        if (first == null) return true;
        return limbByCollider.TryGetValue(first, out Limb owner) && owner == l;
    }

    private bool IsLimbRenderer(Renderer r) => limbRenderers.Contains(r);

    // -------------------------------------------------------------- kurulum

    private void InitLimbs()
    {
        switch (type)
        {
            case BossType.Jet:
                Wing("SOL KANAT", 0.10f, "WingL", "StripeL");
                Wing("SAĞ KANAT", 0.10f, "WingR", "StripeR");
                Weapon("SOL TOP", 0.09f, () => jetPodL = null, "PodL");
                Weapon("SAĞ TOP", 0.09f, () => jetPodR = null, "PodR");
                break;

            case BossType.Shark:
                Wing("SOL YÜZGEÇ", 0.09f, "FinL");
                Wing("SAĞ YÜZGEÇ", 0.09f, "FinR");
                Wing("KUYRUK", 0.09f, "Tail");
                Weapon("ÇENE", 0.10f, () => { sharkJaw = null; sharkJawBroken = true; }, "Jaw");
                break;

            case BossType.Crab:
                Weapon("SOL KISKAÇ", 0.10f, () => { crabArmL = crabTopL = crabBotL = crabMuzzleL = null; }, "ArmL");
                Weapon("SAĞ KISKAÇ", 0.10f, () => { crabArmR = crabTopR = crabBotR = crabMuzzleR = null; }, "ArmR");
                break;

            case BossType.Octopus:
                for (int t = 0; t < BossBuilder.OctoTentacles; t++)
                {
                    int tt = t;
                    var names = new string[BossBuilder.OctoSegments];
                    for (int k = 0; k < names.Length; k++) names[k] = $"T{t}_{k}";
                    Weapon("DOKUNAÇ " + (t + 1), 0.035f, () =>
                    {
                        for (int k = 0; k < BossBuilder.OctoSegments; k++) octoSeg[tt, k] = null;
                    }, names);
                }
                break;

            case BossType.Zeppelin:
                Wing("SOL MOTOR", 0.08f, "EngineL", "PropL");
                Wing("SAĞ MOTOR", 0.08f, "EngineR", "PropR");
                for (int i = 0; i < BossBuilder.ZepTurrets; i++)
                {
                    int ii = i;
                    Weapon("KULE " + (i + 1), 0.06f, () => { zepTurret[ii] = null; zepMuzzle[ii] = null; }, "Turret" + i);
                }
                break;

            case BossType.Demon:
                Wing("SOL KANAT", 0.10f, "WingL");
                Wing("SAĞ KANAT", 0.10f, "WingR");
                Weapon("SOL KOL", 0.09f, () => { demonOrbL = null; demonMuzzleL = null; }, "ArmL");
                Weapon("SAĞ KOL", 0.09f, () => { demonOrbR = null; demonMuzzleR = null; }, "ArmR");
                break;

            case BossType.Angel:
                Wing("SOL KANAT", 0.10f, "WingL");
                Wing("SAĞ KANAT", 0.10f, "WingR");
                Weapon("MIZRAK", 0.09f, () => angelStaff = null, "Spear", "SpearTip");
                Weapon("HALE", 0.07f, () => { angelHalo = null; angelHaloMuzzle = null; }, "Halo");
                break;

            case BossType.KiWarrior:
                Weapon("SOL KOL", 0.09f, () => { kiHandL = null; kiFistL = null; }, "ArmL");
                Weapon("SAĞ KOL", 0.09f, () => { kiHandR = null; kiFistR = null; }, "ArmR");
                break;

            case BossType.MagicalGirl:
                Wing("SOL KANAT", 0.08f, "WingL");
                Wing("SAĞ KANAT", 0.08f, "WingR");
                Weapon("DEĞNEK", 0.12f, () => { starPivot = null; starMuzzle = null; }, "WandStick", "Star");
                break;

            case BossType.Eagle:
                Wing("SOL KANAT", 0.10f, "WingL");
                Wing("SAĞ KANAT", 0.10f, "WingR");
                Weapon("SOL TOP", 0.08f, () => eagleMuzzleL = null, "WingPodL");
                Weapon("SAĞ TOP", 0.08f, () => eagleMuzzleR = null, "WingPodR");
                break;

            case BossType.Golem:
                Weapon("SOL YUMRUK", 0.09f, () => { golemFistL = null; golemMuzzleL = null; }, "FistL");
                Weapon("SAĞ YUMRUK", 0.09f, () => { golemFistR = null; golemMuzzleR = null; }, "FistR");
                break;

            default:   // Ejderha
                Wing("SOL KANAT", 0.10f, "WingL");
                Wing("SAĞ KANAT", 0.10f, "WingR");
                Weapon("ÇENE", 0.10f, () => { dragonJaw = null; dragonJawBroken = true; }, "Jaw");
                break;
        }

        // Ic ice uzuvlar (kanadin ustundeki top gibi): her renderer, collider'inin bagli oldugu uzva sayilir
        limbRenderers.Clear();
        foreach (Limb l in limbs)
        {
            l.rends.Clear();
            foreach (Transform t in l.parts)
            {
                foreach (var r in t.GetComponentsInChildren<Renderer>(true))
                {
                    var c = r.GetComponent<Collider>();
                    if (c == null || (limbByCollider.TryGetValue(c, out Limb owner) && owner == l))
                    {
                        l.rends.Add(r);
                        if (l.kind == LimbKind.Weapon) limbRenderers.Add(r);   // korunurken parlamaz: hedef belli olsun
                    }
                }
            }
        }
    }

    private void Wing(string label, float hpFraction, params string[] partNames)
    {
        AddLimb(label, LimbKind.Wing, hpFraction, null, partNames);
    }

    private void Weapon(string label, float hpFraction, System.Action onBreak, params string[] partNames)
    {
        if (AddLimb(label, LimbKind.Weapon, hpFraction, onBreak, partNames) != null) weaponLimbs++;
    }

    private Limb AddLimb(string label, LimbKind kind, float hpFraction, System.Action onBreak, string[] partNames)
    {
        var limb = new Limb { label = label, kind = kind, onBreak = onBreak };
        foreach (string n in partNames)
        {
            Transform t = Get(n);
            if (t != null) limb.parts.Add(t);
        }
        if (limb.parts.Count == 0) return null;

        limb.maxHp = limb.hp = Mathf.Max(6, Mathf.RoundToInt(enemy.MaxHits * hpFraction));
        // Sonra eklenen uzuv ayni collider'i devralir (kanat once, ustundeki top sonra)
        foreach (Transform t in limb.parts)
            foreach (var c in t.GetComponentsInChildren<Collider>(true))
                limbByCollider[c] = limb;
        limbs.Add(limb);
        return limb;
    }

    // --------------------------------------------------------------- hasar

    /// <summary>
    /// Oyuncu mermisi / isini bir parcayi vurdu: parca bir uzva aitse uzvun cani da duser. Govde korunurken yalniz
    /// silah uzuvlari hasar alir. Hasari bir uzuv aldiysa true (govde korunsa da bossun toplam cani duser).
    /// </summary>
    public bool OnPartHit(Collider c, int damage, Vector3 point)
    {
        if (c == null || enemy == null || enemy.IsDead) return false;
        if (!limbByCollider.TryGetValue(c, out Limb limb) || limb.broken) return false;
        if (limb.kind == LimbKind.Wing && BodyProtected) return false;

        limb.hp -= damage;
        limb.lastHit = Time.time;
        if (limb.kind == LimbKind.Wing) ShedFromWing(limb, point);
        if (limb.hp <= 0) BreakLimb(limb, point);
        return true;
    }

    // Kanat her %25 can kaybinda birkac kucuk parca doker
    private void ShedFromWing(Limb limb, Vector3 point)
    {
        float lost = 1f - Mathf.Clamp01((float)limb.hp / limb.maxHp);
        int stage = Mathf.Min(3, Mathf.FloorToInt(lost * 4f));
        while (limb.shed < stage)
        {
            limb.shed++;
            SpawnDebris(limb, point, 3);
            Sfx.PlayAt(Sfx.Crack, point, 0.55f);
        }
    }

    private void BreakLimb(Limb limb, Vector3 point)
    {
        bool wasGuarded = BodyProtected;
        limb.broken = true;
        limb.hp = 0;
        Vector3 c = LimbCenter(limb, point);

        if (limb.kind == LimbKind.Weapon)
        {
            brokenWeapons++;
            limb.onBreak?.Invoke();
            DetachLimb(limb, c);
            mgr.SpawnExplosion(c, 1.9f);
            mgr.SpawnExplosion(c + Random.onUnitSphere * 3f * transform.lossyScale.x, 1.2f);
            FollowCamera.AddShake(0.9f, 0.5f);
            Sfx.PlayAt(Sfx.Crack, c, 1f);
            mgr.OnBossLimbBroken(enemy, limb.label, true, c);
        }
        else
        {
            // Kanat kopmaz: son parcalarini doker, yanar; boss agirlasir
            SpawnDebris(limb, c, 6);
            if (mgr.SmokeTrailPrefab != null && limb.parts.Count > 0 && limb.parts[0] != null)
            {
                var smoke = Instantiate(mgr.SmokeTrailPrefab, limb.parts[0]);
                smoke.transform.position = c;
                var fire = Instantiate(mgr.SmokeTrailPrefab, limb.parts[0]);
                fire.transform.position = c;
                TintFire(fire);
            }
            wingSpeedMul = Mathf.Max(0.7f, wingSpeedMul - 0.15f);
            mgr.SpawnExplosion(c, 1.4f);
            FollowCamera.AddShake(0.6f, 0.4f);
            Sfx.PlayAt(Sfx.Crack, c, 0.9f);
            mgr.OnBossLimbBroken(enemy, limb.label, false, c);
        }

        // Govde acildi: duyuru (uzuv kopma duyurusunun ardindan)
        if (wasGuarded && !BodyProtected) mgr.OnBossGuardBroken(enemy, c);

        // Uzuv kopunca govde sarsilir: bossun toplam canindan ek bir pay duser (korumaya takilmaz)
        limbGuardOff = true;
        if (!enemy.IsDead) enemy.TakeHit(Mathf.Max(1, Mathf.RoundToInt(enemy.MaxHits * 0.03f)), c);
        limbGuardOff = false;
    }

    // Duman prefabinin turuncu kopyasi (yanan kanat)
    private static void TintFire(GameObject go)
    {
        foreach (var ps in go.GetComponentsInChildren<ParticleSystem>())
        {
            var main = ps.main;
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.55f, 0.15f, 0.9f), new Color(1f, 0.25f, 0.05f, 0.7f));
            main.startSizeMultiplier *= 0.7f;
        }
    }

    // Silah uzvu kopar: parcalari tek bir kutuya toplanip fizikle duser, duman izi birakir
    private void DetachLimb(Limb limb, Vector3 center)
    {
        var holder = new GameObject("KopanUzuv_" + limb.label);
        holder.transform.SetPositionAndRotation(center, transform.rotation);

        foreach (Renderer r in limb.rends)
            if (r != null) r.SetPropertyBlock(null);   // vurus flasinda koptuysa beyaz kalmasin

        foreach (Transform t in limb.parts)
        {
            if (t == null) continue;
            t.SetParent(holder.transform, true);
            foreach (var col in t.GetComponentsInChildren<Collider>(true)) col.enabled = false;
        }

        var prb = holder.AddComponent<Rigidbody>();
        prb.mass = 4f;
        prb.useGravity = true;
        prb.linearDamping = 0.25f;
        prb.angularDamping = 0.2f;
        Vector3 outward = center - transform.position;
        outward = outward.sqrMagnitude > 0.01f ? outward.normalized : Random.onUnitSphere;
        prb.linearVelocity = vel + CombatManager.FrameVelocityAt(center) + outward * 24f + Vector3.up * 8f;
        prb.angularVelocity = Random.onUnitSphere * Random.Range(2.5f, 5f);

        if (mgr.SmokeTrailPrefab != null)
        {
            var smoke = Instantiate(mgr.SmokeTrailPrefab, holder.transform);
            smoke.transform.localPosition = Vector3.zero;
        }
        Destroy(holder, 8f);

        RefreshRenderers();
    }

    // Kopan parcalar boss renderer listesinden cikar (vurus flasi ve olum patlamalari yalniz govdede)
    private void RefreshRenderers()
    {
        renderers = GetComponentsInChildren<Renderer>();
        baseColors = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            Material m = renderers[i].sharedMaterial;
            baseColors[i] = m != null && m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor")
                : (m != null ? m.color : Color.white);
        }
        colliders = GetComponentsInChildren<Collider>();
    }

    // Kucuk metal parcalar: uzvun malzemesiyle, disari ve asagi savrulur
    private void SpawnDebris(Limb limb, Vector3 point, int count)
    {
        Material mat = null;
        foreach (var r in limb.rends) if (r != null) { mat = r.sharedMaterial; break; }
        SpawnDebrisAt(point, mat, count);
    }

    private void SpawnDebrisAt(Vector3 point, Material mat, int count)
    {
        float s = transform.lossyScale.x;
        Vector3 outward = point - transform.position;
        outward = outward.sqrMagnitude > 0.01f ? outward.normalized : Random.onUnitSphere;

        for (int i = 0; i < count; i++)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "BossDebris";
            Destroy(go.GetComponent<Collider>());
            go.transform.SetPositionAndRotation(point + Random.insideUnitSphere * 1.2f * s, Random.rotation);
            go.transform.localScale = new Vector3(Random.Range(0.5f, 1.3f), Random.Range(0.12f, 0.3f), Random.Range(0.4f, 1.0f)) * s;
            var r = go.GetComponent<Renderer>();
            if (mat != null) r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var prb = go.AddComponent<Rigidbody>();
            prb.mass = 0.3f;
            prb.linearDamping = 0.35f;
            prb.linearVelocity = vel + CombatManager.FrameVelocityAt(point)
                                 + (outward + Random.insideUnitSphere * 0.7f) * Random.Range(10f, 24f) + Vector3.up * 5f;
            prb.angularVelocity = Random.onUnitSphere * Random.Range(4f, 11f);
            Destroy(go, 4f);
        }
        mgr.SpawnHitSpark(point, 2.2f);
    }

    // Faz gecisi: buyuk parca koparmak yerine govdeden parca sacilir, patlamalar ve sarsinti
    private void PhaseBurst()
    {
        if (renderers == null || renderers.Length == 0 || mgr == null) return;

        // Mumkunse hasarli bir tasiyici uzuvdan (kanat), yoksa govdenin rastgele bir yerinden
        Limb pick = null;
        foreach (Limb l in limbs)
            if (!l.broken && l.kind == LimbKind.Wing && (pick == null || l.hp < pick.hp)) pick = l;

        Vector3 c;
        Material mat;
        if (pick != null)
        {
            c = LimbCenter(pick, transform.position);
            mat = pick.rends.Count > 0 && pick.rends[0] != null ? pick.rends[0].sharedMaterial : null;
        }
        else
        {
            Renderer r = renderers[Random.Range(0, renderers.Length)];
            c = r != null ? r.bounds.center : transform.position;
            mat = r != null ? r.sharedMaterial : null;
        }

        SpawnDebrisAt(c, mat, 7);
        mgr.SpawnExplosion(c, 1.6f);
        mgr.SpawnExplosion(transform.position + Random.onUnitSphere * RadiusOf(type) * 0.3f, 1.2f);
        FollowCamera.AddShake(1f, 0.6f);
        Sfx.PlayAt(Sfx.Crack, c, 1f);
    }

    private static Vector3 LimbCenter(Limb limb, Vector3 fallback)
    {
        Vector3 sum = Vector3.zero;
        int n = 0;
        foreach (Renderer r in limb.rends)
        {
            if (r == null) continue;
            sum += r.bounds.center;
            n++;
        }
        return n > 0 ? sum / n : fallback;
    }

    // ------------------------------------------------------------------ HUD

    public int LimbCount => limbs.Count;

    /// <summary>
    /// HUD: uzuv can cubugu. Vurulduktan sonra kisa sure, cani %30'un altindaysa surekli gorunur.
    /// Kopan (ya da yanan) uzvun cubugu yoktur.
    /// </summary>
    public bool LimbBar(int i, out Vector3 worldPos, out float fraction, out bool weapon)
    {
        Limb l = limbs[i];
        worldPos = Vector3.zero;
        fraction = 0f;
        weapon = l.kind == LimbKind.Weapon;
        if (l.broken) return false;

        fraction = Mathf.Clamp01((float)l.hp / Mathf.Max(1, l.maxHp));
        // Govde korunurken silah uzuvlarinin cubugu hep acik: neyi vuracagin belli olsun
        bool show = Time.time - l.lastHit < LimbBarShowTime || fraction < LimbLowFraction || (weapon && BodyProtected);
        if (!show) return false;

        worldPos = LimbCenter(l, transform.position);
        return true;
    }

    // Silah uzvu kopunca yerine kullanilacak namlu: verilenlerden saglam ilki, yoksa govde
    private Transform Pick(Transform a, Transform b = null, Transform c = null)
    {
        if (a != null) return a;
        if (b != null) return b;
        if (c != null) return c;
        return transform;
    }
}
