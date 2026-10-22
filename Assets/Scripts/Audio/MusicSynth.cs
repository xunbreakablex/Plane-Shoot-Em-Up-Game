using UnityEngine;

// Muzik sentezleyici: her asamanin kendi tonu, temposu ve akor dizisi olan, bolumlu (giris, A, B, C, A2, ara) uzun
// parcalar uretir; boss parcasi ayri (armonik minor, hizli, dortluk kick, galop bas). Ezgi 2 olculuk bir motiften
// turer: motif her akorda yeniden kurulur (sekans), 8 olculuk cumlenin sonunda akorun kokunde kapanir. Lead'e noktali
// sekizlik yanki eklenir. Sesler dongunun sonunda basa sarilir (kuyruk ucu), boylece geciste cizirti olmaz.
// Render yalniz saf C# kullanir: MusicPlayer onu arka planda (Task) calistirir, AudioClip'i ana is parcaciginda kurar.
public static class MusicSynth
{
    public const int Rate = 22050;
    public const int StageStyles = 5;

    private static readonly int[] Major = { 0, 2, 4, 5, 7, 9, 11 };
    private static readonly int[] Minor = { 0, 2, 3, 5, 7, 8, 10 };
    private static readonly int[] Dorian = { 0, 2, 3, 5, 7, 9, 10 };
    private static readonly int[] HarmMinor = { 0, 2, 3, 5, 7, 8, 11 };
    private static readonly int[] Lydian = { 0, 2, 4, 6, 7, 9, 11 };

    // Bolum katmanlari
    private const int Pad = 1, Bass = 2, Drums = 4, DrumsLight = 8, Arp = 16, Lead = 32, LeadAlt = 64, Harmony = 128,
                      UseB = 256, Fill = 512, Crash = 1024;

    private struct Section
    {
        public int bars, flags;
        public Section(int bars, int flags) { this.bars = bars; this.flags = flags; }
    }

    private class Style
    {
        public float bpm;
        public int key;                       // toniğin MIDI notasi (bas bir oktav altinda, ezgi ~E4-D#5 araliginda)
        public int[] scale, progA, progB;     // akor dereceleri (0 tabanli); her akor 2 olcu
        public int bassMode;                  // 0 sekizlik, 1 uzun (sakin), 2 galop (onaltilik)
        public int drumMode;                  // 1 normal, 2 boss (dortluk kick, onaltilik hat)
        public float pad, bass, arp, lead;
        public int leadWave;                  // 2 kare (parlak), 3 testere (sert)
        public int seed;
        public Section[] form;
    }

    private static readonly Section[] CombatForm =
    {
        new Section(4, Pad | Bass | DrumsLight | Fill),
        new Section(8, Pad | Bass | Drums | Arp | Crash | Fill),
        new Section(8, Pad | Bass | Drums | Arp | Lead | Crash | Fill),
        new Section(8, Pad | Bass | Drums | Arp | LeadAlt | UseB | Crash | Fill),
        new Section(8, Pad | Bass | Drums | Arp | Lead | Harmony | Crash | Fill),
        new Section(4, Pad | Bass | DrumsLight | UseB | Fill),
    };

    private static readonly Section[] BossForm =
    {
        new Section(2, Pad | Bass | DrumsLight | Fill),
        new Section(8, Pad | Bass | Drums | Arp | Crash | Fill),
        new Section(8, Pad | Bass | Drums | Arp | Lead | Crash | Fill),
        new Section(8, Pad | Bass | Drums | Arp | LeadAlt | UseB | Crash | Fill),
        new Section(8, Pad | Bass | Drums | Arp | Lead | Harmony | Crash | Fill),
    };

    private static readonly Section[] HangarForm =
    {
        new Section(8, Pad | Bass | Arp),
        new Section(8, Pad | Bass | Arp | Lead),
    };

