using System.Collections.Generic;
using UnityEngine;

// Kahraman karakterlerin prosedurel "anime sesi": ses dosyasi yok, unlu-benzeri tonlar sentezlenir (temel frekans
// kaymasi + harmonikler + formant vurgusu + vibrato). Her karakterin kendi tinisi var:
//   0 Ay Savascisi: ince, yuksek (kiz sesi)   1 Ki Kahramani: orta, sert bagiris   2 Imparator: pes, hirilti ve kahkaha
// Kullanim: saldirirken ara ara kisa unlem, yetenekte uzun bagiris, vurulunca kisa ciglik.
public static class HeroVoice
{
    private const int Rate = 44100;

    private struct Seg
    {
        public float f0, f1, dur, amp, formA, formB, growl;
        public Seg(float f0, float f1, float dur, float amp, float formA, float formB, float growl = 0f)
        { this.f0 = f0; this.f1 = f1; this.dur = dur; this.amp = amp; this.formA = formA; this.formB = formB; this.growl = growl; }
    }

    private static readonly Dictionary<string, AudioClip> cache = new Dictionary<string, AudioClip>();
    private static float nextAttack;

    /// <summary>Model indeksi kahramansa kahraman sirasi (0-2), degilse -1.</summary>
    public static int HeroOf(int model) => PlaneBuilder.IsHero(model) ? model - PlaneBuilder.HeroStart : -1;

    /// <summary>Saldiri unlemi (ates ederken ~1,5 sn'de bir, rastgele iki varyant).</summary>
    public static void Attack(int hero, Vector3 at)
    {
        if (Time.time < nextAttack) return;
        nextAttack = Time.time + Random.Range(1.3f, 2.2f);
        int v = Random.Range(0, 2);
        Play("atk" + hero + v, hero, at, 0.55f, () => AttackSegs(hero, v));
    }

    /// <summary>Yetenek bagirisi (uzun).</summary>
    public static void Special(int hero, Vector3 at)
    {
        Play("spc" + hero, hero, at, 0.8f, () => SpecialSegs(hero));
    }

    /// <summary>Vurulma cigligi.</summary>
    public static void Hurt(int hero, Vector3 at)
    {
        Play("hurt" + hero, hero, at, 0.6f, () => HurtSegs(hero));
    }

    private static Seg[] AttackSegs(int hero, int v)
    {
        switch (hero)
        {
            case 0: // "ha!" / "ya~!"
                return v == 0
                    ? new[] { new Seg(380f, 470f, 0.22f, 1f, 800f, 1400f) }
                    : new[] { new Seg(330f, 520f, 0.3f, 1f, 700f, 1800f) };
            case 1: // sert "ha!" / "ta!"
                return v == 0
                    ? new[] { new Seg(230f, 190f, 0.2f, 1f, 700f, 1200f, 0.15f) }
                    : new[] { new Seg(260f, 300f, 0.16f, 1f, 650f, 1300f, 0.15f) };
            default: // pes "hmm" / "hah"
                return v == 0
                    ? new[] { new Seg(120f, 105f, 0.35f, 0.9f, 500f, 1000f, 0.45f) }
                    : new[] { new Seg(140f, 120f, 0.22f, 1f, 600f, 1100f, 0.4f) };
        }
    }

