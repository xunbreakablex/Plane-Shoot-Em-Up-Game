// Test botu girisi (Assets/Editor/Bots): klavyeye eklenir. Odakta olmayan editorde enjekte edilen klavye olaylari
// oyuna ulasmiyordu; bot ates ve kaymayi buradan verir. Oyunda hep sifir.
public static class BotInput
{
    public static bool Fire;
    public static float Strafe;   // boss fazi: -1 sol, +1 sag

    public static void Clear()
    {
        Fire = false;
        Strafe = 0f;
    }
}