    private static Style StyleOf(MusicTrack track, int stage)
    {
        stage = ((stage % StageStyles) + StageStyles) % StageStyles;
        if (track == MusicTrack.Hangar)
            return new Style
            {
                bpm = 84f, key = 55, scale = Major, progA = new[] { 0, 5, 3, 4 }, progB = new[] { 3, 4, 0, 5 },
                bassMode = 1, drumMode = 0, pad = 0.55f, bass = 0.22f, arp = 0.045f, lead = 0.06f, leadWave = 2, seed = 7,
                form = HangarForm
            };
        if (track == MusicTrack.Boss)
        {
            int[] keys = { 57, 52, 52, 45, 53 };
            return new Style
            {
                bpm = 150f + 2f * stage, key = keys[stage], scale = HarmMinor, progA = new[] { 0, 5, 3, 4 }, progB = new[] { 5, 3, 0, 4 },
                bassMode = 2, drumMode = 2, pad = 0.25f, bass = 0.5f, arp = 0.035f, lead = 0.07f, leadWave = 3, seed = 101 + stage * 7,
                form = BossForm
            };
        }
        switch (stage)
        {
            case 1:   // Firtina Hatti: dorian, itici
                return new Style
                {
                    bpm = 128f, key = 50, scale = Dorian, progA = new[] { 0, 3, 0, 6 }, progB = new[] { 2, 3, 4, 3 },
                    bassMode = 0, drumMode = 1, pad = 0.3f, bass = 0.42f, arp = 0.045f, lead = 0.065f, leadWave = 2, seed = 23,
                    form = CombatForm
                };
            case 2:   // Kizil Hat: minor, gergin
                return new Style
                {
                    bpm = 134f, key = 52, scale = Minor, progA = new[] { 0, 5, 2, 6 }, progB = new[] { 3, 0, 4, 5 },
                    bassMode = 0, drumMode = 1, pad = 0.28f, bass = 0.45f, arp = 0.04f, lead = 0.065f, leadWave = 3, seed = 37,
                    form = CombatForm
                };
            case 3:   // Cehennem Kapisi: armonik minor, agir
                return new Style
                {
                    bpm = 140f, key = 45, scale = HarmMinor, progA = new[] { 0, 5, 3, 4 }, progB = new[] { 0, 3, 5, 4 },
                    bassMode = 2, drumMode = 1, pad = 0.3f, bass = 0.48f, arp = 0.035f, lead = 0.065f, leadWave = 3, seed = 53,
                    form = CombatForm
                };
            case 4:   // Gokyuzu Tapinagi: lidya, destansi
                return new Style
                {
                    bpm = 120f, key = 53, scale = Lydian, progA = new[] { 0, 1, 0, 1 }, progB = new[] { 5, 1, 4, 0 },
                    bassMode = 0, drumMode = 1, pad = 0.45f, bass = 0.38f, arp = 0.045f, lead = 0.065f, leadWave = 2, seed = 71,
                    form = CombatForm
                };
            default:  // Sabah Devriyesi: majör, kahramanca
                return new Style
                {
                    bpm = 124f, key = 48, scale = Major, progA = new[] { 0, 4, 5, 3 }, progB = new[] { 3, 4, 2, 5 },
                    bassMode = 0, drumMode = 1, pad = 0.3f, bass = 0.42f, arp = 0.045f, lead = 0.07f, leadWave = 2, seed = 11,
                    form = CombatForm
                };
        }
    }

    // ------------------------------------------------------------------ parca

    /// <summary>Parcanin orneklerini uretir (mono, Rate). Is parcacigi guvenli: Unity nesnesine dokunmaz.</summary>
    public static float[] Render(MusicTrack track, int stage)
    {
        Style st = StyleOf(track, stage);
        float beat = 60f / st.bpm;
        float barLen = 4f * beat;
        float step = beat / 4f;   // onaltilik

        int totalBars = 0;
        foreach (Section s in st.form) totalBars += s.bars;
        int n = Mathf.RoundToInt(totalBars * barLen * Rate);
        var mix = new float[n];
        var lead = new float[n];
        var rng = new System.Random(st.seed);

        // Ezginin tonigi: E4-D#5 araligina dusen oktav
        int leadBase = st.key + 12 * Mathf.CeilToInt((64 - st.key) / 12f);
        Melody m1 = Melody.Make(new System.Random(st.seed * 3 + 1));
        Melody m2 = Melody.Make(new System.Random(st.seed * 5 + 2));

        int bar0 = 0;
        foreach (Section sec in st.form)
        {
            int[] prog = (sec.flags & UseB) != 0 ? st.progB : st.progA;
            bool full = (sec.flags & Drums) != 0;
            bool hasLead = (sec.flags & (Lead | LeadAlt)) != 0;
            for (int b = 0; b < sec.bars; b++)
            {
                float t0 = (bar0 + b) * barLen;
                int deg = prog[(b / 2) % prog.Length];
                bool lastBar = b == sec.bars - 1;

                if ((sec.flags & Pad) != 0) PadChord(mix, st, deg, t0, barLen);
                if ((sec.flags & Bass) != 0) BassBar(mix, st, deg, t0, step, !full);
                if ((sec.flags & Arp) != 0) ArpBar(mix, st, deg, t0, step, hasLead ? 0.65f : 1f);
                if ((sec.flags & (Drums | DrumsLight)) != 0)
                    DrumBar(mix, st, t0, step, full, lastBar && (sec.flags & Fill) != 0, b == 0 && (sec.flags & Crash) != 0, rng);
            }

            if (hasLead)
            {
                Melody mel = (sec.flags & LeadAlt) != 0 ? m2 : m1;
                for (int ph = 0; ph + 8 <= sec.bars; ph += 8)
                    mel.Phrase(lead, st, prog, ph, (bar0 + ph) * barLen, step, leadBase, (sec.flags & Harmony) != 0);
            }
            bar0 += sec.bars;
        }

        // Lead yankisi: noktali sekizlik, iki gecis (dongu sonundan basa sarar)
        int delay = Mathf.RoundToInt(beat * 0.75f * Rate);
        var echo = new float[n];
        for (int pass = 0; pass < 2; pass++)
        {
            for (int i = 0; i < n; i++)
            {
                int j = i - delay;
                if (j < 0) j += n;
                echo[i] = lead[j] + echo[j] * 0.35f;
            }
        }

        float peak = 1e-4f;
        for (int i = 0; i < n; i++)
        {
            mix[i] += lead[i] + echo[i] * 0.3f;
            peak = Mathf.Max(peak, Mathf.Abs(mix[i]));
        }
        // Tepeye gore olcekle, yumusak sinirla
        float g = 1.25f / peak;
        for (int i = 0; i < n; i++) mix[i] = (float)System.Math.Tanh(mix[i] * g) * 0.8f;
        return mix;
    }

