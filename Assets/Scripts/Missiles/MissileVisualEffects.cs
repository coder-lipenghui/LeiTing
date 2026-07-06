using System;
using LeiTing.Core;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace LeiTing.Missiles
{
    public enum MissileVisualTrailMode
    {
        UseConfig = 0,
        None = 1,
        Light = 2,
        Smoke = 3,
        LightAndSmoke = 4,
        Contrail = 5
    }

    public struct MissileVisualEffectContext
    {
        public float Radius;
        public bool CanBeDestroyed;
        public Color TailColor;
        public string TailType;
        public float Time;
    }

    [DisallowMultipleComponent]
    public class MissileVisualEffects : MonoBehaviour
    {
        private const float SpritePixelsPerUnit = 100f;
        private const int GlowTextureSize = 64;

        private static Material defaultSpriteMaterial;
        private static Material additiveParticleMaterial;
        private static Material smokeMaterial;
        private static Texture2D smokeTexture;
        private static Sprite glowSprite;

        [Header("Source")]
        [SerializeField] private MissileVisualTrailMode trailMode = MissileVisualTrailMode.UseConfig;
        [SerializeField] private bool useConfigTailColor = true;
        [SerializeField] private Color customTailColor = new Color(1f, 0.72f, 0.22f, 1f);

        [Header("References")]
        [SerializeField] private Transform tailPoint;
        [SerializeField] private MissileTrailController trailController;
        [SerializeField] private Transform lightTrailRoot;
        [SerializeField] private TrailRenderer lightTrailRenderer;
        [SerializeField] private Transform flameRoot;
        [SerializeField] private ParticleSystem flame;
        [SerializeField] private ParticleSystemRenderer flameRenderer;
        [SerializeField] private Transform smokeTrailRoot;
        [SerializeField] private ParticleSystem smokeTrail;
        [SerializeField] private ParticleSystemRenderer smokeTrailRenderer;
        [SerializeField] private Transform sparkRoot;
        [SerializeField] private ParticleSystem sparks;
        [SerializeField] private ParticleSystemRenderer sparksRenderer;
        [SerializeField] private Transform tailGlowRoot;
        [SerializeField] private SpriteRenderer tailGlowRenderer;

        [Header("TrailRenderer")]
        [SerializeField] private LightTrailSettings lightTrail = new LightTrailSettings();

        [Header("Flame_Particle")]
        [SerializeField] private FlameTrailSettings flameTrail = new FlameTrailSettings();

        [Header("Smoke_Particle")]
        [SerializeField] private SmokeTrailSettings smoke = new SmokeTrailSettings();

        [Header("Spark_Particle")]
        [SerializeField] private SparkTrailSettings sparkTrail = new SparkTrailSettings();

        [Header("Tail Glow")]
        [SerializeField] private TailGlowSettings tailGlow = new TailGlowSettings();

        private Color activeTailColor = Color.white;
        private float activeRadius = 0.16f;
        private bool activeLightTrail;
        private bool activeFlame;
        private bool activeSmokeTrail;
        private bool activeSparkTrail;
        private float activeLightTrailDuration;
        private float activeFlameLifetimeMax;
        private float activeSmokeLifetimeMax;
        private float activeSparkLifetimeMax;
        private bool hasAuthoredTailPoint;

#if UNITY_EDITOR
        private bool validateQueued;
#endif

        public MissileVisualTrailMode TrailMode => trailMode;
        public bool UsesConfigTailColor => useConfigTailColor;
        public Color CustomTailColor => customTailColor;
        public Transform TailPoint => tailPoint;
        public MissileTrailController TrailController => trailController;
        public Transform LightTrailRoot => lightTrailRoot;
        public TrailRenderer LightTrail => lightTrailRenderer;
        public Transform FlameRoot => flameRoot;
        public ParticleSystem Flame => flame;
        public ParticleSystemRenderer FlameRenderer => flameRenderer;
        public Transform SmokeTrailRoot => smokeTrailRoot;
        public ParticleSystem SmokeTrail => smokeTrail;
        public ParticleSystemRenderer SmokeTrailRenderer => smokeTrailRenderer;
        public Transform SparkRoot => sparkRoot;
        public ParticleSystem Sparks => sparks;
        public ParticleSystemRenderer SparksRenderer => sparksRenderer;
        public Transform TailGlowRoot => tailGlowRoot;
        public SpriteRenderer TailGlow => tailGlowRenderer;
        public float ReleaseDuration => ResolveReleaseDuration();

        public void EnsureEffectObjects()
        {
            tailPoint = EnsureTailPoint();
            trailController = trailController != null ? trailController : GetComponent<MissileTrailController>();
            if (trailController == null)
            {
                trailController = gameObject.AddComponent<MissileTrailController>();
            }

            lightTrailRoot = EnsureChild(lightTrailRoot, tailPoint, "TrailRenderer", "LightTrail");
            lightTrailRenderer = EnsureComponent<TrailRenderer>(lightTrailRoot.gameObject);

            flameRoot = EnsureChild(flameRoot, tailPoint, "Flame_Particle", "Flame");
            flame = EnsureComponent<ParticleSystem>(flameRoot.gameObject);
            flameRenderer = flameRoot.GetComponent<ParticleSystemRenderer>();

            smokeTrailRoot = EnsureChild(smokeTrailRoot, tailPoint, "Smoke_Particle", "SmokeTrail");
            smokeTrail = EnsureComponent<ParticleSystem>(smokeTrailRoot.gameObject);
            smokeTrailRenderer = smokeTrailRoot.GetComponent<ParticleSystemRenderer>();

            sparkRoot = EnsureChild(sparkRoot, tailPoint, "Spark_Particle", "Sparks");
            sparks = EnsureComponent<ParticleSystem>(sparkRoot.gameObject);
            sparksRenderer = sparkRoot.GetComponent<ParticleSystemRenderer>();

            tailGlowRoot = EnsureChild(tailGlowRoot, tailPoint, "TailGlow");
            tailGlowRenderer = EnsureComponent<SpriteRenderer>(tailGlowRoot.gameObject);
            tailGlowRenderer.sprite = GetGlowSprite();

            trailController.Assign(flame, smokeTrail, sparks, lightTrailRenderer);
        }

        public void Apply(MissileVisualEffectContext context)
        {
            EnsureEffectObjects();

            activeRadius = Mathf.Max(0.04f, context.Radius);
            activeTailColor = ResolveTailColor(context);

            var resolvedMode = ResolveTrailMode(context.TailType);
            var useContrail = resolvedMode == MissileVisualTrailMode.Contrail;
            activeLightTrail = UsesLightTrail(resolvedMode);
            activeFlame = UsesFlame(resolvedMode);
            activeSmokeTrail = UsesSmokeTrail(resolvedMode);
            activeSparkTrail = UsesSparks(resolvedMode, context.CanBeDestroyed, context.TailType);

            ConfigureTailPoint(activeRadius);
            ConfigureLightTrail(activeLightTrail, activeTailColor, activeRadius, useContrail);
            ConfigureFlame(activeFlame, activeTailColor, activeRadius, useContrail);
            ConfigureSmokeTrail(activeSmokeTrail, activeTailColor, activeRadius, context.CanBeDestroyed, useContrail);
            ConfigureSparks(activeSparkTrail, activeTailColor, activeRadius);
            ConfigureTailGlow(activeTailColor, activeRadius, context.Time);

            if (trailController != null)
            {
                trailController.Assign(flame, smokeTrail, sparks, lightTrailRenderer);
            }
        }

        public void Play()
        {
            if (trailController != null)
            {
                trailController.ResetTrail();
                trailController.PlayAll();
                return;
            }

            if (lightTrailRenderer != null)
            {
                lightTrailRenderer.Clear();
                lightTrailRenderer.emitting = lightTrailRenderer.enabled;
            }

            PlayParticle(flame);
            PlayParticle(smokeTrail);
            PlayParticle(sparks);
        }

        public void StopTrail()
        {
            if (trailController != null)
            {
                trailController.StopTrail();
                return;
            }

            if (lightTrailRenderer != null)
            {
                lightTrailRenderer.emitting = false;
            }

            StopParticleEmission(flame);
            StopParticleEmission(smokeTrail);
            StopParticleEmission(sparks);
        }

        public void StopAndClear()
        {
            if (trailController != null)
            {
                trailController.StopAndClear();
            }
            else
            {
                if (lightTrailRenderer != null)
                {
                    lightTrailRenderer.emitting = false;
                    lightTrailRenderer.Clear();
                }

                StopParticleEmissionAndClear(flame);
                StopParticleEmissionAndClear(smokeTrail);
                StopParticleEmissionAndClear(sparks);
            }
        }

        public void UpdateDynamic(float time)
        {
            ConfigureTailGlow(activeTailColor, activeRadius, time);
        }

        public void SetLayer(int layer)
        {
            gameObject.layer = layer;
            SetLayerIfValid(tailPoint, layer);
            SetLayerIfValid(lightTrailRoot, layer);
            SetLayerIfValid(flameRoot, layer);
            SetLayerIfValid(smokeTrailRoot, layer);
            SetLayerIfValid(sparkRoot, layer);
            SetLayerIfValid(tailGlowRoot, layer);
        }

        public void ResetToDefaults(MissileVisualTrailMode defaultTrailMode, Color defaultTailColor, float missileRadius)
        {
            trailMode = defaultTrailMode;
            useConfigTailColor = false;
            customTailColor = defaultTailColor;

            var radius = Mathf.Max(0.04f, missileRadius);
            var isContrail = defaultTrailMode == MissileVisualTrailMode.Contrail;
            lightTrail.duration = isContrail ? 0.52f : Mathf.Lerp(0.22f, 0.46f, Mathf.InverseLerp(0.12f, 0.32f, radius));
            lightTrail.startWidthRadiusScale = isContrail ? 0.38f : defaultTrailMode == MissileVisualTrailMode.LightAndSmoke ? 0.62f : 0.82f;
            lightTrail.endWidth = isContrail ? 0.006f : 0.01f;
            lightTrail.offsetRadiusScale = 0.9f;

            flameTrail.emissionRate = isContrail ? 42f : defaultTrailMode == MissileVisualTrailMode.LightAndSmoke ? 92f : 72f;
            flameTrail.sizeRadiusScaleMin = isContrail ? 0.36f : 0.75f;
            flameTrail.sizeRadiusScaleMax = isContrail ? 1.05f : defaultTrailMode == MissileVisualTrailMode.LightAndSmoke ? 1.95f : 1.55f;

            smoke.lifetimeMin = isContrail ? 1.1f : 0.58f;
            smoke.lifetimeMax = isContrail ? 1.75f : 0.92f;
            smoke.startSpeedMin = isContrail ? 0f : 0.02f;
            smoke.startSpeedMax = isContrail ? 0.035f : 0.16f;
            smoke.sizeRadiusScaleMin = isContrail ? 0.32f : 1.2f;
            smoke.sizeRadiusScaleMax = isContrail ? 0.72f : defaultTrailMode == MissileVisualTrailMode.LightAndSmoke ? 1.95f : 2.45f;
            smoke.emissionRate = isContrail ? 10f : defaultTrailMode == MissileVisualTrailMode.LightAndSmoke ? 16f : 24f;
            smoke.shapeRadiusScale = isContrail ? 0.2f : 0.45f;
            smoke.offsetRadiusScale = isContrail ? 0.9f : 0.78f;
            smoke.sideDrift = isContrail ? 0.025f : 0.08f;
            smoke.backwardSpeedMin = isContrail ? -0.025f : -0.28f;
            smoke.backwardSpeedMax = isContrail ? 0.025f : -0.08f;
            smoke.startAlpha = isContrail ? 0.82f : 0.68f;
            smoke.midAlpha = isContrail ? 0.42f : 0.36f;
            smoke.colorToWhite = isContrail ? 0.92f : 0.45f;
            smoke.midSizeMultiplier = isContrail ? 0.86f : 0.62f;
            smoke.endSizeMultiplier = isContrail ? 0.18f : 0.08f;
            smoke.noiseStrengthRadiusScale = isContrail ? 0.08f : 0.28f;
            smoke.noiseFrequency = isContrail ? 0.9f : 1.6f;
            smoke.noiseScrollSpeed = isContrail ? 0.05f : 0.25f;
            smoke.maxParticles = isContrail ? 160 : 72;

            sparkTrail.emissionRate = defaultTrailMode == MissileVisualTrailMode.LightAndSmoke ? 12f : 0f;

            tailGlow.enabled = true;
            tailGlow.diameterRadiusScale = isContrail ? 1.65f : defaultTrailMode == MissileVisualTrailMode.Smoke ? 1.75f : 2.05f;
            tailGlow.alpha = isContrail ? 0.52f : defaultTrailMode == MissileVisualTrailMode.Smoke ? 0.52f : 0.76f;

            EnsureEffectObjects();
            Apply(new MissileVisualEffectContext
            {
                Radius = radius,
                CanBeDestroyed = false,
                TailColor = defaultTailColor,
                TailType = TrailModeToTailType(defaultTrailMode),
                Time = 0f
            });
            StopAndClear();
        }

        private void Reset()
        {
            EnsureEffectObjects();
            Apply(new MissileVisualEffectContext
            {
                Radius = ResolveRadiusFromCollider(),
                TailColor = customTailColor,
                TailType = "light",
                Time = 0f
            });
            StopAndClear();
        }

        private void OnValidate()
        {
#if UNITY_EDITOR
            if (Application.isPlaying || IsReadOnlyPrefabAssetContext())
            {
                return;
            }

            if (validateQueued)
            {
                return;
            }

            validateQueued = true;
            UnityEditor.EditorApplication.delayCall += ApplyValidatedSettings;
#endif
        }

#if UNITY_EDITOR
        private void ApplyValidatedSettings()
        {
            validateQueued = false;
            if (this == null || Application.isPlaying || IsReadOnlyPrefabAssetContext())
            {
                return;
            }

            EnsureEffectObjects();
            Apply(new MissileVisualEffectContext
            {
                Radius = ResolveRadiusFromCollider(),
                TailColor = customTailColor,
                TailType = "light",
                Time = 0f
            });
            StopAndClear();
        }

        private bool IsReadOnlyPrefabAssetContext()
        {
            return EditorUtility.IsPersistent(gameObject)
                || PrefabUtility.IsPartOfPrefabAsset(gameObject);
        }
#endif

        private void ConfigureTailPoint(float radius)
        {
            if (tailPoint == null)
            {
                return;
            }

            if (!hasAuthoredTailPoint)
            {
                tailPoint.localPosition = Vector3.down * ResolveDefaultTailOffset(radius);
            }

            tailPoint.localRotation = Quaternion.identity;
            tailPoint.localScale = Vector3.one;
        }

        public void UseConfigDrivenSource(bool enabled)
        {
            trailMode = enabled ? MissileVisualTrailMode.UseConfig : trailMode;
            useConfigTailColor = enabled;
        }

        private void ConfigureLightTrail(bool enabled, Color tailColor, float radius, bool useContrail)
        {
            if (lightTrailRenderer == null)
            {
                return;
            }

            lightTrailRoot.localPosition = Vector3.zero;
            lightTrailRoot.localRotation = Quaternion.identity;
            lightTrailRoot.localScale = Vector3.one;

            var duration = useContrail ? Mathf.Max(lightTrail.duration, 0.5f) : lightTrail.duration;
            var startWidth = useContrail
                ? Mathf.Max(0.025f, radius * 0.36f)
                : Mathf.Max(lightTrail.minStartWidth, radius * lightTrail.startWidthRadiusScale);
            var endWidth = useContrail ? Mathf.Min(0.008f, Mathf.Max(0f, lightTrail.endWidth)) : Mathf.Max(0f, lightTrail.endWidth);
            var startAlpha = useContrail ? Mathf.Min(lightTrail.startAlpha, 0.48f) : lightTrail.startAlpha;
            var minVertexDistance = useContrail
                ? Mathf.Max(0.018f, radius * 0.28f)
                : Mathf.Max(0.03f, radius * lightTrail.minVertexDistanceRadiusScale);

            lightTrailRenderer.enabled = enabled;
            lightTrailRenderer.emitting = enabled;
            lightTrailRenderer.time = Mathf.Max(0f, duration);
            lightTrailRenderer.startWidth = startWidth;
            lightTrailRenderer.endWidth = endWidth;
            lightTrailRenderer.startColor = WithAlpha(Color.Lerp(tailColor, Color.white, useContrail ? 0.72f : 0f), startAlpha);
            lightTrailRenderer.endColor = WithAlpha(tailColor, lightTrail.endAlpha);
            lightTrailRenderer.material = lightTrail.material != null ? lightTrail.material : GetDefaultSpriteMaterial();
            lightTrailRenderer.sortingOrder = lightTrail.sortingOrder;
            lightTrailRenderer.numCapVertices = Mathf.Max(0, lightTrail.capVertices);
            lightTrailRenderer.minVertexDistance = minVertexDistance;
            lightTrailRenderer.alignment = LineAlignment.View;
            lightTrailRenderer.textureMode = LineTextureMode.Stretch;
            activeLightTrailDuration = enabled ? lightTrailRenderer.time : 0f;

            if (!enabled)
            {
                lightTrailRenderer.Clear();
            }
        }

        private void ConfigureFlame(bool enabled, Color tailColor, float radius, bool useContrail)
        {
            if (flame == null)
            {
                return;
            }

            flameRoot.localPosition = Vector3.zero;
            flameRoot.localRotation = Quaternion.identity;
            flameRoot.localScale = Vector3.one;

            var main = flame.main;
            main.loop = true;
            main.playOnAwake = false;
            main.prewarm = false;
            main.duration = Mathf.Max(0.05f, flameTrail.duration);
            main.startDelay = 0f;
            var lifetimeMin = useContrail ? Mathf.Min(flameTrail.lifetimeMin, 0.06f) : flameTrail.lifetimeMin;
            var lifetimeMax = useContrail ? Mathf.Min(flameTrail.lifetimeMax, 0.13f) : flameTrail.lifetimeMax;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetimeMin, lifetimeMax);
            main.startSpeed = new ParticleSystem.MinMaxCurve(
                useContrail ? Mathf.Min(flameTrail.startSpeedMin, 0.25f) : flameTrail.startSpeedMin,
                useContrail ? Mathf.Min(flameTrail.startSpeedMax, 0.85f) : flameTrail.startSpeedMax);
            main.startSize = new ParticleSystem.MinMaxCurve(
                Mathf.Max(0.01f, radius * (useContrail ? Mathf.Min(flameTrail.sizeRadiusScaleMin, 0.38f) : flameTrail.sizeRadiusScaleMin)),
                Mathf.Max(0.01f, radius * (useContrail ? Mathf.Min(flameTrail.sizeRadiusScaleMax, 1.05f) : flameTrail.sizeRadiusScaleMax)));
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = WithAlpha(Color.Lerp(Color.white, tailColor, 0.35f), flameTrail.startAlpha);
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.maxParticles = Mathf.Max(1, flameTrail.maxParticles);

            var emission = flame.emission;
            emission.enabled = enabled;
            emission.rateOverTime = enabled
                ? new ParticleSystem.MinMaxCurve(Mathf.Max(0f, useContrail ? Mathf.Min(flameTrail.emissionRate, 44f) : flameTrail.emissionRate))
                : new ParticleSystem.MinMaxCurve(0f);
            emission.rateOverDistance = new ParticleSystem.MinMaxCurve(0f);

            var shape = flame.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = flameTrail.coneAngle;
            shape.radius = Mathf.Max(0.005f, radius * flameTrail.shapeRadiusScale);
            shape.radiusThickness = 1f;
            shape.rotation = new Vector3(90f, 0f, 0f);

            var velocity = flame.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            var sideSpread = useContrail ? Mathf.Min(flameTrail.sideSpread, 0.02f) : flameTrail.sideSpread;
            velocity.x = new ParticleSystem.MinMaxCurve(-sideSpread, sideSpread);
            velocity.y = new ParticleSystem.MinMaxCurve(flameTrail.backwardSpeedMin, flameTrail.backwardSpeedMax);
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

            var colorOverLifetime = flame.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            var hotColor = Color.Lerp(Color.white, tailColor, 0.2f);
            var warmColor = Color.Lerp(tailColor, new Color(1f, 0.18f, 0.03f, 1f), 0.55f);
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(hotColor, 0f),
                    new GradientColorKey(tailColor, 0.36f),
                    new GradientColorKey(warmColor, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(flameTrail.startAlpha, 0f),
                    new GradientAlphaKey(flameTrail.midAlpha, 0.52f),
                    new GradientAlphaKey(0f, 1f)
                });
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);

            var sizeOverLifetime = flame.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0.9f),
                new Keyframe(0.24f, 1f),
                new Keyframe(1f, 0f)));

            var noise = flame.noise;
            noise.enabled = false;

            if (flameRenderer != null)
            {
                flameRenderer.enabled = enabled;
                flameRenderer.renderMode = ParticleSystemRenderMode.Billboard;
                flameRenderer.material = flameTrail.material != null ? flameTrail.material : GetAdditiveParticleMaterial();
                flameRenderer.sortingOrder = flameTrail.sortingOrder;
            }

            if (!enabled)
            {
                flame.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }

            activeFlameLifetimeMax = enabled ? lifetimeMax : 0f;
        }

        private void ConfigureSmokeTrail(bool enabled, Color tailColor, float radius, bool canBeDestroyed, bool useContrail)
        {
            if (smokeTrail == null)
            {
                return;
            }

            smokeTrailRoot.localPosition = Vector3.zero;
            smokeTrailRoot.localRotation = Quaternion.identity;
            smokeTrailRoot.localScale = Vector3.one;

            var main = smokeTrail.main;
            main.loop = true;
            main.playOnAwake = false;
            main.prewarm = false;
            main.duration = Mathf.Max(0.05f, smoke.duration);
            main.startDelay = 0f;
            var lifetimeMin = useContrail ? Mathf.Max(1.1f, smoke.lifetimeMin) : smoke.lifetimeMin;
            var lifetimeMax = useContrail ? Mathf.Max(1.75f, smoke.lifetimeMax) : smoke.lifetimeMax;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetimeMin, lifetimeMax);
            main.startSpeed = new ParticleSystem.MinMaxCurve(
                useContrail ? Mathf.Min(smoke.startSpeedMin, 0.01f) : smoke.startSpeedMin,
                useContrail ? Mathf.Min(smoke.startSpeedMax, 0.035f) : smoke.startSpeedMax);
            main.startSize = new ParticleSystem.MinMaxCurve(
                Mathf.Max(0.01f, radius * (useContrail ? Mathf.Min(smoke.sizeRadiusScaleMin, 0.34f) : smoke.sizeRadiusScaleMin)),
                Mathf.Max(0.01f, radius * (useContrail ? Mathf.Min(smoke.sizeRadiusScaleMax, 0.76f) : smoke.sizeRadiusScaleMax)));
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                WithAlpha(Color.Lerp(tailColor, Color.white, useContrail ? 0.92f : smoke.colorToWhite), useContrail ? Mathf.Max(smoke.startAlpha, 0.8f) : smoke.startAlpha),
                WithAlpha(Color.Lerp(tailColor, smoke.shadowColor, useContrail ? 0.18f : 0.45f), useContrail ? 0.62f : smoke.startAlpha * 0.78f));
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.maxParticles = Mathf.Max(1, useContrail ? Mathf.Max(smoke.maxParticles, 160) : canBeDestroyed ? Mathf.RoundToInt(smoke.maxParticles * 1.3f) : smoke.maxParticles);

            var emission = smokeTrail.emission;
            emission.enabled = enabled;
            emission.rateOverTime = enabled
                ? new ParticleSystem.MinMaxCurve(Mathf.Max(0f, useContrail ? Mathf.Min(smoke.emissionRate, 10f) : smoke.emissionRate))
                : new ParticleSystem.MinMaxCurve(0f);
            emission.rateOverDistance = enabled && useContrail
                ? new ParticleSystem.MinMaxCurve(18f)
                : new ParticleSystem.MinMaxCurve(0f);

            var shape = smokeTrail.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = Mathf.Max(0.005f, radius * (useContrail ? Mathf.Min(smoke.shapeRadiusScale, 0.22f) : smoke.shapeRadiusScale));
            shape.radiusThickness = 1f;

            var velocity = smokeTrail.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = useContrail ? ParticleSystemSimulationSpace.World : ParticleSystemSimulationSpace.Local;
            var sideDrift = useContrail ? Mathf.Min(smoke.sideDrift, 0.025f) : smoke.sideDrift;
            velocity.x = new ParticleSystem.MinMaxCurve(-sideDrift, sideDrift);
            velocity.y = useContrail
                ? new ParticleSystem.MinMaxCurve(-sideDrift, sideDrift)
                : new ParticleSystem.MinMaxCurve(smoke.backwardSpeedMin, smoke.backwardSpeedMax);
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

            var colorOverLifetime = smokeTrail.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            var startColor = Color.Lerp(tailColor, Color.white, useContrail ? 0.92f : smoke.colorToWhite);
            var midColor = useContrail ? new Color(0.86f, 0.9f, 0.94f, 1f) : Color.Lerp(startColor, smoke.shadowColor, 0.35f);
            var endColor = useContrail ? new Color(0.64f, 0.69f, 0.74f, 1f) : smoke.shadowColor;
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(startColor, 0f),
                    new GradientColorKey(midColor, 0.62f),
                    new GradientColorKey(endColor, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(useContrail ? Mathf.Max(smoke.startAlpha, 0.8f) : smoke.startAlpha, 0f),
                    new GradientAlphaKey(useContrail ? Mathf.Max(smoke.midAlpha, 0.42f) : smoke.midAlpha, 0.48f),
                    new GradientAlphaKey(0f, 1f)
                });
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);

            var sizeOverLifetime = smokeTrail.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, useContrail ? 0.72f : 1f),
                new Keyframe(0.6f, useContrail ? Mathf.Max(smoke.midSizeMultiplier, 0.82f) : smoke.midSizeMultiplier),
                new Keyframe(1f, useContrail ? Mathf.Max(smoke.endSizeMultiplier, 0.18f) : smoke.endSizeMultiplier)));

            var noise = smokeTrail.noise;
            noise.enabled = smoke.noiseStrengthRadiusScale > 0f;
            noise.strength = new ParticleSystem.MinMaxCurve(radius * (useContrail ? Mathf.Min(smoke.noiseStrengthRadiusScale, 0.08f) : smoke.noiseStrengthRadiusScale));
            noise.frequency = useContrail ? Mathf.Min(smoke.noiseFrequency, 0.9f) : smoke.noiseFrequency;
            noise.scrollSpeed = useContrail ? Mathf.Min(smoke.noiseScrollSpeed, 0.05f) : smoke.noiseScrollSpeed;

            if (smokeTrailRenderer != null)
            {
                smokeTrailRenderer.enabled = enabled;
                smokeTrailRenderer.renderMode = ParticleSystemRenderMode.Billboard;
                smokeTrailRenderer.material = smoke.material != null ? smoke.material : GetSmokeMaterial();
                smokeTrailRenderer.sortingOrder = smoke.sortingOrder;
            }

            if (!enabled)
            {
                smokeTrail.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }

            activeSmokeLifetimeMax = enabled ? lifetimeMax : 0f;
        }

        private void ConfigureSparks(bool enabled, Color tailColor, float radius)
        {
            if (sparks == null)
            {
                return;
            }

            sparkRoot.localPosition = Vector3.zero;
            sparkRoot.localRotation = Quaternion.identity;
            sparkRoot.localScale = Vector3.one;

            var main = sparks.main;
            main.loop = true;
            main.playOnAwake = false;
            main.prewarm = false;
            main.duration = Mathf.Max(0.05f, sparkTrail.duration);
            main.startDelay = 0f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(sparkTrail.lifetimeMin, sparkTrail.lifetimeMax);
            main.startSpeed = new ParticleSystem.MinMaxCurve(sparkTrail.startSpeedMin, sparkTrail.startSpeedMax);
            main.startSize = new ParticleSystem.MinMaxCurve(
                Mathf.Max(0.005f, radius * sparkTrail.sizeRadiusScaleMin),
                Mathf.Max(0.005f, radius * sparkTrail.sizeRadiusScaleMax));
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                WithAlpha(Color.Lerp(Color.white, tailColor, 0.2f), sparkTrail.startAlpha),
                WithAlpha(tailColor, sparkTrail.startAlpha));
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.maxParticles = Mathf.Max(1, sparkTrail.maxParticles);

            var emission = sparks.emission;
            emission.enabled = enabled;
            emission.rateOverTime = enabled
                ? new ParticleSystem.MinMaxCurve(Mathf.Max(0f, sparkTrail.emissionRate))
                : new ParticleSystem.MinMaxCurve(0f);
            emission.rateOverDistance = new ParticleSystem.MinMaxCurve(0f);

            var shape = sparks.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = sparkTrail.coneAngle;
            shape.radius = Mathf.Max(0.005f, radius * sparkTrail.shapeRadiusScale);
            shape.radiusThickness = 1f;
            shape.rotation = new Vector3(90f, 0f, 0f);

            var velocity = sparks.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.x = new ParticleSystem.MinMaxCurve(-sparkTrail.sideSpread, sparkTrail.sideSpread);
            velocity.y = new ParticleSystem.MinMaxCurve(sparkTrail.backwardSpeedMin, sparkTrail.backwardSpeedMax);
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

            var colorOverLifetime = sparks.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.Lerp(Color.white, tailColor, 0.15f), 0f),
                    new GradientColorKey(tailColor, 0.45f),
                    new GradientColorKey(new Color(1f, 0.22f, 0.04f, 1f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(sparkTrail.startAlpha, 0f),
                    new GradientAlphaKey(sparkTrail.midAlpha, 0.45f),
                    new GradientAlphaKey(0f, 1f)
                });
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);

            var sizeOverLifetime = sparks.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 1f),
                new Keyframe(1f, 0f)));

            var noise = sparks.noise;
            noise.enabled = false;

            if (sparksRenderer != null)
            {
                sparksRenderer.enabled = enabled;
                sparksRenderer.renderMode = ParticleSystemRenderMode.Billboard;
                sparksRenderer.material = sparkTrail.material != null ? sparkTrail.material : GetAdditiveParticleMaterial();
                sparksRenderer.sortingOrder = sparkTrail.sortingOrder;
            }

            if (!enabled)
            {
                sparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }

            activeSparkLifetimeMax = enabled ? sparkTrail.lifetimeMax : 0f;
        }

        private void ConfigureTailGlow(Color tailColor, float radius, float time)
        {
            if (tailGlowRenderer == null)
            {
                return;
            }

            var shouldShow = tailGlow.enabled && (activeLightTrail || activeFlame || activeSmokeTrail || activeSparkTrail);
            tailGlowRenderer.enabled = shouldShow;
            if (!shouldShow)
            {
                return;
            }

            tailGlowRoot.localPosition = Vector3.zero;
            tailGlowRoot.localRotation = Quaternion.identity;

            var pulse = tailGlow.pulseAmount > 0f
                ? 1f + Mathf.Sin(time * tailGlow.pulseSpeed) * tailGlow.pulseAmount
                : 1f;
            var spriteWorldDiameter = GlowTextureSize / SpritePixelsPerUnit;
            var desiredDiameter = Mathf.Max(0.01f, radius * tailGlow.diameterRadiusScale * pulse);
            tailGlowRoot.localScale = Vector3.one * Mathf.Max(0.01f, desiredDiameter / spriteWorldDiameter);

            tailGlowRenderer.sprite = GetGlowSprite();
            tailGlowRenderer.color = WithAlpha(tailColor, tailGlow.alpha);
            tailGlowRenderer.sharedMaterial = tailGlow.material != null ? tailGlow.material : GetDefaultSpriteMaterial();
            tailGlowRenderer.sortingOrder = tailGlow.sortingOrder;
        }

        private Color ResolveTailColor(MissileVisualEffectContext context)
        {
            if (useConfigTailColor && context.TailColor.a > 0f)
            {
                return context.TailColor;
            }

            return customTailColor.a > 0f ? customTailColor : Color.white;
        }

        private MissileVisualTrailMode ResolveTrailMode(string configTailType)
        {
            if (trailMode != MissileVisualTrailMode.UseConfig)
            {
                return trailMode;
            }

            if (string.IsNullOrEmpty(configTailType))
            {
                return MissileVisualTrailMode.Light;
            }

            switch (configTailType.Trim().ToLowerInvariant())
            {
                case "none":
                case "off":
                case "disabled":
                    return MissileVisualTrailMode.None;
                case "smoke":
                case "exhaust":
                    return MissileVisualTrailMode.Smoke;
                case "contrail":
                case "white_smoke":
                case "smoke_line":
                case "smoke_ribbon":
                case "tracking_smoke":
                case "missile_smoke":
                    return MissileVisualTrailMode.Contrail;
                case "fire":
                case "flame":
                case "fire_smoke":
                case "all":
                case "smoke_light":
                case "light_smoke":
                case "heavy":
                case "heavy_smoke":
                case "boss":
                case "spark":
                case "sparks":
                    return MissileVisualTrailMode.LightAndSmoke;
                default:
                    return MissileVisualTrailMode.Light;
            }
        }

        private float ResolveRadiusFromCollider()
        {
            var circle = GetComponent<CircleCollider2D>();
            return circle != null ? Mathf.Max(0.04f, circle.radius) : 0.16f;
        }

        private float ResolveReleaseDuration()
        {
            var duration = 0f;
            if (activeLightTrail && lightTrailRenderer != null && lightTrailRenderer.enabled)
            {
                duration = Mathf.Max(duration, activeLightTrailDuration > 0f ? activeLightTrailDuration : lightTrailRenderer.time);
            }

            if (activeFlame)
            {
                duration = Mathf.Max(duration, activeFlameLifetimeMax > 0f ? activeFlameLifetimeMax : flameTrail.lifetimeMax);
            }

            if (activeSmokeTrail)
            {
                duration = Mathf.Max(duration, activeSmokeLifetimeMax > 0f ? activeSmokeLifetimeMax : smoke.lifetimeMax);
            }

            if (activeSparkTrail)
            {
                duration = Mathf.Max(duration, activeSparkLifetimeMax > 0f ? activeSparkLifetimeMax : sparkTrail.lifetimeMax);
            }

            return Mathf.Clamp(duration + 0.06f, 0.05f, 2.2f);
        }

        private Transform EnsureTailPoint()
        {
            var child = tailPoint != null ? tailPoint : transform.Find("TailPoint");
            if (child == null)
            {
                child = transform.Find("TrailEffectMount");
            }

            hasAuthoredTailPoint = child != null && child.localPosition.sqrMagnitude > 0.0001f;

            if (child == null)
            {
                child = new GameObject("TailPoint").transform;
                child.SetParent(transform, false);
                child.localPosition = Vector3.zero;
                hasAuthoredTailPoint = false;
            }

            child.name = "TailPoint";
            child.SetParent(transform, false);
            child.localRotation = Quaternion.identity;
            child.localScale = Vector3.one;
            child.gameObject.layer = gameObject.layer;
            return child;
        }

        private float ResolveDefaultTailOffset(float radius)
        {
            var visual = transform.Find("Visual");
            var renderer = visual != null ? visual.GetComponent<SpriteRenderer>() : null;
            if (renderer != null && renderer.sprite != null)
            {
                var scaleY = Mathf.Abs(visual.localScale.y);
                var spriteBounds = renderer.sprite.bounds;
                var tailY = visual.localPosition.y + (spriteBounds.center.y - spriteBounds.extents.y * 0.78f) * scaleY;
                var spriteTailOffset = Mathf.Abs(tailY);
                if (spriteTailOffset > 0.01f)
                {
                    return Mathf.Clamp(spriteTailOffset, radius * 0.9f, radius * 2.4f);
                }
            }

            return Mathf.Max(0.02f, radius * 0.9f);
        }

        private Transform EnsureChild(Transform cached, Transform parent, string childName, params string[] fallbackNames)
        {
            parent = parent != null ? parent : transform;

            if (cached != null)
            {
                cached.name = childName;
                cached.SetParent(parent, false);
                cached.gameObject.layer = gameObject.layer;
                cached.localPosition = Vector3.zero;
                cached.localRotation = Quaternion.identity;
                cached.localScale = Vector3.one;
                return cached;
            }

            var child = parent.Find(childName);
            if (child == null && parent != transform)
            {
                child = transform.Find(childName);
            }

            if (child == null && fallbackNames != null)
            {
                for (var index = 0; index < fallbackNames.Length && child == null; index++)
                {
                    var fallbackName = fallbackNames[index];
                    if (string.IsNullOrEmpty(fallbackName))
                    {
                        continue;
                    }

                    child = parent.Find(fallbackName);
                    if (child == null && parent != transform)
                    {
                        child = transform.Find(fallbackName);
                    }
                }
            }

            if (child == null)
            {
                child = new GameObject(childName).transform;
            }

            child.name = childName;
            child.SetParent(parent, false);
            child.localPosition = Vector3.zero;
            child.localRotation = Quaternion.identity;
            child.localScale = Vector3.one;
            child.gameObject.layer = gameObject.layer;
            return child;
        }

        private static T EnsureComponent<T>(GameObject target) where T : Component
        {
            var component = target.GetComponent<T>();
            return component != null ? component : target.AddComponent<T>();
        }

        private static bool UsesLightTrail(MissileVisualTrailMode mode)
        {
            return mode == MissileVisualTrailMode.Light
                || mode == MissileVisualTrailMode.Smoke
                || mode == MissileVisualTrailMode.LightAndSmoke
                || mode == MissileVisualTrailMode.Contrail;
        }

        private static bool UsesFlame(MissileVisualTrailMode mode)
        {
            return mode != MissileVisualTrailMode.None;
        }

        private static bool UsesSmokeTrail(MissileVisualTrailMode mode)
        {
            return mode == MissileVisualTrailMode.Smoke
                || mode == MissileVisualTrailMode.LightAndSmoke
                || mode == MissileVisualTrailMode.Contrail;
        }

        private static bool UsesSparks(MissileVisualTrailMode mode, bool canBeDestroyed, string configTailType)
        {
            if (mode == MissileVisualTrailMode.None)
            {
                return false;
            }

            if (!string.IsNullOrEmpty(configTailType))
            {
                var normalized = configTailType.Trim().ToLowerInvariant();
                if (normalized.Contains("spark") || normalized.Contains("heavy") || normalized.Contains("boss"))
                {
                    return true;
                }
            }

            return canBeDestroyed && mode == MissileVisualTrailMode.LightAndSmoke;
        }

        private static string TrailModeToTailType(MissileVisualTrailMode mode)
        {
            switch (mode)
            {
                case MissileVisualTrailMode.None:
                    return "none";
                case MissileVisualTrailMode.Smoke:
                    return "smoke";
                case MissileVisualTrailMode.LightAndSmoke:
                    return "fire_smoke";
                case MissileVisualTrailMode.Contrail:
                    return "contrail";
                default:
                    return "light";
            }
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = Mathf.Clamp01(alpha);
            return color;
        }

        private static void SetLayerIfValid(Transform target, int layer)
        {
            if (target != null)
            {
                target.gameObject.layer = layer;
            }
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
                ps.Clear(true);
                ps.Play(true);
                return;
            }

            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
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

        private static Material GetDefaultSpriteMaterial()
        {
            if (defaultSpriteMaterial == null)
            {
                defaultSpriteMaterial = SpriteMaterialUtility.CreateSpriteMaterial("Missile Trail Sprite Material");
            }

            return defaultSpriteMaterial;
        }

        private static Material GetAdditiveParticleMaterial()
        {
            if (additiveParticleMaterial == null)
            {
                var shader = Shader.Find("Particles/Additive")
                    ?? Shader.Find("Universal Render Pipeline/Particles/Unlit")
                    ?? Shader.Find("Sprites/Default");
                if (shader != null)
                {
                    additiveParticleMaterial = new Material(shader);
                    if (additiveParticleMaterial.HasProperty("_Color"))
                    {
                        additiveParticleMaterial.SetColor("_Color", Color.white);
                    }
                }
            }

            return additiveParticleMaterial != null ? additiveParticleMaterial : GetDefaultSpriteMaterial();
        }

        private static Material GetSmokeMaterial()
        {
            if (smokeMaterial == null)
            {
                smokeMaterial = SpriteMaterialUtility.CreateSpriteMaterial("Missile Smoke Material", GetSmokeTexture());

                if (smokeMaterial != null)
                {
                    if (smokeMaterial.HasProperty("_Color"))
                    {
                        smokeMaterial.SetColor("_Color", Color.white);
                    }
                }
            }

            return smokeMaterial;
        }

        private static Texture2D GetSmokeTexture()
        {
            if (smokeTexture == null)
            {
                const int size = 96;
                const float radius = size * 0.5f;
                smokeTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp
                };

                for (var y = 0; y < size; y++)
                {
                    for (var x = 0; x < size; x++)
                    {
                        var dx = x + 0.5f - radius;
                        var dy = y + 0.5f - radius;
                        var distance = Mathf.Sqrt(dx * dx + dy * dy) / radius;
                        var alpha = Mathf.Pow(Mathf.Clamp01(1f - distance), 2.1f);
                        smokeTexture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                    }
                }

                smokeTexture.Apply();
            }

            return smokeTexture;
        }

        private static Sprite GetGlowSprite()
        {
            if (glowSprite == null)
            {
                var texture = new Texture2D(GlowTextureSize, GlowTextureSize, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp
                };
                var radius = GlowTextureSize * 0.5f;

                for (var y = 0; y < GlowTextureSize; y++)
                {
                    for (var x = 0; x < GlowTextureSize; x++)
                    {
                        var dx = x + 0.5f - radius;
                        var dy = y + 0.5f - radius;
                        var distance = Mathf.Sqrt(dx * dx + dy * dy) / radius;
                        var alpha = Mathf.Pow(Mathf.Clamp01(1f - distance), 1.8f);
                        texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                    }
                }

                texture.Apply();
                glowSprite = Sprite.Create(
                    texture,
                    new Rect(0, 0, GlowTextureSize, GlowTextureSize),
                    new Vector2(0.5f, 0.5f),
                    SpritePixelsPerUnit);
            }

            return glowSprite;
        }

        [Serializable]
        private class LightTrailSettings
        {
            [Min(0f)] public float duration = 0.32f;
            [Min(0f)] public float startWidthRadiusScale = 0.82f;
            [Min(0f)] public float minStartWidth = 0.04f;
            [Min(0f)] public float endWidth = 0.01f;
            [Range(0f, 1f)] public float startAlpha = 0.72f;
            [Range(0f, 1f)] public float endAlpha = 0f;
            [Min(0f)] public float offsetRadiusScale = 0.85f;
            [Min(0f)] public float minVertexDistanceRadiusScale = 0.45f;
            [Min(0f)] public int capVertices = 4;
            public int sortingOrder = 21;
            public Material material;
        }

        [Serializable]
        private class FlameTrailSettings
        {
            [Min(0.05f)] public float duration = 1f;
            [Min(0f)] public float lifetimeMin = 0.08f;
            [Min(0f)] public float lifetimeMax = 0.16f;
            [Min(0f)] public float startSpeedMin = 0.45f;
            [Min(0f)] public float startSpeedMax = 1.65f;
            [Min(0f)] public float sizeRadiusScaleMin = 0.75f;
            [Min(0f)] public float sizeRadiusScaleMax = 1.65f;
            [Min(0f)] public float emissionRate = 80f;
            [Range(0f, 45f)] public float coneAngle = 18f;
            [Min(0f)] public float shapeRadiusScale = 0.24f;
            [Min(0f)] public float sideSpread = 0.03f;
            public float backwardSpeedMin = -1.35f;
            public float backwardSpeedMax = -0.45f;
            [Range(0f, 1f)] public float startAlpha = 0.95f;
            [Range(0f, 1f)] public float midAlpha = 0.72f;
            [Min(1f)] public int maxParticles = 64;
            public int sortingOrder = 23;
            public Material material;
        }

        [Serializable]
        private class SmokeTrailSettings
        {
            [Min(0.05f)] public float duration = 1f;
            [Min(0f)] public float lifetimeMin = 0.58f;
            [Min(0f)] public float lifetimeMax = 0.92f;
            [Min(0f)] public float startSpeedMin = 0.02f;
            [Min(0f)] public float startSpeedMax = 0.16f;
            [Min(0f)] public float sizeRadiusScaleMin = 1.2f;
            [Min(0f)] public float sizeRadiusScaleMax = 2.35f;
            [Min(0f)] public float emissionRate = 22f;
            [Min(0f)] public float shapeRadiusScale = 0.45f;
            [Min(0f)] public float offsetRadiusScale = 0.8f;
            [Min(0f)] public float sideDrift = 0.08f;
            public float backwardSpeedMin = -0.28f;
            public float backwardSpeedMax = -0.08f;
            [Range(0f, 1f)] public float startAlpha = 0.68f;
            [Range(0f, 1f)] public float midAlpha = 0.36f;
            [Range(0f, 1f)] public float colorToWhite = 0.45f;
            [Range(0f, 1f)] public float midSizeMultiplier = 0.62f;
            [Range(0f, 1f)] public float endSizeMultiplier = 0.08f;
            [Min(0f)] public float noiseStrengthRadiusScale = 0.28f;
            [Min(0f)] public float noiseFrequency = 1.6f;
            public float noiseScrollSpeed = 0.25f;
            [Min(1f)] public int maxParticles = 72;
            public int sortingOrder = 20;
            public Color shadowColor = new Color(0.72f, 0.76f, 0.8f, 1f);
            public Material material;
        }

        [Serializable]
        private class SparkTrailSettings
        {
            [Min(0.05f)] public float duration = 1f;
            [Min(0f)] public float lifetimeMin = 0.14f;
            [Min(0f)] public float lifetimeMax = 0.34f;
            [Min(0f)] public float startSpeedMin = 1f;
            [Min(0f)] public float startSpeedMax = 2.6f;
            [Min(0f)] public float sizeRadiusScaleMin = 0.16f;
            [Min(0f)] public float sizeRadiusScaleMax = 0.34f;
            [Min(0f)] public float emissionRate = 10f;
            [Range(0f, 80f)] public float coneAngle = 34f;
            [Min(0f)] public float shapeRadiusScale = 0.16f;
            [Min(0f)] public float sideSpread = 0.22f;
            public float backwardSpeedMin = -1.8f;
            public float backwardSpeedMax = -0.45f;
            [Range(0f, 1f)] public float startAlpha = 0.9f;
            [Range(0f, 1f)] public float midAlpha = 0.46f;
            [Min(1f)] public int maxParticles = 48;
            public int sortingOrder = 24;
            public Material material;
        }

        [Serializable]
        private class TailGlowSettings
        {
            public bool enabled = true;
            [Min(0f)] public float offsetRadiusScale = 0.88f;
            [Min(0f)] public float diameterRadiusScale = 2.05f;
            [Range(0f, 1f)] public float alpha = 0.72f;
            [Range(0f, 1f)] public float pulseAmount = 0.18f;
            [Min(0f)] public float pulseSpeed = 8f;
            public int sortingOrder = 21;
            public Material material;
        }
    }
}
