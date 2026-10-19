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
        DrawFloatingTexts();
        DrawBanner();
        DrawBottomWarnings();
        DrawCrosshair();
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
            stageBarText.Text = (RunState.Endless ? $"SONSUZ  •  AŞAMA {RunState.Stage + 1}" : $"AŞAMA {RunState.Stage + 1} / {Stages.All.Length}") + $"   •   {title}"
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
            Hud.Bar(br, bf, info.marker);
            string title = string.IsNullOrEmpty(bar.Title) ? info.title.ToUpper() : bar.Title;
            // Baslik degisirse (yeni boss) metin yeniden kurulur; sayilar degisince de
            bool titleChanged = !ReferenceEquals(bossTitleSource, title);
            if (titleChanged) bossTitleSource = title;
            if (bossText.Update(bar.HitsRemaining, bar.MaxHits) || titleChanged)
                bossText.Text = $"{title}   {bar.HitsRemaining} / {bar.MaxHits}";
            Hud.Text(br, bossText.Text, Color.black, 12, TextAnchor.MiddleCenter);
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
        Hud.Text(new Rect(x + 10f, cy, 120f, 20f), "BOMBA [B]", PowerUp.KindColors[3], 14);
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

        // Guclendirmeler
        foreach (var p in PowerUp.All)
        {
            if (p == null) continue;
            if (!RadarPoint(p.transform.position, origin, fwd, right, c, R, true, out Vector2 pos, out bool inside)) continue;
            Color col = PowerUp.KindColors[(int)p.Kind];
            if (!inside) col.a = 0.6f;
            Hud.Disc(pos, 3.4f, col);
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
            Vector3 wp = e.transform.position;
            Vector3 sp = cam.WorldToScreenPoint(wp);
            float dist = Vector3.Distance(camPos, wp);
            bool locked = gun != null && gun.LockedEnemy == e;
            bool missileLock = gun != null && gun.MissileTarget == e && gun.HasMissiles;
            bool aiming = e.IsAimingAt(playerPos);
            bool important = e.IsBoss || e.IsCore || locked || missileLock || e.IsCarrier || aiming;

            Color col = locked ? new Color(1f, 0.9f, 0.2f) : EnemyKinds.Get(e.Kind).marker;
            if (aiming && !locked) col = Color.Lerp(col, new Color(1f, 0.1f, 0.1f), 0.5f + 0.5f * blink);

            Vector2 hp = Hud.ToHud(sp);
            bool onScreen = sp.z > 0f && hp.x > 0f && hp.x < W && hp.y > 0f && hp.y < H;

            // Yer birimleri: hava dusmanlarinin kutu/ok kotasini yemesin; yalniz yakin ve ekrandaki birkaci isaretlenir
            bool ground = e.IsGround && !e.IsCore;
            if (ground && !locked && !missileLock)
            {
                if (!onScreen || dist > 700f || groundBoxes >= 6) continue;
                groundBoxes++;
            }

            if (onScreen)
            {
                if (!important && !ground && (boxes >= 8 || dist > 1300f)) continue;
                if (!ground) boxes++;

                // Uzaktaki ucak mesafe arttikca kuculmesin: kutu en az 28 px (eskiden 20)
                float size = Mathf.Clamp(2400f / Mathf.Max(dist, 1f), 28f, 68f);
                if (e.Kind == EnemyKind.Swarmer) size *= 0.8f;
                if (e.IsBoss) size = Mathf.Max(size, 44f);

                Rect box = new Rect(hp.x - size * 0.5f, hp.y - size * 0.5f, size, size);
                float thick = e.IsBoss ? 3f : 2f;
                // Koyu alt kontur: yesil zemin ve gokyuzunde de secilsin
                Hud.Outline(new Rect(box.x - 1f, box.y - 1f, box.width + 2f, box.height + 2f),
                    new Color(0f, 0f, 0f, 0.7f), thick + 2f);
                Hud.Outline(box, col, thick);

                // Ates etmek uzere: kirmizi yanip sonen uyari
                if (aiming)
                {
                    Rect warn = new Rect(box.xMax + 3f, box.y - 2f, 20f, 20f);
                    Hud.Fill(warn, new Color(1f, 0.1f, 0.1f, 0.55f + 0.45f * blink));
                    Hud.Text(warn, "!", Color.white, 15, TextAnchor.MiddleCenter);
                }

                float topY = box.y;

                // Can: kucukse vurus noktalari, buyukse ince cubuk
                if (e.MaxHits <= 10)
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

                // Mesafe yazisi: en yakin hedef ve onemliler (kalabalikta yazilar karismasin)
                if (important || boxes <= 1)
                    Hud.Text(new Rect(box.x - 30f, box.yMax + 2f, size + 60f, 16f), Hud.Meters(dist), col, 12, TextAnchor.UpperCenter);

                if (missileLock)
                    Hud.Text(new Rect(box.x - 40f, box.yMax + 16f, size + 80f, 16f), "FÜZE KİLİDİ",
                        new Color(1f, 0.9f, 0.2f), 12, TextAnchor.UpperCenter);
            }
            else
            {
                if (!important && arrows >= 4) continue;
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
        if (!bannerActive) return;

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
