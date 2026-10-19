using UnityEngine;

public enum MusicTrack { None, Hangar, Combat, Boss }

// Prosedurel muzik: kod ile uretilen kisa dongu parcalari (hangar, savas, boss). Savasta boss varken boss parcasina
// yumusakca gecer. Duraklatmada kisilir ama durmaz. Ses seviyesi GameAudio.Music.
public class MusicPlayer : MonoBehaviour
{
    private static readonly AudioClip[] Clips = new AudioClip[4];

    private AudioSource a, b;
    private AudioSource active;
    private MusicTrack track = MusicTrack.None;
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
        if (want != track)
        {
            track = want;
            AudioSource next = active == a ? b : a;
            next.clip = Clip(track);
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

    private static AudioClip Clip(MusicTrack t)
    {
        int i = (int)t;
        if (Clips[i] == null) Clips[i] = MusicSynth.Make(t);
        return Clips[i];
    }
}
