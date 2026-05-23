using System;
using System.Collections.Generic;
using LeiTing.Core;
using LeiTing.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace LeiTing.Audio
{
    public class AudioManager : MonoSingleton<AudioManager>
    {
        public const string MenuBgmPath = "Assets/Art/Sound/BGM/BGM_Menu_Main_Loop_01.wav";

        private const float DefaultVolume = 0.7f;
        private const float BgmRetryInterval = 0.5f;
        private const string PersistentBgmSourceName = "PersistentBgmAudioSource";

        private static AudioSource persistentBgmSource;
        private AudioSource bgmSource;
        private AudioSource sfxSource;
        private AudioClip enemyDestroyedClip;
        private AudioClip playerDestroyedClip;
        private readonly Dictionary<string, AudioClip> clipCache = new Dictionary<string, AudioClip>(StringComparer.OrdinalIgnoreCase);
        private float nextBgmRetryTime;

        protected override void Awake()
        {
            base.Awake();

            if (Instance == this)
            {
                sfxSource = GetComponent<AudioSource>();
                if (sfxSource == null)
                {
                    sfxSource = gameObject.AddComponent<AudioSource>();
                }

                ConfigureAudioSource(sfxSource, false);
                bgmSource = GetOrCreatePersistentBgmSource();
            }
        }

        private void Start()
        {
            if (Instance == this && GameSceneManager.IsLobbySceneName(SceneManager.GetActiveScene().name))
            {
                PlayMenuBgm();
            }
        }

        private void Update()
        {
            if (bgmSource == null || bgmSource.clip == null)
            {
                return;
            }

            var musicEnabled = GameSettingManager.MusicEnabled;
            bgmSource.mute = !musicEnabled;

            if (musicEnabled && !bgmSource.isPlaying && Time.unscaledTime >= nextBgmRetryTime)
            {
                StartBgm();
            }
        }

        public void PlayEnemyDestroyed()
        {
            PlayClip(enemyDestroyedClip ??= CreateImpactClip(0.16f, 360f, 80f));
        }

        public void PlayPlayerDestroyed()
        {
            PlayClip(playerDestroyedClip ??= CreateImpactClip(0.32f, 180f, 45f));
        }

        public void PlaySfx(string clipPath)
        {
            if (string.IsNullOrEmpty(clipPath))
            {
                return;
            }

            PlayClip(LoadCachedAudioClip(clipPath));
        }

        public void PlayMenuBgm()
        {
            PlayBgm(MenuBgmPath);
        }

        public void PlayBgm(string clipPath)
        {
            if (bgmSource == null)
            {
                return;
            }

            if (string.IsNullOrEmpty(clipPath))
            {
                StopBgm();
                return;
            }

            var clip = LoadCachedAudioClip(clipPath);
            if (clip == null)
            {
                Debug.LogWarning($"BGM audio clip could not be loaded: {clipPath}", this);
                StopBgm();
                return;
            }

            if (bgmSource.clip != clip)
            {
                bgmSource.Stop();
                bgmSource.clip = clip;
            }

            bgmSource.loop = true;
            bgmSource.mute = !GameSettingManager.MusicEnabled;

            if (!bgmSource.mute && !bgmSource.isPlaying)
            {
                StartBgm();
            }
        }

        public void StopBgm()
        {
            if (bgmSource == null)
            {
                return;
            }

            bgmSource.Stop();
            bgmSource.clip = null;
            bgmSource.loop = false;
            nextBgmRetryTime = 0f;
        }

        private void PlayClip(AudioClip clip)
        {
            if (sfxSource != null && clip != null && GameSettingManager.SoundEnabled)
            {
                sfxSource.PlayOneShot(clip);
            }
        }

        private AudioClip LoadCachedAudioClip(string clipPath)
        {
            var normalizedPath = clipPath.Replace("\\", "/").Trim();
            if (clipCache.TryGetValue(normalizedPath, out var cachedClip))
            {
                return cachedClip;
            }

            var clip = LoadAudioClip(normalizedPath);
            clipCache[normalizedPath] = clip;
            return clip;
        }

        private void StartBgm()
        {
            bgmSource.Play();
            nextBgmRetryTime = Time.unscaledTime + BgmRetryInterval;
        }

        private static void ConfigureAudioSource(AudioSource source, bool loop)
        {
            source.playOnAwake = false;
            source.loop = loop;
            source.volume = DefaultVolume;
            source.spatialBlend = 0f;
        }

        private static AudioSource GetOrCreatePersistentBgmSource()
        {
            if (persistentBgmSource != null)
            {
                return persistentBgmSource;
            }

            var sourceObject = new GameObject(PersistentBgmSourceName);
            UnityEngine.Object.DontDestroyOnLoad(sourceObject);
            persistentBgmSource = sourceObject.AddComponent<AudioSource>();
            ConfigureAudioSource(persistentBgmSource, true);
            return persistentBgmSource;
        }

        private static AudioClip CreateImpactClip(float duration, float startFrequency, float endFrequency)
        {
            const int sampleRate = 22050;
            var sampleCount = Mathf.CeilToInt(sampleRate * duration);
            var samples = new float[sampleCount];

            for (var index = 0; index < sampleCount; index++)
            {
                var t = index / (float)sampleCount;
                var frequency = Mathf.Lerp(startFrequency, endFrequency, t);
                var envelope = Mathf.Pow(1f - t, 2.2f);
                var tone = Mathf.Sin(2f * Mathf.PI * frequency * index / sampleRate);
                var grit = Mathf.Sin(2f * Mathf.PI * frequency * 2.7f * index / sampleRate) * 0.35f;
                samples[index] = (tone + grit) * envelope * 0.32f;
            }

            var clip = AudioClip.Create("GeneratedImpact", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip LoadAudioClip(string clipPath)
        {
#if UNITY_EDITOR
            if (!string.IsNullOrEmpty(clipPath) && clipPath.StartsWith("Assets/", System.StringComparison.OrdinalIgnoreCase))
            {
                var editorClip = AssetDatabase.LoadAssetAtPath<AudioClip>(clipPath);
                if (editorClip != null)
                {
                    return editorClip;
                }
            }
#endif

            return RuntimeAssetCatalog.LoadAudioClip(clipPath)
                ?? Resources.Load<AudioClip>(NormalizeResourcesPath(clipPath));
        }

        private static string NormalizeResourcesPath(string assetPath)
        {
            const string resourcesSegment = "/Resources/";
            var normalized = assetPath.Replace("\\", "/");
            var resourcesIndex = normalized.IndexOf(resourcesSegment, System.StringComparison.OrdinalIgnoreCase);

            if (resourcesIndex >= 0)
            {
                normalized = normalized.Substring(resourcesIndex + resourcesSegment.Length);
            }

            var extensionIndex = normalized.LastIndexOf(".", System.StringComparison.Ordinal);
            if (extensionIndex >= 0)
            {
                normalized = normalized.Substring(0, extensionIndex);
            }

            return normalized;
        }
    }
}
