using UnityEngine;

// Insansi / varlik bosslar (ozgun tasarimlar): Iblis (kanatli seytan), Melek, Ki Savascisi, Yildiz Buyucusu.
// Koordinat: +Z on (oyuncuya bakar), +Y yukari, +X sag. Parca adlari BossBrain.Types3.cs'deki Get() cagrilariyla eslesmeli.
public static partial class BossBuilder
{
    // ===================================================================== IBLIS

    private static void BuildDemon(Transform root)
    {
        Color red = C(0.55f, 0.06f, 0.06f), dark = C(0.14f, 0.07f, 0.1f), bone = C(0.85f, 0.8f, 0.7f);
        Color eye = C(1f, 0.85f, 0.1f), fire = C(1f, 0.4f, 0.05f), mem = C(0.32f, 0.04f, 0.14f);

        Part(root, PrimitiveType.Sphere, "Torso", V(0, 0, 0), V(7.5f, 9f, 6f), red);
        Part(root, PrimitiveType.Sphere, "Chest", V(0, 1.6f, 1.8f), V(6.2f, 4.6f, 3.4f), dark);
        Part(root, PrimitiveType.Capsule, "Lower", V(0, -6f, -0.3f), V(4.6f, 3.6f, 4f), dark);
        Part(root, PrimitiveType.Sphere, "Head", V(0, 6.6f, 1.2f), V(4.4f, 4.4f, 4.6f), red);
        Part(root, PrimitiveType.Cube, "Jaw", V(0, 5.2f, 3.2f), V(2.8f, 1.0f, 2.4f), dark);
        Part(root, PrimitiveType.Capsule, "HornL", V(-1.9f, 8.7f, 0.6f), V(0.9f, 2.6f, 0.9f), bone, V(-20, 0, 32));
        Part(root, PrimitiveType.Capsule, "HornR", V(1.9f, 8.7f, 0.6f), V(0.9f, 2.6f, 0.9f), bone, V(-20, 0, -32));
        Part(root, PrimitiveType.Sphere, "EyeL", V(-1.1f, 6.9f, 3.2f), V(0.9f, 0.7f, 0.5f), eye, default, true);
        Part(root, PrimitiveType.Sphere, "EyeR", V(1.1f, 6.9f, 3.2f), V(0.9f, 0.7f, 0.5f), eye, default, true);

        // Kollar: ates kureleri tutar
        for (int s = -1; s <= 1; s += 2)
        {
            string side = s < 0 ? "L" : "R";
            Transform arm = Pivot(root, "Arm" + side, V(s * 4.4f, 2.4f, 0.8f));
            Part(arm, PrimitiveType.Capsule, "ArmBone" + side, V(s * 1.0f, -0.2f, 2.4f), V(1.5f, 2.8f, 1.5f), red, V(70, 0, s * -10));
            Part(arm, PrimitiveType.Sphere, "Orb" + side, V(s * 1.2f, -0.2f, 5.6f), V(3.2f, 3.2f, 3.2f), fire, default, true);
            Pivot(arm, "MuzzleOrb" + side, V(s * 1.2f, -0.2f, 6.8f));
        }

        // Yarasa kanatlari
        for (int s = -1; s <= 1; s += 2)
        {
            string side = s < 0 ? "L" : "R";
            Transform wing = Pivot(root, "Wing" + side, V(s * 3.4f, 3.4f, -2f));
            Part(wing, PrimitiveType.Capsule, "WingArm" + side, V(s * 6.5f, 2.2f, -1f), V(1.1f, 7f, 1.1f), dark, V(0, 0, s * -62));
            for (int f = 0; f < 3; f++)
            {
                Part(wing, PrimitiveType.Capsule, "Finger" + side + f, V(s * (7.5f + f * 3.4f), -1.2f - f * 1.6f, -2.6f - f * 1.6f),
                    V(0.6f, 6.5f - f * 0.8f, 0.6f), dark, V(0, 0, s * (-105 + f * 14)));
            }
            Part(wing, PrimitiveType.Cube, "WingMem" + side, V(s * 11f, -1.6f, -3.2f), V(15f, 0.3f, 9f), mem, V(0, s * -12f, 0));
        }

        // Kuyruk (mizrakli uc)
        for (int i = 0; i < 4; i++)
            Part(root, PrimitiveType.Sphere, "Tail" + i, V(0, -8f - i * 1.2f, -2.2f - i * 2.8f), V(1.6f - i * 0.25f, 1.6f - i * 0.25f, 2.4f), red);
        Part(root, PrimitiveType.Cube, "TailSpade", V(0, -12.4f, -13.4f), V(0.5f, 3f, 3f), fire, V(0, 0, 45), true);

        Pivot(root, "MuzzleMouth", V(0, 5.4f, 4.8f));
    }