    private static Seg[] SpecialSegs(int hero)
    {
        switch (hero)
        {
            case 0: // yukselen uzun "aaaa~" (buyu sozu)
                return new[] { new Seg(340f, 400f, 0.35f, 0.9f, 750f, 1500f), new Seg(400f, 560f, 0.75f, 1f, 800f, 1700f) };
            case 1: // "KA-ME-HA-ME-HAAA": dort kisa hece + uzun bagiris
                return new[]
                {
                    new Seg(210f, 230f, 0.18f, 0.9f, 700f, 1200f, 0.1f), new Seg(0f, 0f, 0.06f, 0f, 0f, 0f),
                    new Seg(220f, 240f, 0.18f, 0.9f, 500f, 1500f, 0.1f), new Seg(0f, 0f, 0.06f, 0f, 0f, 0f),
                    new Seg(230f, 250f, 0.18f, 0.95f, 700f, 1200f, 0.1f), new Seg(0f, 0f, 0.06f, 0f, 0f, 0f),
                    new Seg(240f, 260f, 0.18f, 0.95f, 500f, 1500f, 0.1f), new Seg(0f, 0f, 0.08f, 0f, 0f, 0f),
                    new Seg(250f, 330f, 0.9f, 1f, 750f, 1300f, 0.2f)
                };
            default: // kotu kahkaha: pes, kisa darbeler
                return new[]
                {
                    new Seg(140f, 120f, 0.16f, 1f, 600f, 1000f, 0.5f), new Seg(0f, 0f, 0.07f, 0f, 0f, 0f),
                    new Seg(135f, 115f, 0.16f, 1f, 600f, 1000f, 0.5f), new Seg(0f, 0f, 0.07f, 0f, 0f, 0f),
                    new Seg(130f, 110f, 0.16f, 1f, 600f, 1000f, 0.5f), new Seg(0f, 0f, 0.07f, 0f, 0f, 0f),
                    new Seg(125f, 95f, 0.45f, 1f, 550f, 950f, 0.6f)
                };
        }
    }

    private static Seg[] HurtSegs(int hero)
    {
        switch (hero)
        {
            case 0: return new[] { new Seg(620f, 380f, 0.32f, 1f, 900f, 2000f) };          // "kyaa!"
            case 1: return new[] { new Seg(260f, 170f, 0.26f, 1f, 650f, 1200f, 0.2f) };    // "ugh!"
            default: return new[] { new Seg(150f, 90f, 0.4f, 1f, 550f, 1000f, 0.5f) };     // "grrh"
        }
    }

    private static void Play(string key, int hero, Vector3 at, float volume, System.Func<Seg[]> make)
    {
        AudioClip clip;
        if (!cache.TryGetValue(key, out clip) || clip == null)
        {
            clip = Synth(key, make());
            cache[key] = clip;
        }
        if (clip != null) AudioSource.PlayClipAtPoint(clip, at, volume);
    }

    // Unlu sentezi: harmonik toplam, iki formant tepesiyle agirlikli; vibrato; hirilti (growl) alt-harmonik modulasyonu
    private static AudioClip Synth(string name, Seg[] segs)
    {
        float total = 0f;
        foreach (var s in segs) total += s.dur;
        int n = Mathf.Max(1, (int)(Rate * (total + 0.05f)));
        var d = new float[n];
        var rng = new System.Random(name.GetHashCode());
        float phase = 0f;
        int i = 0;
        foreach (var s in segs)
        {
            int len = (int)(Rate * s.dur);
            for (int k = 0; k < len && i < n; k++, i++)
            {
                if (s.amp <= 0f) { d[i] = 0f; continue; }
                float t = k / (float)Rate;
                float u = k / (float)len;
                float f = Mathf.Lerp(s.f0, s.f1, u) * (1f + 0.025f * Mathf.Sin(2f * Mathf.PI * 5.5f * t + 1f));
                phase += 2f * Mathf.PI * f / Rate;
                float v = 0f;
                for (int h = 1; h <= 10; h++)
                {
                    float hf = f * h;
                    float w = Mathf.Exp(-Mathf.Pow((hf - s.formA) / 220f, 2f)) + 0.7f * Mathf.Exp(-Mathf.Pow((hf - s.formB) / 320f, 2f)) + 0.25f / h;
                    v += Mathf.Sin(phase * h) * w;
                }
                if (s.growl > 0f) v *= 1f - s.growl * 0.5f * (1f + Mathf.Sin(phase * 0.5f));
                float env = Mathf.Min(1f, u / 0.08f) * Mathf.Min(1f, (1f - u) / 0.25f);
                float breath = ((float)rng.NextDouble() * 2f - 1f) * 0.03f;
                d[i] = Mathf.Clamp((v * 0.22f + breath) * env * s.amp, -1f, 1f);
            }
        }
        var clip = AudioClip.Create("voice_" + name, n, 1, Rate, false);
        clip.SetData(d, 0);
        return clip;
    }
}
