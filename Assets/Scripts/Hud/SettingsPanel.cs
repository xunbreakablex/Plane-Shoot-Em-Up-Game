using UnityEngine;

// Ayarlar paneli (duraklat menusu ve hangarda ortak): ses, fare, kamera, kontrol semasi.
// Hud.Begin() ile acilmis sanal 1280x720 uzayinda cizilir. Kapat dugmesine basilirsa true doner.
public static class SettingsPanel
{
    private static GUIStyle button;

    // Gamepad odagi: 0-4 sliderlar, 5 fare Y, 6 kontrol, 7 nisan, 8 zorluk, 9 hiz efekti, 10 geri
    private static int focus;
    private static int padStep;        // bu karede sol/sag (slider ayari / dugme degistirme)
    private static bool padAccept;

    public static bool Draw()
    {
        if (button == null) button = new GUIStyle(GUI.skin.button) { fontStyle = FontStyle.Bold, fontSize = 15 };

        focus = PadNav.Move(focus, 11);
        padStep = PadNav.Horizontal;
        padAccept = PadNav.Accept;
        if (PadNav.Back) { UserSettings.Save(); return true; }

        float W = Hud.W, H = Hud.H;
        Hud.Fill(new Rect(0f, 0f, W, H), new Color(0f, 0f, 0f, 0.8f));

        float pw = 640f, ph = 570f;
        Rect panel = new Rect((W - pw) * 0.5f, (H - ph) * 0.5f, pw, ph);
        Hud.Fill(panel, new Color(0.06f, 0.09f, 0.14f, 0.98f));
        Hud.Fill(new Rect(panel.x, panel.y, pw, 4f), new Color(0.35f, 0.7f, 1f));
        Hud.Text(new Rect(panel.x, panel.y + 12f, pw, 40f), "AYARLAR", Color.white, 30, TextAnchor.MiddleCenter);

        float x = panel.x + 28f, w = pw - 56f, y = panel.y + 66f, rowH = 44f;
        float labelW = 230f;

        // Ses
        float master = Slider(x, y, w, labelW, "ANA SES", GameAudio.Master, 0f, 1f, v => $"%{v * 100f:0}", 0);
        if (!Mathf.Approximately(master, GameAudio.Master)) GameAudio.Master = master;
        y += rowH;

        float music = Slider(x, y, w, labelW, "MÜZİK", GameAudio.Music, 0f, 1f, v => $"%{v * 100f:0}", 1);
        if (!Mathf.Approximately(music, GameAudio.Music)) GameAudio.Music = music;
        y += rowH;

        // Fare ve kamera
        float sens = Slider(x, y, w, labelW, "FARE HASSASİYETİ", UserSettings.MouseSens, 0.3f, 3f, v => $"x{v:0.0}", 2);
        if (!Mathf.Approximately(sens, UserSettings.MouseSens)) UserSettings.MouseSens = sens;
        y += rowH;

        float shake = Slider(x, y, w, labelW, "KAMERA SARSINTISI", UserSettings.Shake, 0f, 1f, v => v < 0.02f ? "kapalı" : $"%{v * 100f:0}", 3);
        if (!Mathf.Approximately(shake, UserSettings.Shake)) UserSettings.Shake = shake;
        y += rowH;

        float dist = Slider(x, y, w, labelW, "KAMERA MESAFESİ", UserSettings.CameraDistance, 0.8f, 1.7f, v => $"x{v:0.00}", 4);
        if (!Mathf.Approximately(dist, UserSettings.CameraDistance)) UserSettings.CameraDistance = dist;
        y += rowH + 6f;

        // Dugmeler
        float bw = (w - 12f) * 0.5f, bh = 40f;
        if (Click(new Rect(x, y, bw, bh), "FARE Y EKSENİ:  " + (UserSettings.InvertY ? "TERS" : "NORMAL"), 5))
            UserSettings.InvertY = !UserSettings.InvertY;
        if (Click(new Rect(x + bw + 12f, y, bw, bh), $"KONTROL:  {ControlSettings.Label}", 6))
            ControlSettings.Scheme = ControlSettings.Next(ControlSettings.Scheme);
        y += bh + 10f;

        // FARE semasinda nisan hep otomatik: dugme farenin calisma bicimini secer (sanal cubuk / dogrudan)
        if (ControlSettings.MouseSteering)
        {
            if (Click(new Rect(x, y, bw, bh), $"FARE:  {ControlSettings.MouseModeLabel}", 7))
                ControlSettings.MouseStick = !ControlSettings.MouseStick;
        }
        else if (Click(new Rect(x, y, bw, bh), $"NİŞAN:  {ControlSettings.AimLabel}", 7))
            ControlSettings.AutoAim = !ControlSettings.AutoAim;

        if (Click(new Rect(x + bw + 12f, y, bw, bh), $"ZORLUK:  {Difficulty.Label}", 8))
            Difficulty.Next();
        y += bh + 10f;

        if (Click(new Rect(x, y, w, bh), "HIZ EFEKTİ (çizgiler + bulanıklık):  " + (UserSettings.SpeedFx ? "AÇIK" : "KAPALI"), 9))
            UserSettings.SpeedFx = !UserSettings.SpeedFx;
        y += bh + 10f;

        bool close = Click(new Rect(x, y, w, bh), "GERİ", 10);
        y += bh + 12f;
        Hud.Text(new Rect(panel.x, y, pw, 20f), ControlSettings.SteerHint, new Color(1f, 0.9f, 0.55f), 12, TextAnchor.MiddleCenter, false);
        Hud.Text(new Rect(panel.x, y + 20f, pw, 20f), "Gamepad: sol çubuk kayma (sağ-sol / yukarı-aşağı), sağ çubuk yönlendirme, RB/LB hızlan/yavaşla, RT ateş, LT zoom, X bomba, Y füze, menüde dpad/A/B",
            new Color(0.75f, 0.82f, 0.9f), 12, TextAnchor.MiddleCenter, false);

        if (close) UserSettings.Save();   // diske yalniz kapatirken yazilir (her karede degil)
        return close;
    }

    private static bool Click(Rect r, string text, int index)
    {
        bool c = GUI.Button(r, text, button);
        if (index == focus && GUI.enabled)
        {
            PadNav.Highlight(r);
            // Odaklanan dugme: A ya da sol/sag ile degistirilir (hepsi iki/uc durumlu)
            if (padAccept || padStep != 0) c = true;
        }
        if (c) GameAudio.PlayUi(Sfx.Click, 0.5f);
        return c;
    }

    private static float Slider(float x, float y, float w, float labelW, string label, float value, float min, float max, System.Func<float, string> fmt, int index)
    {
        Hud.Text(new Rect(x, y, labelW, 30f), label, new Color(0.85f, 0.9f, 0.98f), 16, TextAnchor.MiddleLeft, false);
        float sw = w - labelW - 80f;
        Rect sr = new Rect(x + labelW, y + 9f, sw, 20f);
        if (index == focus)
        {
            PadNav.Highlight(new Rect(x, y + 2f, w, 28f));
            if (padStep != 0) value = Mathf.Clamp(value + padStep * (max - min) * 0.05f, min, max);
        }
        float v = GUI.HorizontalSlider(sr, value, min, max);
        Hud.Text(new Rect(x + labelW + sw + 8f, y, 72f, 30f), fmt(v), Color.white, 15, TextAnchor.MiddleRight, false);
        return v;
    }
}
