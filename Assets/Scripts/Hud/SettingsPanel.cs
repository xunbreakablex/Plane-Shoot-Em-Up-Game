using UnityEngine;

// Ayarlar paneli (duraklat menusu ve hangarda ortak): ses, fare, kamera, kontrol semasi.
// Hud.Begin() ile acilmis sanal 1280x720 uzayinda cizilir. Kapat dugmesine basilirsa true doner.
public static class SettingsPanel
{
    private static GUIStyle button;

    public static bool Draw()
    {
        if (button == null) button = new GUIStyle(GUI.skin.button) { fontStyle = FontStyle.Bold, fontSize = 15 };

        float W = Hud.W, H = Hud.H;
        Hud.Fill(new Rect(0f, 0f, W, H), new Color(0f, 0f, 0f, 0.8f));

        float pw = 640f, ph = 470f;
        Rect panel = new Rect((W - pw) * 0.5f, (H - ph) * 0.5f, pw, ph);
        Hud.Fill(panel, new Color(0.06f, 0.09f, 0.14f, 0.98f));
        Hud.Fill(new Rect(panel.x, panel.y, pw, 4f), new Color(0.35f, 0.7f, 1f));
        Hud.Text(new Rect(panel.x, panel.y + 12f, pw, 40f), "AYARLAR", Color.white, 30, TextAnchor.MiddleCenter);

        float x = panel.x + 28f, w = pw - 56f, y = panel.y + 66f, rowH = 44f;
        float labelW = 230f;

        // Ses
        float master = Slider(x, y, w, labelW, "ANA SES", GameAudio.Master, 0f, 1f, v => $"%{v * 100f:0}");
        if (!Mathf.Approximately(master, GameAudio.Master)) GameAudio.Master = master;
        y += rowH;

        float music = Slider(x, y, w, labelW, "MÜZİK", GameAudio.Music, 0f, 1f, v => $"%{v * 100f:0}");
        if (!Mathf.Approximately(music, GameAudio.Music)) GameAudio.Music = music;
        y += rowH;

        // Fare ve kamera
        float sens = Slider(x, y, w, labelW, "FARE HASSASİYETİ", UserSettings.MouseSens, 0.3f, 3f, v => $"x{v:0.0}");
        if (!Mathf.Approximately(sens, UserSettings.MouseSens)) UserSettings.MouseSens = sens;
        y += rowH;

        float shake = Slider(x, y, w, labelW, "KAMERA SARSINTISI", UserSettings.Shake, 0f, 1f, v => v < 0.02f ? "kapalı" : $"%{v * 100f:0}");
        if (!Mathf.Approximately(shake, UserSettings.Shake)) UserSettings.Shake = shake;
        y += rowH;

        float dist = Slider(x, y, w, labelW, "KAMERA MESAFESİ", UserSettings.CameraDistance, 0.8f, 1.7f, v => $"x{v:0.00}");
        if (!Mathf.Approximately(dist, UserSettings.CameraDistance)) UserSettings.CameraDistance = dist;
        y += rowH + 6f;

        // Dugmeler
        float bw = (w - 12f) * 0.5f, bh = 40f;
        if (Click(new Rect(x, y, bw, bh), "FARE Y EKSENİ:  " + (UserSettings.InvertY ? "TERS" : "NORMAL")))
            UserSettings.InvertY = !UserSettings.InvertY;
        if (Click(new Rect(x + bw + 12f, y, bw, bh), $"KONTROL:  {ControlSettings.Label}"))
            ControlSettings.Scheme = ControlSettings.Next(ControlSettings.Scheme);
        y += bh + 10f;

        GUI.enabled = !ControlSettings.MouseSteering;
        if (Click(new Rect(x, y, bw, bh), $"NİŞAN:  {ControlSettings.AimLabel}"))
            ControlSettings.AutoAim = !ControlSettings.AutoAim;
        GUI.enabled = true;

        bool close = Click(new Rect(x + bw + 12f, y, bw, bh), "GERİ");
        y += bh + 12f;

        Hud.Text(new Rect(panel.x, y, pw, 20f), ControlSettings.SteerHint, new Color(1f, 0.9f, 0.55f), 12, TextAnchor.MiddleCenter, false);
        Hud.Text(new Rect(panel.x, y + 20f, pw, 20f), "Gamepad: sol çubuk kayma/gaz, sağ çubuk yönlendirme, RT ateş, LT zoom, LB/RB aşağı/yukarı, X bomba, Y füze",
            new Color(0.75f, 0.82f, 0.9f), 12, TextAnchor.MiddleCenter, false);

        UserSettings.Save();
        return close;
    }

    private static bool Click(Rect r, string text)
    {
        bool c = GUI.Button(r, text, button);
        if (c) GameAudio.PlayUi(Sfx.Click, 0.5f);
        return c;
    }

    private static float Slider(float x, float y, float w, float labelW, string label, float value, float min, float max, System.Func<float, string> fmt)
    {
        Hud.Text(new Rect(x, y, labelW, 30f), label, new Color(0.85f, 0.9f, 0.98f), 16, TextAnchor.MiddleLeft, false);
        float sw = w - labelW - 80f;
        float v = GUI.HorizontalSlider(new Rect(x + labelW, y + 9f, sw, 20f), value, min, max);
        Hud.Text(new Rect(x + labelW + sw + 8f, y, 72f, 30f), fmt(v), Color.white, 15, TextAnchor.MiddleRight, false);
        return v;
    }
}
