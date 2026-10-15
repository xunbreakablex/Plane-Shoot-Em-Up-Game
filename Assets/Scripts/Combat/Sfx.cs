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
