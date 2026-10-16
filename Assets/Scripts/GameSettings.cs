using UnityEngine;

// Oyun ayarlari: denge ve oynanis ozellestirmeleri tek bir ScriptableObject'te.
// Varlik: Assets/Resources/GameSettings.asset (Inspector'dan duzenlenir; oyun calisirken de degisiklikler
// aninda okunur). Kodda: GameSettings.Current.<alan>. Varlik bulunamazsa varsayilan degerler kullanilir.
[CreateAssetMenu(fileName = "GameSettings", menuName = "Plane ShootEmUp/Game Settings")]
public class GameSettings : ScriptableObject
{
    // ------------------------------------------------------- hasar / carpisma

    [Header("Çarpışma")]
    [Tooltip("Düşmana çarpınca alınan hasar = düşman türünün çarpma hasarı (Sürü 1, Avcı 2, Ağır 4, boss 4-6) x bu çarpan")]
    [Min(0f)] public float ramDamageMultiplier = 1f;
    [Tooltip("Açıksa çarpma hasarı kalkanı atlar ve doğrudan gövdeye işler (kalkan çarpmayı bedava yapmasın)")]
    public bool ramIgnoresShield = true;
    [Tooltip("Çarparak düşürülen düşman puan, kombo ve hurda verir mi")]
    public bool ramKillGivesScore = true;

    // ------------------------------------------------------- asama gecisi

    [Header("Aşama geçişi")]
    [Tooltip("Aşama bitince (sonraki aşamaya ya da hangara geçerken) canın en fazla bu oranı kadar onarılır")]
    [Range(0f, 1f)] public float stageClearHeal = 0.5f;
    [Tooltip("Yeni aşama başlarken kısa ölümsüzlük (sn)")]
    [Min(0f)] public float stageStartInvulnerability = 2.5f;

    // ------------------------------------------------------------------ magaza

    [Header("Mağaza")]
    [Tooltip("Açıksa mağaza duraklat menüsünden de (savaş ortasında) açılır. Kapalıyken yalnız aşama aralarında.")]
    public bool shopInPauseMenu = false;
    [Tooltip("Tek seferlik ürünler (onarım, kalkan, füze) her aşamada en fazla kaç kez alınabilir. 0 = sınırsız")]
    [Min(0)] public int consumableLimitPerStage = 1;

    // ---------------------------------------------------------------- kontrol

    [Header("Kontrol")]
    [Tooltip("Açıksa W burnu yukarı, S aşağı kaldırır. Kapalıysa (varsayılan) uçuş simülatörü gibi: W burun aşağı, S yukarı.")]
    public bool wPitchesUp = false;
    [Tooltip("Elle yatışın (Arcade'de Q/E, Klasik'te A/D) azami hızı (derece/sn). Hızlı yatan uçaklar bunun üstüne çıkamaz.")]
    [Range(60f, 500f)] public float maxManualRollRate = 200f;

    [Header("Fare ile uçuş (FARE şeması)")]
    [Tooltip("Açıksa oyun tipi (arcade) uçuş: fare burnu doğrudan çevirir (FPS gibi), hız gaza bağlıdır, stall ve G sınırı yoktur. Kapalıysa fizik modeli (gerçekçi, yavaş).")]
    public bool mouseArcadeFlight = true;
    [Tooltip("Fare hassasiyeti: büyük = aynı fare hareketiyle burun daha çok döner")]
    [Range(0.1f, 5f)] public float mouseSensitivity = 1f;
    [Tooltip("Açıksa fare yukarı = burun aşağı (ters). Kapalıyken fare yukarı = burun yukarı.")]
    public bool invertMouseY = false;
    [Tooltip("Arcade uçuşta azami dönüş hızı çarpanı (uçağın kendi çevikliği x bu). Büyük = daha hızlı döner.")]
    [Range(0.5f, 3f)] public float mouseTurnRate = 1.25f;
    [Tooltip("Arcade uçuşta fare hareketinin uçağa geçme süresi (sn). Dönüş yavaş başlar, hızlanır, yavaşça durur. Küçük = keskin, büyük = yumuşak ama gecikmeli.")]
    [Range(0.02f, 0.5f)] public float mouseSmoothing = 0.16f;
    [Tooltip("Arcade uçuşta sağa-sola dönerken uçağın görsel yatışı (derece)")]
    [Range(0f, 80f)] public float mouseVisualBank = 50f;
    [Tooltip("Yalnız fizik modelinde (arcade kapalı): fare durunca yönelmenin sönme hızı (1/sn)")]
    [Range(1f, 30f)] public float mouseSteerDecay = 8f;
    [Tooltip("FARE şemasında W/S bırakılınca gaz seyir hızına döner (kapalıysa rölantiye iner)")]
    public bool throttleReturnsToCruise = true;

