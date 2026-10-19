using UnityEngine;

// Ucak parcalari katalogu. Sayilar tek yerde durur; dengeyi buradan ayarla.
//
// Guc butcesi: motor guc KAPASITESI verir; silah, kalkan ve taretler guc TUKETIR.
// Agirlik: toplam agirlik ucagin ivmesini, seyir hizini ve ceviklingini etkiler.
[System.Serializable]
public class PartDef
{
    public string name;
    public string description;

    public float weight;     // agirlik birimi (ucak govdesi ~100)
    public float power;      // motor: kapasite, digerleri: tuketim

    // Performans carpanlari (motor / kanat / zirh)
    public float thrust = 1f;
    public float maxSpeed = 1f;
    public float cruise = 1f;
    public float roll = 1f;
    public float maxG = 1f;
    public float span = 1f;
    public int blades = 2;
    public float hp;

    // Kalkan
    public float shield;
    public float regen;

    // Silah / taret
    public float rate;
    public float damage = 1f;
    public float speed = 380f;
    public float spread = 0.25f;
    public float range = 500f;
    public int pellets = 1;    // tek atista cikan mermi sayisi (saçma); her biri 'damage' vurur
    public int pierce;         // delinen dusman sayisi (ray tufegi)

    /// <summary>Atis basina beklenen hasar: saçma peletlerinin ~%70'i isabet eder (DPS gostergesi ve hangar kapisi).</summary>
    public float ShotDamage => pellets > 1 ? damage * pellets * 0.7f : damage;

    // Lazer (yalniz silahlar): 0 = mermi, 1 = kesintisiz isin, 2 = darbeli isin, 3 = portal lazeri (burnun onunde
    // acilan portaldan darbeli isin). Isin aninda vurur ve onundeki herkesi deler; rate = ORTALAMA vurus/sn
    // (darbeli isinda aciklik orani beamOn / (beamOn + beamOff) ile bolunerek aciktaki vurus hizi bulunur).
    public int beam;
    public float beamOn = 0.45f;
    public float beamOff = 0.55f;

    // Fuze
    public int ammo;           // rampa basina azami fuze
    public float reload;       // bir fuzeyi yenileme suresi (sn)
    public float turn = 100f;  // donus hizi (derece/sn)
    public float blast = 12f;  // patlama yaricapi (m)
}

[System.Serializable]
public struct PaintScheme
{
    public string name;
    public Color primary;
    public Color secondary;
    public Color accent;
    public bool original;
}

public static class PartCatalog
{
    public const int WeaponSlots = 4;
    public const int ShieldSlots = 3;
    public const int TurretSlots = 2;
    public const int MissileSlots = 2;

    public const float BaseWeight = 100f;

    // Ucak adlari AircraftCatalog'dan gelir (hangar sekmeleri)
    public static string[] ModelNames
    {
        get
        {
            var names = new string[AircraftCatalog.All.Length];
            for (int i = 0; i < names.Length; i++) names[i] = AircraftCatalog.All[i].name;
            return names;
        }
    }

    public static readonly PaintScheme[] Paints =
    {
        new PaintScheme { name = "Orijinal", original = true },
        new PaintScheme { name = "Çöl",       primary = new Color(0.76f, 0.65f, 0.42f), secondary = new Color(0.86f, 0.78f, 0.58f), accent = new Color(0.45f, 0.28f, 0.14f) },
        new PaintScheme { name = "Kış",       primary = new Color(0.90f, 0.93f, 0.96f), secondary = new Color(0.72f, 0.78f, 0.85f), accent = new Color(0.15f, 0.35f, 0.75f) },
        new PaintScheme { name = "Kızıl Baron", primary = new Color(0.72f, 0.08f, 0.08f), secondary = new Color(0.55f, 0.06f, 0.06f), accent = new Color(0.08f, 0.08f, 0.08f) },
        new PaintScheme { name = "Gece",      primary = new Color(0.08f, 0.10f, 0.22f), secondary = new Color(0.14f, 0.16f, 0.30f), accent = new Color(0.95f, 0.80f, 0.15f) },
        new PaintScheme { name = "Orman",     primary = new Color(0.17f, 0.30f, 0.14f), secondary = new Color(0.30f, 0.26f, 0.15f), accent = new Color(0.85f, 0.85f, 0.85f) },
    };