    // ------------------------------------------------------------------ katmanlar

    private static int FloorDiv(int a, int b) => a >= 0 ? a / b : (a - b + 1) / b;

    /// <summary>Gam derecesi (oktav tasabilir) -> tonige gore yarim ses.</summary>
    private static int ScaleSemis(Style st, int idx)
    {
        int oct = FloorDiv(idx, 7);
        return st.scale[idx - oct * 7] + 12 * oct;
    }

    private static float Hz(int midi) => 440f * Mathf.Pow(2f, (midi - 69) / 12f);

    private static void PadChord(float[] d, Style st, int deg, float t0, float barLen)
    {
        for (int k = 0; k < 3; k++)
            Note(d, t0, barLen, Hz(st.key + 12 + ScaleSemis(st, deg + 2 * k)), st.pad * 0.09f, 0.35f, 0.5f, 0);
    }

    private static void BassBar(float[] d, Style st, int deg, float t0, float step, bool light)
    {
        int root = st.key - 12 + ScaleSemis(st, deg);
        if (root > st.key - 5) root -= 12;
        float g = st.bass * 0.2f;
        if (light || st.bassMode == 1)
        {
            // Uzun: kok + besli (yarim notalar)
            Note(d, t0, step * 7f, Hz(root), g, 0.01f, 0.2f, 1);
            Note(d, t0 + step * 8f, step * 7f, Hz(root + 7), g * 0.9f, 0.01f, 0.2f, 1);
            return;
        }
        if (st.bassMode == 2)
        {
            // Galop: onaltiliklar, her vurusun sonunda oktav
            for (int s = 0; s < 16; s++)
            {
                int note = s % 4 == 3 ? root + 12 : root;
                Note(d, t0 + s * step, step * 0.8f, Hz(note), g * (s % 4 == 0 ? 1.1f : 0.8f), 0.004f, 0.05f, 1);
            }
            return;
        }
        int[] pat = { 0, 0, 12, 0, 0, 7, 12, 0 };
        for (int e = 0; e < 8; e++)
            Note(d, t0 + e * step * 2f, step * 1.7f, Hz(root + pat[e]), g * (e % 2 == 0 ? 1.05f : 0.85f), 0.006f, 0.08f, 1);
    }

    private static void ArpBar(float[] d, Style st, int deg, float t0, float step, float mul)
    {
        int[] pat = { 0, 2, 4, 7, 4, 2, 7, 9 };
        for (int s = 0; s < 16; s++)
        {
            int midi = st.key + 12 + ScaleSemis(st, deg + pat[s % pat.Length]);
            Note(d, t0 + s * step, step * 0.9f, Hz(midi), st.arp * mul * (s % 4 == 0 ? 1.2f : 0.8f), 0.003f, 0.05f, 4);
        }
    }

