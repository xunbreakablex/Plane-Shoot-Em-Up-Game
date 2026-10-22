using System.Collections.Generic;
using UnityEngine;

// CombatManager'in HUD kismi (sanal 1280x720 izgarasi, bolgeler Hud.cs'de):
//   En ust    : asama ilerleme cubugu                  DrawStageBar
//   Ust orta  : skor, kombo, can, kalkan, boss         DrawTopCenter
//   Sag ust   : ufuk (FlightTestManager) + bilgi panel DrawRightColumn
//   Sag alt   : radar                                  DrawRadar
//   Merkez    : nisangah, yuzen yazilar, duyuru        DrawCrosshair / DrawFloatingTexts / DrawBanner
//   Sol ust, alt sol: ucus bilgisi ve ipucu            (FlightTestManager)
public partial class CombatManager
{
    private Texture2D arrowTexture;
    private float rightBlockBottom;
    private readonly List<Rect> placedTexts = new List<Rect>();
    private readonly List<EnemyPlane> markerOrder = new List<EnemyPlane>();
    private readonly List<float> markerDist = new List<float>();

    // Metin onbellekleri (degisince yeniden kurulur)
    private readonly Hud.TextCache scoreText = new Hud.TextCache();
    private readonly Hud.TextCache comboText = new Hud.TextCache();
    private readonly Hud.TextCache healthText = new Hud.TextCache();
    private readonly Hud.TextCache shieldText = new Hud.TextCache();
    private readonly Hud.TextCache bossText = new Hud.TextCache();
    private readonly Hud.TextCache scrapText = new Hud.TextCache();
    private readonly Hud.TextCache stageBarText = new Hud.TextCache();
    private readonly Hud.TextCache highScoreText = new Hud.TextCache();
    private float highScoreReadAt;
    private string bossTitleSource;
    private readonly List<ArrowMark> arrowMarks = new List<ArrowMark>();

    private const float RadarRadius = 74f;
    private const float RadarRange = 850f;

    private void OnGUI()
    {
        // Yalniz cizim: Layout / fare / klavye olaylarinda HUD'u yeniden kurmak bos is ve cop uretirdi
        if (Event.current.type != EventType.Repaint) return;

        if (cam == null) return;

        // Duraklat menusu, magaza, asama/sonuc ekranlari acikken oyun ici HUD gizlenir
        if (GameState.Paused) return;

        EnsureArrow();

        Hud.Begin();
        DrawHitFlash();
        DrawStageBar();
        DrawTopCenter();
        DrawRightColumn();      // rightBlockBottom'i hesaplar; oklar ona gore kacar
        DrawRadar();
        DrawMarkers();
        DrawBossLimbBars();
        DrawCurtainGap();
        DrawFloatingTexts();
        DrawBanner();
        DrawBottomWarnings();
        DrawVirtualStick();
        DrawCrosshair();
        DrawDamageArcs();
        Hud.End();
    }

    private void EnsureArrow()
    {
        if (arrowTexture != null) return;

        arrowTexture = new Texture2D(32, 32, TextureFormat.RGBA32, false);
        for (int y = 0; y < 32; y++)
        for (int x = 0; x < 32; x++)
        {
            // Yukari bakan ucgen
            float half = (y / 31f) * 16f;
            bool inside = Mathf.Abs(x - 15.5f) <= half * 0.9f;
            arrowTexture.SetPixel(x, 31 - y, inside ? Color.white : Color.clear);
        }
        arrowTexture.Apply();
    }

    // Boss uzuv can cubuklari: vurulan uzvun ustunde kisa sure, cani %30'un altindaki uzuvlarda surekli
    private BossBrain limbBrain;
    private EnemyPlane limbBrainOf;

    private void DrawBossLimbBars()
    {
        EnemyPlane b = ActiveBoss;
        if (b == null) return;
        if (limbBrainOf != b)
        {
            limbBrainOf = b;
            limbBrain = b.GetComponent<BossBrain>();
        }
        if (limbBrain == null) return;

        for (int i = 0; i < limbBrain.LimbCount; i++)
        {
            if (!limbBrain.LimbBar(i, out Vector3 wp, out float frac, out bool weapon)) continue;
            Vector3 sp = cam.WorldToScreenPoint(wp);
            if (sp.z <= 0f) continue;

            Vector2 h = Hud.ToHud(sp);
            const float w = 58f, hgt = 6f;
            var r = new Rect(h.x - w * 0.5f, h.y - 18f, w, hgt);
            Color fill = frac < 0.3f ? new Color(1f, 0.25f, 0.2f)
                : (frac < 0.6f ? new Color(1f, 0.75f, 0.2f) : new Color(0.45f, 0.95f, 0.45f));
            // Silah uzvu turuncu, tasiyici uzuv (kanat) acik gri cerceveli
            Hud.Fill(new Rect(r.x - 1.5f, r.y - 1.5f, r.width + 3f, r.height + 3f),
                weapon ? new Color(1f, 0.55f, 0.2f, 0.9f) : new Color(0.85f, 0.88f, 0.92f, 0.65f));
            Hud.Bar(r, frac, fill);
        }
    }

    // Perde saldirisi: duvardaki bosluk; yildiz carki: kollar arasindaki (carkla donen) bosluk. Bosluk oyuncunun
    // hizasinda ve cogu zaman ekranin disinda kalir (kamera ucagin hemen arkasinda): ucaktan bosluga ok cizilir
    // (perdede yesil, carkta pembe); oyuncu bosluktaysa ucagin cevresinde halka.
    private void DrawCurtainGap()
    {
        if (limbBrain == null || limbBrainOf == null || limbBrainOf.IsDead) return;
        if (limbBrain.CurtainGap(out Vector3 gp)) DrawGapArrow(gp, new Color(0.3f, 1f, 0.45f), 38f, "BOŞLUK", "BOŞLUKTASIN");
        else if (limbBrain.WheelGap(out Vector3 wg)) DrawGapArrow(wg, new Color(1f, 0.45f, 0.85f), 24f, "BOŞLUK", "BOŞLUKTASIN");
        else if (limbBrain.KiWaveDanger(out Vector3 kc, out float kr, out float kLeft)) DrawKiWaveExit(kc, kr, kLeft);
        else if (limbBrain.SweepDanger(out Vector3 bc, out bool vert, out bool inside)) DrawSweepExit(bc, vert, inside);
        else if (limbBrain.ZoneDanger(out Vector3 zx, out float zLeft))
        {
            // Isaretli bolgenin icindesin: ekran kenari kizarir, en yakin guvenli noktaya ok ve kalan sure
            float urgency = Mathf.Clamp01(1f - zLeft / 1.5f);
            DrawVignette(new Color(1f, 0.25f, 0.08f), (0.2f + 0.3f * urgency) * (0.8f + 0.2f * Mathf.Sin(Time.unscaledTime * 18f)));
            DrawGapArrow(zx, new Color(1f, 0.45f, 0.2f), 6f, $"KAÇ!  {Mathf.Max(0f, zLeft):0.0}", null, 1.4f);
        }
    }

