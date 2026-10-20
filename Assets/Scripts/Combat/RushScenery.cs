using System.Collections.Generic;
using UnityEngine;

// Boss fazinda hiz hissi: acik denizde bulut ve dalga yok. Oyuncunun onune bulut kumeleri ve su ustune beyaz kopuk
// seritleri dizer; arkada kalani yeniden ileri tasir (havuz, yeni nesne uretmez). Bulutlar ucus koridorunun
// ustunde ya da yaninda kalir: boss'u ortmez.
public class RushScenery : MonoBehaviour
{
    private const int CloudCount = 34;
    private const int FoamCount = 120;

    private readonly List<Transform> clouds = new List<Transform>();
    private readonly List<Transform> foam = new List<Transform>();
    private Material cloudMat;
    private CombatManager mgr;
    private bool active;
    private GameObject root;

    public void Begin(CombatManager manager)
    {
        mgr = manager;
        Build();
        Transform pt = mgr.PlayerTransform;
        if (pt == null) return;

        Vector3 p = pt.position;
        Vector3 f = mgr.RushHeading;
        foreach (Transform c in clouds) PlaceCloud(c, p, f, Random.Range(-300f, 3600f));
        foreach (Transform s in foam) PlaceFoam(s, p, f, Random.Range(-100f, 1700f));
        root.SetActive(true);
        active = true;
    }

    public void End()
    {
        active = false;
        if (root != null) root.SetActive(false);
    }

    private void Build()
    {
        if (root != null) return;
        root = new GameObject("RushScenery");

        // Sahnedeki bulut malzemesi (yoksa duz beyaz)
        GameObject clouds0 = GameObject.Find("Clouds");
        if (clouds0 != null && clouds0.transform.childCount > 0)
        {
            var r = clouds0.transform.GetChild(0).GetComponent<Renderer>();
            if (r != null) cloudMat = r.sharedMaterial;
        }

        for (int i = 0; i < CloudCount; i++) clouds.Add(MakePiece(PrimitiveType.Sphere, "RushCloud"));
        for (int i = 0; i < FoamCount; i++) foam.Add(MakePiece(PrimitiveType.Cube, "RushFoam"));
        root.SetActive(false);
    }

    private Transform MakePiece(PrimitiveType prim, string name)
    {
        var go = GameObject.CreatePrimitive(prim);
        go.name = name;
        Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(root.transform, false);
        var r = go.GetComponent<Renderer>();
        if (cloudMat != null) r.sharedMaterial = cloudMat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        return go.transform;
    }

    // Bulut: cogu ucus koridorunun ustunde (genis katman), bir kismi yanlarda alcakta
    private static void PlaceCloud(Transform c, Vector3 p, Vector3 f, float ahead)
    {
        Vector3 right = Vector3.Cross(Vector3.up, f).normalized;
        bool high = Random.value < 0.7f;
        float side = high ? Random.Range(-1800f, 1800f) : (Random.value < 0.5f ? -1f : 1f) * Random.Range(420f, 1700f);
        float y = CombatManager.SeaLevel + (high ? Random.Range(390f, 720f) : Random.Range(60f, 300f));
        c.position = new Vector3(p.x, 0f, p.z) + f * ahead + right * side + Vector3.up * y;
        float s = Random.Range(0.55f, 1.25f);
        c.localScale = new Vector3(140f, 44f, 88f) * s;
        c.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
    }

    // Kopuk: su yuzeyinde ince beyaz serit
    private static void PlaceFoam(Transform s, Vector3 p, Vector3 f, float ahead)
    {
        Vector3 right = Vector3.Cross(Vector3.up, f).normalized;
        s.position = new Vector3(p.x, CombatManager.SeaLevel + 0.12f, p.z) + f * ahead + right * Random.Range(-950f, 950f);
        s.localScale = new Vector3(Random.Range(5f, 20f), 0.15f, Random.Range(1.5f, 5f));
        s.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
    }

    private void LateUpdate()
    {
        if (!active || mgr == null) return;
        Transform pt = mgr.PlayerTransform;
        if (pt == null) return;

        Vector3 p = pt.position;
        Vector3 f = mgr.RushHeading;
        Vector3 right = Vector3.Cross(Vector3.up, f).normalized;

        foreach (Transform c in clouds)
        {
            Vector3 rel = c.position - p;
            if (Vector3.Dot(rel, f) < -500f || Mathf.Abs(Vector3.Dot(rel, right)) > 2600f)
                PlaceCloud(c, p, f, Random.Range(2800f, 3800f));
        }
        foreach (Transform s in foam)
        {
            Vector3 rel = s.position - p;
            if (Vector3.Dot(rel, f) < -160f || Mathf.Abs(Vector3.Dot(rel, right)) > 1300f)
                PlaceFoam(s, p, f, Random.Range(1300f, 1800f));
        }
    }

    private void OnDestroy()
    {
        if (root != null) Destroy(root);
    }
}