    private static void DrumBar(float[] d, Style st, float t0, float step, bool full, bool fill, bool crash, System.Random rng)
    {
        bool boss = st.drumMode == 2;
        if (crash) Cymbal(d, t0, 0.07f, rng);
        for (int s = 0; s < 16; s++)
        {
            float t = t0 + s * step;
            if (!full)
            {
                // Giris / ara: yalniz ilk vurus ve sekizlik hat
                if (s == 0) Kick(d, t, 0.3f);
                if (s % 2 == 0) Hat(d, t, 0.05f, rng);
                continue;
            }
            bool kick = boss ? (s % 4 == 0 || s == 14) : (s == 0 || s == 8 || s == 10);
            if (kick) Kick(d, t, 0.34f);
            if (s == 4 || s == 12) Snare(d, t, 0.22f, rng);
            if (boss) Hat(d, t, s % 2 == 0 ? 0.07f : 0.04f, rng);
            else if (s % 2 == 0) Hat(d, t, s % 4 == 2 ? 0.08f : 0.05f, rng);
        }
        // Gecis dolgusu: son vurusta yukselen trampa
        if (fill)
            for (int s = 12; s < 16; s++)
                Snare(d, t0 + s * step, 0.08f + 0.05f * (s - 12), rng);
    }

    // ------------------------------------------------------------------ ezgi

    private class Melody
    {
        private static readonly int[][] Bar1 =
        {
            new[] { 4, 2, 2, 4, 4 }, new[] { 2, 2, 4, 2, 2, 4 }, new[] { 6, 2, 4, 4 },
            new[] { 3, 3, 2, 4, 4 }, new[] { 4, 4, 2, 2, 4 }, new[] { 2, 2, 2, 2, 4, 4 },
        };
        private static readonly int[][] Bar2 =
        {
            new[] { 4, 4, 8 }, new[] { 6, 2, 8 }, new[] { 2, 2, 4, 8 }, new[] { 4, 2, 2, 4, -4 }, new[] { 3, 3, 2, 8 },
        };
        private static readonly int[] CadenceBar = { 4, 12 };   // kapanis: kisa nota + uzun kok
        private static readonly int[] Moves = { -2, -1, -1, 1, 1, 2, 0, 1, -1, 3, -3 };
        private const int Lo = -3, Hi = 9;   // ezgi araligi (tonige gore gam derecesi)

        private int[][] rhythm;   // iki olcu, onaltilik sureler; negatif = es
        private int[] contour;    // nota basina derece adimi

        public static Melody Make(System.Random r)
        {
            var m = new Melody { rhythm = new[] { Bar1[r.Next(Bar1.Length)], Bar2[r.Next(Bar2.Length)] } };
            int count = m.rhythm[0].Length + m.rhythm[1].Length;
            m.contour = new int[count];
            for (int i = 0; i < count; i++)
            {
                int mv = Moves[r.Next(Moves.Length)];
                // Yay: ilk yari cogunlukla yukari, ikinci yari asagi
                if (i < count / 2 && mv < 0 && r.NextDouble() < 0.5) mv = -mv;
                if (i >= count / 2 && mv > 0 && r.NextDouble() < 0.5) mv = -mv;
                m.contour[i] = mv;
            }
            return m;
        }

        // Akor tonuna (kok, ucluk, besli) en yakin derece; esitlikte asagi
        private static int Snap(int idx, int chordDeg, bool rootOnly = false)
        {
            for (int dist = 0; dist <= 3; dist++)
            {
                foreach (int cand in new[] { idx - dist, idx + dist })
                {
                    int rel = ((cand - chordDeg) % 7 + 7) % 7;
                    if (rel == 0 || (!rootOnly && (rel == 2 || rel == 4))) return cand;
                }
            }
            return idx;
        }

        private static int Keep(int idx)
        {
            while (idx > Hi) idx -= 2;
            while (idx < Lo) idx += 2;
            return idx;
        }

