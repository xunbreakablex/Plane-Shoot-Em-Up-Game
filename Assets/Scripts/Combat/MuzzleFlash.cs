using UnityEngine;

// Namlu noktasi + agiz parlamasi. Nesnenin konumu namlu UCU, ileri yonu (forward) namlu yonudur;
// PlayerShot mermiyi buradan cikarir. Parlama kisa (~50 ms), iki katman: kameraya donuk yuvarlak
// parlama + namlu yonunde uzayan alev dili. Bosta bilesen kapali durur.
public class MuzzleFlash : MonoBehaviour
{
    public const float Duration = 0.05f;

    private static Mesh quadMesh;
    private static Material discMat, streakMat;
    private static Camera cam;

    private Transform disc, streak;
    private Renderer discRenderer, streakRenderer;
    private float baseSize;
    private float bornAt;
    private float scaleRoll, angleRoll;

    /// <summary>
    /// Namlu ucuna namlu noktasi + parlama olusturur (yalniz oyunda). parent'in ileri yonu namlu yonu
    /// olmali (ana silahlar: ucak koku, taret: donen kafa).
    /// </summary>
    public static MuzzleFlash Create(Transform parent, string name, Vector3 localTip, float size)
    {
        EnsureAssets();

        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localTip;
        go.transform.localRotation = Quaternion.identity;

        var f = go.AddComponent<MuzzleFlash>();
        f.baseSize = size;
        f.disc = MakeQuad("Disc", go.transform, discMat, out f.discRenderer);
        f.streak = MakeQuad("Streak", go.transform, streakMat, out f.streakRenderer);
        f.SetVisible(false);
        f.enabled = false;
        return f;
    }

    private static Transform MakeQuad(string name, Transform parent, Material mat, out Renderer renderer)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = quadMesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        renderer = mr;
        return go.transform;
    }

    private static void EnsureAssets()
    {
        if (quadMesh == null)
        {
            // Collider'siz mesh (primitive collider'i gecikmeli silinir ve ucaga carpar)
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quadMesh = q.GetComponent<MeshFilter>().sharedMesh;
            DestroyImmediate(q);
        }

        if (discMat != null && streakMat != null) return;

        Material source = CombatManager.Instance != null ? CombatManager.Instance.fxMaterial : null;
        if (source == null)
        {
            Shader s = Shader.Find("Sprites/Default");
            source = s != null ? new Material(s) : null;
        }
        if (source == null) return;

        discMat = new Material(source) { color = new Color(1f, 0.93f, 0.6f, 1f) };
        streakMat = new Material(source) { color = new Color(1f, 0.62f, 0.2f, 0.95f) };
    }

    private void SetVisible(bool on)
    {
        if (discRenderer != null) discRenderer.enabled = on;
        if (streakRenderer != null) streakRenderer.enabled = on;
    }

    /// <summary>Tek atis parlamasi; her seferinde boyut ve aci biraz degisir.</summary>
    public void Fire()
    {
        bornAt = Time.time;
        scaleRoll = Random.Range(0.8f, 1.25f);
        angleRoll = Random.Range(0f, 360f);
        SetVisible(true);
        enabled = true;
        Place();
    }

    private void LateUpdate()
    {
        if (Time.time - bornAt > Duration)
        {
            SetVisible(false);
            enabled = false;
            return;
        }
        Place();
    }

    private void Place()
    {
        if (cam == null) cam = Camera.main;
        if (cam == null) return;

        // 0 -> 1: hizla buyur, sonra soner
        float t = Mathf.Clamp01((Time.time - bornAt) / Duration);
        float k = (1f - t) * scaleRoll * baseSize;

        Vector3 p = transform.position;
        Vector3 toFlash = p - cam.transform.position;
        Vector3 gunFwd = transform.forward;

        // Yuvarlak parlama: kameraya doner, her atista farkli aci
        disc.rotation = Quaternion.LookRotation(toFlash, cam.transform.up) * Quaternion.Euler(0f, 0f, angleRoll);
        disc.localScale = Vector3.one * (0.9f * k);

        // Alev dili: kameraya donuk ama namlu yonunde uzar, ucu namlunun onunde. Kovalama kamerasi
        // namluya tam arkadan bakarken dil uctan gorunur (yon belirsiz): o zaman gizlenir.
        float side = Vector3.Cross(toFlash.normalized, gunFwd).magnitude;
        bool showStreak = side > 0.15f;
        if (streakRenderer != null) streakRenderer.enabled = showStreak;
        if (!showStreak) return;

        streak.rotation = Quaternion.LookRotation(toFlash, gunFwd);
        float len = 1.8f * k;
        streak.localScale = new Vector3(0.45f * k, len, 1f);
        streak.position = p + gunFwd * (len * 0.5f);
    }
}
