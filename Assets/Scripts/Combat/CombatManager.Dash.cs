using System.Collections.Generic;
using UnityEngine;

// Kahraman kacisi (FARE semasinda Q / E, Arcade / Klasik'te Z / X, gamepad dpad sol/sag): kahraman aninda yana sicrar;
// ucaklarda yok (onlari ucaktan ayiran hareket).
//   Ay Savascisi  "Yildiz Adimi": pembe yildiz tozu, kisa bekleme
//   Ki Kahramani  "Anlik Isinlanma": en uzun sicrama, mavi iz
//   Imparator     "Karanlik Adim": mor duman, en kisa ama uzun dokunulmazlik
// Geride saydam bir "hayalet" kopyasi kalir ve soner; sicrama boyunca kisa dokunulmazlik (mermiden kacis).
public partial class CombatManager
{
    private float nextDash;
    private float dashCooldown = 1f;
    private static bool dashHintShown;

    private struct DashDef
    {
        public float distance, cooldown, invuln;
        public Color color;
        public string name;
    }

    private static DashDef DashOf(int model)
    {
        switch (model - PlaneBuilder.HeroStart)
        {
            case 0: return new DashDef { distance = 40f, cooldown = 1.4f, invuln = 0.35f, color = new Color(1f, 0.5f, 0.85f, 0.55f), name = "YILDIZ ADIMI" };
            case 1: return new DashDef { distance = 55f, cooldown = 1.8f, invuln = 0.3f, color = new Color(0.4f, 0.8f, 1f, 0.55f), name = "IŞINLANMA" };
            default: return new DashDef { distance = 32f, cooldown = 1.6f, invuln = 0.6f, color = new Color(0.55f, 0.2f, 0.9f, 0.6f), name = "KARANLIK ADIM" };
        }
    }

    /// <summary>HUD: kahramanin kacis hazirlik orani (0..1) ve adi; kahraman degilse false.</summary>
    public bool DashStatus(out float ready, out string name)
    {
        ready = 1f;
        name = "";
        int model = flight != null ? flight.CurrentIndex : -1;
        if (!PlaneBuilder.IsHero(model)) return false;
        name = DashOf(model).name;
        ready = Mathf.Clamp01(1f - (nextDash - Time.time) / Mathf.Max(dashCooldown, 0.01f));
        return true;
    }

    private void UpdateDashInput()
    {
        int model = flight != null ? flight.CurrentIndex : -1;
        if (!PlaneBuilder.IsHero(model) || GameState.Paused) return;

        // Ilk kez kahramanla ucarken bir kez hatirlat
        if (!dashHintShown && RunState.Active && Time.timeSinceLevelLoad > 4f)
        {
            dashHintShown = true;
            Announce(DashOf(model).name + ":  " + ControlSettings.DashKeys + " ile yana kaç (kısa dokunulmazlık)", new Color(0.55f, 1f, 0.9f), 4f);
        }

        var kb = UnityEngine.InputSystem.Keyboard.current;
        var pad = UnityEngine.InputSystem.Gamepad.current;
        int dir = 0;
        // Arcade / Klasik'te Q/E yatis / dumen: kacis orada Z / X (eskiden her yatista istemeden sicraniyordu)
        bool mouse = ControlSettings.MouseSteering;
        if ((kb != null && (mouse ? kb.qKey : kb.zKey).wasPressedThisFrame) || (pad != null && pad.dpad.left.wasPressedThisFrame)) dir = -1;
        if ((kb != null && (mouse ? kb.eKey : kb.xKey).wasPressedThisFrame) || (pad != null && pad.dpad.right.wasPressedThisFrame)) dir = 1;
        if (dir != 0) TryDash(dir);
    }