        /// <summary>8 olculuk cumle: motif (1-2), akorda sekans (3-4), motif (5-6), motifin ilk olcusu + kapanis (7-8).</summary>
        public void Phrase(float[] d, Style st, int[] prog, int barInSection, float t0, float step, int baseMidi, bool harmony)
        {
            float barLen = step * 16f;
            int Chord(int bar) => prog[((barInSection + bar) / 2) % prog.Length];

            int firstStart = Snap(2, Chord(0));
            int idx = firstStart;
            for (int inst = 0; inst < 4; inst++)
            {
                int bar = inst * 2;
                int chord0 = Chord(bar);
                // Sekans: motif, akorun kayma miktari kadar kaydirilarak baslar
                int shift = ((chord0 - Chord(0)) % 7 + 7) % 7;
                if (shift > 3) shift -= 7;
                idx = Keep(Snap(firstStart + shift, chord0));

                int note = 0;
                for (int bb = 0; bb < 2; bb++)
                {
                    int[] rh = inst == 3 && bb == 1 ? CadenceBar : rhythm[bb];
                    int chord = Chord(bar + bb);
                    int pos = 0;
                    for (int k = 0; k < rh.Length; k++, note++)
                    {
                        int dur = Mathf.Abs(rh[k]);
                        if (rh[k] > 0)
                        {
                            bool cadence = inst == 3 && bb == 1 && k == rh.Length - 1;
                            if (note > 0) idx += contour[note % contour.Length];
                            bool strong = pos % 8 == 0 || dur >= 8;
                            if (cadence) idx = Snap(idx, chord, true);
                            else if (strong) idx = Snap(idx, chord);
                            idx = Keep(idx);

                            float t = t0 + (bar + bb) * barLen + pos * step;
                            float len = dur * step * (dur >= 8 ? 0.95f : 0.85f);
                            float vib = dur >= 6 ? 0.006f : 0f;
                            Note(d, t, len, Hz(baseMidi + ScaleSemis(st, idx)), st.lead, 0.008f, 0.09f, st.leadWave, vib);
                            if (harmony)
                                Note(d, t, len, Hz(baseMidi + ScaleSemis(st, idx - 2)), st.lead * 0.5f, 0.008f, 0.09f, st.leadWave, vib);
                        }
                        pos += dur;
                    }
                }
            }
        }
    }

    // ------------------------------------------------------------------ sesler

    // Tek nota. wave: 0 pad (iki hafif akortsuz sinus), 1 bas (yumusak testere), 2 lead kare, 3 lead testere, 4 arp (pluck)
    private static void Note(float[] d, float start, float length, float freq, float gain, float attack, float release, int wave, float vibrato = 0f)
    {
        int n = d.Length;
        int s0 = Mathf.RoundToInt(start * Rate);
        int len = Mathf.RoundToInt((length + release) * Rate);
        float phase = 0f, phase2 = 0f, lp = 0f;
        for (int i = 0; i < len; i++)
        {
            float t = i / (float)Rate;
            float env = Mathf.Min(1f, t / Mathf.Max(attack, 0.0005f));
            if (t > length) env *= Mathf.Clamp01(1f - (t - length) / Mathf.Max(release, 0.001f));
            float f = freq;
            if (vibrato > 0f && t > 0.18f) f *= 1f + vibrato * Mathf.Sin(t * 2f * Mathf.PI * 5.5f) * Mathf.Min(1f, (t - 0.18f) * 4f);
            phase += f / Rate;
            phase -= Mathf.Floor(phase);
            float v;
            switch (wave)
            {
                case 1:
                    lp += ((phase * 2f - 1f) - lp) * 0.2f;
                    v = lp;
                    break;
                case 2:
                    v = ((phase < 0.4f ? 1f : -1f) * 0.45f + Mathf.Sin(phase * 2f * Mathf.PI) * 0.55f) * (0.75f + 0.25f * Mathf.Exp(-t * 5f));
                    break;
                case 3:
                    lp += ((phase * 2f - 1f) - lp) * 0.35f;
                    v = lp * 1.3f * (0.8f + 0.2f * Mathf.Exp(-t * 5f));
                    break;
                case 4:
                    v = ((phase < 0.5f ? 1f : -1f) * 0.4f + Mathf.Sin(phase * 2f * Mathf.PI) * 0.6f) * Mathf.Exp(-t * 9f);
                    break;
                default:
                    phase2 += f * 1.004f / Rate;
                    phase2 -= Mathf.Floor(phase2);
                    v = Mathf.Sin(phase * 2f * Mathf.PI) * 0.5f + Mathf.Sin(phase2 * 2f * Mathf.PI) * 0.5f + 0.25f * Mathf.Sin(phase * 4f * Mathf.PI);
                    break;
            }
            d[(s0 + i) % n] += v * env * gain;
        }
    }

    private static void Kick(float[] d, float start, float gain)
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

    private static void Snare(float[] d, float start, float gain, System.Random rng)
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

    private static void Hat(float[] d, float start, float gain, System.Random rng)
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

    // Zil: bolum basinda uzun, parlak gurultu
    private static void Cymbal(float[] d, float start, float gain, System.Random rng)
    {
        int n = d.Length;
        int s0 = Mathf.RoundToInt(start * Rate);
        int len = Mathf.RoundToInt(1.3f * Rate);
        float prev = 0f;
        for (int i = 0; i < len; i++)
        {
            float t = i / (float)Rate;
            float noise = (float)rng.NextDouble() * 2f - 1f;
            float hp = noise - prev;
            prev = noise;
            d[(s0 + i) % n] += hp * Mathf.Exp(-t * 2.6f) * gain;
        }
    }
}
