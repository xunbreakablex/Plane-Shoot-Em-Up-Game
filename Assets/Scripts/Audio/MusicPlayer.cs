using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public enum MusicTrack { None, Hangar, Combat, Boss }

// Prosedurel muzik: kod ile uretilen parcalar (hangar, her asamaya ozgu savas ve boss). Parcalar arka planda uretilir;
// hazir olana kadar eski parca calar, savas parcasi calarken ayni asamanin boss parcasi onceden hazirlanir. Savasta boss varken boss parcasina
// yumusakca gecer. Duraklatmada kisilir ama durmaz. Ses seviyesi GameAudio.Music.
public class MusicPlayer : MonoBehaviour
{
    private static readonly Dictionary<int, AudioClip> Clips = new Dictionary<int, AudioClip>();
    private static readonly Dictionary<int, Task<float[]>> Pending = new Dictionary<int, Task<float[]>>();
    private int currentKey = -1;

    private AudioSource a, b;
    private AudioSource active;
    private MusicTrack forced = MusicTrack.None;
    private float fade;

    /// <summary>Sahnede tek bir MusicPlayer olusturur. forcedTrack None ise savas durumuna gore secilir.</summary>
    public static MusicPlayer Ensure(GameObject host, MusicTrack forcedTrack)
    {
        var mp = host.GetComponent<MusicPlayer>();
        if (mp == null) mp = host.AddComponent<MusicPlayer>();
        mp.forced = forcedTrack;
        return mp;
    }

    private void Awake()
    {
        a = MakeSource();
        b = MakeSource();
        active = a;
        GameAudio.Changed += Refresh;
    }

    private void OnDestroy()
    {
        GameAudio.Changed -= Refresh;
    }

    private AudioSource MakeSource()
    {
        var s = gameObject.AddComponent<AudioSource>();
        s.loop = true;
        s.playOnAwake = false;
        s.spatialBlend = 0f;
        s.volume = 0f;
        s.ignoreListenerPause = true;   // duraklatmada da calar (kisik)
        return s;
    }

    private void Refresh() { }

    private MusicTrack Wanted()
    {
        if (forced != MusicTrack.None) return forced;

        CombatManager cm = CombatManager.Instance;
        if (cm == null) return MusicTrack.Combat;
        if (RunState.Ended || RunState.StageIsClear) return MusicTrack.Hangar;
        return cm.ActiveBoss != null ? MusicTrack.Boss : MusicTrack.Combat;
    }

    private void Update()
    {
        MusicTrack want = Wanted();
        int style = StyleOf(want);
        int key = KeyOf(want, style);
        AudioClip wantClip = key != currentKey ? Clip(want, style) : null;   // hazir degilse eski parca calmaya devam eder
        if (want == MusicTrack.Combat) Clip(MusicTrack.Boss, style);           // boss parcasini onceden hazirla
        else if (forced == MusicTrack.Hangar) Clip(MusicTrack.Combat, StyleOf(MusicTrack.Combat));   // hangarda: siradaki asamanin parcasi
        if (wantClip != null)
        {
            currentKey = key;
            AudioSource next = active == a ? b : a;
            next.clip = wantClip;
            next.volume = 0f;
            next.Play();
            AudioSource prev = active;
            active = next;
            fade = 0f;
            prevSource = prev;
        }

        float level = GameAudio.Music * 0.32f * (GameState.Paused ? 0.35f : 1f);
        fade = Mathf.Min(1f, fade + Time.unscaledDeltaTime / 1.6f);
        active.volume = Mathf.Lerp(active.volume, level * fade, 8f * Time.unscaledDeltaTime);
        if (prevSource != null && prevSource != active)
        {
            prevSource.volume = Mathf.MoveTowards(prevSource.volume, 0f, Time.unscaledDeltaTime * 0.35f);
            if (prevSource.volume <= 0.001f) prevSource.Stop();
        }
    }

    private AudioSource prevSource;

    private static int StyleOf(MusicTrack t) => t == MusicTrack.Hangar ? 0 : Mathf.Max(0, RunState.Stage) % MusicSynth.StageStyles;
    private static int KeyOf(MusicTrack t, int style) => (int)t * 16 + style;

    /// <summary>Parca hazirsa doner; degilse uretimi arka planda baslatir (ya da surdurur) ve null doner.</summary>
    private static AudioClip Clip(MusicTrack t, int style)
    {
        int key = KeyOf(t, style);
        if (Clips.TryGetValue(key, out AudioClip c) && c != null) return c;
        if (Pending.TryGetValue(key, out Task<float[]> task))
        {
            if (!task.IsCompleted) return null;
            Pending.Remove(key);
            float[] data = task.Status == TaskStatus.RanToCompletion ? task.Result : null;
            if (data == null || data.Length == 0)
            {
                if (task.Exception != null) Debug.LogException(task.Exception);
                data = new float[MusicSynth.Rate];   // sessiz yedek: tekrar tekrar denenmesin
            }
            c = AudioClip.Create($"music_{t}_{style}", data.Length, 1, MusicSynth.Rate, false);
            c.SetData(data, 0);
            Clips[key] = c;
            return c;
        }
#if UNITY_WEBGL && !UNITY_EDITOR
        Pending[key] = Task.FromResult(MusicSynth.Render(t, style));   // WebGL'de is parcacigi yok
#else
        Pending[key] = Task.Run(() => MusicSynth.Render(t, style));
#endif
        return null;
    }
}