    public static readonly PartDef[] Engines =
    {
        new PartDef { name = "Standart Motor", description = "Dengeli motor. 2 kanatlı pervane. Güç kapasitesi 10.",
                      power = 10f, weight = 0f, thrust = 1f, maxSpeed = 1f, blades = 2 },
        new PartDef { name = "Turbo Motor", description = "Daha fazla itki ve hız. 4 kanatlı pervane. Güç kapasitesi 14 ama ağır.",
                      power = 14f, weight = 14f, thrust = 1.3f, maxSpeed = 1.12f, blades = 4 },
        new PartDef { name = "Hafif Motor", description = "Hafif ve ekonomik. 3 kanatlı pervane. Güç kapasitesi sadece 8.",
                      power = 8f, weight = -8f, thrust = 0.9f, maxSpeed = 0.97f, blades = 3 },
        new PartDef { name = "Jet Motoru", description = "Çok güçlü itki ve yüksek hız. Güç kapasitesi 17; ağır ve pahalı.",
                      power = 17f, weight = 22f, thrust = 1.45f, maxSpeed = 1.2f, blades = 4 },
    };

    public static readonly PartDef[] Wings =
    {
        new PartDef { name = "Standart Kanat", description = "Dengeli kaldırma ve yatış.",
                      span = 1f, cruise = 1f, roll = 1f, maxG = 1f, maxSpeed = 1f, weight = 0f },
        new PartDef { name = "Uzun Kanat", description = "Düşük stall hızı, kolay süzülme. Yatış yavaş, G sınırı düşük.",
                      span = 1.3f, cruise = 0.85f, roll = 0.8f, maxG = 0.9f, maxSpeed = 0.95f, weight = 6f },
        new PartDef { name = "Kısa Kanat", description = "Çok çevik ve hızlı; ama yüksek stall hızı.",
                      span = 0.78f, cruise = 1.12f, roll = 1.35f, maxG = 1.1f, maxSpeed = 1.06f, weight = -2f },
        new PartDef { name = "Delta Kanat", description = "Yüksek hızda kararlı ve çevik; düşük hızda süzülmez (yüksek stall hızı).",
                      span = 0.9f, cruise = 1.2f, roll = 1.15f, maxG = 1.25f, maxSpeed = 1.1f, weight = 4f },
    };

    public static readonly PartDef[] Armors =
    {
        new PartDef { name = "Hafif Gövde", description = "Standart gövde. 12 can.", hp = 12f, weight = 0f },
        new PartDef { name = "Çelik Zırh", description = "Daha dayanıklı: 18 can. Ağırlık +14.", hp = 18f, weight = 14f },
        new PartDef { name = "Ağır Zırh", description = "Çok dayanıklı: 28 can. Ağırlık +30, çevikliği düşürür.", hp = 28f, weight = 30f },
    };

