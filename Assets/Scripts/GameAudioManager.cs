using System.Collections.Generic;
using UnityEngine;

public class GameAudioManager : MonoBehaviour
{
    private const string AlarmLoopKey = "alarm_loop";
    private const string MasterVolumePref = "Audio.Master";
    private const string SfxVolumePref = "Audio.Sfx";
    private const string MusicVolumePref = "Audio.Music";

    public static GameAudioManager Instance { get; private set; }

    [Header("Library")]
    [SerializeField] private GameAudioLibrary audioLibrary;

    [Header("Volumes")]
    [SerializeField, Range(0f, 1f)] private float sfxVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float ambientVolume = 0.9f;
    [SerializeField, Range(0f, 1f)] private float outcomeVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float footstepsVolume = 0.9f;
    [SerializeField, Range(0f, 1f)] private float masterVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float musicVolume = 1f;

    [Header("Damage Cooldowns")]
    [SerializeField] private float fireHurtMinInterval = 0.9f;
    [SerializeField] private float coughMinInterval = 1.3f;

    private AudioSource sfx2DSource;
    private AudioSource ambient2DSource;
    private AudioSource outcomes2DSource;
    private AudioSource footsteps2DSource;
    private readonly Dictionary<string, AudioSource> loopSources = new Dictionary<string, AudioSource>();

    private float lastFireHurtTime = -100f;
    private float lastCoughTime = -100f;
    private bool outcomePlayed;

    public float MasterVolume => masterVolume;
    public float SfxVolume => sfxVolume;
    public float MusicVolume => musicVolume;

    public bool HasLibrary => audioLibrary != null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureInstanceExists()
    {
        if (Instance != null)
        {
            return;
        }

        GameObject managerObject = new GameObject("GameAudioManager");
        managerObject.AddComponent<GameAudioManager>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        LoadVolumeSettings();
        EnsureCoreSources();
        EnsureLibraryReady();
        EnsurePlayerFootstepEmitter();
        ApplyVolumeSettings();
    }

    public void PlayDoorOpen(Vector3 position)
    {
        PlayOneShot3D(audioLibrary?.doorOpen.GetRandomClip(), position, GetCombinedSfxVolume());
    }

    public void PlayDoorClose(Vector3 position)
    {
        PlayOneShot3D(audioLibrary?.doorClose.GetRandomClip(), position, GetCombinedSfxVolume());
    }

    public void PlayHeatCheck(Vector3 position)
    {
        PlayOneShot3D(audioLibrary?.heatCheck.GetRandomClip(), position, GetCombinedSfxVolume());
    }

    public void TryPlayFireHurt(Vector3 position)
    {
        if (Time.unscaledTime - lastFireHurtTime < fireHurtMinInterval)
        {
            return;
        }

        lastFireHurtTime = Time.unscaledTime;
        PlayOneShot2D(audioLibrary?.fireHurt.GetRandomClip(), sfx2DSource, GetCombinedSfxVolume());
    }

    public void TryPlayCough(Vector3 position)
    {
        if (Time.unscaledTime - lastCoughTime < coughMinInterval)
        {
            return;
        }

        lastCoughTime = Time.unscaledTime;
        PlayOneShot2D(audioLibrary?.cough.GetRandomClip(), sfx2DSource, GetCombinedSfxVolume());
    }

    public void StartAlarmLoop()
    {
        AudioClip loopClip = audioLibrary?.fireAlarmLoop;
        if (loopClip == null)
        {
            return;
        }

        AudioSource source = GetOrCreateLoopSource(AlarmLoopKey);
        if (source.isPlaying && source.clip == loopClip)
        {
            return;
        }

        source.clip = loopClip;
        source.loop = true;
        source.volume = GetCombinedSfxVolume() * ambientVolume;
        source.Play();
    }

    public void StopAlarmLoop()
    {
        if (!loopSources.TryGetValue(AlarmLoopKey, out AudioSource source) || source == null)
        {
            return;
        }

        source.Stop();
    }

    public void PlayFootstepConcrete()
    {
        PlayOneShot2D(audioLibrary?.concreteSteps.GetRandomClip(), footsteps2DSource, GetCombinedSfxVolume() * footstepsVolume);
    }

    public void PlaySuccessOnce()
    {
        if (outcomePlayed)
        {
            return;
        }

        outcomePlayed = true;
        PlayOneShot2D(audioLibrary?.success, outcomes2DSource, GetCombinedSfxVolume() * outcomeVolume);
    }

