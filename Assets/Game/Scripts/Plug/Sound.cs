using System.Collections.Generic;
using System.Timers;
using UnityEngine;

/// <summary>声音</summary>
public class Sound
{
    /// <summary>声音大小-音乐</summary>
    public static float VolumeMusic = 0.5f;

    /// <summary>声音大小-游戏音效</summary>
    public static float VolumeSound = 1;
    /// <summary>声音大小-弹幕功能音效（后续弹幕专用 SFX）</summary>
    public static float VolumeBarrage = 1;
    public static float VideoVolume = 1;

    static GameObject MusicManager;
    static GameObject LoopSoundManager;

    static GameObject[] SoundManager;
    static GameObject[] BarrageSoundManager;
    public static Dictionary<string, AudioClip> musicBGM = new Dictionary<string, AudioClip>();
    /// <summary>音效最大数量(同时存在)</summary>
    static int MaxSoundNum = 20;
    /// <summary>当前使用的音效索引</summary>
    static int SoundIndex = 0;
    static int BarrageSoundIndex = 0;
    static string loopingSoundUrl;

    public static bool hasPlayMusic = false;//是否正在播放音乐
    public static string PlayingMusicName;//正在播放的音乐名称
    static Sound()
    {
        MusicManager = new GameObject("#MusicManager#");
        MusicManager.AddComponent<AudioSource>();
        Object.DontDestroyOnLoad(MusicManager);

        SoundManager = new GameObject[MaxSoundNum];
        BarrageSoundManager = new GameObject[MaxSoundNum];
    }

    public static void OnSetVolume(float musicValue, float soundValue)
    {
        VolumeMusic = musicValue;
        VolumeSound = soundValue;
        VideoVolume = soundValue;
        PlayerPrefs.SetFloat("VolumeMusic", VolumeMusic);
        PlayerPrefs.SetFloat("VolumeSound", VolumeSound);

        if (MusicManager) MusicManager.GetComponent<AudioSource>().volume = VolumeMusic;
    }
    /// <summary>获得音乐大小</summary>
    public static float GetVolumeMusic()
    {
        return VolumeMusic;
    }

    /// <summary>获得音效大小</summary>
    public static float GetVolumeSound()
    {
        return VolumeSound;
    }
    /// <summary>暂停或继续播放音乐</summary>
    public static void PauseOrPlayVolumeMusic(bool pause = true)
    {
        if (pause)
        {
            hasPlayMusic = false;
            MusicManager.GetComponent<AudioSource>().Pause();
        }
        else
        {
            hasPlayMusic = true;
            MusicManager.GetComponent<AudioSource>().Play();
        }
    }

    /// <summary>设置音乐大小</summary>
    public static void SetVolumeMusic(float num)
    {
        VolumeMusic = Mathf.Clamp01(num);
        if (MusicManager != null)
        {
            var src = MusicManager.GetComponent<AudioSource>();
            if (src != null) src.volume = VolumeMusic;
        }
    }

    /// <summary>设置游戏音效大小（含循环音效通道，实时生效）。</summary>
    public static void SetVolumeSound(float num)
    {
        VolumeSound = Mathf.Clamp01(num);
        if (SoundManager != null)
        {
            foreach (var i in SoundManager)
            {
                if (i == null) continue;
                var src = i.GetComponent<AudioSource>();
                if (src != null) src.volume = VolumeSound;
            }
        }
        if (LoopSoundManager != null)
        {
            var loopSrc = LoopSoundManager.GetComponent<AudioSource>();
            if (loopSrc != null) loopSrc.volume = VolumeSound;
        }
    }

    /// <summary>设置面板「游戏音量」：同步 BGM + 游戏音效并立即应用到在播通道。</summary>
    public static void SetGameVolume(float num)
    {
        float v = Mathf.Clamp01(num);
        SetVolumeSound(v);
        SetVolumeMusic(v);
    }

    /// <summary>设置弹幕功能音效大小（独立通道，与游戏音效互不影响）。</summary>
    public static void SetVolumeBarrage(float num)
    {
        VolumeBarrage = Mathf.Clamp01(num);
        if (BarrageSoundManager == null) return;
        foreach (var i in BarrageSoundManager)
        {
            if (i == null) continue;
            i.GetComponent<AudioSource>().volume = VolumeBarrage;
        }
    }

