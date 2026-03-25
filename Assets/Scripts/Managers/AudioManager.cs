using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RunAndGun
{
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [Header("Music")]
        [SerializeField] private AudioSource musicSourceA;
        [SerializeField] private AudioSource musicSourceB;
        [SerializeField] private float crossfadeDuration = 1.5f;

        [Header("SFX Pool")]
        [SerializeField] private int sfxPoolSize = 16;
        [SerializeField] private Transform sfxPoolParent;

        [Header("Volume Defaults")]
        [SerializeField] [Range(0f, 1f)] private float defaultMusicVolume = 0.7f;
        [SerializeField] [Range(0f, 1f)] private float defaultSfxVolume = 1f;

        private const string PrefKeyMusicVolume = "RG_MusicVol";
        private const string PrefKeySfxVolume = "RG_SfxVol";
        private const string PrefKeyMuted = "RG_Muted";

        private readonly List<AudioSource> sfxPool = new List<AudioSource>();
        private AudioSource activeMusicSource;
        private AudioSource inactiveMusicSource;
        private Coroutine crossfadeCoroutine;

        private float musicVolume;
        private float sfxVolume;
        private bool isMuted;

        public float MusicVolume => musicVolume;
        public float SfxVolume => sfxVolume;
        public bool IsMuted => isMuted;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            InitializeMusicSources();
            InitializeSfxPool();
            LoadVolumeSettings();
        }

        private void InitializeMusicSources()
        {
            if (musicSourceA == null)
            {
                GameObject goA = new GameObject("MusicSourceA");
                goA.transform.SetParent(transform);
                musicSourceA = goA.AddComponent<AudioSource>();
            }

            if (musicSourceB == null)
            {
                GameObject goB = new GameObject("MusicSourceB");
                goB.transform.SetParent(transform);
                musicSourceB = goB.AddComponent<AudioSource>();
            }

            ConfigureMusicSource(musicSourceA);
            ConfigureMusicSource(musicSourceB);

            activeMusicSource = musicSourceA;
            inactiveMusicSource = musicSourceB;
        }

        private void ConfigureMusicSource(AudioSource source)
        {
            source.loop = true;
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.priority = 0;
        }

        private void InitializeSfxPool()
        {
            if (sfxPoolParent == null)
            {
                GameObject poolGo = new GameObject("SFXPool");
                poolGo.transform.SetParent(transform);
                sfxPoolParent = poolGo.transform;
            }

            for (int i = 0; i < sfxPoolSize; i++)
            {
                CreatePooledSfxSource();
            }
        }

        private AudioSource CreatePooledSfxSource()
        {
            GameObject go = new GameObject($"SFX_{sfxPool.Count}");
            go.transform.SetParent(sfxPoolParent);
            AudioSource source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            sfxPool.Add(source);
            return source;
        }

        private void LoadVolumeSettings()
        {
            musicVolume = PlayerPrefs.GetFloat(PrefKeyMusicVolume, defaultMusicVolume);
            sfxVolume = PlayerPrefs.GetFloat(PrefKeySfxVolume, defaultSfxVolume);
            isMuted = PlayerPrefs.GetInt(PrefKeyMuted, 0) == 1;

            ApplyMusicVolume();
        }

        private void SaveVolumeSettings()
        {
            PlayerPrefs.SetFloat(PrefKeyMusicVolume, musicVolume);
            PlayerPrefs.SetFloat(PrefKeySfxVolume, sfxVolume);
            PlayerPrefs.SetInt(PrefKeyMuted, isMuted ? 1 : 0);
            PlayerPrefs.Save();
        }

        public void PlayMusic(AudioClip clip)
        {
            if (clip == null) return;

            if (activeMusicSource.clip == clip && activeMusicSource.isPlaying)
                return;

            if (crossfadeCoroutine != null)
                StopCoroutine(crossfadeCoroutine);

            crossfadeCoroutine = StartCoroutine(CrossfadeMusic(clip));
        }

        public void StopMusic()
        {
            if (crossfadeCoroutine != null)
                StopCoroutine(crossfadeCoroutine);

            crossfadeCoroutine = StartCoroutine(FadeOutMusic());
        }

        private IEnumerator CrossfadeMusic(AudioClip newClip)
        {
            AudioSource fadingOut = activeMusicSource;
            AudioSource fadingIn = inactiveMusicSource;

            fadingIn.clip = newClip;
            fadingIn.volume = 0f;
            fadingIn.Play();

            float effectiveVolume = isMuted ? 0f : musicVolume;
            float elapsed = 0f;

            while (elapsed < crossfadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / crossfadeDuration;

                fadingOut.volume = Mathf.Lerp(effectiveVolume, 0f, t);
                fadingIn.volume = Mathf.Lerp(0f, effectiveVolume, t);

                yield return null;
            }

            fadingOut.Stop();
            fadingOut.clip = null;
            fadingOut.volume = 0f;
            fadingIn.volume = effectiveVolume;

            activeMusicSource = fadingIn;
            inactiveMusicSource = fadingOut;
            crossfadeCoroutine = null;
        }

        private IEnumerator FadeOutMusic()
        {
            float startVolume = activeMusicSource.volume;
            float elapsed = 0f;

            while (elapsed < crossfadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / crossfadeDuration;
                activeMusicSource.volume = Mathf.Lerp(startVolume, 0f, t);
                yield return null;
            }

            activeMusicSource.Stop();
            activeMusicSource.clip = null;
            crossfadeCoroutine = null;
        }

        public void PlaySFX(AudioClip clip, float volumeScale = 1f)
        {
            if (clip == null || isMuted) return;

            AudioSource source = GetAvailableSfxSource();
            source.spatialBlend = 0f;
            source.clip = clip;
            source.volume = sfxVolume * volumeScale;
            source.Play();
        }

        public void PlaySFXAtPoint(AudioClip clip, Vector3 position, float volumeScale = 1f, float spatialBlend = 1f, float maxDistance = 30f)
        {
            if (clip == null || isMuted) return;

            AudioSource source = GetAvailableSfxSource();
            source.transform.position = position;
            source.spatialBlend = spatialBlend;
            source.minDistance = 1f;
            source.maxDistance = maxDistance;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.clip = clip;
            source.volume = sfxVolume * volumeScale;
            source.Play();
        }

        public AudioSource PlaySFXLoop(AudioClip clip, float volumeScale = 1f)
        {
            if (clip == null || isMuted) return null;

            AudioSource source = GetAvailableSfxSource();
            source.spatialBlend = 0f;
            source.clip = clip;
            source.volume = sfxVolume * volumeScale;
            source.loop = true;
            source.Play();
            return source;
        }

        public void StopSFXLoop(AudioSource source)
        {
            if (source == null) return;
            source.loop = false;
            source.Stop();
        }

        private AudioSource GetAvailableSfxSource()
        {
            for (int i = 0; i < sfxPool.Count; i++)
            {
                if (!sfxPool[i].isPlaying)
                    return sfxPool[i];
            }

            return CreatePooledSfxSource();
        }

        public void SetMusicVolume(float volume)
        {
            musicVolume = Mathf.Clamp01(volume);
            ApplyMusicVolume();
            SaveVolumeSettings();
        }

        public void SetSfxVolume(float volume)
        {
            sfxVolume = Mathf.Clamp01(volume);
            SaveVolumeSettings();
        }

        public void ToggleMute()
        {
            isMuted = !isMuted;
            ApplyMusicVolume();
            SaveVolumeSettings();

            if (isMuted)
            {
                foreach (AudioSource source in sfxPool)
                {
                    if (source.isPlaying)
                        source.Stop();
                }
            }
        }

        public void SetMuted(bool muted)
        {
            if (isMuted == muted) return;
            ToggleMute();
        }

        private void ApplyMusicVolume()
        {
            float effectiveVolume = isMuted ? 0f : musicVolume;
            if (activeMusicSource != null && activeMusicSource.isPlaying)
                activeMusicSource.volume = effectiveVolume;
        }
    }
}