    // ===================================================================== MELEK

    private static void BuildAngel(Transform root)
    {
        Color white = C(0.94f, 0.94f, 0.97f), gold = C(0.98f, 0.8f, 0.25f), skin = C(0.96f, 0.82f, 0.7f);
        Color glow = C(1f, 0.95f, 0.6f), hair = C(0.98f, 0.9f, 0.55f), blue = C(0.45f, 0.65f, 0.95f);

        Part(root, PrimitiveType.Capsule, "Robe", V(0, -3.2f, 0), V(5.4f, 5.2f, 4.6f), white);
        Part(root, PrimitiveType.Sphere, "Torso", V(0, 2.2f, 0), V(5.4f, 6f, 4f), white);
        Part(root, PrimitiveType.Cube, "Sash", V(0, 0.4f, 0.1f), V(5.6f, 0.9f, 4.2f), blue);
        Part(root, PrimitiveType.Sphere, "Head", V(0, 6.8f, 0.4f), V(3.8f, 4f, 3.8f), skin);
        Part(root, PrimitiveType.Sphere, "Hair", V(0, 7.2f, -0.5f), V(4.3f, 4.4f, 4.2f), hair);
        Part(root, PrimitiveType.Sphere, "EyeL", V(-0.8f, 6.9f, 2.0f), V(0.55f, 0.7f, 0.3f), blue, default, true);
        Part(root, PrimitiveType.Sphere, "EyeR", V(0.8f, 6.9f, 2.0f), V(0.55f, 0.7f, 0.3f), blue, default, true);

        // Hale: donen kucuk altin kureler
        Transform halo = Pivot(root, "Halo", V(0, 10.4f, 0.2f));
        for (int i = 0; i < 14; i++)
        {
            float a = Mathf.PI * 2f * i / 14f;
            Part(halo, PrimitiveType.Sphere, "HaloBead" + i, V(Mathf.Cos(a) * 3.2f, 0f, Mathf.Sin(a) * 3.2f), V(0.8f, 0.8f, 0.8f), glow, default, true);
        }

        // Kanatlar: yelpaze gibi tuyler
        for (int s = -1; s <= 1; s += 2)
        {
            string side = s < 0 ? "L" : "R";
            Transform wing = Pivot(root, "Wing" + side, V(s * 2.6f, 3.6f, -1.8f));
            for (int f = 0; f < 6; f++)
            {
                // Yukari ve disa acilan yelpaze: alttaki tuyler kisa ve yatik, ustteki uzun ve dik
                float len = 9f + f * 1.1f;
                Part(wing, PrimitiveType.Cube, "Feather" + side + f, V(s * (4.2f + f * 0.5f), 0.2f + f * 1.1f, -f * 0.4f),
                    V(len, 0.4f, 3.4f), f % 2 == 0 ? white : gold, V(0, 0, s * (6 + f * 13f)));
            }
        }

        // Kollar ve mizrak
        for (int s = -1; s <= 1; s += 2)
        {
            string side = s < 0 ? "L" : "R";
            Part(root, PrimitiveType.Capsule, "Arm" + side, V(s * 3.6f, 2.6f, 1.6f), V(1.3f, 2.6f, 1.3f), white, V(60, 0, s * -12));
        }
        Part(root, PrimitiveType.Cylinder, "Spear", V(4.4f, 2.6f, 5f), V(0.35f, 7.5f, 0.35f), gold, V(90, 0, 0));
        Part(root, PrimitiveType.Cube, "SpearTip", V(4.4f, 2.6f, 12.8f), V(0.9f, 0.9f, 3.2f), glow, V(0, 0, 45), true);
        Pivot(root, "MuzzleStaff", V(4.4f, 2.6f, 14.6f));
        Pivot(root, "MuzzleHalo", V(0, 10.4f, 1.8f));
    }

    // ============================================================ KI SAVASCISI