    // Index 0 = bos
    public static readonly PartDef[] Weapons =
    {
        new PartDef { name = "— Boş —", description = "Bu silah noktası boş." },
        new PartDef { name = "Makineli Tüfek", description = "Hızlı atış (12/sn), 1 hasar. Güç 1.",
                      rate = 12f, damage = 1f, speed = 380f, spread = 0.25f, power = 1f, weight = 4f },
        new PartDef { name = "Ağır Top", description = "Yavaş (3/sn) ama mermi başına 2 hasar. Güç 2.",
                      rate = 3f, damage = 2f, speed = 330f, spread = 0.1f, power = 2f, weight = 10f },
        new PartDef { name = "Çift Namlu", description = "Çok hızlı (18/sn), dağınık. Güç 2.",
                      rate = 18f, damage = 1f, speed = 400f, spread = 0.6f, power = 2f, weight = 7f },
        new PartDef { name = "Lazer Topu", description = "Kesintisiz ışın: anında vurur, önündeki herkesi deler (6 vuruş/sn, 2 hasar). Güç 3.",
                      rate = 6f, damage = 2f, range = 750f, beam = 1, power = 3f, weight = 9f },
        new PartDef { name = "Darbeli Lazer", description = "Kısa, güçlü ışın patlamaları (0,45 sn açık / 0,55 sn kapalı, açıkken 10 vuruş/sn, 3 hasar), delici. Güç 3.",
                      rate = 4.5f, damage = 3f, range = 850f, beam = 2, beamOn = 0.45f, beamOff = 0.55f, power = 3f, weight = 8f },
        new PartDef { name = "Portal Lazeri", description = "Burnunun önünde bir portal açılır, ışın oradan darbeler halinde çıkar (açık 0,6 / kapalı 0,6 sn), delici. Güç 4.",
                      rate = 5f, damage = 3f, range = 850f, beam = 3, beamOn = 0.6f, beamOff = 0.6f, power = 4f, weight = 11f },
        new PartDef { name = "Saçma Topu", description = "Yakın mesafe canavarı: her atışta 7 saçma (1,8 atış/sn), 350 m menzil. Güç 2.",
                      rate = 1.8f, damage = 1f, speed = 330f, spread = 5f, range = 350f, pellets = 7, power = 2f, weight = 9f },
        new PartDef { name = "Ray Tüfeği", description = "Yavaş ama devasa: mermi başına 6 hasar, hızlı mermi, 3 düşmanı delip geçer (1,2 atış/sn). Güç 4.",
                      rate = 1.2f, damage = 6f, speed = 900f, spread = 0f, range = 1500f, pierce = 3, power = 4f, weight = 12f },
    };

    public static readonly PartDef[] Shields =
    {
        new PartDef { name = "— Boş —", description = "Bu slotta kalkan jeneratörü yok." },
        new PartDef { name = "Kalkan Jeneratörü", description = "+4 kalkan, saniyede +0.6 yenilenir. Güç 2.",
                      shield = 4f, regen = 0.6f, power = 2f, weight = 5f },
        new PartDef { name = "Güçlü Jeneratör", description = "+8 kalkan, saniyede +1.0 yenilenir. Güç 4.",
                      shield = 8f, regen = 1.0f, power = 4f, weight = 10f },
    };

    // Kanat altina takilir. F tusuyla ates edilir; kilitli (veya burun onundeki) dusmani takip eder.
    public static readonly PartDef[] Missiles =
    {
        new PartDef { name = "— Boş —", description = "Bu kanat altı rampası boş." },
        new PartDef { name = "Güdümlü Füze", description = "Hedefi takip eder, 3 hasar (3 vuruş), 12 m patlama. Rampa başına 3 füze, 8 sn'de bir yenilenir. Güç 2.",
                      damage = 3f, speed = 170f, turn = 110f, blast = 12f, range = 1400f, ammo = 3, reload = 8f, power = 2f, weight = 8f },
        new PartDef { name = "Ağır Füze", description = "Daha yavaş ve hantal ama 5 hasar, 16 m patlama. Rampa başına 2 füze, 14 sn'de bir yenilenir. Güç 3.",
                      damage = 5f, speed = 145f, turn = 80f, blast = 16f, range = 1400f, ammo = 2, reload = 14f, power = 3f, weight = 14f },
        new PartDef { name = "Sürü Füzesi", description = "Küçük, çevik ve bol: 2 hasar, 9 m patlama. Rampa başına 6 füze, 5 sn'de bir yenilenir. Güç 2.",
                      damage = 2f, speed = 190f, turn = 170f, blast = 9f, range = 1400f, ammo = 6, reload = 5f, power = 2f, weight = 10f },
    };

    public static readonly PartDef[] Turrets =
    {
        new PartDef { name = "— Boş —", description = "Bu slotta taret yok." },
        new PartDef { name = "Otomatik Taret", description = "En yakın düşmana kendiliğinden ateş eder (5/sn, 450 m). Güç 2.",
                      rate = 5f, damage = 1f, speed = 360f, spread = 1.2f, range = 450f, power = 2f, weight = 8f },
        new PartDef { name = "Ağır Taret", description = "Yavaş ama 2 hasar verir (2/sn, 600 m). Güç 3.",
                      rate = 2f, damage = 2f, speed = 340f, spread = 0.6f, range = 600f, power = 3f, weight = 14f },
    };
}
