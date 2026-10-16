using UnityEngine;

// Oyuncu silahlarinin (ana silahlar ve taretler) TEK ates yolu:
//   1) Mermi namlu ucunun (MuzzleFlash nesnesi) hemen onunden cikar
//   2) Namlu agiz parlamasi tetiklenir
//   3) Mermi namludan ciktiktan sonra GameSettings.bulletHiddenAfterMuzzle kadar gorunmez kalir (Bullet yapar)
// Yeni bir oyuncu silahi eklenirse namlu ucuna MuzzleFlash.Create ile nokta koyup buradan ates et.
public static class PlayerShot
{
    /// <summary>Merminin namlu ucunun ne kadar onunde dogdugu (m): namlu/ucak collider'ina girmesin.</summary>
    public const float TipClearance = 0.1f;

    /// <summary>Namlu noktasindan mermi cikis konumu.</summary>
    public static Vector3 MuzzlePoint(MuzzleFlash muzzle)
    {
        Transform t = muzzle.transform;
        return t.position + t.forward * TipClearance;
    }

    /// <summary>Namlu ucundan ates eder ve parlamayi tetikler. Namlu noktasi yoksa verilen konumu kullanir.</summary>
    public static Bullet Fire(Bullet prefab, MuzzleFlash muzzle, Vector3 fallbackPosition, Vector3 direction,
        float speed, int damage, Transform owner, int pierce = 0)
    {
        if (prefab == null) return null;

        Vector3 pos = muzzle != null ? MuzzlePoint(muzzle) : fallbackPosition;
        Bullet b = Bullet.Spawn(prefab, pos, direction, speed, damage, owner, true, pierce);
        if (muzzle != null) muzzle.Fire();
        return b;
    }

    /// <summary>
    /// Namlu yonunde (barrelDirection) cikar ve nisan noktasina ease-in ile kivrilir (GameSettings.bulletEaseToAim).
    /// Ayar kapaliysa ya da sapma cok kucukse dogrudan nisan yonunde cikar. Dusmani takip etmez.
    /// </summary>
    public static Bullet FireEased(Bullet prefab, MuzzleFlash muzzle, Vector3 fallbackPosition, Vector3 barrelDirection,
        Vector3 aimPoint, float speed, int damage, Transform owner, int pierce = 0)
    {
        Vector3 pos = muzzle != null ? MuzzlePoint(muzzle) : fallbackPosition;
        Vector3 aimDir = (aimPoint - pos).normalized;

        GameSettings gs = GameSettings.Current;
        bool ease = gs.bulletEaseToAim && Vector3.Angle(barrelDirection, aimDir) > 0.5f;

        Bullet b = Fire(prefab, muzzle, fallbackPosition, ease ? barrelDirection : aimDir, speed, damage, owner, pierce);
        if (b != null && ease) b.EaseTowards(aimPoint, gs.bulletEaseTime);
        return b;
    }
}
