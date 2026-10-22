using UnityEngine;

// Darius tarzi boss girisi: boss dogmadan once BossWarnSeconds boyunca "UYARI". Muzik susar, siren calar; ekranin
// ustunde ve altinda kayan kirmizi UYARI seritleri, ortada "UYARI!!" ve "<AD> HIZLA YAKLASIYOR". Sure dolunca boss
// dogar, boss muzigi hemen girer. Uyari surerken senaryo dalgalari durur (havadaki ucaklar boss fazinda cekilir).
// Test komutu (DebugSpawnBoss) uyarisiz dogurur.
public partial class CombatManager
{
    private const float BossWarnSeconds = 3.2f;

    private bool bossWarnPending;
    private float bossWarnStart = -10f;
    private BossType bossWarnType;
    private EnemyKind bossWarnKind;

    /// <summary>Boss uyarisi suruyor (muzik susar, dalgalar durur).</summary>
    public bool BossWarningActive => bossWarnPending;

    private void WarnThenSpawnBoss(BossType type, EnemyKind kind)
    {
        if (bossWarnPending) return;
        bossWarnPending = true;
        bossWarnStart = Time.time;
        bossWarnType = type;
        bossWarnKind = kind;
        if (uiAudio != null) uiAudio.PlayOneShot(Sfx.Siren, 0.8f);
        FollowCamera.AddShake(0.25f, BossWarnSeconds * 0.8f);
    }

    private void CancelBossWarning()
    {
        bossWarnPending = false;
        bossWarnStart = -10f;
    }

    // UpdateDirector'dan: sure dolunca boss dogar
    private void UpdateBossWarning()
    {
        if (!bossWarnPending) return;
        if (RunState.Ended || RunState.StageIsClear) { CancelBossWarning(); return; }
        if (Time.time - bossWarnStart < BossWarnSeconds) return;
        bossWarnPending = false;
        SpawnBossEncounter(bossWarnType, bossWarnKind);
    }

    private void DrawBossWarning()
    {
        if (!bossWarnPending) return;
        float age = Time.time - bossWarnStart;
        float a = Mathf.Clamp01(age / 0.25f) * Mathf.Clamp01((BossWarnSeconds - age) / 0.35f);
        float blink = 0.6f + 0.4f * Mathf.Abs(Mathf.Sin(age * Mathf.PI * 2.5f));
        Color red = new Color(1f, 0.16f, 0.1f);

        DrawVignette(red, 0.32f * a * blink);

        // Kayan seritler: ust ve alt, ters yonlerde
        float bandH = 30f;
        DrawWarnBand(Hud.H * 0.22f, bandH, age * 160f, a);
        DrawWarnBand(Hud.H * 0.70f, bandH, -age * 160f, a);

        // Orta yazi
        float cy = Hud.H * 0.30f;
        int big = Mathf.RoundToInt(58f * (1f + 0.15f * Mathf.Exp(-age * 6f)));
        Hud.Text(new Rect(0f, cy, Hud.W, big * 1.3f), "UYARI!!", new Color(1f, 0.25f, 0.18f, a * blink), big, TextAnchor.UpperCenter);

        string what = bossWarnKind == EnemyKind.FinalBoss ? "SON DÜŞMAN" : (bossWarnKind >= EnemyKind.Boss ? "DEV SAVAŞ MAKİNESİ" : "ZIRHLI DÜŞMAN");
        string line = $"{what}  «{BossBrain.TitleOf(bossWarnType)}»  HIZLA YAKLAŞIYOR";
        float lineW = Mathf.Min(Hud.W - 40f, 900f);
        Rect lr = new Rect(Hud.W * 0.5f - lineW * 0.5f, cy + big * 1.35f, lineW, 36f);
        Hud.Fill(lr, new Color(0f, 0f, 0f, 0.45f * a));
        Hud.Text(lr, line, new Color(1f, 0.85f, 0.75f, a), 22, TextAnchor.MiddleCenter);
    }

    private static void DrawWarnBand(float y, float h, float scroll, float a)
    {
        // Opak: alttaki panel yazilari (sag kolon) seritten gorunmesin
        Hud.Fill(new Rect(0f, y, Hud.W, h), new Color(0.6f, 0.04f, 0.02f, 0.88f * a));
        Hud.Fill(new Rect(0f, y, Hud.W, 2f), new Color(1f, 0.4f, 0.3f, 0.9f * a));
        Hud.Fill(new Rect(0f, y + h - 2f, Hud.W, 2f), new Color(1f, 0.4f, 0.3f, 0.9f * a));
        const float spacing = 190f;
        float off = Mathf.Repeat(scroll, spacing);
        for (float x = -spacing + off; x < Hud.W + spacing; x += spacing)
            Hud.Text(new Rect(x, y, spacing, h), "UYARI", new Color(1f, 0.92f, 0.85f, 0.95f * a), 18, TextAnchor.MiddleCenter);
    }
}
