using UnityEngine;

namespace TheLastGift.Audio
{
    /// <summary>
    /// Small persistent audio hub for the game. Music and sound effects use
    /// separate sources, while AudioListener controls the master volume.
    /// </summary>
    public sealed class LastGiftAudioManager : MonoBehaviour
    {
        private const string MasterVolumeKey = "LastGift.MasterVolume";
        private const string MusicVolumeKey = "LastGift.MusicVolume";
        private const string SfxVolumeKey = "LastGift.SfxVolume";

        private static LastGiftAudioManager instance;

        private AudioSource musicSource;
        private AudioSource sfxSource;

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

        public static void EnsureInstance()
        {
            Instance.ApplyVolumes();
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

            masterVolume = PlayerPrefs.GetFloat(MasterVolumeKey, 1f);
            musicVolume = PlayerPrefs.GetFloat(MusicVolumeKey, 0.8f);
            sfxVolume = PlayerPrefs.GetFloat(SfxVolumeKey, 1f);

            musicSource = gameObject.AddComponent<AudioSource>();
            musicSource.playOnAwake = false;
            musicSource.loop = true;

            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;
            sfxSource.loop = false;

            ApplyVolumes();
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused)
                SaveSettings();
        }

        private void OnApplicationQuit()
        {
            SaveSettings();
        }

        private void OnDestroy()
        {
            if (instance == this)
                instance = null;
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

        /// <summary>
        /// Starts or replaces the looping background music.
        /// Call this when a music AudioClip is added to the project.
        /// </summary>
        public static void PlayMusic(AudioClip clip)
        {
            if (clip == null)
                return;

            LastGiftAudioManager manager = Instance;
            if (manager.musicSource.clip == clip && manager.musicSource.isPlaying)
                return;

            manager.musicSource.clip = clip;
            manager.musicSource.Play();
        }

        public static void StopMusic()
        {
            LastGiftAudioManager manager = Instance;
            manager.musicSource.Stop();
            manager.musicSource.clip = null;
        }

        /// <summary>
        /// Plays a one-shot sound effect using the dedicated effects channel.
        /// </summary>
        public static void PlaySfx(AudioClip clip)
        {
            if (clip == null)
                return;

            Instance.sfxSource.PlayOneShot(clip);
        }

        private void ApplyVolumes()
        {
            AudioListener.volume = masterVolume;

            if (musicSource != null)
                musicSource.volume = musicVolume;

            if (sfxSource != null)
                sfxSource.volume = sfxVolume;
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
