using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TheLastGift.Audio
{
    public enum LastGiftMusicTheme { MainMenu, Workshop, Ending }

    public enum LastGiftSoundEffect
    {
        UiClick, GiftDrag, GiftPacked, Error, NewOrder, DialogueAdvance, OrderComplete
    }

    /// <summary>
    /// Persistent audio hub for The Last Gift. Music uses two sources for crossfades;
    /// effects use a separate one-shot channel. Settings survive application restarts.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LastGiftAudioManager : MonoBehaviour
    {
        private const string MasterVolumeKey = "LastGift.MasterVolume";
        private const string MusicVolumeKey = "LastGift.MusicVolume";
        private const string SfxVolumeKey = "LastGift.SfxVolume";

        private static LastGiftAudioManager instance;
        private readonly AudioSource[] musicSources = new AudioSource[2];
        private readonly float[] musicSourceGains = new float[2];
        private readonly Dictionary<LastGiftSoundEffect, AudioClip> soundCache =
            new Dictionary<LastGiftSoundEffect, AudioClip>();

        private AudioSource sfxSource;
        private Coroutine musicFadeRoutine;
        private int activeMusicSourceIndex = -1;
        private float masterVolume = 1f;
        private float musicVolume = 0.8f;
        private float sfxVolume = 1f;

        public static float MasterVolume => Instance.masterVolume;
        public static float MusicVolume => Instance.musicVolume;
        public static float SfxVolume => Instance.sfxVolume;

        private static LastGiftAudioManager Instance
        {
            get
            {
                if (instance == null)
                {
                    GameObject audioObject = new GameObject("The Last Gift Audio");
                    instance = audioObject.AddComponent<LastGiftAudioManager>();
                }
                return instance;
            }
        }

        public static void EnsureInstance() { Instance.ApplyVolumes(); }

        /// <summary>
        /// Guarantees exactly one enabled listener and keeps it attached to the active camera.
        /// Call this when a scene supplies or creates its rendering camera.
        /// </summary>
        public static AudioListener EnsureSingleAudioListener(Camera targetCamera)
        {
            if (targetCamera == null)
                return null;

            AudioListener targetListener = targetCamera.GetComponent<AudioListener>();
            if (targetListener == null)
                targetListener = targetCamera.gameObject.AddComponent<AudioListener>();

            targetListener.enabled = true;

            AudioListener[] listeners = Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None);
            for (int i = 0; i < listeners.Length; i++)
            {
                AudioListener listener = listeners[i];
                if (listener != null && listener != targetListener)
                    listener.enabled = false;
            }

            return targetListener;
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
            masterVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(MasterVolumeKey, 1f));
            musicVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(MusicVolumeKey, 0.8f));
            sfxVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(SfxVolumeKey, 1f));

            musicSources[0] = CreateMusicSource("Music — A");
            musicSources[1] = CreateMusicSource("Music — B");
            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;
            sfxSource.loop = false;
            sfxSource.spatialBlend = 0f;
            ApplyVolumes();
        }

        private AudioSource CreateMusicSource(string sourceName)
        {
            AudioSource source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0f;
            source.dopplerLevel = 0f;
            source.name = sourceName;
            return source;
        }

        private void OnApplicationPause(bool paused) { if (paused) SaveSettings(); }
        private void OnApplicationQuit() { SaveSettings(); }

        private void OnDestroy()
        {
            if (instance == this)
            {
                SaveSettings();
                instance = null;
            }
        }

        public static void SetMasterVolume(float value)
        {
            LastGiftAudioManager manager = Instance;
            manager.masterVolume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(MasterVolumeKey, manager.masterVolume);
            manager.ApplyVolumes();
        }

        public static void SetMusicVolume(float value)
        {
            LastGiftAudioManager manager = Instance;
            manager.musicVolume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(MusicVolumeKey, manager.musicVolume);
            manager.ApplyVolumes();
        }

        public static void SetSfxVolume(float value)
        {
            LastGiftAudioManager manager = Instance;
            manager.sfxVolume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(SfxVolumeKey, manager.sfxVolume);
            manager.ApplyVolumes();
        }

        /// <summary>Plays a looping Resources music theme, crossfading from the previous theme.</summary>
        public static void PlayMusic(LastGiftMusicTheme theme, float fadeDuration = 1.4f)
        {
            string path;
            switch (theme)
            {
                case LastGiftMusicTheme.MainMenu: path = "Audio/Music/MenuTheme"; break;
                case LastGiftMusicTheme.Workshop: path = "Audio/Music/WorkshopTheme"; break;
                case LastGiftMusicTheme.Ending: path = "Audio/Music/EndingTheme"; break;
                default:
                    Debug.LogWarning("LastGiftAudioManager: Unknown music theme " + theme + ".");
                    return;
            }

            AudioClip clip = Resources.Load<AudioClip>(path);
            if (clip == null)
            {
                Debug.LogWarning("LastGiftAudioManager: Music clip was not found at Resources/" + path + ".");
                return;
            }
            PlayMusic(clip, fadeDuration);
        }

        /// <summary>Plays a custom looping clip, crossfading without restarting the current clip.</summary>
        public static void PlayMusic(AudioClip clip, float fadeDuration = 1.4f)
        {
            if (clip != null) Instance.ChangeMusic(clip, fadeDuration);
        }

        private void ChangeMusic(AudioClip clip, float fadeDuration)
        {
            if (activeMusicSourceIndex >= 0)
            {
                AudioSource active = musicSources[activeMusicSourceIndex];
                if (active != null && active.clip == clip && active.isPlaying)
                    return;
            }

            if (musicFadeRoutine != null)
            {
                StopCoroutine(musicFadeRoutine);
                musicFadeRoutine = null;
            }

            int previousIndex = activeMusicSourceIndex;
            for (int i = 0; i < musicSources.Length; i++)
            {
                if (i == previousIndex) continue;
                musicSources[i].Stop();
                musicSources[i].clip = null;
                musicSourceGains[i] = 0f;
            }

            if (previousIndex >= 0 && !musicSources[previousIndex].isPlaying)
            {
                musicSources[previousIndex].Stop();
                musicSources[previousIndex].clip = null;
                musicSourceGains[previousIndex] = 0f;
                previousIndex = -1;
            }

            int nextIndex = previousIndex == 0 ? 1 : 0;
            AudioSource next = musicSources[nextIndex];
            next.Stop();
            next.clip = clip;
            next.loop = true;
            next.time = 0f;
            next.Play();
            musicSourceGains[nextIndex] = 0f;
            activeMusicSourceIndex = nextIndex;
            ApplyVolumes();

            if (previousIndex < 0 || fadeDuration <= 0f)
            {
                if (previousIndex >= 0)
                {
                    musicSources[previousIndex].Stop();
                    musicSources[previousIndex].clip = null;
                    musicSourceGains[previousIndex] = 0f;
                }
                musicSourceGains[nextIndex] = 1f;
                ApplyVolumes();
                return;
            }

            musicFadeRoutine = StartCoroutine(CrossFadeMusic(
                previousIndex, nextIndex, musicSourceGains[previousIndex], fadeDuration));
        }

        private IEnumerator CrossFadeMusic(int from, int to, float fromGain, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                float t = Mathf.Clamp01(elapsed / duration);
                musicSourceGains[from] = Mathf.Lerp(fromGain, 0f, t);
                musicSourceGains[to] = t;
                ApplyVolumes();
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            musicSourceGains[from] = 0f;
            musicSources[from].Stop();
            musicSources[from].clip = null;
            musicSourceGains[to] = 1f;
            ApplyVolumes();
            musicFadeRoutine = null;
        }

        public static void StopMusic()
        {
            LastGiftAudioManager manager = Instance;
            if (manager.musicFadeRoutine != null)
            {
                manager.StopCoroutine(manager.musicFadeRoutine);
                manager.musicFadeRoutine = null;
            }

            for (int i = 0; i < manager.musicSources.Length; i++)
            {
                manager.musicSources[i].Stop();
                manager.musicSources[i].clip = null;
                manager.musicSourceGains[i] = 0f;
            }
            manager.activeMusicSourceIndex = -1;
            manager.ApplyVolumes();
        }

        /// <summary>Plays one of the original one-shot effects stored under Resources/Audio/Sfx.</summary>
        public static void PlaySfx(LastGiftSoundEffect effect)
        {
            LastGiftAudioManager manager = Instance;
            if (!manager.soundCache.TryGetValue(effect, out AudioClip clip))
            {
                clip = Resources.Load<AudioClip>(manager.GetSfxResourcePath(effect));
                if (clip != null) manager.soundCache.Add(effect, clip);
            }

            if (clip == null)
            {
                Debug.LogWarning("LastGiftAudioManager: Sound effect was not found for " + effect + ".");
                return;
            }
            manager.sfxSource.PlayOneShot(clip);
        }

        /// <summary>Plays a caller-supplied one-shot clip.</summary>
        public static void PlaySfx(AudioClip clip)
        {
            if (clip != null) Instance.sfxSource.PlayOneShot(clip);
        }

        /// <summary>
        /// Plays the standard new-order chime for randomized Christmas workshop events.
        /// This uses the existing SFX resource and volume channel.
        /// </summary>
        public static void PlayChristmasEventChime()
        {
            PlaySfx(LastGiftSoundEffect.NewOrder);
        }

        private string GetSfxResourcePath(LastGiftSoundEffect effect)
        {
            switch (effect)
            {
                case LastGiftSoundEffect.UiClick: return "Audio/Sfx/UiClick";
                case LastGiftSoundEffect.GiftDrag: return "Audio/Sfx/GiftDrag";
                case LastGiftSoundEffect.GiftPacked: return "Audio/Sfx/GiftPacked";
                case LastGiftSoundEffect.Error: return "Audio/Sfx/Error";
                case LastGiftSoundEffect.NewOrder: return "Audio/Sfx/NewOrder";
                case LastGiftSoundEffect.DialogueAdvance: return "Audio/Sfx/DialogueAdvance";
                case LastGiftSoundEffect.OrderComplete: return "Audio/Sfx/OrderComplete";
                default: return string.Empty;
            }
        }

        private void ApplyVolumes()
        {
            AudioListener.volume = masterVolume;
            for (int i = 0; i < musicSources.Length; i++)
                if (musicSources[i] != null)
                    musicSources[i].volume = musicVolume * musicSourceGains[i];
            if (sfxSource != null) sfxSource.volume = sfxVolume;
        }

        private void SaveSettings()
        {
            PlayerPrefs.SetFloat(MasterVolumeKey, masterVolume);
            PlayerPrefs.SetFloat(MusicVolumeKey, musicVolume);
            PlayerPrefs.SetFloat(SfxVolumeKey, sfxVolume);
            PlayerPrefs.Save();
        }
    }
}