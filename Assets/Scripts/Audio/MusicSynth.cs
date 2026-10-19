using UnityEngine;

// Muzik sentezleyici: kisa sentez sesleri (bas, arpej, pad, davul) ile dikisiz donen 8 olculuk parcalar uretir.
// Sesler dongunun sonunda basa sarilir (kuyruk ucu), boylece geciste cizirti olmaz.
public static class MusicSynth
{
    private const int Rate = 22050;

    // Am - F - C - G  (kok notalar, MIDI)
    private static readonly int[] Roots = { 45, 41, 48, 43 };
    private static readonly int[][] Chords =
    {
        new[] { 0, 3, 7 }, new[] { 0, 4, 7 }, new[] { 0, 4, 7 }, new[] { 0, 4, 7 }
    };

    private static float Hz(int midi) { return 440f * Mathf.Pow(2f, (midi - 69) / 12f); }

    public static AudioClip Make(MusicTrack track)
    {
        float bpm; bool drums; bool lead; float padGain; float bassGain;
        switch (track)
        {
            case MusicTrack.Hangar: bpm = 76f; drums = false; lead = true; padGain = 0.5f; bassGain = 0.25f; break;
            case MusicTrack.Boss: bpm = 142f; drums = true; lead = true; padGain = 0.22f; bassGain = 0.5f; break;
            default: bpm = 112f; drums = true; lead = true; padGain = 0.3f; bassGain = 0.4f; break;
        }

        int bars = 8;
        float beat = 60f / bpm;
        int total = Mathf.RoundToInt(bars * 4 * beat * Rate);
        var d = new float[total];
        var rng = new System.Random(11 + (int)track);

        for (int bar = 0; bar < bars; bar++)
        {
            int prog = (bar / 2) % 4;
            int root = Roots[prog];
            int[] ch = Chords[prog];
            float barStart = bar * 4 * beat;

            // Pad: akor sesleri, tum olcu boyunca
            for (int k = 0; k < ch.Length; k++)
                AddTone(d, barStart, 4 * beat * 1.05f, Hz(root + 12 + ch[k]), padGain * 0.12f, 0.6f, 0.9f, 0);

            // Bas: sekizlikler
            for (int e = 0; e < 8; e++)
            {
                float t = barStart + e * beat * 0.5f;
                int note = root - (e % 4 == 3 ? 0 : 0) + (e == 5 ? 7 : 0);
                AddTone(d, t, beat * 0.45f, Hz(note), bassGain * 0.18f, 0.01f, 0.12f, 1);
            }

            // Arpej / lead: onaltilik nota (hangar'da yavas)
            if (lead)
            {
                int steps = track == MusicTrack.Hangar ? 4 : 8;
                float stepLen = 4 * beat / steps;
                for (int s = 0; s < steps; s++)
                {
                    int[] pat = { 0, 7, 12, 7, 3, 10, 15, 10 };
                    int note = root + 24 + (track == MusicTrack.Boss ? ch[(s + bar) % ch.Length] + (s % 2) * 12 : ch[s % ch.Length] + (s % 4 == 3 ? 12 : 0));
                    if (track == MusicTrack.Hangar) note = root + 24 + pat[s % pat.Length] % 12;
                    AddTone(d, barStart + s * stepLen, stepLen * 0.9f, Hz(note), track == MusicTrack.Hangar ? 0.05f : 0.045f, 0.004f, 0.08f, 2);
                }
            }

            // Davul: vurus + kapali hat + (boss) trampa
            if (drums)
            {
                for (int q = 0; q < 4; q++)
                {
                    float t = barStart + q * beat;
                    AddKick(d, t, 0.34f);
                    if (track == MusicTrack.Boss && q % 2 == 1) AddSnare(d, t, 0.2f, rng);
                    AddHat(d, t + beat * 0.5f, 0.08f, rng);
                    if (track == MusicTrack.Boss) AddHat(d, t, 0.05f, rng);
                }
            }
        }

        // Yumusak sinirlama
        for (int i = 0; i < total; i++) d[i] = (float)System.Math.Tanh(d[i] * 1.4f) * 0.85f;

        var clip = AudioClip.Create("music_" + track, total, 1, Rate, false);
        clip.SetData(d, 0);
        return clip;
    }

    // Tek nota: attack/release (sn), dalga turu 0 = sinus+harmonik (pad), 1 = testere (bas), 2 = kare (lead)
    private static void AddTone(float[] d, float start, float length, float freq, float gain, float attack, float release, int wave)
    {
        int n = d.Length;
        int s0 = Mathf.RoundToInt(start * Rate);
        int len = Mathf.RoundToInt((length + release) * Rate);
        float phase = 0f;
        float lp = 0f;
        for (int i = 0; i < len; i++)
        {
            float t = i / (float)Rate;
            float env = Mathf.Clamp01(t / Mathf.Max(attack, 0.0005f));
            if (t > length) env *= Mathf.Clamp01(1f - (t - length) / Mathf.Max(release, 0.001f));
            phase += freq / Rate;
            phase -= Mathf.Floor(phase);
            float v;
            if (wave == 1)
            {
                float saw = phase * 2f - 1f;
                lp += (saw - lp) * 0.18f;             // yumusatilmis testere
                v = lp;
            }
            else if (wave == 2)
            {
                v = (phase < 0.5f ? 1f : -1f) * 0.5f + Mathf.Sin(phase * 2f * Mathf.PI) * 0.5f;
                v *= Mathf.Exp(-t * 6f);
            }
            else
            {
                v = Mathf.Sin(phase * 2f * Mathf.PI) + 0.4f * Mathf.Sin(phase * 4f * Mathf.PI);
            }
            d[(s0 + i) % n] += v * env * gain;
        }
    }

    private static void AddKick(float[] d, float start, float gain)
    {
        int n = d.Length;
        int s0 = Mathf.RoundToInt(start * Rate);
        int len = Mathf.RoundToInt(0.18f * Rate);
        float phase = 0f;
        for (int i = 0; i < len; i++)
        {
            float t = i / (float)Rate;
            float f = Mathf.Lerp(150f, 42f, Mathf.Clamp01(t / 0.12f));
            phase += f / Rate;
            d[(s0 + i) % n] += Mathf.Sin(phase * 2f * Mathf.PI) * Mathf.Exp(-t * 18f) * gain;
        }
    }

    private static void AddSnare(float[] d, float start, float gain, System.Random rng)
    {
        int n = d.Length;
        int s0 = Mathf.RoundToInt(start * Rate);
        int len = Mathf.RoundToInt(0.14f * Rate);
        for (int i = 0; i < len; i++)
        {
            float t = i / (float)Rate;
            float noise = (float)rng.NextDouble() * 2f - 1f;
            d[(s0 + i) % n] += (noise * 0.7f + Mathf.Sin(t * 2f * Mathf.PI * 190f) * 0.4f) * Mathf.Exp(-t * 26f) * gain;
        }
    }

    private static void AddHat(float[] d, float start, float gain, System.Random rng)
    {
        int n = d.Length;
        int s0 = Mathf.RoundToInt(start * Rate);
        int len = Mathf.RoundToInt(0.05f * Rate);
        float prev = 0f;
        for (int i = 0; i < len; i++)
        {
            float t = i / (float)Rate;
            float noise = (float)rng.NextDouble() * 2f - 1f;
            float hp = noise - prev;
            prev = noise;
            d[(s0 + i) % n] += hp * Mathf.Exp(-t * 60f) * gain * 0.5f;
        }
    }
}
