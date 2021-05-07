using System;
using System.Linq;
using Extensions;
using General;
using Hellmade.Sound;
using UnityEngine;
using Audio = General.Audio;
using Random = UnityEngine.Random;

public class AudioController : PersistentSingleton<AudioController>
{
    private const float MaxVolume = 1;
    [SerializeField] private StringAudioDictionary soundClips = new StringAudioDictionary();
    [SerializeField] private StringAudioDictionary musicClips = new StringAudioDictionary();

    [SerializeField] [Range(0, MaxVolume)] private float musicVolume = MaxVolume / 2;
    [SerializeField] [Range(0, MaxVolume)] private float soundVolume = MaxVolume / 2;

    public string defaultMusic;
    [SerializeField] private bool loopMusicPerDefault = true;
    [SerializeField] private bool cacheAudioListener = true;
    [SerializeField] private bool setMaxDistanceOnAudioSource = true;

    public bool IsMusicPlaying => _musicPlaying != -1;

    public AudioListener AudioListener
    {
        get
        {
            if (!cacheAudioListener)
                return FindAudioListener();

            if (!_cachedAudioListener)
                _cachedAudioListener = FindAudioListener();

            return _cachedAudioListener;
        }
    }

    private bool _loaded;
    private int _musicPlaying = -1;
    private AudioListener _cachedAudioListener;

    public new void Awake()
    {
        if (!InitSingletonInstance())
            return;

        EazySoundManager.IgnoreDuplicateMusic = false;
        musicVolume = PlayerPrefs.GetFloat("MusicVolume", MaxVolume / 2);
        soundVolume = PlayerPrefs.GetFloat("SoundVolume", MaxVolume / 2);
        SetMusicVolume(musicVolume);
        SetSoundVolume(soundVolume);
        _loaded = true;
    }

    private AudioListener FindAudioListener()
    {
        return FindObjectsOfType<AudioListener>().FirstOrDefault(c => c.enabled);
    }

    //  ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~ Volumes  ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
    public float SetMusicVolume(float newMusicVolume)
    {
        musicVolume = Mathf.Clamp(newMusicVolume, 0f, MaxVolume);
        EazySoundManager.GlobalMusicVolume = musicVolume;
        PlayerPrefs.SetFloat("MusicVolume", musicVolume);
        return musicVolume;
    }

    public float SetSoundVolume(float newSoundVolume)
    {
        soundVolume = Mathf.Clamp(newSoundVolume, 0f, MaxVolume);
        EazySoundManager.GlobalSoundsVolume = soundVolume;
        EazySoundManager.GlobalUISoundsVolume = soundVolume;
        PlayerPrefs.SetFloat("SoundVolume", soundVolume);
        return soundVolume;
    }

    private void OnValidate()
    {
        if (!_loaded)
            return;

        SetMusicVolume(musicVolume);
        SetSoundVolume(soundVolume);
    }

    public float MusicVolume => musicVolume;
    public float SoundVolume => soundVolume;

    //  ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~ Simple play functions  ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~

    #region PLAY FUNCTIONS

    public int PlaySound(string key, AudioOptions options = default)
    {
        return PlaySoundWithKey(key, options);
    }

    public int PlayPositionedSound(string key, Transform sourceTransform, AudioOptions options = default)
    {
        options.SourceTransform = sourceTransform;
        return PlaySoundWithKey(key, options);
    }

    public int PlayUISound(string key, AudioOptions options = default)
    {
        return PlaySoundWithKey(key, options, PlayType.UISound);
    }

    public void PlaySoundFromUI(string key)
    {
        PlaySoundWithKey(key, default, PlayType.UISound);
    }

    public int PlayRandomSound(string key, AudioOptions options = default)
    {
        var soundEntries = soundClips.Where(kvp => kvp.Key.StartsWith(key)).ToList();
        if (!soundEntries.Any())
            return -1;

        var entry = soundEntries[Random.Range(0, soundEntries.Count)];
        return PlayAudioEntry(entry.Value, options, PlayType.Sound);
    }

    public int PlayMusic(string key, AudioOptions options = default)
    {
        return PlayMusicWithKey(key, options);
    }

    public int PlayDefaultMusic(AudioOptions options = default)
    {
        return PlayMusicWithKey(defaultMusic, options);
    }

    public int PlayAudio(Audio clip, AudioOptions options = default, PlayType playType = PlayType.Sound)
    {
        return PlayAudioEntry(clip, options, playType);
    }

    public int PlayAudioClip(AudioClip clip, AudioOptions options = default, PlayType playType = PlayType.Sound)
    {
        return PlayAudioClipInternal(clip, options, playType);
    }

    public void StopAllSounds()
    {
        EazySoundManager.StopAllSounds();
        EazySoundManager.StopAllUISounds();
    }

    public void StopMusic()
    {
        EazySoundManager.StopAllMusic();
    }