    // Supurme: oyuncu kilitli seritteyse en yakin kenara buyuk "KAC" oku (yatay taramada yukari/asagi, dikeyde saga/sola)
    private void DrawSweepExit(Vector3 bandCenter, bool vertical, bool inside)
    {
        PlaneController plane = flight != null ? flight.Current : null;
        if (plane == null || plane.IsCrashed) return;
        Vector3 pp = plane.transform.position;
        if (!inside)
        {
            Vector3 psp = cam.WorldToScreenPoint(pp);
            if (psp.z > 0f)
                Hud.Text(new Rect(Hud.ToHud(psp).x - 80f, Hud.ToHud(psp).y + 40f, 160f, 18f), "ŞERİDİN DIŞINDASIN", new Color(0.4f, 1f, 0.55f), 13, TextAnchor.UpperCenter);
            return;
        }
        Vector3 right = Vector3.Cross(Vector3.up, RushHeading).normalized;
        Vector3 axis = vertical ? right : Vector3.up;
        float side = Vector3.Dot(pp - bandCenter, axis);
        // Kayma alaninin kenarina sikismissa obur yana
        float lim = vertical ? RushHalfWidth - 20f : RushUp - 20f;
        float limLo = vertical ? -RushHalfWidth + 20f : -RushDown + 20f;
        float pf = vertical ? Vector3.Dot(pp - RushOrigin, right) : pp.y - RushOrigin.y;
        float dir = side >= 0f ? 1f : -1f;
        if (pf + dir * 30f > lim || pf + dir * 30f < limLo) dir = -dir;
        Vector3 exit = pp + axis * dir * 40f;
        DrawVignette(new Color(1f, 0.3f, 0.08f), 0.22f + 0.1f * Mathf.Sin(Time.unscaledTime * 16f));
        DrawGapArrow(exit, new Color(1f, 0.35f, 0.2f), 6f, vertical ? "YANA KAÇ!" : (dir > 0f ? "YUKARI KAÇ!" : "AŞAĞI KAÇ!"), null, 1.5f);
    }

    // Ki dalgasi: cember kameranin onundeki duzlemde ekrana sigmaz. Oyuncu icindeyse ekran kenarlari kirmizi yanar (ates
    // yaklastikca guclenir), en yakin cikisa buyuk kirmizi "KAC" oku ve kalan sure; isin icindekini vurunca ekran parlar.
    // Disaridaysa yesil onay.
    private void DrawKiWaveExit(Vector3 center, float radius, float timeLeft)
    {
        PlaneController plane = flight != null ? flight.Current : null;
        if (plane == null || plane.IsCrashed) return;
        Vector3 pp = plane.transform.position;
        Vector3 right = Vector3.Cross(Vector3.up, RushHeading).normalized;
        Vector2 rel = new Vector2(Vector3.Dot(pp - center, right), pp.y - center.y);
        float dist = rel.magnitude;
        if (dist >= radius)
        {
            Vector3 psp = cam.WorldToScreenPoint(pp);
            if (psp.z > 0f && timeLeft > -0.9f)
                Hud.Text(new Rect(Hud.ToHud(psp).x - 80f, Hud.ToHud(psp).y + 40f, 160f, 18f), "DIŞARIDASIN", new Color(0.4f, 1f, 0.55f), 13, TextAnchor.UpperCenter);
            return;
        }

        bool firing = limbBrain != null && limbBrain.KiWaveFiring;
        float urgency = firing ? 1f : Mathf.Clamp01(1f - timeLeft / 2f);
        float beat = 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * Mathf.Lerp(8f, 24f, urgency));
        DrawVignette(new Color(1f, 0.12f, 0.08f), (0.25f + 0.37f * urgency) * beat);
        if (firing) Hud.Fill(new Rect(0f, 0f, Hud.W, Hud.H), new Color(0.75f, 0.95f, 1f, 0.18f * beat));

