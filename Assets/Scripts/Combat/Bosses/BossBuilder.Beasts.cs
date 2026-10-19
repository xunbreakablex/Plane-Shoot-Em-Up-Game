using UnityEngine;

// Boss gövdeleri: Çelik Kartal (mekanik yirtici kus) ve Tas Dev (yüzen kaya golem). Özgün tasarimlar.
// Koordinat: +Z on (oyuncuya bakar), +Y yukari, +X sag. Parca adlari BossBrain.Types4.cs'deki Get() cagrilariyla eslesmeli.
public static partial class BossBuilder
{
    // ================================================================== KARTAL

    private static void BuildEagle(Transform root)
    {
        Color steel = C(0.42f, 0.46f, 0.52f), dark = C(0.12f, 0.13f, 0.17f), gold = C(0.95f, 0.75f, 0.2f);
        Color red = C(0.75f, 0.1f, 0.08f), glow = C(1f, 0.3f, 0.15f), light = C(0.7f, 0.74f, 0.8f);

        Part(root, PrimitiveType.Sphere, "Body", V(0, 0, 0), V(6.5f, 5.6f, 13f), steel);
        Part(root, PrimitiveType.Sphere, "Chest", V(0, -0.6f, 3.6f), V(5.6f, 4.8f, 6f), light);
        Part(root, PrimitiveType.Sphere, "Head", V(0, 2.2f, 7.4f), V(3.8f, 3.6f, 4.2f), steel);
        Part(root, PrimitiveType.Cube, "Beak", V(0, 1.5f, 10f), V(1.5f, 1.3f, 3.2f), gold, V(14, 0, 0));
        Part(root, PrimitiveType.Cube, "BeakHook", V(0, 0.6f, 11.4f), V(0.9f, 1.2f, 1.2f), gold);
        Part(root, PrimitiveType.Sphere, "EyeL", V(-1.2f, 3.0f, 8.8f), V(0.8f, 0.6f, 0.6f), glow, default, true);
        Part(root, PrimitiveType.Sphere, "EyeR", V(1.2f, 3.0f, 8.8f), V(0.8f, 0.6f, 0.6f), glow, default, true);
        Part(root, PrimitiveType.Cube, "Crest", V(0, 4.4f, 6.8f), V(0.5f, 1.8f, 2.6f), red, V(-25, 0, 0));

        // Kanatlar: uc kat tuy sirasi, kanat ucunda namlu
        for (int s = -1; s <= 1; s += 2)
        {
            string side = s < 0 ? "L" : "R";
            Transform wing = Pivot(root, "Wing" + side, V(s * 3.2f, 1.2f, 1.5f));
            Part(wing, PrimitiveType.Cube, "WingBone" + side, V(s * 9f, 0.4f, 0f), V(18f, 0.9f, 2.8f), steel);
            for (int f = 0; f < 5; f++)
            {
                float len = 7f + f * 0.6f;
                Part(wing, PrimitiveType.Cube, "Primary" + side + f, V(s * (5f + f * 3.6f), 0.1f, -2.6f - f * 0.5f),
                    V(3.2f, 0.35f, len), f % 2 == 0 ? dark : light, V(0, s * -6f, 0));
            }
            Part(wing, PrimitiveType.Sphere, "WingPod" + side, V(s * 18.5f, 0.2f, 0.4f), V(1.8f, 1.8f, 3f), glow, default, true);
            Pivot(wing, "MuzzleWing" + side, V(s * 18.5f, 0.2f, 2.4f));
        }

        // Kuyruk: yelpaze
        for (int i = -2; i <= 2; i++)
            Part(root, PrimitiveType.Cube, "Tail" + (i + 2), V(i * 1.3f, 0.2f, -9.5f), V(1.1f, 0.35f, 6.5f), i % 2 == 0 ? dark : red, V(0, i * 9f, 0));

        // Pencereler
        for (int s = -1; s <= 1; s += 2)
            Part(root, PrimitiveType.Capsule, "Talon" + (s < 0 ? "L" : "R"), V(s * 1.6f, -3.4f, 3f), V(0.7f, 1.8f, 0.7f), gold, V(35, 0, 0));

        Pivot(root, "MuzzleBeak", V(0, 1.2f, 12.4f));
    }

    // ==================================================================== DEV

    private static void BuildGolem(Transform root)
    {
        Color rock = C(0.38f, 0.36f, 0.34f), dark = C(0.17f, 0.16f, 0.17f), moss = C(0.26f, 0.38f, 0.22f);
        Color rune = C(0.35f, 0.9f, 1f), core = C(0.5f, 1f, 0.95f);

        Part(root, PrimitiveType.Cube, "Torso", V(0, 0, 0), V(10f, 11f, 6.5f), rock);
        Part(root, PrimitiveType.Cube, "Belly", V(0, -6.6f, 0.2f), V(7.4f, 3.6f, 5.2f), dark);
        Part(root, PrimitiveType.Cube, "Moss", V(0, 5.6f, -0.4f), V(10.4f, 1f, 6.9f), moss);
        Part(root, PrimitiveType.Cube, "Head", V(0, 8.6f, 0.8f), V(4.6f, 4.2f, 4.4f), rock);
        Part(root, PrimitiveType.Cube, "Brow", V(0, 9.9f, 2.8f), V(4.9f, 0.9f, 1.2f), dark);
        Part(root, PrimitiveType.Cube, "EyeL", V(-1.2f, 8.8f, 3.1f), V(1.1f, 0.6f, 0.4f), rune, default, true);
        Part(root, PrimitiveType.Cube, "EyeR", V(1.2f, 8.8f, 3.1f), V(1.1f, 0.6f, 0.4f), rune, default, true);
        Part(root, PrimitiveType.Sphere, "Core", V(0, 0.8f, 3.4f), V(3.2f, 3.2f, 1.6f), core, default, true);
        Part(root, PrimitiveType.Cube, "RuneBar", V(0, -2.2f, 3.3f), V(6f, 0.5f, 0.3f), rune, default, true);
        Pivot(root, "MuzzleCore", V(0, 0.8f, 4.6f));

        // Omuz taslari ve yuzen yumruklar
        for (int s = -1; s <= 1; s += 2)
        {
            string side = s < 0 ? "L" : "R";
            Part(root, PrimitiveType.Sphere, "Shoulder" + side, V(s * 6.6f, 4.2f, 0f), V(4.6f, 4f, 4.6f), rock);
            Transform fist = Pivot(root, "Fist" + side, V(s * 11.5f, -1.5f, 3.5f));
            Part(fist, PrimitiveType.Cube, "FistRock" + side, V(0, 0, 0), V(5.4f, 5f, 5.4f), rock);
            Part(fist, PrimitiveType.Cube, "FistRune" + side, V(0, 0, 2.8f), V(2.4f, 2.4f, 0.4f), rune, default, true);
            Pivot(fist, "MuzzleFist" + side, V(0, 0, 3.6f));
        }

        // Alt govde: yuzen kaya parcalari
        for (int i = 0; i < 4; i++)
            Part(root, PrimitiveType.Cube, "Float" + i, V((i - 1.5f) * 2.6f, -10.5f - (i % 2) * 1.5f, 0.3f), V(2.4f, 2.4f, 2.4f), i % 2 == 0 ? rock : dark, V(20 * i, 30 * i, 10));
    }
}
