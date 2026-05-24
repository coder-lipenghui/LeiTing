using LeiTing.Core;
using LeiTing.UI;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace LeiTing.Audio
{
    [AddComponentMenu("LeiTing/Audio/Aircraft Engine Audio")]
    [DisallowMultipleComponent]
    public sealed class AircraftEngineAudio : MonoBehaviour
    {
        [Header("Engine Sound")]
        [Tooltip("Audio asset path, for example Assets/Art/Sound/SFX/Enemy/SFX_Enemy_Engine_Loop_Small_01.wav.")]
        [SerializeField] private string clipPath;
        [Tooltip("Optional direct reference. When assigned, it overrides Clip Path.")]
        [SerializeField] private AudioClip clipOverride;
        [SerializeField, Range(0f, 1f)] private float volume = 0.35f;
        [SerializeField, Range(0.1f, 3f)] private float pitch = 1f;
        [SerializeField] private bool loop = true;
        [SerializeField] private bool playOnEnable = true;

        private AudioSource audioSource;
        private bool missingClipWarningLogged;

        public string ClipPath => clipPath;
        public AudioClip ClipOverride => clipOverride;

        private void Awake()
        {
            ConfigureAudioSource();
        }

        private void OnEnable()
        {
            ConfigureAudioSource();

            if (playOnEnable)
            {
                Play();
            }
        }

        private void Update()
        {
            if (audioSource != null)
            {
                audioSource.mute = !GameSettingManager.SoundEnabled;
            }
        }

        private void OnDisable()
        {
            Stop();
        }

        public void Play()
        {
            ConfigureAudioSource();

            var clip = ResolveClip();
            if (clip == null)
            {
                if (!missingClipWarningLogged)
                {
                    Debug.LogWarning($"Aircraft engine audio clip could not be loaded: {clipPath}", this);
                    missingClipWarningLogged = true;
                }

                return;
            }

            missingClipWarningLogged = false;
            audioSource.clip = clip;
            audioSource.mute = !GameSettingManager.SoundEnabled;

            if (!audioSource.isPlaying)
            {
                audioSource.Play();
            }
        }

        public void Stop()
        {
            if (audioSource != null && audioSource.isPlaying)
            {
                audioSource.Stop();
            }
        }

        private void ConfigureAudioSource()
        {
            audioSource = audioSource != null ? audioSource : GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }

            audioSource.playOnAwake = false;
            audioSource.loop = loop;
            audioSource.volume = volume;
            audioSource.pitch = pitch;
            audioSource.spatialBlend = 0f;
        }

        private AudioClip ResolveClip()
        {
            if (clipOverride != null)
            {
                return clipOverride;
            }

            if (string.IsNullOrWhiteSpace(clipPath))
            {
                return null;
            }

#if UNITY_EDITOR
            if (clipPath.StartsWith("Assets/", System.StringComparison.OrdinalIgnoreCase))
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
            var normalized = assetPath.Replace("\\", "/").Trim();
            var resourcesIndex = normalized.IndexOf(resourcesSegment, System.StringComparison.OrdinalIgnoreCase);

            if (resourcesIndex >= 0)
            {
                normalized = normalized.Substring(resourcesIndex + resourcesSegment.Length);
            }

            var extensionIndex = normalized.LastIndexOf(".", System.StringComparison.Ordinal);
            return extensionIndex >= 0 ? normalized.Substring(0, extensionIndex) : normalized;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            volume = Mathf.Clamp01(volume);
            pitch = Mathf.Clamp(pitch, 0.1f, 3f);

            if (Application.isPlaying)
            {
                ConfigureAudioSource();

                if (!playOnEnable)
                {
                    Stop();
                }
                else if (isActiveAndEnabled && (!audioSource.isPlaying || audioSource.clip != ResolveClip()))
                {
                    Stop();
                    Play();
                }
            }
        }
#endif
    }
}
