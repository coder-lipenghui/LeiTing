using UnityEngine;

namespace LeiTing.Missiles
{
    [DisallowMultipleComponent]
    public class MissileTrailController : MonoBehaviour
    {
        [Header("Particles")]
        [SerializeField] private ParticleSystem flame;
        [SerializeField] private ParticleSystem smoke;
        [SerializeField] private ParticleSystem sparks;

        [Header("Trail")]
        [SerializeField] private TrailRenderer trail;

        public ParticleSystem Flame => flame;
        public ParticleSystem Smoke => smoke;
        public ParticleSystem Sparks => sparks;
        public TrailRenderer Trail => trail;

        private void OnEnable()
        {
            ResetTrail();
            PlayAll();
        }

        public void Assign(ParticleSystem flameParticle, ParticleSystem smokeParticle, ParticleSystem sparkParticle, TrailRenderer trailRenderer)
        {
            flame = flameParticle;
            smoke = smokeParticle;
            sparks = sparkParticle;
            trail = trailRenderer;
        }

        public void ResetTrail()
        {
            if (trail != null)
            {
                trail.Clear();
                trail.emitting = trail.enabled;
            }

            ClearParticle(flame);
            ClearParticle(smoke);
            ClearParticle(sparks);
        }

        public void PlayAll()
        {
            if (trail != null)
            {
                trail.emitting = trail.enabled;
            }

            PlayParticle(flame);
            PlayParticle(smoke);
            PlayParticle(sparks);
        }

        public void StopTrail()
        {
            if (trail != null)
            {
                trail.emitting = false;
            }

            StopParticleEmission(flame);
            StopParticleEmission(smoke);
            StopParticleEmission(sparks);
        }

        public void StopAndClear()
        {
            if (trail != null)
            {
                trail.emitting = false;
                trail.Clear();
            }

            StopParticleEmissionAndClear(flame);
            StopParticleEmissionAndClear(smoke);
            StopParticleEmissionAndClear(sparks);
        }

        private static void PlayParticle(ParticleSystem ps)
        {
            if (ps == null)
            {
                return;
            }

            var emission = ps.emission;
            if (emission.enabled && ps.gameObject.activeInHierarchy)
            {
                ps.Play(true);
                return;
            }

            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private static void ClearParticle(ParticleSystem ps)
        {
            if (ps == null)
            {
                return;
            }

            ps.Clear(true);
        }

        private static void StopParticleEmission(ParticleSystem ps)
        {
            if (ps == null)
            {
                return;
            }

            ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        private static void StopParticleEmissionAndClear(ParticleSystem ps)
        {
            if (ps == null)
            {
                return;
            }

            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }
}
