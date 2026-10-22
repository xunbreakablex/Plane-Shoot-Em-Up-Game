using System.Collections.Generic;
using UnityEngine;

// Kahramanlarin "insan" hareketi (ucak gibi degil): ucus modeli kok nesneyi ucak gibi yatirir, bu bilesen "Body"
// pivotunda yatisin cogunu geri alir (kahraman hep dike yakin durur, donuslerde hafif egilir) ve uzuvlari canlandirir:
//   bacaklar hafifce sallanir, sac kuyruklari / pelerin hizla geriye dalgalanir, buyu halkalari doner,
//   ki aurasi titrer, ates ederken eller / degnek ucu parlar, kacista Ay Savascisi kendi ekseninde bir tur doner.
// Hangarda (PlaneController yokken) yalniz bosta canlanma calisir.
public class HeroAnimator : MonoBehaviour
{
    private const float RollKeep = 0.3f;   // ucak yatisinin kahramanda gorunen orani (geri kalani telafi edilir)

    private class Part
    {
        public Transform t;
        public Vector3 basePos, baseScale;
        public Quaternion baseRot;
        public Vector3 pivot;              // donme noktasi (Body yerelinde)
        public float phase;
    }

    private int hero;
    private Transform body;
    private Quaternion bodyBase;
    private PlaneController plane;
    private readonly List<Part> legs = new List<Part>();
    private readonly List<Part> tails = new List<Part>();
    private readonly List<Part> capes = new List<Part>();
    private readonly List<Part> rings = new List<Part>();
    private readonly List<Part> auras = new List<Part>();
    private readonly List<Part> glows = new List<Part>();   // ates ederken parlayan el / degnek ucu
    private float spinStart = -10f, spinDir = 1f;
    private float lastSpeed;

    public void Init(int heroIndex)
    {
        hero = heroIndex;
        body = transform.Find("Body");
        plane = GetComponent<PlaneController>();
        if (body == null) return;
        bodyBase = body.localRotation;

        foreach (Transform c in body)
        {
            switch (c.name)
            {
                case "LegL": case "LegR":
                    legs.Add(Make(c, new Vector3(0f, c.localScale.y, 0f)));   // ust uc (kalca)
                    break;
                case "BootL": case "BootR":
                    // Cizme bacakla birlikte doner: pivotu asagida ayni taraftaki bacagin kalcasina esitlenir
                    legs.Add(Make(c, Vector3.zero));
                    break;
                case "Tail":
                    tails.Add(Make(c, new Vector3(0f, c.localScale.y, 0f)));
                    break;
                case "Cape":
                    capes.Add(Make(c, new Vector3(0f, c.localScale.y * 0.5f, 0f)));
                    break;
                case "MagicRing": case "MagicRingInner": case "Ring": case "RingInner":
                    rings.Add(Make(c, Vector3.zero));
                    break;
                case "Aura":
                    auras.Add(Make(c, Vector3.zero));
                    break;
                case "FistL": case "FistR": case "StarCore": case "Hand": case "HandL":
                    glows.Add(Make(c, Vector3.zero));
                    break;
            }
        }
        // Cizmelerin pivotu: ayni taraftaki bacagin kalcasi
        foreach (var p in legs)
        {
            if (!p.t.name.StartsWith("Boot")) continue;
            string side = p.t.name.Substring(4);
            foreach (var l in legs)
                if (l.t.name == "Leg" + side) { p.pivot = l.pivot; p.phase = l.phase; }
        }
    }

    private static Part Make(Transform t, Vector3 pivotOffset)
    {
        return new Part
        {
            t = t, basePos = t.localPosition, baseRot = t.localRotation, baseScale = t.localScale,
            pivot = t.localPosition + pivotOffset,
            phase = t.localPosition.x > 0f ? Mathf.PI : 0f
        };
    }

    /// <summary>Kacis basladi: Ay Savascisi kendi ekseninde bir tur doner.</summary>
    public void OnDash(int dir)
    {
        spinStart = Time.time;
        spinDir = dir >= 0 ? -1f : 1f;
    }

