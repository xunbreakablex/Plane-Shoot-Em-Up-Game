using System;
using UnityEngine;

// Ses ayarlari (kalici) ve arayuz sesi. Master AudioListener.volume'a, muzik MusicPlayer'a uygulanir.
public static class GameAudio
{
    private const string MasterKey = "PlaneShootEmUp.Vol.Master";
    private const string MusicKey = "PlaneShootEmUp.Vol.Music";

    private static float master = -1f, music = -1f;
    private static AudioSource uiSource;

    /// <summary>Muzik ses seviyesi degisti (MusicPlayer dinler).</summary>
    public static event Action Changed;

    public static float Master
    {
        get { if (master < 0f) master = PlayerPrefs.GetFloat(MasterKey, 0.9f); return master; }
        set { master = Mathf.Clamp01(value); PlayerPrefs.SetFloat(MasterKey, master); Apply(); }
    }

    public static float Music
    {
        get { if (music < 0f) music = PlayerPrefs.GetFloat(MusicKey, 0.6f); return music; }
        set { music = Mathf.Clamp01(value); PlayerPrefs.SetFloat(MusicKey, music); Apply(); }
    }

    public static void Apply()
    {
        AudioListener.volume = Master;
        Changed?.Invoke();
    }

    /// <summary>Arayuz sesi: duraklatmada da calar (AudioListener.pause'u yok sayar).</summary>
    public static void PlayUi(AudioClip clip, float volume = 0.5f)
    {
        if (clip == null) return;
        if (uiSource == null)
        {
            var go = new GameObject("UiAudio");
            UnityEngine.Object.DontDestroyOnLoad(go);
            uiSource = go.AddComponent<AudioSource>();
            uiSource.spatialBlend = 0f;
            uiSource.ignoreListenerPause = true;
            uiSource.playOnAwake = false;
        }
        uiSource.PlayOneShot(clip, volume);
    }
}