    private static void BuildKiWarrior(Transform root)
    {
        Color gi = C(0.9f, 0.45f, 0.08f), blue = C(0.12f, 0.2f, 0.62f), skin = C(0.93f, 0.74f, 0.58f);
        Color hair = C(1f, 0.9f, 0.3f), ki = C(0.5f, 0.85f, 1f), dark = C(0.1f, 0.1f, 0.14f), white = C(0.95f, 0.95f, 0.95f);

        Part(root, PrimitiveType.Sphere, "Torso", V(0, 0, 0), V(6.4f, 8f, 4f), gi);
        Part(root, PrimitiveType.Cube, "Belt", V(0, -3.2f, 0), V(6.4f, 1.2f, 4.2f), blue);
        Part(root, PrimitiveType.Capsule, "LegL", V(-1.5f, -7f, 0), V(2.5f, 4f, 2.5f), gi);
        Part(root, PrimitiveType.Capsule, "LegR", V(1.5f, -7f, 0), V(2.5f, 4f, 2.5f), gi);
        Part(root, PrimitiveType.Cube, "BootL", V(-1.5f, -11.2f, 0.4f), V(2.8f, 1.6f, 3.6f), blue);
        Part(root, PrimitiveType.Cube, "BootR", V(1.5f, -11.2f, 0.4f), V(2.8f, 1.6f, 3.6f), blue);
        Part(root, PrimitiveType.Sphere, "Head", V(0, 6f, 0.6f), V(3.8f, 4f, 3.8f), skin);
        Part(root, PrimitiveType.Sphere, "EyeL", V(-0.9f, 6.2f, 2.3f), V(0.7f, 0.5f, 0.3f), white, default, true);
        Part(root, PrimitiveType.Sphere, "EyeR", V(0.9f, 6.2f, 2.3f), V(0.7f, 0.5f, 0.3f), white, default, true);
        Part(root, PrimitiveType.Cube, "Brow", V(0, 6.9f, 2.2f), V(3f, 0.3f, 0.4f), dark);

        // Sivri sac (parlak): arkaya ve yukari kirik dilimler
        Transform hairRoot = Pivot(root, "Hair", V(0, 8f, 0.2f));
        for (int i = 0; i < 7; i++)
        {
            float a = -60f + i * 20f;
            float len = i == 3 ? 6.2f : 4.6f - Mathf.Abs(i - 3) * 0.35f;
            Part(hairRoot, PrimitiveType.Cube, "HairSpike" + i, V(Mathf.Sin(a * Mathf.Deg2Rad) * 2.2f, len * 0.4f, -0.6f),
                V(1.1f, len, 1.1f), hair, V(-12, 0, -a * 0.6f), true);
        }

        // Aura alevleri
        Transform aura = Pivot(root, "Aura", V(0, 0, -0.6f));
        for (int i = 0; i < 8; i++)
        {
            float a = Mathf.PI * 2f * i / 8f;
            Part(aura, PrimitiveType.Cube, "AuraFlame" + i, V(Mathf.Cos(a) * 5.6f, Mathf.Sin(a) * 7.4f, 0f), V(0.9f, 5.2f, 0.9f), ki,
                V(0, 0, a * Mathf.Rad2Deg - 90f), true, false);
        }

        // Kollar: yumruklarda ki kuresi
        for (int s = -1; s <= 1; s += 2)
        {
            string side = s < 0 ? "L" : "R";
            Transform arm = Pivot(root, "Arm" + side, V(s * 3.9f, 2.4f, 0.6f));
            Part(arm, PrimitiveType.Capsule, "ArmBone" + side, V(s * 0.8f, -0.4f, 2.5f), V(1.9f, 2.8f, 1.9f), skin, V(75, 0, s * -8));
            Part(arm, PrimitiveType.Sphere, "Fist" + side, V(s * 0.9f, -0.4f, 5.6f), V(2.6f, 2.6f, 2.6f), ki, default, true);
            Pivot(arm, "MuzzleHand" + side, V(s * 0.9f, -0.4f, 7.0f));
        }
    }

    // ======================================================= YILDIZ BUYUCUSU

