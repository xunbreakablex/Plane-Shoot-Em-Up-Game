using System;
using UnityEngine;

// Oyun ayarlari: denge ve oynanis ozellestirmeleri tek bir ScriptableObject'te.
// Varlik: Assets/Resources/GameSettings.asset (Inspector'dan duzenlenir; oyun calisirken de degisiklikler
// aninda okunur). Kodda: GameSettings.Current.<alan>. Varlik bulunamazsa varsayilan degerler kullanilir.
[CreateAssetMenu(fileName = "GameSettings", menuName = "Plane ShootEmUp/Game Settings")]
public class GameSettings : ScriptableObject
{
    // ------------------------------------------------------------------ sahne

    [Header("Sahne")]
    [Tooltip("Acikken hangardaki SAVASA BASLA uzay prototipini (SpacePrototype sahnesi) acar; kapaliyken klasik savas sahnesi.")]
    public bool useSpacePrototype = false;

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

    // ------------------------------------------------------------------- nisan

    [Header("Otomatik nişan")]
    [Tooltip("Burundan en fazla bu açı içindeki düşmanlar hedeflenir (derece)")]
    [Range(5f, 60f)] public float autoAimCone = 30f;
    [Tooltip("Otomatik nişan menzili (m)")]
    [Min(100f)] public float autoAimRange = 1100f;

    // ------------------------------------------------------------------ mermi

    [Header("Mermi")]
    [Tooltip("Oyuncu mermisi namludan çıktıktan sonra bu kadar süre görünmez kalır (sn)")]
    [Range(0f, 0.2f)] public float bulletHiddenAfterMuzzle = 0.02f;

    // --------------------------------------------------------------- ucus

    [Header("Arcade uçuş (A/D dönüş)")]
    [Tooltip("A/D tam basışta dönüş G'si = uçağın azami G'sinin bu kesri (büyük = daha sert dönüş)")]
    [Range(0.3f, 1f)] public float arcadeTurnGFraction = 0.85f;
    [Tooltip("A/D ile hedef yatışa geçiş hızı sınırı (derece/sn)")]
    [Range(60f, 500f)] public float arcadeBankRate = 220f;

    // ------------------------------------------------------- uzay prototipi

    [Header("Uzay prototipi")]
    public SpacePrototypeSettings space = new SpacePrototypeSettings();

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

[Serializable]
public class SpacePrototypeSettings
{
    [Header("Hareket (WASD)")]
    [Tooltip("Gemi koridor boyunca bu hızla kendiliğinden ilerler (m/s)")]
    [Min(5f)] public float forwardSpeed = 75f;
    [Tooltip("WASD ile yana / yukarı-aşağı azami hız (m/s)")]
    [Min(5f)] public float strafeSpeed = 45f;
    [Tooltip("Yana hareket ivmesi (m/s²); büyük = daha keskin")]
    [Min(10f)] public float strafeAcceleration = 180f;
    [Tooltip("Hareket ederken gemi yatış açısı (derece, görsel)")]
    [Range(0f, 80f)] public float bankAngle = 35f;

    [Header("Koridor")]
    [Tooltip("Koridorun yarı genişliği (m): gemi merkezden en fazla bu kadar sağa-sola gidebilir")]
    [Min(10f)] public float corridorHalfWidth = 70f;
    [Tooltip("Koridorun yarı yüksekliği (m)")]
    [Min(10f)] public float corridorHalfHeight = 42f;
    [Tooltip("Koridorun yerden yüksekliği (m); düşman yapay zekâsının zemin kontrolleriyle karışmasın diye yüksek")]
    public float corridorAltitude = 800f;

    [Header("Meteor duvarı")]
    [Tooltip("Her parçada (segmentte) duvara dizilen meteor sayısı")]
    [Range(4, 120)] public int meteorsPerSegment = 34;
    [Tooltip("Bir segmentin uzunluğu (m)")]
    [Min(20f)] public float segmentLength = 120f;
    [Tooltip("Geminin önünde bu mesafeye kadar meteor üretilir (m)")]
    [Min(200f)] public float viewAhead = 1600f;
    [Tooltip("Duvar kalınlığı (m): meteorlar koridor sınırından bu kadar dışarıya kadar dağılır")]
    [Min(5f)] public float wallThickness = 80f;
    [Tooltip("Meteor yarıçapı aralığı (m)")]
    public Vector2 meteorRadius = new Vector2(5f, 22f);
    [Tooltip("Meteorlara değince alınan hasar (kalkanı atlar)")]
    [Min(0)] public int meteorDamage = 2;
    [Tooltip("Meteora değince gemiyi koridor merkezine doğru iten hız (m/s)")]
    [Min(0f)] public float meteorKnockback = 40f;

    [Header("Düşmanlar")]
    [Tooltip("Açıksa düşmanlar ve bosslar koridor içinde tutulur (meteor duvarının içinden geçmesinler)")]
    public bool keepEnemiesInCorridor = true;
}
