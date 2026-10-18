using System.Collections.Generic;
using UnityEngine;

// CombatManager'in bomba kismi: B tusu, dusman mermilerini temizler, yakin dusmanlara agir hasar verir.
public partial class CombatManager
{
    public const float BombRadius = 800f;
    private const float BombSpeed = 1500f;

    private float nextBombTime;
    private float bombFlash;
    private static Texture2D ringTexture;

    private class BombRing
    {
        public Transform root;
        public Material mat;
        public float born;
    }

    private readonly List<BombRing> bombRings = new List<BombRing>();

    public float BombFlash => bombFlash;

    public bool TryBomb()
    {
        if (!RunState.Active) return false;

        PlaneController plane = flight != null ? flight.Current : null;
        if (plane == null || plane.IsCrashed) return false;
        if (Time.time < nextBombTime) return false;

        if (!RunState.UseBomb())
        {
            Announce("BOMBA YOK", new Color(1f, 0.5f, 0.4f), 1.2f);
            return false;
        }

        nextBombTime = Time.time + 1.5f;
        Vector3 center = plane.transform.position;

        // Mermi yagmuru temizlenir; herkes agir hasar alir (boss daha fazla)
        Bullet.ClearEnemyBullets();
        var snapshot = new List<EnemyPlane>(enemies);
        foreach (var e in snapshot)
        {
            if (e == null || e.IsDead) continue;
            if ((e.transform.position - center).sqrMagnitude > BombRadius * BombRadius) continue;
            // Boss'a yuzde hasar (can ne olursa olsun anlamli ama oldurucu degil)
            // Yer birimleri bombadan az etkilenir (us tek bombayla bitmesin)
            int bombDamage = e.IsBoss ? Mathf.Max(25, Mathf.RoundToInt(e.MaxHits * 0.05f)) : (e.IsGround ? 4 : 12);
            e.TakeHit(bombDamage, e.transform.position);
        }

        PlayerHealth ph = CurrentPlayerHealth;
        if (ph != null) ph.GrantInvulnerability(2f);

        SpawnExplosion(center + plane.transform.forward * 20f);
        Sfx.PlayAt(Sfx.Pulse, center, 1f);
        bombFlash = 1f;
        SpawnBombRing(center);
        return true;
    }

    private void SpawnBombRing(Vector3 center)
    {
        if (fxMaterial == null) return;

        if (ringTexture == null)
        {
            const int size = 128;
            ringTexture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size * 2f - 1f;
                float dy = (y + 0.5f) / size * 2f - 1f;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float edge = Mathf.Exp(-Mathf.Pow((r - 0.93f) / 0.045f, 2f));
                float fill = r < 0.93f ? 0.1f * (1f - r) : 0f;
                ringTexture.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(edge + fill)));
            }
            ringTexture.Apply();
        }

        var ring = new BombRing
        {
            born = Time.time,
            root = new GameObject("BombRing").transform,
            mat = new Material(fxMaterial) { mainTexture = ringTexture }
        };
        ring.root.position = center;

        // Collider'siz quad: primitive'in MeshCollider'i bir kare boyunca ucakla carpisip dusurebilir
        for (int i = 0; i < 2; i++)
        {
            var q = new GameObject("Ring" + i);
            q.transform.SetParent(ring.root, false);
            q.transform.localRotation = i == 0 ? Quaternion.Euler(90f, 0f, 0f) : Quaternion.identity;
            q.AddComponent<MeshFilter>().sharedMesh = QuadMesh();
            var r = q.AddComponent<MeshRenderer>();
            r.sharedMaterial = ring.mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }

        bombRings.Add(ring);
    }

    private static Mesh quadMesh;

    private static Mesh QuadMesh()
    {
        if (quadMesh != null) return quadMesh;

        quadMesh = new Mesh { name = "BombQuad" };
        quadMesh.vertices = new[]
        {
            new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
            new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f)
        };
        quadMesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
        quadMesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
        quadMesh.RecalculateNormals();
        quadMesh.RecalculateBounds();
        return quadMesh;
    }

    private void UpdateBombRings()
    {
        for (int k = bombRings.Count - 1; k >= 0; k--)
        {
            BombRing r = bombRings[k];
            float radius = (Time.time - r.born) * BombSpeed;

            if (radius >= BombRadius)
            {
                if (r.root != null) Destroy(r.root.gameObject);
                if (r.mat != null) Destroy(r.mat);
                bombRings.RemoveAt(k);
                continue;
            }

            float scale = Mathf.Max(0.1f, radius * 2f);
            r.root.localScale = new Vector3(scale, scale, scale);

            Color c = PowerUp.KindColors[(int)PickupKind.Bomb];
            c.a = Mathf.Lerp(0.95f, 0f, radius / BombRadius);
            r.mat.color = c;
        }
    }
}