    private static void BuildMagicalGirl(Transform root)
    {
        Color pink = C(1f, 0.55f, 0.75f), white = C(0.97f, 0.96f, 0.98f), blue = C(0.4f, 0.65f, 0.98f);
        Color skin = C(0.99f, 0.84f, 0.74f), hair = C(1f, 0.82f, 0.35f), gold = C(1f, 0.85f, 0.3f);
        Color star = C(1f, 0.95f, 0.5f), eye = C(0.5f, 0.2f, 0.9f), wing = C(0.8f, 0.9f, 1f);

        Part(root, PrimitiveType.Sphere, "Skirt", V(0, -3.2f, 0), V(9.5f, 5.4f, 9.5f), pink);
        Part(root, PrimitiveType.Cylinder, "SkirtTrim", V(0, -5.4f, 0), V(9.6f, 0.35f, 9.6f), white, default, false, false);
        Part(root, PrimitiveType.Cube, "Bodice", V(0, 1.4f, 0), V(3.8f, 4.2f, 2.8f), white);
        Part(root, PrimitiveType.Sphere, "Head", V(0, 6f, 0.4f), V(4f, 4.2f, 4f), skin);
        Part(root, PrimitiveType.Sphere, "HairBack", V(0, 6.6f, -0.9f), V(5f, 5f, 4.6f), hair);
        Part(root, PrimitiveType.Sphere, "Bangs", V(0, 7.6f, 1.2f), V(4.2f, 1.6f, 2.2f), hair);
        Part(root, PrimitiveType.Sphere, "EyeL", V(-1f, 6.1f, 2.2f), V(0.9f, 1.1f, 0.35f), eye, default, true);
        Part(root, PrimitiveType.Sphere, "EyeR", V(1f, 6.1f, 2.2f), V(0.9f, 1.1f, 0.35f), eye, default, true);

        // Iki yandan uzun kuyruklar (sallanir)
        for (int s = -1; s <= 1; s += 2)
        {
            string side = s < 0 ? "L" : "R";
            Transform tail = Pivot(root, "Tail" + side, V(s * 3.2f, 7.2f, -1f));
            for (int i = 0; i < 5; i++)
                Part(tail, PrimitiveType.Sphere, "TailBall" + side + i, V(s * (0.6f + i * 0.5f), -i * 2.6f, -i * 0.4f),
                    V(2.6f - i * 0.3f, 3.2f, 2.6f - i * 0.3f), hair);
            Part(tail, PrimitiveType.Sphere, "TailBow" + side, V(0, 0, 0), V(1.3f, 1.3f, 1.3f), pink);
        }

        // Buyuk kurdele
        Part(root, PrimitiveType.Cube, "BowL", V(-2.2f, 2.6f, 1.7f), V(3.2f, 2.4f, 0.9f), pink, V(0, 0, 20));
        Part(root, PrimitiveType.Cube, "BowR", V(2.2f, 2.6f, 1.7f), V(3.2f, 2.4f, 0.9f), pink, V(0, 0, -20));
        Part(root, PrimitiveType.Sphere, "Brooch", V(0, 2.6f, 2.1f), V(1.5f, 1.5f, 1.2f), star, default, true);

        // Peri kanatlari
        for (int s = -1; s <= 1; s += 2)
        {
            string side = s < 0 ? "L" : "R";
            Transform w = Pivot(root, "Wing" + side, V(s * 1.8f, 3f, -1.6f));
            Part(w, PrimitiveType.Cube, "WingUp" + side, V(s * 4.6f, 3.2f, -0.6f), V(7.5f, 0.3f, 4.6f), wing, V(0, 0, s * 30));
            Part(w, PrimitiveType.Cube, "WingDown" + side, V(s * 3.8f, -1.2f, -0.6f), V(5.6f, 0.3f, 3.4f), wing, V(0, 0, s * -22));
        }

        // Kol ve degnek (yildiz uclu)
        Part(root, PrimitiveType.Capsule, "ArmL", V(-2.8f, 2.2f, 1f), V(1.1f, 2.2f, 1.1f), skin, V(45, 0, 15));
        Part(root, PrimitiveType.Capsule, "ArmR", V(3.2f, 2.4f, 2f), V(1.1f, 2.4f, 1.1f), skin, V(75, 0, -8));
        Part(root, PrimitiveType.Cylinder, "WandStick", V(3.6f, 2.6f, 5f), V(0.3f, 4f, 0.3f), gold, V(90, 0, 0));
        Transform starPivot = Pivot(root, "Star", V(3.6f, 2.6f, 9.6f));
        for (int i = 0; i < 5; i++)
            Part(starPivot, PrimitiveType.Cube, "StarPoint" + i, V(0, 0, 0), V(0.7f, 3.6f, 0.7f), star, V(0, 0, i * 72), true, false);
        Part(starPivot, PrimitiveType.Sphere, "StarCore", V(0, 0, 0), V(1.8f, 1.8f, 1.8f), white, default, true, false);
        Pivot(root, "MuzzleStar", V(3.6f, 2.6f, 10.6f));
    }
}