    public bool TryDash(int dir)
    {
        PlaneController plane = flight != null ? flight.Current : null;
        if (plane == null || plane.IsCrashed || !RunState.Active) return false;
        int model = flight.CurrentIndex;
        if (!PlaneBuilder.IsHero(model) || Time.time < nextDash) return false;

        DashDef d = DashOf(model);
        Vector3 from = plane.transform.position;
        Vector3 carry = plane.Velocity;   // hayaletler kahramanin ileri hiziyla tasinir (yaninda kalir, geride kaybolmaz)
        float moved = plane.DashSideways(dir * d.distance);
        if (Mathf.Abs(moved) < 1f) return false;   // cercevenin kenarinda: sicranamaz

        dashCooldown = d.cooldown;
        nextDash = Time.time + d.cooldown;
        PlayerHealth ph = CurrentPlayerHealth;
        if (ph != null) ph.GrantInvulnerability(d.invuln);

        // Eski yerden yeniye parlak cizgi + kivilcim + ses; kamera arkasindan yetisir
        Vector3 to = plane.transform.position;
        FollowCamera.AddDashLag(to - from);
        HeroAnimator anim = plane.GetComponent<HeroAnimator>();
        if (anim != null) anim.OnDash(dir);

        // Yol boyunca uc ardisik hayalet (anime "afterimage"): yeni yere yakin olan daha belirgin; kamera
        // kahramani izledigi icin eski yerdeki tek hayalet kadraj disinda kaliyordu
        float[] at = { 0.3f, 0.6f, 0.85f };
        for (int i = 0; i < at.Length; i++)
        {
            Color c = d.color;
            c.a *= 0.45f + 0.55f * at[i];
            SpawnDashGhost(plane.transform, c, (from - to) * (1f - at[i]), 0.3f + 0.15f * i, carry);
        }
        SpawnDashStreak(from, to, d.color, carry);
        SpawnHitSpark(from, 1.6f);
        SpawnHitSpark(to, 1.2f);
        Sfx.PlayAt(model - PlaneBuilder.HeroStart == 1 ? Sfx.Zap : Sfx.Pickup, to, 0.5f);
        HeroVoice.Attack(model - PlaneBuilder.HeroStart, to);
        FollowCamera.AddShake(0.25f, 0.15f);
        return true;
    }

    // Hayalet: kahramanin tum parcalarinin saydam bir kopyasi bulundugu yerde kalir ve 0,45 sn'de soner
    private void SpawnDashGhost(Transform plane, Color color, Vector3 offset, float life, Vector3 carry)
    {
        if (fxMaterial == null) return;
        var root = new GameObject("DashGhost");
        root.transform.position = plane.position + offset;   // buyurken kahramanin cevresinde buyusun
        // Duz beyaz doku: fx malzemesinin yumusak daire dokusu primitive UV'lerinde kopyayi gorunmez yapiyordu
        var mat = new Material(fxMaterial) { color = color, mainTexture = Texture2D.whiteTexture };
        foreach (var mf in plane.GetComponentsInChildren<MeshFilter>())
        {
            var mr = mf.GetComponent<MeshRenderer>();
            if (mr == null || !mr.enabled || mf.sharedMesh == null) continue;
            var g = new GameObject("G");
            g.transform.SetParent(root.transform, false);
            g.transform.SetPositionAndRotation(mf.transform.position + offset, mf.transform.rotation);
            g.transform.localScale = mf.transform.lossyScale;
            g.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
            var r = g.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }
        var fade = root.AddComponent<DashFade>();
        fade.mat = mat;
        fade.life = life;
        fade.drift = carry;   // boss fazinda cerceveyle birlikte gitsin (geride kalmasin)
    }

    private void SpawnDashStreak(Vector3 from, Vector3 to, Color color, Vector3 carry)
    {
        if (fxMaterial == null) return;
        var go = new GameObject("DashStreak");
        var lr = go.AddComponent<LineRenderer>();
        var mat = new Material(fxMaterial) { color = Color.white };
        lr.sharedMaterial = mat;
        go.transform.position = from;
        lr.useWorldSpace = false;          // nesneyle birlikte tasinsin
        lr.positionCount = 2;
        lr.SetPosition(0, Vector3.zero);
        lr.SetPosition(1, to - from);
        lr.startWidth = 0.4f;
        lr.endWidth = 2.2f;
        lr.startColor = new Color(color.r, color.g, color.b, 0f);
        lr.endColor = new Color(color.r, color.g, color.b, 0.9f);
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        var fade = go.AddComponent<DashFade>();
        fade.mat = mat;
        fade.line = lr;
        fade.life = 0.3f;
        fade.drift = carry;
    }
}

// Kacis efektlerinin sonmesi: saydamlik azalir, hafif buyur, sure dolunca yok olur
public class DashFade : MonoBehaviour
{
    public Material mat;
    public LineRenderer line;
    public float life = 0.4f;
    public Vector3 drift;
    private float age;
    private Color start;

    private void Start()
    {
        if (mat != null) start = mat.color;
    }

    private void Update()
    {
        age += Time.deltaTime;
        float k = Mathf.Clamp01(1f - age / life);
        transform.position += drift * Time.deltaTime;
        if (line != null)
        {
            line.widthMultiplier = k;
        }
        else if (mat != null)
        {
            Color c = start;
            c.a *= k;
            mat.color = c;
            transform.localScale = Vector3.one * (1f + 0.15f * (1f - k));
        }
        if (age >= life)
        {
            if (mat != null) Destroy(mat);
            Destroy(gameObject);
        }
    }
}
