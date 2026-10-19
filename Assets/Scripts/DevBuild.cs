// Test araclari (R/P ile yeniden dogma, savasta ucak degistirme, konsol) yalniz editorde ve development build'de acik.
// Yayin build'inde false olur: kodlar derlenir ama calismaz ve yardim panelinde gorunmez.
public static class DevBuild
{
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public const bool Enabled = true;
#else
    public const bool Enabled = false;
#endif
}