    // ------------------------------------------------------------------- nisan

    [Header("Otomatik nişan")]
    [Tooltip("Burundan en fazla bu açı içindeki düşmanlar hedeflenir (derece). Ateş konisinden biraz geniş tutulmalı.")]
    [Range(2f, 60f)] public float autoAimCone = 12f;
    [Tooltip("Otomatik nişan menzili (m)")]
    [Min(100f)] public float autoAimRange = 1100f;
    [Tooltip("Nişanın yeni hedefe (ya da hedef yokken burnun önüne) dönme süresi (sn). Nişan anında atlamaz, bu sürede yumuşakça yönelir. 0 = anında.")]
    [Range(0f, 2f)] public float aimAcquireTime = 0.35f;

    // ------------------------------------------------------------------ mermi

    [Header("Mermi")]
    [Tooltip("Mermi burnun baktığı yönden en fazla bu kadar sapabilir (derece). Küçük = daha düz atış.")]
    [Range(1f, 45f)] public float fireCone = 8f;
    [Tooltip("Oyuncu mermilerinin (silah ve taret) hız çarpanı")]
    [Range(0.5f, 3f)] public float playerBulletSpeedMultiplier = 1.5f;
    [Tooltip("Oyuncu mermisi namludan çıktıktan sonra bu kadar süre görünmez kalır (sn)")]
    [Range(0f, 0.2f)] public float bulletHiddenAfterMuzzle = 0.02f;
    [Tooltip("Açıksa mermi namludan dümdüz çıkar ve nişan noktasına yumuşakça (ease-in) kıvrılır; kapalıysa doğrudan nişan yönünde çıkar. Düşmanı takip etmez (güdümlü değil).")]
    public bool bulletEaseToAim = true;
    [Tooltip("Kıvrılmanın tam güce ulaşma süresi (sn). Büyük = daha yumuşak, daha geç kıvrılır.")]
    [Range(0.03f, 0.6f)] public float bulletEaseTime = 0.18f;

    // --------------------------------------------------------------- ucus

    [Header("Arcade uçuş (A/D dönüş)")]
    [Tooltip("A/D tam basışta dönüş G'si = uçağın azami G'sinin bu kesri (büyük = daha sert dönüş)")]
    [Range(0.3f, 1f)] public float arcadeTurnGFraction = 0.85f;
    [Tooltip("A/D ile hedef yatışa geçiş hızı sınırı (derece/sn)")]
    [Range(60f, 500f)] public float arcadeBankRate = 220f;

    // ------------------------------------------------------------------ erisim

    private static GameSettings current;
    private static bool warned;

    /// <summary>Etkin ayarlar (Resources/GameSettings). Bulunamazsa varsayilanlarla gecici bir kopya.</summary>
    public static GameSettings Current
    {
        get
        {
            if (current != null) return current;

            current = Resources.Load<GameSettings>("GameSettings");
            if (current == null)
            {
                if (!warned)
                {
                    warned = true;
                    Debug.LogWarning("GameSettings varligi bulunamadi (Assets/Resources/GameSettings.asset); varsayilanlar kullaniliyor.");
                }
                current = CreateInstance<GameSettings>();
            }
            return current;
        }
    }
}