    /// <summary>
    /// 播放弹幕功能音效（走 VolumeBarrage）。
    /// 凡弹幕玩法相关、与原厨房游戏无关的音效都应走此接口。
    /// </summary>
    public static void PlayBarrageSound(string url, bool show_warn_log = false)
    {
        if (string.IsNullOrEmpty(url)) return;
        AudioClip clip = LoadAudioClip(url, show_warn_log);
        if (clip == null)
        {
            if (show_warn_log) Debug.LogWarning($"[Sound] 弹幕音效缺失: {url}");
            return;
        }

        if (BarrageSoundManager == null)
            BarrageSoundManager = new GameObject[MaxSoundNum];

        int id = BarrageSoundIndex++ % MaxSoundNum;
        if (BarrageSoundManager[id] == null)
        {
            BarrageSoundManager[id] = new GameObject("#BarrageSound#");
            BarrageSoundManager[id].AddComponent<AudioSource>();
            Object.DontDestroyOnLoad(BarrageSoundManager[id]);
        }

        var source = BarrageSoundManager[id].GetComponent<AudioSource>();
        source.clip = clip;
        source.loop = false;
        source.volume = VolumeBarrage;
        source.Play();
    }

    /// <summary>播放音乐</summary>
    public static AudioSource PlayMusic(string url, bool loop)
    {
        AudioClip clip = LoadAudioClip(url, true);
        if (clip == null) return null;
        PlayingMusicName = clip.name;
        hasPlayMusic = true;
        return PlayAndReturn(MusicManager, clip, loop);
    }

    /// <summary>播放音乐</summary>
    public static void PlayMusic(string url, bool show_warn_log = true, bool loop = true)
    {
        AudioClip clip = LoadAudioClip(url, show_warn_log);
        if (clip == null) return;
        PlayingMusicName = clip.name;
        Play(MusicManager, clip, loop);
        hasPlayMusic = true;
    }
    private static string sound;
    private static bool delayPlay = true;
    /// <summary>播放音效</summary>
    public static void PlaySound(string url, bool show_warn_log = true)
    {
        if (sound != url || delayPlay)
        {
            sound = url;
            //vv.log("播放音效:", url);

            AudioClip clip = LoadAudioClip(url, show_warn_log);
            if (clip == null) return;

            int id = SoundIndex++ % MaxSoundNum;
            if (SoundManager[id] == null)
            {
                SoundManager[id] = new GameObject("#SoundManager#");
                SoundManager[id].AddComponent<AudioSource>();
                Object.DontDestroyOnLoad(SoundManager[id]);
            }

            Play(SoundManager[id], clip, false);
        }
    }

    /// <summary>单通道循环音效（同 url 已在播则不重开）。</summary>
    public static void PlayLoopSound(string url, bool show_warn_log = false)
    {
        if (string.IsNullOrEmpty(url)) return;
        if (LoopSoundManager == null)
        {
            LoopSoundManager = new GameObject("#LoopSoundManager#");
            LoopSoundManager.AddComponent<AudioSource>();
            Object.DontDestroyOnLoad(LoopSoundManager);
        }
        var src = LoopSoundManager.GetComponent<AudioSource>();
        if (src.isPlaying && loopingSoundUrl == url) return;

        AudioClip clip = LoadAudioClip(url, show_warn_log);
        if (clip == null) return;
        loopingSoundUrl = url;
        src.clip = clip;
        src.loop = true;
        src.volume = VolumeSound;
        src.Play();
    }

    /// <summary>停止循环音效。</summary>
    public static void StopLoopSound(string url = null)
    {
        if (LoopSoundManager == null) return;
        if (!string.IsNullOrEmpty(url) && loopingSoundUrl != url) return;
        var src = LoopSoundManager.GetComponent<AudioSource>();
        if (src != null) src.Stop();
        loopingSoundUrl = null;
    }

    /// <summary>加载声音资源</summary>
    public static AudioClip LoadAudioClip(string url, bool show_warn_log)
    {
        AudioClip clip = null;
        try
        {
            if (musicBGM != null && musicBGM.ContainsKey(url))
            {
                clip = musicBGM[url];
            }
            else
            {
                clip = Loaded.Load<AudioClip>($"{url}");
            }
        }
        catch { }
        return clip;
    }

    /// <summary>播放声音</summary>
    static void Play(GameObject obj, AudioClip clip, bool loop)
    {
        AudioSource source = obj.GetComponent<AudioSource>();
        source.clip = clip;
        source.loop = loop;
        source.volume = loop ? VolumeMusic : VolumeSound;
        source.Play();
    }
    static AudioSource PlayAndReturn(GameObject obj, AudioClip clip, bool loop)
    {
        AudioSource source = obj.GetComponent<AudioSource>();
        source.clip = clip;
        source.loop = loop;
        source.volume = loop ? VolumeMusic : VolumeSound;
        source.Play();
        return source;
    }

}

