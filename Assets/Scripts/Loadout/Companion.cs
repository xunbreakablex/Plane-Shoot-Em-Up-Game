using UnityEngine;

// Kahraman karakterlerin "tareti": omuzda ucan kucuk (chibi) yardimci. Govde + kafa + gozler + minik silah;
// AutoTurret kafayi (Head) hedefe cevirir, mermi onun onundeki namlu noktasindan cikar. Yerinde yukari-asagi salinir.
public class Companion : MonoBehaviour
{
    public Transform Head { get; private set; }

    private Vector3 basePos;
    private float phase;
    private static Material[] cache;

    private static Material Mat(Color c, bool emissive)
    {
        var m = new Material(Shader.Find(emissive ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit"));
        m.color = c;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        return m;
    }

    /// <summary>kind: 0 Ay Savascisi, 1 Ki Kahramani, 2 Imparator; strong: agir yardimci (biraz buyuk).</summary>
    public void Build(int kind, bool strong)
    {
        basePos = transform.localPosition;
        phase = Random.value * 6.28f;
        float s = strong ? 1.2f : 1f;

        Color costume, skin, hair, weapon;
        switch (kind)
        {
            case 0: costume = new Color(1f, 0.55f, 0.75f); skin = new Color(0.99f, 0.84f, 0.74f); hair = new Color(1f, 0.82f, 0.35f); weapon = new Color(1f, 0.95f, 0.5f); break;
            case 1: costume = new Color(0.9f, 0.45f, 0.08f); skin = new Color(0.93f, 0.74f, 0.58f); hair = new Color(1f, 0.9f, 0.3f); weapon = new Color(0.5f, 0.85f, 1f); break;
            default: costume = new Color(0.12f, 0.1f, 0.14f); skin = new Color(0.85f, 0.82f, 0.75f); hair = new Color(0.1f, 0.08f, 0.1f); weapon = new Color(0.7f, 0.35f, 1f); break;
        }

        var head = new GameObject("Head");
        head.transform.SetParent(transform, false);
        Head = head.transform;

        Piece(Head, PrimitiveType.Sphere, "Body", new Vector3(0f, -0.02f, 0f), new Vector3(0.24f, 0.22f, 0.22f) * s, Mat(costume, false));
        Piece(Head, PrimitiveType.Sphere, "Face", new Vector3(0f, 0.2f, 0.02f), new Vector3(0.3f, 0.3f, 0.3f) * s, Mat(skin, false));
        Piece(Head, PrimitiveType.Sphere, "Hair", new Vector3(0f, 0.27f, -0.06f), new Vector3(0.33f, 0.26f, 0.3f) * s, Mat(hair, false));
        Material eye = Mat(Color.white, true);
        Piece(Head, PrimitiveType.Sphere, "EyeL", new Vector3(-0.06f, 0.2f, 0.15f), new Vector3(0.05f, 0.06f, 0.03f) * s, eye);
        Piece(Head, PrimitiveType.Sphere, "EyeR", new Vector3(0.06f, 0.2f, 0.15f), new Vector3(0.05f, 0.06f, 0.03f) * s, eye);
        if (kind == 0)
        {
            Piece(Head, PrimitiveType.Cylinder, "Wand", new Vector3(0.12f, 0f, 0.22f), new Vector3(0.03f, 0.16f, 0.03f) * s, Mat(new Color(1f, 0.85f, 0.3f), false), new Vector3(90f, 0f, 0f));
            Piece(Head, PrimitiveType.Sphere, "Star", new Vector3(0.12f, 0f, 0.4f), new Vector3(0.1f, 0.1f, 0.1f) * s, Mat(weapon, true));
        }
        else
        {
            Piece(Head, PrimitiveType.Sphere, "HandL", new Vector3(-0.12f, 0f, 0.28f), new Vector3(0.1f, 0.1f, 0.1f) * s, Mat(weapon, true));
            Piece(Head, PrimitiveType.Sphere, "HandR", new Vector3(0.12f, 0f, 0.28f), new Vector3(0.1f, 0.1f, 0.1f) * s, Mat(weapon, true));
        }
    }

    private static void Piece(Transform parent, PrimitiveType type, string name, Vector3 pos, Vector3 scale, Material mat, Vector3 euler = default)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = PlaneBuilder.Prefix + name;
        Object.Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localEulerAngles = euler;
        go.transform.localScale = scale;
        var r = go.GetComponent<Renderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
    }

    private void Update()
    {
        float t = Time.unscaledTime;
        transform.localPosition = basePos + new Vector3(0f, Mathf.Sin(t * 3.1f + phase) * 0.07f, Mathf.Sin(t * 1.7f + phase) * 0.03f);
    }
}