    #endregion

    // Basically wrappers for EazySoundManager's method, which fetch the Audio from the Dictionary
    private int PlaySoundWithKey(string key, AudioOptions options, PlayType playType = PlayType.Sound)
    {
        if (key == null || !soundClips.TryGetValue(key, out var soundEntry))
            return -1;

        if (playType == PlayType.Music)
        {
            playType = PlayType.Sound;
        }

        return PlayAudioEntry(soundEntry, options, playType);
    }

    private int PlayMusicWithKey(string key, AudioOptions options)
    {
        if (key == null || !musicClips.TryGetValue(key, out var musicEntry))
            return -1;

        return PlayAudioEntry(musicEntry, options, PlayType.Music);
    }

    private int PlayAudioEntry(Audio audioEntry, AudioOptions options, PlayType playType)
    {
        if (!audioEntry)
            return -1;

        var clip = audioEntry.AudioClip;
        if (!clip)
            return -1;

        // Set music to looping if this is wanted and if the option is not overriden
        if (playType == PlayType.Music && loopMusicPerDefault && options.Loop == null)
        {
            options.Loop = true;
        }

        var playOptions = UnifyAudioOptions(audioEntry, options);

        if (!IsInRange(playOptions, playType))
            return -1;

        playOptions = ApplyVariations(playOptions);

        return PlayAudioClipInternal(clip, playOptions, playType);
    }

    private int PlayAudioClipInternal(AudioClip clip, AudioOptions options, PlayType playType)
    {
        if (!Application.isPlaying)
        {
            return -1;
        }

        var volume = options.Volume ?? 1;
        var pitch = options.Pitch ?? 1;
        var loop = options.Loop ?? playType == PlayType.Music;

        int id;
        Hellmade.Sound.Audio eazyAudio;
        switch (playType)
        {
            case PlayType.Music:
                id = EazySoundManager.PlayMusic(clip, volume, loop, true);
                eazyAudio = EazySoundManager.GetMusicAudio(id);
                _musicPlaying = id;
                break;
            case PlayType.UISound:
                id = EazySoundManager.PlayUISound(clip, volume);
                eazyAudio = EazySoundManager.GetUISoundAudio(id);
                break;
            case PlayType.Sound:
            default:
                id = EazySoundManager.PlaySound(clip, volume, loop, options.SourceTransform);
                eazyAudio = EazySoundManager.GetSoundAudio(id);

                if (setMaxDistanceOnAudioSource && options.MaxRange.HasValue)
                {
                    eazyAudio.AudioSource.maxDistance = options.MaxRange.Value;
                }

                break;
        }

        if (eazyAudio != null)
        {
            eazyAudio.Pitch = pitch;
        }

        return id;
    }

    #region OPTIONS PROCESSING

    private AudioOptions UnifyAudioOptions(Audio audioEntry, AudioOptions options)
    {
        options.Loop = options.Loop ?? audioEntry.loop;
        options.Volume = options.Volume ?? audioEntry.volume;
        options.Pitch = options.Pitch ?? audioEntry.pitch;
        options.VolumeVariation = options.VolumeVariation ?? audioEntry.volumeVariation;
        options.PitchVariation = options.PitchVariation ?? audioEntry.pitchVariation;
        options.MaxRange = options.MaxRange ?? (audioEntry.useMaxRange ? audioEntry.maxRange : (float?) null);

        return options;
    }

    private AudioOptions ApplyVariations(AudioOptions options)
    {
        var volume = options.Volume ?? 1;
        var volumeVariation = options.VolumeVariation ?? 0;
        if (volumeVariation > 0)
        {
            volume += Random.Range(-volumeVariation, volumeVariation);
        }

        var pitch = options.Pitch ?? 1;
        var pitchVariation = options.PitchVariation ?? 0;
        if (pitchVariation > 0)
        {
            pitch += Random.Range(-pitchVariation, pitchVariation);
        }

        options.Volume = volume;
        options.Pitch = pitch;
        return options;
    }

    private bool IsInRange(AudioOptions options, PlayType playType)
    {
        if (playType != PlayType.Sound || !options.SourceTransform || options.MaxRange == null)
            return true;

        var listener = AudioListener;
        if (!listener)
            return false;

        var distance = Vector3.Distance(listener.transform.position, options.SourceTransform.position);
        return distance <= options.MaxRange;
    }

    #endregion

    public struct AudioOptions
    {
        public bool? Loop;
        public float? Volume;
        public float? Pitch;
        public float? VolumeVariation;
        public float? PitchVariation;

        public Transform SourceTransform;
        public float? MaxRange;
    }

    public enum PlayType
    {
        Sound,
        Music,
        UISound,
    }

    [Serializable]
    public struct AudioEntry
    {
        public string Key;
        public Audio Audio;
    }
}