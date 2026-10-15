using UnityEngine;

// Kalkan yuzeyinde mermi degen noktada beliren, buyuyup sonen kucuk renkli alan.
public class ShieldPatch : MonoBehaviour
{
    public float duration = 0.7f;
    public float startSize = 2f;
    public float endSize = 3.8f;
    public Color color = new Color(0.35f, 0.85f, 1f, 0.9f);

    private Renderer rend;
    private Material mat;
    private float age;

    private void Awake()
    {
        rend = GetComponent<Renderer>();
        if (rend != null) mat = rend.material;   // kopya: asset'e dokunmaz
    }

    private void Update()
    {
        age += Time.deltaTime;
        float t = Mathf.Clamp01(age / duration);

        float size = Mathf.Lerp(startSize, endSize, 1f - (1f - t) * (1f - t));
        transform.localScale = new Vector3(size, size, 1f);

        if (mat != null)
        {
            Color c = color;
            c.a = color.a * (1f - t) * (1f - t);
            mat.color = c;
        }

        if (t >= 1f) Destroy(gameObject);
    }
}
