using LeiTing.Core;
using UnityEngine;

namespace LeiTing.Audio
{
    public class AudioManager : MonoSingleton<AudioManager>
    {
        private AudioSource audioSource;
        private AudioClip enemyDestroyedClip;
        private AudioClip playerDestroyedClip;

        protected override void Awake()
        {
            base.Awake();

            if (Instance == this)
            {
                audioSource = GetComponent<AudioSource>() ?? gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
                audioSource.volume = 0.7f;
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

        private void PlayClip(AudioClip clip)
        {
            if (audioSource != null && clip != null)
            {
                audioSource.PlayOneShot(clip);
            }
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
    }
}