    private void LateUpdate()
    {
        if (body == null) return;
        float t = Time.time;
        float dt = Mathf.Max(Time.deltaTime, 1e-4f);

        // Hiz (pelerin / sac dalgalanmasi) ve yatis
        float speed = plane != null ? plane.Speed : 0f;
        float speedK = Mathf.Clamp01(speed / 120f);
        float accel = (speed - lastSpeed) / dt;
        lastSpeed = speed;

        Vector3 r = transform.right, u = transform.up;
        float roll = Mathf.Atan2(r.y, u.y) * Mathf.Rad2Deg;
        float keepRoll = Mathf.Abs(u.y) < 0.2f ? 1f : RollKeep;   // ters / dik ucusta telafi yok (garip donmesin)
        Quaternion counter = Quaternion.AngleAxis(-roll * (1f - keepRoll), Vector3.forward);

        // Kacis donusu (yalniz Ay Savascisi): 0,35 sn'de bir tam tur
        float spin = 0f;
        if (hero == 0)
        {
            float k = (t - spinStart) / 0.35f;
            if (k >= 0f && k < 1f) spin = Mathf.SmoothStep(0f, 360f, k) * spinDir;
        }
        body.localRotation = counter * Quaternion.AngleAxis(spin, Vector3.up) * bodyBase;

        // Bacaklar: hafif sallanma; hizlaninca geriye savrulur (pozitif X: alt uc geriye)
        float legBack = 8f + 14f * speedK + Mathf.Clamp(accel * 0.3f, 0f, 10f);
        foreach (var p in legs)
            Rotate(p, Quaternion.Euler(legBack + Mathf.Sin(t * 2.6f + p.phase) * 7f, 0f, 0f));

        // Sac kuyruklari: hizla geriye kalkar, dalgalanir
        foreach (var p in tails)
            Rotate(p, Quaternion.Euler(15f + 45f * speedK + Mathf.Sin(t * 7f + p.phase) * 8f, 0f, Mathf.Sin(t * 3.1f + p.phase) * 6f));

        // Pelerin: arkaya dalgalanir (pozitif X ust kenar etrafinda alt kenari geriye goturur)
        foreach (var p in capes)
            Rotate(p, Quaternion.Euler(12f + 40f * speedK + Mathf.Sin(t * 9f) * 6f * (0.4f + speedK), 0f, Mathf.Sin(t * 4.3f) * 3f));

        // Buyu / karanlik halkalar doner, nefes alir gibi buyur kuculur
        for (int i = 0; i < rings.Count; i++)
        {
            Part p = rings[i];
            float dir = (i & 1) == 0 ? 1f : -1f;
            p.t.localRotation = p.baseRot * Quaternion.AngleAxis(t * 90f * dir, Vector3.up);
            p.t.localScale = Vector3.Scale(p.baseScale, new Vector3(1f + 0.06f * Mathf.Sin(t * 3f + i), 1f, 1f + 0.06f * Mathf.Sin(t * 3f + i)));
        }

        // Ki aurasi: dilimler ayri ayri uzar kisalir (alev gibi)
        for (int i = 0; i < auras.Count; i++)
        {
            Part p = auras[i];
            float f = 0.75f + 0.35f * Mathf.PerlinNoise(t * 6f, i * 1.7f) + 0.25f * speedK;
            p.t.localScale = new Vector3(p.baseScale.x, p.baseScale.y * f, p.baseScale.z);
        }

        // Ates ederken eller / degnek ucu parlar (nabiz gibi buyur)
        bool firing = false;
        CombatManager cm = CombatManager.Instance;
        if (plane != null && cm != null && cm.gun != null && cm.flight != null && cm.flight.Current == plane) firing = cm.gun.IsFiring;
        float glow = firing ? 1.25f + 0.2f * Mathf.Sin(t * 30f) : 1f + 0.05f * Mathf.Sin(t * 4f);
        foreach (var p in glows) p.t.localScale = p.baseScale * glow;
    }

    private static void Rotate(Part p, Quaternion q)
    {
        p.t.localRotation = q * p.baseRot;
        p.t.localPosition = p.pivot + q * (p.basePos - p.pivot);
    }
}