        // En kisa cikis (kayma alaninin kenarina sikismissa kenar boyunca)
        if (!limbBrain.KiWaveExit(out Vector3 exit))
        {
            Vector2 n = dist > 0.5f ? rel / dist : Vector2.right;
            exit = center + (right * n.x + Vector3.up * n.y) * (radius + 10f);
        }
        string label = timeLeft > 0f ? $"KAÇ!  {timeLeft:0.0}" : "KAÇ!";
        DrawGapArrow(exit, new Color(1f, 0.3f, 0.25f), 10f, label, null, 1.7f);
    }

    // Ekran kenarlarinda renkli parilti (ortasi saydam); tehlike bolgesindeyken
    private static Texture2D vignetteTexture;

    private static void DrawVignette(Color color, float alpha)
    {
        if (vignetteTexture == null)
        {
            const int size = 64;
            vignetteTexture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                float r = Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) * 0.55f + new Vector2(dx, dy).magnitude * 0.45f;
                vignetteTexture.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.62f, 1.02f, r))));
            }
            vignetteTexture.Apply();
        }
        GUI.color = new Color(color.r, color.g, color.b, Mathf.Clamp01(alpha));
        GUI.DrawTexture(new Rect(0f, 0f, Hud.W, Hud.H), vignetteTexture);
        GUI.color = Color.white;
    }

    private void DrawGapArrow(Vector3 gp, Color color, float gapRadius, string label, string insideLabel, float scale = 1f)
    {
        PlaneController plane = flight != null ? flight.Current : null;
        if (plane == null || plane.IsCrashed) return;

        Vector3 pp = plane.transform.position;
        Vector3 psp = cam.WorldToScreenPoint(pp);
        if (psp.z <= 0f) return;
        Vector2 ph = Hud.ToHud(psp);

        float pulse = 0.65f + 0.35f * Mathf.Sin(Time.unscaledTime * 14f);
        Color green = new Color(color.r, color.g, color.b, pulse);

        // Boslugun oyuncuya gore yeri (cercevenin sag ve yukari eksenlerinde)
        Vector3 right = Vector3.Cross(Vector3.up, RushHeading).normalized;
        float dx = Vector3.Dot(gp - pp, right), dy = gp.y - pp.y;
        if (insideLabel != null && Mathf.Abs(dx) < 26f && Mathf.Abs(dy) < 26f)
        {
            Hud.Ring(ph, 40f, green);
            Hud.Text(new Rect(ph.x - 70f, ph.y + 44f, 140f, 18f), insideLabel, green, 13, TextAnchor.UpperCenter);
            return;
        }

        // Ucaktan bosluga ok (HUD'da y asagi)
        Vector2 n = new Vector2(dx, -dy).normalized;
        float len = Mathf.Clamp(new Vector2(dx, dy).magnitude * 1.1f, 50f, 170f);
        float angle = Mathf.Atan2(n.x, -n.y) * Mathf.Rad2Deg;
        Vector2 start = ph + n * 34f;
        float shaft = 5f * scale, head = 28f * scale;
        Hud.RotateAround(angle, start);
        Hud.Fill(new Rect(start.x - shaft * 0.5f, start.y - len, shaft, len), green);
        Hud.ClearRotation();

        Vector2 tip = start + n * len;
        Hud.RotateAround(angle, tip);
        GUI.color = green;
        GUI.DrawTexture(new Rect(tip.x - head * 0.5f, tip.y - head * 0.5f, head, head), arrowTexture);
        GUI.color = Color.white;
        Hud.ClearRotation();
        int fontSize = Mathf.RoundToInt(13f * Mathf.Lerp(1f, scale, 0.6f));
        Hud.Text(new Rect(tip.x - 80f, tip.y + (n.y > 0f ? head * 0.5f : -head * 0.5f - fontSize - 6f), 160f, fontSize + 6f), label, green, fontSize, TextAnchor.UpperCenter);

        // Bosluk ekrandaysa orada da halka (boyutu boslugun gercek genisligi kadar)
        Vector3 sp = cam.WorldToScreenPoint(gp);
        if (sp.z > 1f)
        {
            Vector2 h = Hud.ToHud(sp);
            if (h.x > 0f && h.x < Hud.W && h.y > 0f && h.y < Hud.H)
            {
                float fovTan = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
                Hud.Ring(h, Mathf.Clamp(gapRadius / (sp.z * fovTan) * (Hud.H * 0.5f), 18f, 220f), green);
            }
        }
    }

    private void DrawHitFlash()
    {
        if (hitFlash > 0f)
            Hud.Fill(new Rect(0f, 0f, Hud.W, Hud.H), new Color(1f, 0f, 0f, hitFlash * 0.3f));
        if (bombFlash > 0f)
            Hud.Fill(new Rect(0f, 0f, Hud.W, Hud.H), new Color(0.9f, 0.8f, 1f, bombFlash * 0.55f));
    }

    // ------------------------------------------------------------ asama cubugu

    private void DrawStageBar()
    {
        float w = Hud.W;
        Rect bar = new Rect(0f, 0f, w, Hud.TopBar);
        Hud.Fill(bar, new Color(0f, 0f, 0f, 0.6f));

        bool fight = RunState.Phase == RunPhase.BossFight;
        EnemyPlane activeBoss = ActiveBoss;
        float frac;
        Color fill;
        if (activeBoss != null)
        {
            frac = (float)activeBoss.HitsRemaining / Mathf.Max(1, activeBoss.MaxHits);
            fill = activeBoss.IsStageBoss ? new Color(0.95f, 0.3f, 0.25f) : new Color(1f, 0.62f, 0.2f);
        }
        else if (fight || RunState.StageIsClear)
        {
            frac = 1f;
            fill = new Color(0.4f, 0.9f, 0.5f);
        }
        else
        {
            frac = WaveCount > 0 ? (float)WavesSpawned / WaveCount : 0f;
            fill = new Color(0.3f, 0.62f, 1f);
        }

        Hud.Fill(new Rect(0f, 0f, w * frac, Hud.TopBar), fill);
        Hud.Fill(new Rect(0f, Hud.TopBar - 1f, w, 1f), new Color(1f, 1f, 1f, 0.25f));

        // Etiket yalniz asama / boss durumu degisince yeniden kurulur
        int bossState = fight ? 2 : (activeBoss != null ? 1 : 0);
        if (stageBarText.Update(RunState.Stage, bossState, stage != null ? stage.title.GetHashCode() : 0))
        {
            string title = stage != null ? stage.title : "";
            stageBarText.Text = (RunState.Endless ? $"SONSUZ  •  AŞAMA {RunState.Stage + 1}" : $"AŞAMA {RunState.Stage + 1} / {RunState.StageTotal}") + $"   •   {title}"
                                + (fight ? "   •   BOSS" : (activeBoss != null ? "   •   ORTA BOSS" : ""));
        }
        Hud.Text(bar, stageBarText.Text, Color.white, 12, TextAnchor.MiddleCenter);

        // HighScore PlayerPrefs'ten okunur (kayit defteri): saniyede birkac kez yeterli
        if (Time.unscaledTime >= highScoreReadAt)
        {
            highScoreReadAt = Time.unscaledTime + 0.5f;
            int hs = RunState.HighScore;
            if (highScoreText.Update(hs)) highScoreText.Text = $"REKOR {hs:N0}";
        }
        Hud.Text(new Rect(w - 260f, 0f, 248f, Hud.TopBar), highScoreText.Text,
            new Color(0.85f, 0.92f, 1f), 11, TextAnchor.MiddleRight, false);
    }

    // --------------------------------------------------------------- ust orta

    private void DrawTopCenter()
    {
        float w = 360f;
        float x = Hud.W * 0.5f - w * 0.5f;
        float y = Hud.TopY;

        // Skor + kombo
        int score = RunState.Score;
        if (scoreText.Update(score)) scoreText.Text = score.ToString("N0");
        Hud.Text(new Rect(x, y - 2f, w, 38f), scoreText.Text, Color.white, 32, TextAnchor.MiddleCenter);

        if (Streak > 1)
        {
            Color cc = TierColor(Mathf.Clamp(StreakTier(Streak), 0, 6));
            int streak = Streak, mulI = Mathf.RoundToInt(ComboMultiplier * 10f);
            if (comboText.Update(streak, mulI)) comboText.Text = $"KOMBO x{streak}   (puan ×{mulI / 10f:0.0})";
            Hud.Text(new Rect(x, y + 34f, w, 18f), comboText.Text, cc, 13, TextAnchor.MiddleCenter);
            float remain = Mathf.Clamp01(1f - (Time.time - lastKillTime) / streakWindow);
            Hud.Bar(new Rect(x + w * 0.25f, y + 53f, w * 0.5f, 3f), remain, cc);
        }
        else
        {
            Hud.Text(new Rect(x, y + 34f, w, 18f), "SKOR", new Color(0.75f, 0.8f, 0.88f), 11, TextAnchor.MiddleCenter, false);
        }
        y += 60f;

        PlayerHealth ph = CurrentPlayerHealth;
        if (ph != null)
        {
            float frac = ph.maxHealth > 0 ? (float)ph.Health / ph.maxHealth : 0f;
            Rect hp = new Rect(x, y, w, 18f);
            Hud.Bar(hp, frac, Color.Lerp(new Color(1f, 0.25f, 0.2f), new Color(0.3f, 0.9f, 0.4f), frac));
            if (healthText.Update(ph.Health, ph.maxHealth)) healthText.Text = $"CAN  {ph.Health} / {ph.maxHealth}";
            Hud.Text(hp, healthText.Text, Color.white, 13, TextAnchor.MiddleCenter);
            y += 21f;

            if (ph.maxShield > 0f)
            {
                float sf = Mathf.Clamp01(ph.Shield / ph.maxShield);
                Rect sh = new Rect(x, y, w, 13f);
                Hud.Bar(sh, sf, new Color(0.3f, 0.75f, 1f));
                int shI = Mathf.RoundToInt(ph.Shield), shMax = Mathf.RoundToInt(ph.maxShield);
                if (shieldText.Update(shI, shMax)) shieldText.Text = $"KALKAN  {shI} / {shMax}";
                Hud.Text(sh, shieldText.Text, Color.white, 11, TextAnchor.MiddleCenter);
                y += 16f;
            }
        }
        y += 6f;

        // Boss can cubugu
        EnemyPlane bar = ActiveBoss;
        if (bar != null)
        {
            EnemyKindInfo info = EnemyKinds.Get(bar.Kind);
            float bw = 520f;
            Rect br = new Rect(Hud.W * 0.5f - bw * 0.5f, y, bw, 16f);
            float bf = Mathf.Clamp01((float)bar.HitsRemaining / Mathf.Max(1, bar.MaxHits));
            // Dolgu biraz koyu: acik renkli bosslarda (sari, beyaz) da beyaz yazi okunur
            Color mk = info.marker;
            Hud.Bar(br, bf, new Color(mk.r * 0.78f, mk.g * 0.78f, mk.b * 0.78f, 1f));
            // Cerceve: cubuk azaldikca bos kismin siniri da gorunsun
            Hud.Outline(new Rect(br.x - 2f, br.y - 2f, br.width + 4f, br.height + 4f), new Color(0f, 0f, 0f, 0.8f), 2f);
            string title = string.IsNullOrEmpty(bar.Title) ? info.title.ToUpper() : bar.Title;
            // Baslik degisirse (yeni boss) metin yeniden kurulur; sayilar degisince de
            bool titleChanged = !ReferenceEquals(bossTitleSource, title);
            if (titleChanged) bossTitleSource = title;
            if (bossText.Update(bar.HitsRemaining, bar.MaxHits) || titleChanged)
                bossText.Text = $"{title}   {bar.HitsRemaining} / {bar.MaxHits}";
            // Beyaz + golge: hem dolu (acik renk) hem bos (koyu) kisimda okunur
            Hud.Text(br, bossText.Text, Color.white, 12, TextAnchor.MiddleCenter);
        }

        // Us hedefi (komuta merkezi): boss cubugunun altinda
        DrawBaseObjective(y + (bar != null ? 22f : 0f));
    }

    // ------------------------------------------------------------- sag kolon

    // Ufkun altinda: hurda, guc seviyesi, bomba, fuze, ipucu
    private void DrawRightColumn()
    {
        float w = Hud.RightColumnWidth;
        float x = Hud.W - Hud.Margin - w;
        float y = Hud.TopY + Hud.HorizonSize + 10f;

        const float rowH = 22f;
        float h = 10f + rowH * 2f + 20f + 24f + 24f;
        if (gun != null && gun.HasMissiles) h += 20f;
        bool dash = DashStatus(out float dashReady, out string dashName);
        if (dash) h += 22f;
        bool overdrive = RunState.OverdriveActive;
        if (overdrive) h += 22f;

        Hud.Panel(new Rect(x, y, w, h));

        float cy = y + 6f;
        int scrap = RunUpgrades.Scrap;
        if (scrapText.Update(scrap)) scrapText.Text = $"HURDA   {scrap}";
        Hud.Text(new Rect(x + 10f, cy, w - 20f, 26f), scrapText.Text, new Color(1f, 0.85f, 0.3f), 20);
        cy += 28f;

        // Guc seviyesi: P ikonlariyla yukselir
        Hud.Text(new Rect(x + 10f, cy, 100f, 20f), "GÜÇ", PowerUp.KindColors[0], 14);
        int shown = RunState.MaxPower + 1;
        for (int p = 0; p < shown; p++)
        {
            Rect pr = new Rect(x + w - 10f - (shown - p) * 20f, cy + 3f, 16f, 14f);
            Hud.Fill(pr, p <= RunState.PowerLevel ? PowerUp.KindColors[0] : new Color(1f, 1f, 1f, 0.15f));
        }
        cy += rowH;

        // Bomba sayisi: B tusu
        Hud.Text(new Rect(x + 10f, cy, 140f, 20f), BombLabel, PowerUp.KindColors[3], 14);
        for (int b = 0; b < RunState.MaxBombs; b++)
        {
            Rect br = new Rect(x + w - 10f - (RunState.MaxBombs - b) * 20f, cy + 3f, 16f, 14f);
            Hud.Fill(br, b < RunState.Bombs ? PowerUp.KindColors[3] : new Color(1f, 1f, 1f, 0.15f));
        }
        cy += rowH + 4f;

        if (overdrive)
        {
            Color oc = PowerUp.KindColors[(int)PickupKind.Overdrive];
            Hud.Text(new Rect(x + 10f, cy, 140f, 20f), "AŞIRI YÜK", oc, 14);
            Rect bar = new Rect(x + 110f, cy + 5f, w - 120f, 10f);
            Hud.Fill(bar, new Color(1f, 1f, 1f, 0.15f));
            Hud.Fill(new Rect(bar.x, bar.y, bar.width * Mathf.Clamp01(RunState.OverdriveLeft / RunState.OverdriveSeconds), bar.height), oc);
            cy += 22f;
        }

        if (gun != null && gun.HasMissiles)
        {
            Hud.Text(new Rect(x + 10f, cy, w - 20f, 20f), gun.MissileStatus, new Color(1f, 0.9f, 0.45f), 13);
            cy += 20f;
        }

        // Kahraman kacisi: Q / E, dolma cubugu
        if (dash)
        {
            Color dc = dashReady >= 1f ? new Color(0.55f, 1f, 0.9f) : new Color(0.55f, 0.7f, 0.8f);
            Hud.Text(new Rect(x + 10f, cy, 170f, 20f), dashName + " [Q/E]", dc, 13);
            Rect bar = new Rect(x + 190f, cy + 5f, w - 200f, 10f);
            Hud.Fill(bar, new Color(1f, 1f, 1f, 0.15f));
            Hud.Fill(new Rect(bar.x, bar.y, bar.width * dashReady, bar.height), dc);
            cy += 22f;
        }

        Hud.Text(new Rect(x + 10f, cy, w - 20f, 18f), "Rozetli düşman = güçlendirme düşürür", new Color(0.8f, 0.88f, 1f), 11, TextAnchor.UpperLeft, false);
        cy += 20f;
        Hud.Text(new Rect(x + 10f, cy, w - 20f, 18f), "ESC: Mağaza / duraklat", new Color(0.7f, 0.75f, 0.82f), 12, TextAnchor.UpperLeft, false);

        rightBlockBottom = y + h;
    }

    // ------------------------------------------------------------------ radar

    // Ustten gorunum: ucak merkezde, burun yukari. Dusmanlar turune gore renkli, guclendirmeler kendi renginde.
    private void DrawRadar()
    {
        PlaneController plane = flight != null ? flight.Current : null;
        if (plane == null) return;

        float R = RadarRadius;
        Vector2 c = new Vector2(Hud.W - Hud.Margin - R - 2f, Hud.H - Hud.Margin - R - 2f);

        Hud.Disc(c, R + 3f, new Color(0f, 0f, 0f, 0.55f));
        Hud.Disc(c, R, new Color(0.04f, 0.1f, 0.15f, 0.72f));
        Hud.Ring(c, R * 0.5f, new Color(1f, 1f, 1f, 0.12f));
        Hud.Ring(c, R, new Color(0.5f, 0.8f, 1f, 0.55f));

        Transform t = plane.transform;
        Vector3 fwd = Vector3.ProjectOnPlane(t.forward, Vector3.up);
        if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.ProjectOnPlane(t.up, Vector3.up);
        if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;
        fwd.Normalize();
        Vector3 right = Vector3.Cross(Vector3.up, fwd);
        Vector3 origin = t.position;

        // Guclendirmeler: koyu kenarli elmas (dusmanlar yuvarlak nokta; renkleri cakissa da sekilden ayrilir)
        foreach (var p in PowerUp.All)
        {
            if (p == null) continue;
            if (!RadarPoint(p.transform.position, origin, fwd, right, c, R, true, out Vector2 pos, out bool inside)) continue;
            Color col = PowerUp.KindColors[(int)p.Kind];
            if (!inside) col.a = 0.6f;
            Hud.RotateAround(45f, pos);
            Hud.Fill(new Rect(pos.x - 3.8f, pos.y - 3.8f, 7.6f, 7.6f), new Color(0f, 0f, 0f, 0.75f * col.a));
            Hud.Fill(new Rect(pos.x - 2.6f, pos.y - 2.6f, 5.2f, 5.2f), col);
            Hud.ClearRotation();
        }

        // Dusmanlar (kenarda kirpilir: yon bilgisi her zaman gorunsun)
        foreach (var e in enemies)
        {
            if (e == null || e.IsDead) continue;
            RadarPoint(e.transform.position, origin, fwd, right, c, R, true, out Vector2 p, out bool inside);

            EnemyKindInfo info = EnemyKinds.Get(e.Kind);
            float size = e.IsBoss || e.IsCore ? 5.5f : (e.IsGround ? 1.8f : (e.Kind == EnemyKind.Heavy ? 4f : 2.8f));
            Color col = info.marker;
            if (!inside) col.a = 0.55f;
            Hud.Disc(p, size, col);
            if (e.IsCarrier) Hud.Ring(p, size + 2.5f, PowerUp.KindColors[e.CarrierDrop]);
        }

        // Ucak: merkezde burun yukari bakan isaret
        GUI.color = Color.white;
        GUI.DrawTexture(new Rect(c.x - 5f, c.y - 6f, 10f, 12f), arrowTexture);

        Hud.Text(new Rect(c.x - R, c.y - R - 17f, R * 2f, 16f), "RADAR", new Color(0.7f, 0.82f, 0.95f), 10, TextAnchor.MiddleCenter, false);
    }

    private static bool RadarPoint(Vector3 world, Vector3 origin, Vector3 fwd, Vector3 right,
        Vector2 center, float radius, bool clampToEdge, out Vector2 pos, out bool inside)
    {
        Vector3 d = world - origin;
        d.y = 0f;
        Vector2 v = new Vector2(Vector3.Dot(d, right), Vector3.Dot(d, fwd)) / RadarRange * radius;
        float m = v.magnitude;
        float limit = radius - 3f;

        inside = m <= limit;
        if (!inside)
        {
            if (!clampToEdge)
            {
                pos = center;
                return false;
            }
            v = v / m * limit;
        }

        pos = center + new Vector2(v.x, -v.y);
        return true;
    }

    // ----------------------------------------------------------------- isaretler

    private struct ArrowMark
    {
        public Vector2 pos;
        public float angle;
        public Color color;
        public float dist;
        public int count;
        public bool boss;
        public string prefix;
        public bool threat;     // oyuncuya yaklasan ekran disi mermi
    }

    // Yakin birkac dusman icin cerceve + can; ekran disindakiler icin en yakin 4 ok.
    // Guclendirmeler harf kutusuyla gosterilir; en yakin ekran disi olan icin ok vardir.
    private void DrawMarkers()
    {
        float W = Hud.W, H = Hud.H;
        Vector2 c = new Vector2(W * 0.5f, H * 0.5f);
        Vector3 camPos = cam.transform.position;

        // Yakindan uzaga siralama: kapanis (closure) ve karsilastirici temsilcisi cop uretmesin diye
        // mesafeler listeye yazilir ve ekleme siralamasi yapilir (dusman sayisi az)
        markerOrder.Clear();
        markerDist.Clear();
        foreach (var e in enemies)
        {
            if (e == null || e.IsDead) continue;

            float sq = (e.transform.position - camPos).sqrMagnitude;
            int at = markerOrder.Count;
            while (at > 0 && markerDist[at - 1] > sq) at--;
            markerOrder.Insert(at, e);
            markerDist.Insert(at, sq);
        }

        int boxes = 0, arrows = 0, groundBoxes = 0;
        arrowMarks.Clear();

        PlaneController playerPlane = flight != null ? flight.Current : null;
        Vector3 playerPos = playerPlane != null ? playerPlane.transform.position : camPos;
        float blink = 0.5f + 0.5f * Mathf.Sin(Time.time * 12f);

        foreach (var e in markerOrder)
        {
            // Boss fazinda yalniz boss isaretlenir (uzakta kalan us ve yer birimleri ekrani doldurmasin)
            if (RushActive && !e.IsBoss) continue;

            Vector3 wp = e.transform.position;
            Vector3 sp = cam.WorldToScreenPoint(wp);
            float dist = Vector3.Distance(camPos, wp);
            bool locked = gun != null && gun.LockedEnemy == e;
            bool missileLock = gun != null && gun.MissileTarget == e && gun.HasMissiles;
            bool aiming = e.IsAimingAt(playerPos);
            bool important = e.IsBoss || e.IsCore || locked || missileLock || e.IsCarrier || aiming || e.IsAce;

            Color col = locked ? new Color(1f, 0.9f, 0.2f) : (e.IsAce ? new Color(1f, 0.55f, 0.15f) : EnemyKinds.Get(e.Kind).marker);
            if (aiming && !locked) col = Color.Lerp(col, new Color(1f, 0.1f, 0.1f), 0.5f + 0.5f * blink);

            Vector2 hp = Hud.ToHud(sp);
            bool onScreen = sp.z > 0f && hp.x > 0f && hp.x < W && hp.y > 0f && hp.y < H;

            // Yer birimleri: hava dusmanlarinin kutu/ok kotasini yemesin; yalniz yakin ve ekrandaki birkaci isaretlenir
            bool ground = e.IsGround && !e.IsCore;
            if (ground && !locked && !missileLock)
            {
                if (!onScreen || dist > 550f || groundBoxes >= 3) continue;
                groundBoxes++;
            }

            if (onScreen)
            {
                if (!important && !ground && (boxes >= 5 || dist > 1300f)) continue;
                if (!ground) boxes++;

                // Kutu hedefin GERCEK ekran boyutunu cevreler (yakindaki ucagi ortmesin); uzakta en az 28 px.
                // Hedef ekranda buyukse tam kutu yerine kose isaretleri: ucak ve vurus efektleri gorunur kalir.
                float fovTan = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
                float projected = (e.HitRadius * 2.4f) / (2f * Mathf.Max(dist, 1f) * fovTan) * H;
                float size = Mathf.Clamp(projected * 1.15f, 28f, e.IsBoss ? 600f : 360f);
                if (e.IsBoss) size = Mathf.Max(size, 44f);

                Rect box = new Rect(hp.x - size * 0.5f, hp.y - size * 0.5f, size, size);
                float thick = e.IsBoss ? 3f : 2f;
                if (size < 70f)
                {
                    // Koyu alt kontur: yesil zemin ve gokyuzunde de secilsin
                    Hud.Outline(new Rect(box.x - 1f, box.y - 1f, box.width + 2f, box.height + 2f),
                        new Color(0f, 0f, 0f, 0.7f), thick + 2f);
                    Hud.Outline(box, col, thick);
                }
                else DrawCorners(box, col, thick, Mathf.Min(size * 0.22f, 26f));

                // Ates etmek uzere: kirmizi yanip sonen uyari
                if (aiming)
                {
                    Rect warn = new Rect(box.xMax + 3f, box.y - 2f, 20f, 20f);
                    Hud.Fill(warn, new Color(1f, 0.1f, 0.1f, 0.55f + 0.45f * blink));
                    Hud.Text(warn, "!", Color.white, 15, TextAnchor.MiddleCenter);
                }

                float topY = box.y;

                // Can: kucukse vurus noktalari, buyukse ince cubuk (siradan yer birimlerinde yok: ekran sakin kalsin)
                if (ground && !locked && !missileLock) { }
                else if (e.MaxHits <= 10)
                {
                    float pipW = 9f, pipH = 5f, gap = 3f;
                    float total = e.MaxHits * (pipW + gap) - gap;
                    float px = box.center.x - total * 0.5f;
                    float py = box.y - pipH - 4f;
                    for (int i = 0; i < e.MaxHits; i++)
                        Hud.Fill(new Rect(px + i * (pipW + gap), py, pipW, pipH),
                            i < e.HitsRemaining ? col : new Color(0f, 0f, 0f, 0.55f));
                    topY = py;
                }
                else
                {
                    Rect hb = new Rect(box.center.x - 24f, box.y - 9f, 48f, 5f);
                    Hud.Bar(hb, (float)e.HitsRemaining / e.MaxHits, col);
                    topY = hb.y;
                }

                // Guclendirme tasiyicisi: rozet (P / S / M / B / +)
                if (e.IsCarrier)
                {
                    Color kc = PowerUp.KindColors[e.CarrierDrop];
                    Rect chip = new Rect(box.center.x - 11f, topY - 24f, 22f, 20f);
                    Hud.Fill(chip, kc);
                    Hud.Text(chip, PowerUp.Letters[e.CarrierDrop], Color.black, 14, TextAnchor.MiddleCenter);
                }

                // Mesafe yazisi: yalniz onemliler ve en yakin hava hedefi (yer birimlerinde yok)
                if (important || (!ground && boxes <= 1))
                    Hud.Text(new Rect(box.x - 30f, box.yMax + 2f, size + 60f, 16f), Hud.Meters(dist), col, 12, TextAnchor.UpperCenter);

                if (missileLock)
                    Hud.Text(new Rect(box.x - 40f, box.yMax + 16f, size + 80f, 16f), "FÜZE KİLİDİ",
                        new Color(1f, 0.9f, 0.2f), 12, TextAnchor.UpperCenter);

                // As pilotun adi kutunun ustunde
                if (e.IsAce)
                    Hud.Text(new Rect(box.center.x - 120f, topY - 22f, 240f, 18f), e.Title,
                        new Color(1f, 0.6f, 0.2f), 13, TextAnchor.MiddleCenter);
            }
            else
            {
                if (!important && (arrows >= 3 || ground)) continue;
                arrows++;
                AddArrow(c, hp, sp.z < 0f, col, dist, e.IsBoss || e.IsCore,
                    e.IsCarrier ? PowerUp.Letters[e.CarrierDrop] + " " : (e.IsCore ? "ÜS " : null));
            }
        }

        CollectBulletThreats(c, playerPlane, blink);

        // Guclendirmeler
        PowerUp nearestOff = null;
        float nearestOffDist = float.MaxValue;
        foreach (var p in PowerUp.All)
        {
            if (p == null) continue;
            Vector3 wp = p.transform.position;
            Vector3 sp = cam.WorldToScreenPoint(wp);
            float dist = Vector3.Distance(camPos, wp);
            Vector2 hp = Hud.ToHud(sp);
            bool onScreen = sp.z > 0f && hp.x > 0f && hp.x < W && hp.y > 0f && hp.y < H;
            Color kc = PowerUp.KindColors[(int)p.Kind];

            if (onScreen)
            {
                if (dist > 900f) continue;
                Rect chip = new Rect(hp.x - 12f, hp.y - 36f, 24f, 22f);
                Hud.Fill(new Rect(chip.x - 2f, chip.y - 2f, chip.width + 4f, chip.height + 4f), new Color(0f, 0f, 0f, 0.7f));
                Hud.Fill(chip, kc);
                Hud.Text(chip, PowerUp.Letters[(int)p.Kind], Color.black, 15, TextAnchor.MiddleCenter);
                Hud.Text(new Rect(hp.x - 40f, hp.y - 14f, 80f, 16f), Hud.Meters(dist), kc, 11, TextAnchor.UpperCenter);
            }
            else if (dist < nearestOffDist)
            {
                nearestOffDist = dist;
                nearestOff = p;
            }
        }

        if (nearestOff != null)
        {
            Vector3 sp = cam.WorldToScreenPoint(nearestOff.transform.position);
            AddArrow(c, Hud.ToHud(sp), sp.z < 0f, PowerUp.KindColors[(int)nearestOff.Kind], nearestOffDist, false,
                PowerUp.Letters[(int)nearestOff.Kind] + " ");
        }

        foreach (var am in arrowMarks)
        {
            float s = am.boss ? 30f : 22f;
            Color arrowCol = am.color;
            if (am.threat)
            {
                s = 30f;
                arrowCol.a = 0.6f + 0.4f * blink;
            }

            Hud.RotateAround(am.angle, am.pos);
            GUI.color = arrowCol;
            GUI.DrawTexture(new Rect(am.pos.x - s * 0.5f, am.pos.y - s * 0.5f, s, s), arrowTexture);
            GUI.color = Color.white;
            Hud.ClearRotation();

            string label;
            if (am.threat)
                label = am.count > 1 ? $"MERMİ  x{am.count}" : "MERMİ";
            else if (am.prefix == null && am.count == 1)
                label = Hud.Meters(am.dist);                       // en sik durum: onbellekli, cop yok
            else
                label = (am.prefix ?? "") + (am.count > 1 ? $"{am.dist:0} m  x{am.count}" : $"{am.dist:0} m");
            Hud.Text(new Rect(am.pos.x - 55f, am.pos.y + 15f, 110f, 16f), label, arrowCol, 12, TextAnchor.UpperCenter);
        }
    }

    // Oyuncuya carpacak (en yakin gecis mesafesi < ~10 m, <= 1,8 sn) mermiler: ekran disindakiler icin
    // kenarda kirmizi ok, ekranda gorunenler icin ustlerinde nabiz atan halka.
    private void CollectBulletThreats(Vector2 c, PlaneController plane, float blink)
    {
        if (plane == null || plane.IsCrashed) return;

        float W = Hud.W, H = Hud.H;
        Vector3 p = plane.transform.position;
        Vector3 vp = plane.Velocity;
        IReadOnlyList<Bullet> bullets = Bullet.Enemy;

        int arrowsAdded = 0, ringsAdded = 0;
        Color danger = new Color(1f, 0.15f, 0.15f);

        for (int i = 0; i < bullets.Count; i++)
        {
            Bullet b = bullets[i];
            if (b == null) continue;

            Vector3 r = b.transform.position - p;
            Vector3 v = b.Velocity - vp;
            float vv = v.sqrMagnitude;
            if (vv < 1f) continue;

            float t = -Vector3.Dot(r, v) / vv;
            if (t < 0.05f || t > 1.8f) continue;
            if ((r + v * t).sqrMagnitude > 10f * 10f) continue;

            Vector3 sp = cam.WorldToScreenPoint(b.transform.position);
            Vector2 hp = Hud.ToHud(sp);
            bool onScreen = sp.z > 0f && hp.x > 0f && hp.x < W && hp.y > 0f && hp.y < H;

            if (onScreen)
            {
                if (ringsAdded >= 10) continue;
                ringsAdded++;
                Hud.Ring(hp, 17f + 5f * blink, new Color(danger.r, danger.g, danger.b, 0.9f));
            }
            else
            {
                if (arrowsAdded >= 12) continue;
                arrowsAdded++;
                AddArrow(c, hp, sp.z < 0f, danger, r.magnitude, false, null, true);
            }
        }
    }

    // Ekran disindaki hedef icin kenarda ok; ayni yondeki oklar tek oka birlesir
    private void AddArrow(Vector2 c, Vector2 hp, bool behind, Color col, float dist, bool isBoss, string prefix,
        bool threat = false)
    {
        float W = Hud.W, H = Hud.H;

        // Arkada kalan hedefin izdusumu aynadir: merkeze gore ters cevir
        Vector2 p = behind ? c - (hp - c) : hp;
        Vector2 d = p - c;
        if (d.sqrMagnitude < 1f) d = new Vector2(0f, -1f);

        // Kenar: panellerden uzak guvenli dikdortgen
        float left = Hud.SafeSide, right = W - Hud.SafeSide;
        float top = Hud.SafeTop, bottom = H - Hud.SafeBottom;

        float sx = d.x > 0f ? (right - c.x) / Mathf.Max(d.x, 0.001f) : (left - c.x) / Mathf.Min(d.x, -0.001f);
        float sy = d.y > 0f ? (bottom - c.y) / Mathf.Max(d.y, 0.001f) : (top - c.y) / Mathf.Min(d.y, -0.001f);
        Vector2 pos = c + d * Mathf.Min(sx, sy);

        // Sag ust bilgi panelinin altina it
        if (pos.x > W - Hud.RightColumnWidth - 40f && pos.y < rightBlockBottom + 24f)
            pos.y = rightBlockBottom + 24f;

        // Sag alttaki radarin ustune binmesin
        float radarTop = H - Hud.Margin - RadarRadius * 2f - 30f;
        if (pos.x > W - Hud.Margin - RadarRadius * 2f - 30f && pos.y > radarTop)
            pos.y = radarTop;

        float angle = Mathf.Atan2(d.x, -d.y) * Mathf.Rad2Deg;

        // Ayni yonde birikenler tek oka birlesir ("x3" ile); tehdit okleri yalniz kendi aralarinda
        if (prefix == null)
        {
            for (int k = 0; k < arrowMarks.Count; k++)
            {
                ArrowMark am = arrowMarks[k];
                if (am.prefix != null || am.threat != threat) continue;
                if ((am.pos - pos).sqrMagnitude > 46f * 46f) continue;
                am.count++;
                am.dist = Mathf.Min(am.dist, dist);
                if (isBoss) { am.boss = true; am.color = col; }
                arrowMarks[k] = am;
                return;
            }
        }

        arrowMarks.Add(new ArrowMark
        {
            pos = pos, angle = angle, color = col, dist = dist, count = 1, boss = isBoss, prefix = prefix,
            threat = threat
        });
    }

    // ------------------------------------------------------------ yuzen yazilar

    // Yuzen yazilar (kombo kelimeleri, puan). Panellere binmemesi icin guvenli alana sikistirilir.
    private void DrawFloatingTexts()
    {
        float W = Hud.W, H = Hud.H;
        placedTexts.Clear();

        foreach (var t in texts)
        {
            float age = Time.time - t.born;
            Vector3 world = t.position + Vector3.up * (age * 6f);
            Vector3 sp = cam.WorldToScreenPoint(world);
            if (sp.z <= 0f) continue;

            float punch = 1f + 0.5f * Mathf.Exp(-age * 7f);
            float alpha = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1.3f, 2.1f, age));
            int size = Mathf.RoundToInt(28f * t.scale * punch);

            // Metnin gorunen kutusu (yaklasik genislik): ekran kenarlarindan ve ust paneller altindan tasmaz
            float tw = t.text.Length * size * 0.55f;
            Vector2 p = Hud.ToHud(sp);
            // Sag bilgi kolonunun (ve kenarlarin) icine tasmasin
            float maxX = W - Hud.RightColumnWidth - 40f - tw * 0.5f;
            p.x = Mathf.Clamp(p.x, tw * 0.5f + 12f, Mathf.Max(maxX, tw * 0.5f + 13f));
            p.y = Mathf.Clamp(p.y, Hud.FloatTop + size, Mathf.Max(Hud.FloatTop + size + 1f, H - Hud.SafeBottom - 20f));

            Rect box = new Rect(p.x - tw * 0.5f, p.y - size, tw, size * 1.15f);
            for (int guard = 0; guard < 8; guard++)
            {
                bool hit = false;
                foreach (var other in placedTexts)
                {
                    if (!other.Overlaps(box)) continue;
                    box.y = other.yMax + 2f;
                    hit = true;
                    break;
                }
                if (!hit) break;
            }
            placedTexts.Add(box);

            Color col = t.color;
            col.a = alpha;
            Hud.Text(new Rect(p.x - 300f, box.y, 600f, size * 1.6f), t.text, col, size, TextAnchor.UpperCenter);
        }
    }

    // Boss, asama ve ipuclari: tek satirlik buyuk duyuru
    private void DrawBanner()
    {
        if (!bannerActive || string.IsNullOrEmpty(currentBanner.text)) return;

        float age = Time.time - bannerStart;
        float fadeIn = Mathf.Clamp01(age / 0.15f);
        float fadeOut = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(currentBanner.duration - 0.5f, currentBanner.duration, age));
        float a = fadeIn * fadeOut;

        // Sag kolona ve sol panele binmemesi icin orta seritte kalir; uzun metin kuculur
        float bandW = Hud.W - 2f * (Hud.RightColumnWidth + 40f);
        float fit = bandW / Mathf.Max(1f, currentBanner.text.Length * 0.56f);
        float baseSize = Mathf.Min(32f, fit);

        float punch = 1f + 0.3f * Mathf.Exp(-age * 7f);
        int size = Mathf.RoundToInt(baseSize * punch);
        float y = 176f;

        Rect band = new Rect(Hud.W * 0.5f - bandW * 0.5f, y - 6f, bandW, 32f * 1.35f + 12f);
        Hud.Fill(band, new Color(0f, 0f, 0f, 0.4f * a));

        Color col = currentBanner.color;
        col.a = a;
        Hud.Text(new Rect(band.x, y, band.width, size * 1.4f), currentBanner.text, col, size, TextAnchor.UpperCenter);
    }

    private void DrawBottomWarnings()
    {
        float cx = Hud.W * 0.5f;
        float y = Hud.H - 62f;

        if (gun != null && !gun.HasWeapons)
        {
            Hud.Text(new Rect(cx - 320f, y, 640f, 22f),
                "SİLAH TAKILI DEĞİL — hangardan silah tak (ESC → Hangara Dön)",
                new Color(1f, 0.6f, 0.2f), 14, TextAnchor.MiddleCenter);
            y -= 26f;
        }

        PlaneController plane = flight != null ? flight.Current : null;
        PlayerHealth ph = CurrentPlayerHealth;

        // Yere cok yaklasma: carpmak oyunu bitirir
        if (plane != null && !plane.IsCrashed && !plane.IsGrounded)
        {
            Vector3 p = plane.transform.position;
            float agl = p.y - GroundHeight(p);
            if (agl < 80f && plane.VerticalSpeed < -4f)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 14f);
                Hud.Text(new Rect(cx - 200f, y - 30f, 400f, 36f), "YÜKSEL!", new Color(1f, 0.3f + 0.4f * pulse, 0.2f), 30, TextAnchor.MiddleCenter);
            }
        }

        // Savas alani disi: geri don (sure dolunca can gider)
        if (OutsideArena && plane != null && !plane.IsCrashed)
        {
            float left = Mathf.Max(0f, GameSettings.Current.arenaGraceSeconds - OutsideSeconds);
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 10f);
            string text = left > 0f ? $"SAVAŞ ALANINDAN ÇIKIYORSUN — DÖN!  ({left:0})" : "SAVAŞ ALANI DIŞI — CAN GİDİYOR, DÖN!";
            Hud.Text(new Rect(cx - 320f, Hud.H * 0.34f, 640f, 34f), text, new Color(1f, 0.55f + 0.3f * pulse, 0.2f), 24, TextAnchor.MiddleCenter);
        }

        // Dusuk can
        if (plane != null && ph != null && ph.maxHealth > 0 && (float)ph.Health / ph.maxHealth <= 0.3f && !plane.IsCrashed)
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 8f);
            Hud.Text(new Rect(cx - 200f, y + 4f, 400f, 24f), "CAN DÜŞÜK", new Color(1f, 0.25f, 0.2f, 0.6f + 0.4f * pulse), 18, TextAnchor.MiddleCenter);
        }
    }

    // Kose isaretleri (koyu konturlu): buyuk hedefte tam kutu yerine
    private static void DrawCorners(Rect b, Color c, float t, float len)
    {
        Color dark = new Color(0f, 0f, 0f, 0.7f);
        for (int pass = 0; pass < 2; pass++)
        {
            Color col = pass == 0 ? dark : c;
            float o = pass == 0 ? 1f : 0f, tt = pass == 0 ? t + 2f : t;
            Hud.Fill(new Rect(b.x - o, b.y - o, len + 2f * o, tt), col);
            Hud.Fill(new Rect(b.x - o, b.y - o, tt, len + 2f * o), col);
            Hud.Fill(new Rect(b.xMax - len - o, b.y - o, len + 2f * o, tt), col);
            Hud.Fill(new Rect(b.xMax - tt + o, b.y - o, tt, len + 2f * o), col);
            Hud.Fill(new Rect(b.x - o, b.yMax - tt + o, len + 2f * o, tt), col);
            Hud.Fill(new Rect(b.x - o, b.yMax - len - o, tt, len + 2f * o), col);
            Hud.Fill(new Rect(b.xMax - len - o, b.yMax - tt + o, len + 2f * o, tt), col);
            Hud.Fill(new Rect(b.xMax - tt + o, b.yMax - len - o, tt, len + 2f * o), col);
        }
    }

    // Hasar yonu: her darbe icin ekran merkezi cevresinde, darbenin geldigi yonde kirmizi yay (1 sn)
    private void DrawDamageArcs()
    {
        if (damageArcs.Count == 0 || cam == null) return;
        Vector2 center = new Vector2(Hud.W * 0.5f, Hud.H * 0.5f);
        Transform ct = cam.transform;
        for (int i = damageArcs.Count - 1; i >= 0; i--)
        {
            DamageArc a = damageArcs[i];
            float age = Time.unscaledTime - a.born;
            if (age > 1.1f) { damageArcs.RemoveAt(i); continue; }

            // Darbe yonu: ucak merkezinden carpma noktasina (kalkan/govde yuzeyi darbenin geldigi tarafa bakar),
            // kamera eksenlerinde. Arkadan gelen darbe ekranin altinda, onden gelen ustunde gorunur.
            Transform pt = PlayerTransform;
            if (pt == null) continue;
            Vector3 d = ct.InverseTransformDirection(a.from - pt.position);
            float ang = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;   // pusula: ust = on, alt = arka, yanlar = yanlar

            float alpha = Mathf.Clamp01(1f - age / 1.1f) * (a.hull ? 0.95f : 0.6f);
            Color col = a.hull ? new Color(1f, 0.15f, 0.1f, alpha) : new Color(0.4f, 0.8f, 1f, alpha);   // kalkan darbesi mavi
            float radius = 150f;
            Hud.RotateAround(ang, center);
            for (int k = -3; k <= 3; k++)
            {
                // Yay: 7 parca, ortasi kalin
                float w = 34f - Mathf.Abs(k) * 4f, h = 7f - Mathf.Abs(k) * 0.6f;
                Hud.RotateAround(ang + k * 6f, center);
                Hud.Fill(new Rect(center.x - w * 0.5f, center.y - radius - h, w, h), col);
            }
            Hud.ClearRotation();
        }
    }

    // Sanal cubuk (FARE semasi, Battlefront 2 tarzi): nisangahin cevresinde cubugun menzil halkasi ve fareyle itilen
    // imlec. Imlec merkezden uzaklastikca ucak o yone daha hizli doner; ortadaki olu bolgede duz ucar.
    private const float StickHudRadius = 118f;

    private void DrawVirtualStick()
    {
        if (gun == null) return;
        PlaneController plane = flight != null ? flight.Current : null;
        if (plane == null || !plane.VirtualStickOn || Cursor.lockState != CursorLockMode.Locked) return;

        Vector2 c = Hud.ToHud(gun.CrosshairScreenPos);
        Vector2 s = plane.VirtualStick;
        float m = s.magnitude;
        bool turning = m > PlaneController.StickDeadzone;

        Hud.Ring(c, StickHudRadius, new Color(1f, 1f, 1f, turning ? 0.16f + 0.14f * m : 0.08f));
        Vector2 p = c + new Vector2(s.x, -s.y) * StickHudRadius;
        Color cur = turning ? new Color(0.6f, 0.95f, 1f, 0.95f) : new Color(0.85f, 0.95f, 1f, 0.55f);

        if (turning)
        {
            // Merkezden imlece cizgi (acik gokte de seçilsin diye koyu golgeli): donus yonu ve buyuklugu
            Vector2 dvec = p - c;
            float len = dvec.magnitude - 11f;
            if (len > 2f)
            {
                float angle = Mathf.Atan2(dvec.x, -dvec.y) * Mathf.Rad2Deg;
                Hud.RotateAround(angle, c);
                Hud.Fill(new Rect(c.x - 2f, c.y - len - 5f, 4f, len), new Color(0f, 0f, 0f, 0.3f));
                Hud.Fill(new Rect(c.x - 1f, c.y - len - 5f, 2f, len), new Color(0.6f, 0.95f, 1f, 0.45f + 0.4f * m));
                Hud.ClearRotation();
            }
        }

        // Imlec: koyu zemin uzerinde kalin camgobegi halka ve beyaz nokta
        Hud.Disc(p, 10.5f, new Color(0f, 0f, 0f, 0.35f));
        Hud.Ring(p, 9f, cur);
        Hud.Ring(p, 8f, cur);
        Hud.Ring(p, 7f, cur);
        Hud.Disc(p, 2.4f, Color.white);
    }

    private void DrawCrosshair()
    {
        if (gun == null) return;

        // Sade nisangah: koyu konturlu kucuk nokta. Hedef imlecin yanindayken hafifce kirmiziya doner.
        Vector2 p = Hud.ToHud(gun.CrosshairScreenPos);
        bool locked = gun.LockedEnemy != null;
        Color col = locked ? new Color(1f, 0.45f, 0.4f) : new Color(0.95f, 1f, 0.95f);

        Hud.Disc(p, 3.4f, new Color(0f, 0f, 0f, 0.6f));
        Hud.Disc(p, 2.2f, col);

        // Vurus onayi: vurulunca dort kisa capraz cizgi (beyaz), oldurunce daha buyuk ve kirmizi
        float hitAge = Time.unscaledTime - hitMarkerTime;
        float killAge = Time.unscaledTime - killMarkerTime;
        if (killAge < 0.3f) DrawHitMarker(p, 1f - killAge / 0.3f, new Color(1f, 0.25f, 0.2f), 14f, 11f);
        else if (hitAge < 0.14f) DrawHitMarker(p, 1f - hitAge / 0.14f, Color.white, 9f, 7f);

        DrawHeat(p);
    }

    // Silah isisi: nisangahin altinda; 1'e ulasinca kilitlenir, beyaz isaret ateşe donus esigi
    private void DrawHeat(Vector2 p)
    {
        if (gun.Heat < 0.02f && !gun.Overheated) return;

        const float w = 96f;
        var r = new Rect(p.x - w * 0.5f, p.y + 28f, w, 6f);
        Color c = gun.Overheated
            ? Color.Lerp(new Color(1f, 0.15f, 0.1f), new Color(1f, 0.6f, 0.2f), Mathf.PingPong(Time.unscaledTime * 5f, 1f))
            : Color.Lerp(new Color(0.4f, 0.9f, 0.4f), new Color(1f, 0.3f, 0.15f), Mathf.Clamp01((gun.Heat - 0.3f) / 0.7f));
        Hud.Bar(r, gun.Heat, c);
        Hud.Fill(new Rect(r.x + w * PlayerGun.ReleaseHeat - 1f, r.y - 2f, 2f, r.height + 4f), new Color(1f, 1f, 1f, 0.7f));
        if (gun.Overheated)
            Hud.Text(new Rect(p.x - 90f, r.yMax + 3f, 180f, 16f), "AŞIRI ISINDI", new Color(1f, 0.35f, 0.25f), 12, TextAnchor.UpperCenter);
    }

    private static void DrawHitMarker(Vector2 p, float k, Color c, float gap, float len)
    {
        c.a = Mathf.Clamp01(k * 1.4f);
        float g = gap + (1f - k) * 3f;
        for (int i = 0; i < 4; i++)
        {
            Hud.RotateAround(45f + 90f * i, p);
            Hud.Fill(new Rect(p.x + g, p.y - 1.2f, len, 2.4f), c);
        }
        Hud.ClearRotation();
    }
}