    public void PlayFailOnce()
    {
        if (outcomePlayed)
        {
            return;
        }

        outcomePlayed = true;
        PlayOneShot2D(audioLibrary?.fail, outcomes2DSource, GetCombinedSfxVolume() * outcomeVolume);
    }

    public void SetMasterVolume(float value, bool save = true)
    {
        masterVolume = Mathf.Clamp01(value);
        if (save)
        {
            PlayerPrefs.SetFloat(MasterVolumePref, masterVolume);
            PlayerPrefs.Save();
        }
        ApplyVolumeSettings();
    }

    public void SetSfxVolume(float value, bool save = true)
    {
        sfxVolume = Mathf.Clamp01(value);
        if (save)
        {
            PlayerPrefs.SetFloat(SfxVolumePref, sfxVolume);
            PlayerPrefs.Save();
        }
        ApplyVolumeSettings();
    }

    public void SetMusicVolume(float value, bool save = true)
    {
        musicVolume = Mathf.Clamp01(value);
        if (save)
        {
            PlayerPrefs.SetFloat(MusicVolumePref, musicVolume);
            PlayerPrefs.Save();
        }
        ApplyVolumeSettings();
    }

    private void EnsureCoreSources()
    {
        sfx2DSource = CreateChildSource("Sfx2DSource");
        ambient2DSource = CreateChildSource("Ambient2DSource");
        outcomes2DSource = CreateChildSource("Outcome2DSource");
        footsteps2DSource = CreateChildSource("Footsteps2DSource");
    }

    private void EnsureLibraryReady()
    {
        if (audioLibrary == null)
        {
            // In builds, we attempt to load the library from a Resources folder.
            // You should place your GameAudioLibrary asset inside a folder named 'Resources'.
            audioLibrary = Resources.Load<GameAudioLibrary>("GameAudioLibrary");

            if (audioLibrary == null)
            {
                audioLibrary = ScriptableObject.CreateInstance<GameAudioLibrary>();
            }
        }

        audioLibrary.AutoResolveMissingClips();
    }

    private AudioSource GetOrCreateLoopSource(string key)
    {
        if (loopSources.TryGetValue(key, out AudioSource existing) && existing != null)
        {
            return existing;
        }

        AudioSource created = CreateChildSource("Loop_" + key);
        loopSources[key] = created;
        return created;
    }

    private AudioSource CreateChildSource(string name)
    {
        Transform existing = transform.Find(name);
        AudioSource source;
        if (existing != null)
        {
            source = existing.GetComponent<AudioSource>();
            if (source == null)
            {
                source = existing.gameObject.AddComponent<AudioSource>();
            }
        }
        else
        {
            GameObject child = new GameObject(name);
            child.transform.SetParent(transform, false);
            source = child.AddComponent<AudioSource>();
        }

        source.playOnAwake = false;
        source.spatialBlend = 0f;
        return source;
    }

    private static void PlayOneShot2D(AudioClip clip, AudioSource source, float volume)
    {
        if (clip == null || source == null)
        {
            return;
        }

        source.PlayOneShot(clip, volume);
    }

    private static void PlayOneShot3D(AudioClip clip, Vector3 position, float volume)
    {
        if (clip == null)
        {
            return;
        }

        AudioSource.PlayClipAtPoint(clip, position, volume);
    }

    private static void EnsurePlayerFootstepEmitter()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
        {
            return;
        }

        if (player.GetComponent<PlayerFootstepAudio>() == null)
        {
            player.AddComponent<PlayerFootstepAudio>();
        }
    }

    private void ApplyVolumeSettings()
    {
        if (ambient2DSource != null)
        {
            ambient2DSource.volume = GetCombinedSfxVolume() * ambientVolume;
        }

        if (outcomes2DSource != null)
        {
            outcomes2DSource.volume = GetCombinedSfxVolume() * outcomeVolume;
        }

        if (footsteps2DSource != null)
        {
            footsteps2DSource.volume = GetCombinedSfxVolume() * footstepsVolume;
        }

        foreach (AudioSource loopSource in loopSources.Values)
        {
            if (loopSource != null)
            {
                loopSource.volume = GetCombinedSfxVolume() * ambientVolume;
            }
        }
    }

    private void LoadVolumeSettings()
    {
        masterVolume = PlayerPrefs.GetFloat(MasterVolumePref, masterVolume);
        sfxVolume = PlayerPrefs.GetFloat(SfxVolumePref, sfxVolume);
        musicVolume = PlayerPrefs.GetFloat(MusicVolumePref, musicVolume);
    }

    private float GetCombinedSfxVolume()
    {
        return masterVolume * sfxVolume;
    }
}
