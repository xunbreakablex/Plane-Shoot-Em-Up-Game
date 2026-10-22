using UnityEngine;

// Prosedurel ses efektleri (harici ses dosyasi gerektirmez).
public static class Sfx
{
    private const int Rate = 44100;
    private static AudioClip shot, explosion, hit;

    public static AudioClip Shot => shot != null ? shot : (shot = MakeShot());
    public static AudioClip Explosion => explosion != null ? explosion : (explosion = MakeExplosion());
    public static AudioClip Hit => hit != null ? hit : (hit = MakeHit());
    public static AudioClip ShieldHit => shieldHit != null ? shieldHit : (shieldHit = MakeShieldHit());
    public static AudioClip MissileLaunch => launch != null ? launch : (launch = MakeLaunch());

    private static AudioClip shieldHit, launch;

    public static AudioClip Pickup => pickup != null ? pickup : (pickup = MakePickup());
    public static AudioClip LevelUp => levelUp != null ? levelUp : (levelUp = MakeLevelUp());
    public static AudioClip Pulse => pulse != null ? pulse : (pulse = MakePulse());
    public static AudioClip Zap => zap != null ? zap : (zap = MakeZap());

    private static AudioClip pickup, levelUp, pulse, zap;

    // Kristal toplama: kisa, yukselen "blip" (pitch ile art arda toplamada yukselir)
    private static AudioClip MakePickup()
    {
        int n = (int)(Rate * 0.1f);
        var d = new float[n];
        float phase = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float f = Mathf.Lerp(880f, 1480f, t / 0.1f);
            phase += 2f * Mathf.PI * f / Rate;
            d[i] = Mathf.Sin(phase) * Mathf.Exp(-t * 22f) * 0.5f;
        }
        return Create("pickup", d);
    }

    // Seviye atlama: yukselen dort notali arpej
    private static AudioClip MakeLevelUp()
    {
        float[] notes = { 523.25f, 659.25f, 783.99f, 1046.5f };
        float noteLen = 0.11f;
        int n = (int)(Rate * (noteLen * notes.Length + 0.25f));
        var d = new float[n];
        for (int k = 0; k < notes.Length; k++)
        {
            int start = (int)(k * noteLen * Rate);
            for (int i = start; i < n; i++)
            {
                float t = (i - start) / (float)Rate;
                float s = Mathf.Sin(2f * Mathf.PI * notes[k] * t) + 0.35f * Mathf.Sin(4f * Mathf.PI * notes[k] * t);
                d[i] += s * Mathf.Exp(-t * 7f) * 0.28f;
            }
        }
        for (int i = 0; i < n; i++) d[i] = Mathf.Clamp(d[i], -1f, 1f);
        return Create("levelUp", d);
    }

    // Sok dalgasi: alcalan ugultu + gurultu patlamasi
    private static AudioClip MakePulse()
    {
        int n = (int)(Rate * 0.55f);
        var d = new float[n];
        var rng = new System.Random(7);
        float phase = 0f;
        float y = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float f = Mathf.Lerp(220f, 55f, Mathf.Clamp01(t / 0.5f));
            phase += 2f * Mathf.PI * f / Rate;
            float noise = (float)rng.NextDouble() * 2f - 1f;
            y += (noise - y) * 0.15f;
            d[i] = (Mathf.Sin(phase) * 0.7f + y * 0.6f) * Mathf.Exp(-t * 5f) * Mathf.Clamp01(t * 80f);
        }
        return Create("pulse", d);
    }

    // Yildirim: kisa, sert cizirti
    private static AudioClip MakeZap()
    {
        int n = (int)(Rate * 0.16f);
        var d = new float[n];
        var rng = new System.Random(9);
        float prev = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float noise = (float)rng.NextDouble() * 2f - 1f;
            float hp = noise - prev * 0.6f;      // basit yuksek geciren
            prev = noise;
            float env = Mathf.Exp(-t * 20f) * (0.6f + 0.4f * Mathf.Sin(t * 900f));
            d[i] = hp * env * 0.7f;
        }
        return Create("zap", d);
    }

    private static AudioClip MakeShieldHit()
    {
        int n = (int)(Rate * 0.18f);
        var d = new float[n];
        float phase = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float f = Mathf.Lerp(1100f, 320f, t / 0.18f);       // alcalan "zap"
            phase += 2f * Mathf.PI * f / Rate;
            d[i] = Mathf.Sin(phase) * Mathf.Exp(-t * 16f) * 0.55f;
        }
        return Create("shieldHit", d);
    }

    private static AudioClip MakeLaunch()
    {
        int n = (int)(Rate * 0.7f);
        var d = new float[n];
        var rng = new System.Random(4);
        float y = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float noise = (float)rng.NextDouble() * 2f - 1f;
            y += (noise - y) * 0.2f;
            d[i] = y * 1.6f * Mathf.Clamp01(t * 30f) * Mathf.Exp(-t * 3.2f);
        }
        return Create("launch", d);
    }

    // ---- Lazer, boss, uyari, arayuz sesleri
    private static AudioClip beamHum, beamCharge, warning, victory, bossRoar, click, bossAttack, killDing, shotgun, plasma, rail, crack, lockTick;

    public static AudioClip BeamHum => beamHum != null ? beamHum : (beamHum = MakeBeamHum());
    public static AudioClip BeamCharge => beamCharge != null ? beamCharge : (beamCharge = MakeBeamCharge());
    public static AudioClip Warning => warning != null ? warning : (warning = MakeWarning());
    public static AudioClip Victory => victory != null ? victory : (victory = MakeVictory());
    public static AudioClip BossRoar => bossRoar != null ? bossRoar : (bossRoar = MakeBossRoar());
    public static AudioClip Click => click != null ? click : (click = MakeClick());
    public static AudioClip Lock => lockTick != null ? lockTick : (lockTick = MakeLock());
    public static AudioClip Crack => crack != null ? crack : (crack = MakeCrack());
    public static AudioClip Shotgun => shotgun != null ? shotgun : (shotgun = MakeShotgun());
    public static AudioClip Plasma => plasma != null ? plasma : (plasma = MakePlasma());
    public static AudioClip Rail => rail != null ? rail : (rail = MakeRail());
    public static AudioClip KillDing => killDing != null ? killDing : (killDing = MakeKillDing());
    public static AudioClip BossAttack => bossAttack != null ? bossAttack : (bossAttack = MakeBossAttack());

    // Ruzgar ugultusu (Kartal firtina kanadi): yavasca dalgalanan suzgecli gurultu, kabarip diner
    private static AudioClip wind;
    public static AudioClip Wind => wind != null ? wind : (wind = MakeWind());

    private static AudioClip MakeWind()
    {
        const float len = 4f;
        int n = (int)(Rate * len);
        var d = new float[n];
        var rng = new System.Random(77);
        float y = 0f, y2 = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float noise = (float)rng.NextDouble() * 2f - 1f;
            // Suzgec acikligi yavasca dalgalanir: "vuuuu" ugultusu
            float cut = 0.02f + 0.05f * (0.5f + 0.5f * Mathf.Sin(t * 2.3f) * Mathf.Sin(t * 0.9f + 1f));
            y += (noise - y) * cut;
            y2 += (y - y2) * 0.3f;
            float env = Mathf.Clamp01(t / 0.9f) * Mathf.Clamp01((len - t) / 1.2f);
            d[i] = Mathf.Clamp(y2 * 7f * env, -1f, 1f);
        }
        return Create("wind", d);
    }

    // Lazer uguldamasi: 0,5 sn dikisiz dongu (frekanslar tam periyot icin 100 Hz'in katlari)
    private static AudioClip MakeBeamHum()
    {
        int n = Rate / 2;
        var d = new float[n];
        var rng = new System.Random(21);
        float y = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float noise = (float)rng.NextDouble() * 2f - 1f;
            y += (noise - y) * 0.05f;
            float saw = ((t * 200f) % 1f) * 2f - 1f;
            d[i] = (Mathf.Sin(2f * Mathf.PI * 100f * t) * 0.5f + saw * 0.18f + Mathf.Sin(2f * Mathf.PI * 400f * t) * 0.1f + y * 0.25f) * 0.7f;
        }
        return Create("beamHum", d);
    }

    // Yukselen sarj sesi (boss isini hazirligi)
    private static AudioClip MakeBeamCharge()
    {
        int n = (int)(Rate * 0.9f);
        var d = new float[n];
        float phase = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float f = Mathf.Lerp(140f, 1100f, (t / 0.9f) * (t / 0.9f));
            phase += 2f * Mathf.PI * f / Rate;
            d[i] = (Mathf.Sin(phase) * 0.5f + Mathf.Sin(phase * 2f) * 0.2f) * Mathf.Clamp01(t * 6f) * Mathf.Clamp01((0.9f - t) * 8f);
        }
        return Create("beamCharge", d);
    }

    private static AudioClip MakeWarning()
    {
        int n = (int)(Rate * 0.26f);
        var d = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float f = t < 0.12f ? 880f : 660f;
            float env = (t < 0.12f ? Mathf.Exp(-(t) * 12f) : Mathf.Exp(-(t - 0.12f) * 12f));
            d[i] = Mathf.Sin(2f * Mathf.PI * f * t) * env * 0.5f;
        }
        return Create("warning", d);
    }

    private static AudioClip MakeVictory()
    {
        float[] notes = { 523.25f, 659.25f, 783.99f, 1046.5f, 783.99f, 1046.5f, 1318.5f };
        float[] len = { 0.14f, 0.14f, 0.14f, 0.3f, 0.12f, 0.12f, 0.6f };
        int n = (int)(Rate * 1.9f);
        var d = new float[n];
        float start = 0f;
        for (int k = 0; k < notes.Length; k++)
        {
            int s0 = (int)(start * Rate);
            for (int i = s0; i < n; i++)
            {
                float t = (i - s0) / (float)Rate;
                float s = Mathf.Sin(2f * Mathf.PI * notes[k] * t) + 0.4f * Mathf.Sin(4f * Mathf.PI * notes[k] * t) + 0.2f * Mathf.Sin(6f * Mathf.PI * notes[k] * t);
                d[i] += s * Mathf.Exp(-t * 3.5f) * 0.22f;
            }
            start += len[k];
        }
        for (int i = 0; i < n; i++) d[i] = Mathf.Clamp(d[i], -1f, 1f);
        return Create("victory", d);
    }

    // Boss girisi: alcak, titresen kukreme
    private static AudioClip MakeBossRoar()
    {
        int n = (int)(Rate * 1.4f);
        var d = new float[n];
        var rng = new System.Random(31);
        float phase = 0f, y = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float f = 52f + Mathf.Sin(t * 14f) * 9f - t * 12f;
            phase += 2f * Mathf.PI * f / Rate;
            float noise = (float)rng.NextDouble() * 2f - 1f;
            y += (noise - y) * 0.06f;
            float saw = ((phase / (2f * Mathf.PI)) % 1f) * 2f - 1f;
            d[i] = (saw * 0.6f + Mathf.Sin(phase) * 0.5f + y * 0.5f) * Mathf.Clamp01(t * 10f) * Mathf.Exp(-t * 1.4f);
        }
        for (int i = 0; i < n; i++) d[i] = Mathf.Clamp(d[i], -1f, 1f);
        return Create("bossRoar", d);
    }

    // Oldurme onayi: kisa, parlak iki notalik "ding"
    private static AudioClip MakeKillDing()
    {
        int n = (int)(Rate * 0.16f);
        var d = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float a = Mathf.Sin(2f * Mathf.PI * 1046f * t) * Mathf.Exp(-t * 28f);
            float b = t > 0.045f ? Mathf.Sin(2f * Mathf.PI * 1568f * (t - 0.045f)) * Mathf.Exp(-(t - 0.045f) * 30f) : 0f;
            d[i] = (a * 0.5f + b * 0.55f) * 0.6f;
        }
        return Create("killDing", d);
    }

    // Saçma: kalin, alcak patlama
    private static AudioClip MakeShotgun()
    {
        int n = (int)(Rate * 0.22f);
        var d = new float[n];
        var rng = new System.Random(51);
        float y = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            y += ((float)rng.NextDouble() * 2f - 1f - y) * 0.25f;
            d[i] = (y * 0.9f + Mathf.Sin(2f * Mathf.PI * 90f * t) * 0.7f) * Mathf.Exp(-t * 20f);
        }
        return Create("shotgun", d);
    }

    // Plazma: yukselen-dusen "bwoop"
    private static AudioClip MakePlasma()
    {
        int n = (int)(Rate * 0.2f);
        var d = new float[n];
        float phase = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float f = Mathf.Lerp(900f, 220f, t / 0.2f);
            phase += 2f * Mathf.PI * f / Rate;
            d[i] = (Mathf.Sin(phase) * 0.5f + Mathf.Sign(Mathf.Sin(phase * 0.5f)) * 0.15f) * Mathf.Exp(-t * 11f);
        }
        return Create("plasma", d);
    }

    // Ray tufegi: keskin "zap" + alcak vurus
    private static AudioClip MakeRail()
    {
        int n = (int)(Rate * 0.3f);
        var d = new float[n];
        float phase = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float f = Mathf.Lerp(3200f, 400f, Mathf.Clamp01(t / 0.12f));
            phase += 2f * Mathf.PI * f / Rate;
            d[i] = (Mathf.Sin(phase) * 0.45f * Mathf.Exp(-t * 14f) + Mathf.Sin(2f * Mathf.PI * 70f * t) * 0.6f * Mathf.Exp(-t * 9f));
        }
        return Create("rail", d);
    }

    // Uzuv kopmasi: metal yirtilmasi (kisa gurultu patlamasi + alcak catirti)
    private static AudioClip MakeCrack()
    {
        int n = (int)(Rate * 0.35f);
        var d = new float[n];
        var rng = new System.Random(77);
        float y = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float noise = (float)rng.NextDouble() * 2f - 1f;
            y += (noise - y) * 0.45f;
            float crackle = (rng.NextDouble() < 0.02 ? (float)rng.NextDouble() : 0f) * Mathf.Exp(-t * 6f);
            d[i] = (y * 0.8f * Mathf.Exp(-t * 14f) + Mathf.Sin(2f * Mathf.PI * 55f * t) * 0.5f * Mathf.Exp(-t * 7f) + crackle * 0.8f);
        }
        return Create("crack", d);
    }

    // Nisan alindi: iki kisa tiz blip
    private static AudioClip MakeLock()
    {
        int n = (int)(Rate * 0.16f);
        var d = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float seg = t < 0.07f ? t : (t >= 0.09f ? t - 0.09f : -1f);
            if (seg < 0f) continue;
            d[i] = Mathf.Sin(2f * Mathf.PI * 1900f * seg) * Mathf.Exp(-seg * 60f) * 0.45f;
        }
        return Create("lock", d);
    }

    private static AudioClip MakeClick()
    {
        int n = (int)(Rate * 0.05f);
        var d = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            d[i] = Mathf.Sin(2f * Mathf.PI * 1200f * t) * Mathf.Exp(-t * 90f) * 0.5f;
        }
        return Create("click", d);
    }

    // Boss saldirisi baslarken: alcak "vurus"
    private static AudioClip MakeBossAttack()
    {
        int n = (int)(Rate * 0.3f);
        var d = new float[n];
        var rng = new System.Random(41);
        float phase = 0f, y = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float f = Mathf.Lerp(180f, 60f, t / 0.3f);
            phase += 2f * Mathf.PI * f / Rate;
            float noise = (float)rng.NextDouble() * 2f - 1f;
            y += (noise - y) * 0.12f;
            d[i] = (Mathf.Sin(phase) * 0.7f + y * 0.4f) * Mathf.Exp(-t * 9f);
        }
        return Create("bossAttack", d);
    }

    public static void PlayAt(AudioClip clip, Vector3 position, float volume = 1f)
    {
        if (clip != null) AudioSource.PlayClipAtPoint(clip, position, volume);
    }

    private static AudioClip MakeShot()
    {
        int n = (int)(Rate * 0.09f);
        var d = new float[n];
        var rng = new System.Random(1);
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float noise = (float)rng.NextDouble() * 2f - 1f;
            float thump = Mathf.Sin(2f * Mathf.PI * 140f * t);
            d[i] = (noise * 0.7f + thump * 0.6f) * Mathf.Exp(-t * 45f);
        }
        return Create("shot", d);
    }

    private static AudioClip MakeExplosion()
    {
        int n = (int)(Rate * 1.6f);
        var d = new float[n];
        var rng = new System.Random(2);
        float y = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float noise = (float)rng.NextDouble() * 2f - 1f;
            y += (noise - y) * 0.08f;                       // basit low-pass
            float rumble = Mathf.Sin(2f * Mathf.PI * 55f * t) * Mathf.Exp(-t * 3f) * 0.6f;
            d[i] = Mathf.Clamp(y * 3f * Mathf.Exp(-t * 2.2f) + rumble, -1f, 1f);
        }
        return Create("explosion", d);
    }

    private static AudioClip MakeHit()
    {
        int n = (int)(Rate * 0.12f);
        var d = new float[n];
        var rng = new System.Random(3);
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float noise = (float)rng.NextDouble() * 2f - 1f;
            d[i] = Mathf.Sin(2f * Mathf.PI * 1500f * t) * Mathf.Exp(-t * 40f) * 0.5f
                   + noise * Mathf.Exp(-t * 60f) * 0.5f;
        }
        return Create("hit", d);
    }

    private static AudioClip Create(string name, float[] data)
    {
        var clip = AudioClip.Create(name, data.Length, 1, Rate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
