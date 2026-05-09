using System;
using UnityEngine;

namespace LeiTing.Missiles
{
    public enum MissileVisualTrailMode
    {
        UseConfig = 0,
        None = 1,
        Light = 2,
        Smoke = 3,
        LightAndSmoke = 4
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
        private const string FlameParticleName = "Flame_Particle";
        private const string SmokeParticleName = "Smoke_Particle";
        private const string SparkParticleName = "Spark_Particle";
        private const string TrailRendererName = "TrailRenderer";
        private const string LegacyLightTrailName = "LightTrail";
        private const string LegacySmokeTrailName = "SmokeTrail";

        private static Material defaultSpriteMaterial;
        private static Material smokeMaterial;
        private static Texture2D smokeTexture;
        private static Sprite glowSprite;

        [Header("Source")]
        [SerializeField] private MissileVisualTrailMode trailMode = MissileVisualTrailMode.UseConfig;
        [SerializeField] private bool useConfigTailColor = true;
        [SerializeField] private Color customTailColor = new Color(1f, 0.72f, 0.22f, 1f);

        [Header("References")]
        [SerializeField] private Transform flameParticleRoot;
        [SerializeField] private ParticleSystem flameParticle;
        [SerializeField] private ParticleSystemRenderer flameParticleRenderer;
        [SerializeField] private Transform lightTrailRoot;
        [SerializeField] private TrailRenderer lightTrailRenderer;
        [SerializeField] private Transform smokeTrailRoot;
        [SerializeField] private ParticleSystem smokeTrail;
        [SerializeField] private ParticleSystemRenderer smokeTrailRenderer;
        [SerializeField] private Transform sparkParticleRoot;
        [SerializeField] private ParticleSystem sparkParticle;
        [SerializeField] private ParticleSystemRenderer sparkParticleRenderer;
        [SerializeField] private Transform tailGlowRoot;
        [SerializeField] private SpriteRenderer tailGlowRenderer;

        [Header("Flame_Particle")]
        [SerializeField] private FlameParticleSettings flame = new FlameParticleSettings();

        [Header("Smoke_Particle")]
        [SerializeField] private SmokeTrailSettings smoke = new SmokeTrailSettings();

        [Header("Spark_Particle")]
        [SerializeField] private SparkParticleSettings spark = new SparkParticleSettings();

        [Header("TrailRenderer")]
        [SerializeField] private LightTrailSettings lightTrail = new LightTrailSettings();

        [Header("Tail Glow")]
        [SerializeField] private TailGlowSettings tailGlow = new TailGlowSettings();

        private Color activeTailColor = Color.white;
        private float activeRadius = 0.16f;
        private bool activeTailGlowEnabled = true;

#if UNITY_EDITOR
        private bool validateQueued;
#endif

        public MissileVisualTrailMode TrailMode => trailMode;
        public bool UsesConfigTailColor => useConfigTailColor;
        public Color CustomTailColor => customTailColor;
        public Transform FlameParticleRoot => flameParticleRoot;
        public ParticleSystem FlameParticle => flameParticle;
        public ParticleSystemRenderer FlameParticleRenderer => flameParticleRenderer;
        public Transform LightTrailRoot => lightTrailRoot;
        public TrailRenderer LightTrail => lightTrailRenderer;
        public Transform SmokeTrailRoot => smokeTrailRoot;
        public ParticleSystem SmokeTrail => smokeTrail;
        public ParticleSystemRenderer SmokeTrailRenderer => smokeTrailRenderer;
        public Transform SparkParticleRoot => sparkParticleRoot;
        public ParticleSystem SparkParticle => sparkParticle;
        public ParticleSystemRenderer SparkParticleRenderer => sparkParticleRenderer;
        public Transform TailGlowRoot => tailGlowRoot;
        public SpriteRenderer TailGlow => tailGlowRenderer;

        public void EnsureEffectObjects()
        {
            EnsureSettings();

            flameParticleRoot = EnsureChild(flameParticleRoot, FlameParticleName);
            flameParticle = EnsureComponent<ParticleSystem>(flameParticleRoot.gameObject);
            flameParticleRenderer = flameParticleRoot.GetComponent<ParticleSystemRenderer>();

            smokeTrailRoot = EnsureChild(smokeTrailRoot, SmokeParticleName, LegacySmokeTrailName);
            smokeTrail = EnsureComponent<ParticleSystem>(smokeTrailRoot.gameObject);
            smokeTrailRenderer = smokeTrailRoot.GetComponent<ParticleSystemRenderer>();

            sparkParticleRoot = EnsureChild(sparkParticleRoot, SparkParticleName);
            sparkParticle = EnsureComponent<ParticleSystem>(sparkParticleRoot.gameObject);
            sparkParticleRenderer = sparkParticleRoot.GetComponent<ParticleSystemRenderer>();

            lightTrailRoot = EnsureChild(lightTrailRoot, TrailRendererName, LegacyLightTrailName);
            lightTrailRenderer = EnsureComponent<TrailRenderer>(lightTrailRoot.gameObject);

            tailGlowRoot = EnsureChild(tailGlowRoot, "TailGlow");
            tailGlowRenderer = EnsureComponent<SpriteRenderer>(tailGlowRoot.gameObject);
            tailGlowRenderer.sprite = GetGlowSprite();
        }

        public void Apply(MissileVisualEffectContext context)
        {
            EnsureEffectObjects();

            activeRadius = Mathf.Max(0.04f, context.Radius);
            activeTailColor = ResolveTailColor(context);

            var resolvedMode = ResolveTrailMode(context.TailType);
            activeTailGlowEnabled = resolvedMode != MissileVisualTrailMode.None;
            ConfigureFlameParticle(UsesFlameParticle(resolvedMode), activeTailColor, activeRadius);
            ConfigureSmokeTrail(UsesSmokeTrail(resolvedMode), activeTailColor, activeRadius, context.CanBeDestroyed);
            ConfigureSparkParticle(UsesSparkParticle(resolvedMode), activeTailColor, activeRadius);
            ConfigureLightTrail(UsesLightTrail(resolvedMode), activeTailColor, activeRadius);
            ConfigureTailGlow(activeTailGlowEnabled, activeTailColor, activeRadius, context.Time);
        }

        public void Play()
        {
            if (lightTrailRenderer != null)
            {
                lightTrailRenderer.Clear();
            }

            PlayParticleIfEmitting(flameParticle, flameParticleRenderer);
            PlayParticleIfEmitting(smokeTrail, smokeTrailRenderer);
            PlayParticleIfEmitting(sparkParticle, sparkParticleRenderer);
        }

        public void StopAndClear()
        {
            if (lightTrailRenderer != null)
            {
                lightTrailRenderer.Clear();
            }

            StopParticle(flameParticle);
            StopParticle(smokeTrail);
            StopParticle(sparkParticle);
        }

        public void UpdateDynamic(float time)
        {
            ConfigureTailGlow(activeTailGlowEnabled, activeTailColor, activeRadius, time);
        }

        public void SetLayer(int layer)
        {
            gameObject.layer = layer;
            SetLayerIfValid(flameParticleRoot, layer);
            SetLayerIfValid(lightTrailRoot, layer);
            SetLayerIfValid(smokeTrailRoot, layer);
            SetLayerIfValid(sparkParticleRoot, layer);
            SetLayerIfValid(tailGlowRoot, layer);
        }

        public void ResetToDefaults(MissileVisualTrailMode defaultTrailMode, Color defaultTailColor, float missileRadius)
        {
            EnsureSettings();

            trailMode = defaultTrailMode;
            useConfigTailColor = false;
            customTailColor = defaultTailColor;

            var radius = Mathf.Max(0.04f, missileRadius);
            flame.emissionRate = defaultTrailMode == MissileVisualTrailMode.LightAndSmoke ? 46f : 54f;
            flame.sizeRadiusScaleMax = defaultTrailMode == MissileVisualTrailMode.LightAndSmoke ? 1.7f : 1.95f;
            flame.offsetRadiusScale = 0.86f;

            lightTrail.duration = Mathf.Lerp(0.22f, 0.46f, Mathf.InverseLerp(0.12f, 0.32f, radius));
            lightTrail.startWidthRadiusScale = defaultTrailMode == MissileVisualTrailMode.LightAndSmoke ? 0.62f : 0.82f;
            lightTrail.endWidth = 0.01f;
            lightTrail.offsetRadiusScale = 0.9f;

            smoke.emissionRate = defaultTrailMode == MissileVisualTrailMode.LightAndSmoke ? 16f : 24f;
            smoke.sizeRadiusScaleMax = defaultTrailMode == MissileVisualTrailMode.LightAndSmoke ? 1.95f : 2.45f;
            smoke.offsetRadiusScale = 0.78f;

            spark.emissionRate = defaultTrailMode == MissileVisualTrailMode.LightAndSmoke ? 7f : 10f;
            spark.offsetRadiusScale = 0.9f;

            tailGlow.enabled = true;
            tailGlow.diameterRadiusScale = defaultTrailMode == MissileVisualTrailMode.Smoke ? 1.75f : 2.05f;
            tailGlow.alpha = defaultTrailMode == MissileVisualTrailMode.Smoke ? 0.52f : 0.76f;

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
                TailType = ResolveEditorTailType(),
                Time = 0f
            });
            StopAndClear();
        }

        private void OnValidate()
        {
#if UNITY_EDITOR
            if (Application.isPlaying)
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
            if (this == null || Application.isPlaying)
            {
                return;
            }

            EnsureEffectObjects();
            Apply(new MissileVisualEffectContext
            {
                Radius = ResolveRadiusFromCollider(),
                TailColor = customTailColor,
                TailType = ResolveEditorTailType(),
                Time = 0f
            });
            StopAndClear();
        }
#endif

        private void ConfigureFlameParticle(bool enabled, Color tailColor, float radius)
        {
            if (flameParticle == null)
            {
                return;
            }

            flameParticleRoot.localPosition = Vector3.down * Mathf.Max(0.01f, radius * flame.offsetRadiusScale);
            flameParticleRoot.localRotation = Quaternion.identity;
            flameParticleRoot.localScale = Vector3.one;

            var main = flameParticle.main;
            main.loop = true;
            main.playOnAwake = false;
            main.prewarm = false;
            main.duration = Mathf.Max(0.05f, flame.duration);
            main.startDelay = 0f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(flame.lifetimeMin, flame.lifetimeMax);
            main.startSpeed = new ParticleSystem.MinMaxCurve(flame.startSpeedMin, flame.startSpeedMax);
            main.startSize = new ParticleSystem.MinMaxCurve(
                Mathf.Max(0.005f, radius * flame.sizeRadiusScaleMin),
                Mathf.Max(0.005f, radius * flame.sizeRadiusScaleMax));
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                WithAlpha(Color.Lerp(tailColor, Color.white, flame.coreToWhite), flame.startAlpha),
                WithAlpha(Color.Lerp(tailColor, flame.edgeColor, 0.45f), flame.startAlpha * 0.88f));
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.maxParticles = Mathf.Max(1, flame.maxParticles);

            var emission = flameParticle.emission;
            emission.enabled = enabled;
            emission.rateOverTime = enabled
                ? new ParticleSystem.MinMaxCurve(Mathf.Max(0f, flame.emissionRate))
                : new ParticleSystem.MinMaxCurve(0f);

            var shape = flameParticle.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = Mathf.Max(0.004f, radius * flame.shapeRadiusScale);
            shape.radiusThickness = 1f;

            var velocity = flameParticle.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.x = new ParticleSystem.MinMaxCurve(-flame.sideDrift, flame.sideDrift);
            velocity.y = new ParticleSystem.MinMaxCurve(flame.backwardSpeedMin, flame.backwardSpeedMax);
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

            var colorOverLifetime = flameParticle.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            var coreColor = Color.Lerp(tailColor, Color.white, flame.coreToWhite);
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(coreColor, 0f),
                    new GradientColorKey(Color.Lerp(tailColor, flame.edgeColor, 0.55f), 0.45f),
                    new GradientColorKey(flame.edgeColor, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(flame.startAlpha, 0f),
                    new GradientAlphaKey(flame.midAlpha, 0.45f),
                    new GradientAlphaKey(0f, 1f)
                });
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);

            var sizeOverLifetime = flameParticle.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, flame.startSizeMultiplier),
                new Keyframe(0.42f, flame.midSizeMultiplier),
                new Keyframe(1f, flame.endSizeMultiplier)));

            if (flameParticleRenderer != null)
            {
                flameParticleRenderer.enabled = enabled;
                flameParticleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
                flameParticleRenderer.material = flame.material != null ? flame.material : GetSmokeMaterial();
                flameParticleRenderer.sortingOrder = flame.sortingOrder;
            }

            if (!enabled)
            {
                flameParticle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        private void ConfigureLightTrail(bool enabled, Color tailColor, float radius)
        {
            if (lightTrailRenderer == null)
            {
                return;
            }

            lightTrailRoot.localPosition = Vector3.down * Mathf.Max(0.02f, radius * lightTrail.offsetRadiusScale);
            lightTrailRoot.localRotation = Quaternion.identity;
            lightTrailRoot.localScale = Vector3.one;

            lightTrailRenderer.enabled = enabled;
            lightTrailRenderer.emitting = enabled;
            lightTrailRenderer.time = Mathf.Max(0f, lightTrail.duration);
            lightTrailRenderer.startWidth = Mathf.Max(lightTrail.minStartWidth, radius * lightTrail.startWidthRadiusScale);
            lightTrailRenderer.endWidth = Mathf.Max(0f, lightTrail.endWidth);
            lightTrailRenderer.startColor = WithAlpha(tailColor, lightTrail.startAlpha);
            lightTrailRenderer.endColor = WithAlpha(tailColor, lightTrail.endAlpha);
            lightTrailRenderer.material = lightTrail.material != null ? lightTrail.material : GetDefaultSpriteMaterial();
            lightTrailRenderer.sortingOrder = lightTrail.sortingOrder;
            lightTrailRenderer.numCapVertices = Mathf.Max(0, lightTrail.capVertices);
            lightTrailRenderer.alignment = LineAlignment.View;
            lightTrailRenderer.textureMode = LineTextureMode.Stretch;

            if (!enabled)
            {
                lightTrailRenderer.Clear();
            }
        }

        private void ConfigureSmokeTrail(bool enabled, Color tailColor, float radius, bool canBeDestroyed)
        {
            if (smokeTrail == null)
            {
                return;
            }

            smokeTrailRoot.localPosition = Vector3.down * Mathf.Max(0.02f, radius * smoke.offsetRadiusScale);
            smokeTrailRoot.localRotation = Quaternion.identity;
            smokeTrailRoot.localScale = Vector3.one;

            var main = smokeTrail.main;
            main.loop = true;
            main.playOnAwake = false;
            main.prewarm = false;
            main.duration = Mathf.Max(0.05f, smoke.duration);
            main.startDelay = 0f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(smoke.lifetimeMin, smoke.lifetimeMax);
            main.startSpeed = new ParticleSystem.MinMaxCurve(smoke.startSpeedMin, smoke.startSpeedMax);
            main.startSize = new ParticleSystem.MinMaxCurve(
                Mathf.Max(0.01f, radius * smoke.sizeRadiusScaleMin),
                Mathf.Max(0.01f, radius * smoke.sizeRadiusScaleMax));
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                WithAlpha(Color.Lerp(tailColor, Color.white, smoke.colorToWhite), smoke.startAlpha),
                WithAlpha(Color.Lerp(tailColor, smoke.shadowColor, 0.45f), smoke.startAlpha * 0.78f));
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.maxParticles = Mathf.Max(1, canBeDestroyed ? Mathf.RoundToInt(smoke.maxParticles * 1.3f) : smoke.maxParticles);

            var emission = smokeTrail.emission;
            emission.enabled = enabled;
            emission.rateOverTime = enabled
                ? new ParticleSystem.MinMaxCurve(Mathf.Max(0f, smoke.emissionRate))
                : new ParticleSystem.MinMaxCurve(0f);

            var shape = smokeTrail.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = Mathf.Max(0.005f, radius * smoke.shapeRadiusScale);
            shape.radiusThickness = 1f;

            var velocity = smokeTrail.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.x = new ParticleSystem.MinMaxCurve(-smoke.sideDrift, smoke.sideDrift);
            velocity.y = new ParticleSystem.MinMaxCurve(smoke.backwardSpeedMin, smoke.backwardSpeedMax);
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

            var colorOverLifetime = smokeTrail.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            var startColor = Color.Lerp(tailColor, Color.white, smoke.colorToWhite);
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(startColor, 0f),
                    new GradientColorKey(Color.Lerp(startColor, smoke.shadowColor, 0.35f), 0.62f),
                    new GradientColorKey(smoke.shadowColor, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(smoke.startAlpha, 0f),
                    new GradientAlphaKey(smoke.midAlpha, 0.48f),
                    new GradientAlphaKey(0f, 1f)
                });
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);

            var sizeOverLifetime = smokeTrail.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 1f),
                new Keyframe(0.6f, smoke.midSizeMultiplier),
                new Keyframe(1f, smoke.endSizeMultiplier)));

            var noise = smokeTrail.noise;
            noise.enabled = smoke.noiseStrengthRadiusScale > 0f;
            noise.strength = new ParticleSystem.MinMaxCurve(radius * smoke.noiseStrengthRadiusScale);
            noise.frequency = smoke.noiseFrequency;
            noise.scrollSpeed = smoke.noiseScrollSpeed;

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
        }

        private void ConfigureSparkParticle(bool enabled, Color tailColor, float radius)
        {
            if (sparkParticle == null)
            {
                return;
            }

            sparkParticleRoot.localPosition = Vector3.down * Mathf.Max(0.01f, radius * spark.offsetRadiusScale);
            sparkParticleRoot.localRotation = Quaternion.identity;
            sparkParticleRoot.localScale = Vector3.one;

            var main = sparkParticle.main;
            main.loop = true;
            main.playOnAwake = false;
            main.prewarm = false;
            main.duration = Mathf.Max(0.05f, spark.duration);
            main.startDelay = 0f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(spark.lifetimeMin, spark.lifetimeMax);
            main.startSpeed = new ParticleSystem.MinMaxCurve(spark.startSpeedMin, spark.startSpeedMax);
            main.startSize = new ParticleSystem.MinMaxCurve(
                Mathf.Max(0.002f, radius * spark.sizeRadiusScaleMin),
                Mathf.Max(0.002f, radius * spark.sizeRadiusScaleMax));
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                WithAlpha(Color.Lerp(tailColor, Color.white, spark.colorToWhite), spark.startAlpha),
                WithAlpha(spark.edgeColor, spark.startAlpha * 0.8f));
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.maxParticles = Mathf.Max(1, spark.maxParticles);

            var emission = sparkParticle.emission;
            emission.enabled = enabled;
            emission.rateOverTime = enabled
                ? new ParticleSystem.MinMaxCurve(Mathf.Max(0f, spark.emissionRate))
                : new ParticleSystem.MinMaxCurve(0f);

            var shape = sparkParticle.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = Mathf.Max(0.003f, radius * spark.shapeRadiusScale);
            shape.radiusThickness = 1f;

            var velocity = sparkParticle.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.x = new ParticleSystem.MinMaxCurve(-spark.sideVelocity, spark.sideVelocity);
            velocity.y = new ParticleSystem.MinMaxCurve(spark.backwardSpeedMin, spark.backwardSpeedMax);
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

            var colorOverLifetime = sparkParticle.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            var startColor = Color.Lerp(tailColor, Color.white, spark.colorToWhite);
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(startColor, 0f),
                    new GradientColorKey(Color.Lerp(startColor, spark.edgeColor, 0.45f), 0.55f),
                    new GradientColorKey(spark.edgeColor, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(spark.startAlpha, 0f),
                    new GradientAlphaKey(spark.midAlpha, 0.45f),
                    new GradientAlphaKey(0f, 1f)
                });
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);

            var sizeOverLifetime = sparkParticle.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 1f),
                new Keyframe(1f, spark.endSizeMultiplier)));

            if (sparkParticleRenderer != null)
            {
                sparkParticleRenderer.enabled = enabled;
                sparkParticleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
                sparkParticleRenderer.material = spark.material != null ? spark.material : GetSmokeMaterial();
                sparkParticleRenderer.sortingOrder = spark.sortingOrder;
            }

            if (!enabled)
            {
                sparkParticle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        private void ConfigureTailGlow(bool modeEnabled, Color tailColor, float radius, float time)
        {
            if (tailGlowRenderer == null)
            {
                return;
            }

            tailGlowRenderer.enabled = modeEnabled && tailGlow.enabled;
            if (!tailGlowRenderer.enabled)
            {
                return;
            }

            tailGlowRoot.localPosition = Vector3.down * Mathf.Max(0.01f, radius * tailGlow.offsetRadiusScale);
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
                case "fire":
                case "flame":
                case "spark":
                case "fire_smoke":
                case "all":
                case "smoke_light":
                case "light_smoke":
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

        private string ResolveEditorTailType()
        {
            var editorMode = trailMode == MissileVisualTrailMode.UseConfig ? MissileVisualTrailMode.LightAndSmoke : trailMode;
            return TrailModeToTailType(editorMode);
        }

        private Transform EnsureChild(Transform cached, string childName, params string[] legacyChildNames)
        {
            if (cached != null)
            {
                cached.SetParent(transform, false);
                cached.name = childName;
                cached.gameObject.layer = gameObject.layer;
                return cached;
            }

            var child = transform.Find(childName);
            if (child == null && legacyChildNames != null)
            {
                for (var index = 0; index < legacyChildNames.Length; index++)
                {
                    child = transform.Find(legacyChildNames[index]);
                    if (child != null)
                    {
                        child.name = childName;
                        break;
                    }
                }
            }

            if (child == null)
            {
                child = new GameObject(childName).transform;
                child.SetParent(transform, false);
            }

            child.localPosition = Vector3.zero;
            child.localRotation = Quaternion.identity;
            child.localScale = Vector3.one;
            child.gameObject.layer = gameObject.layer;
            return child;
        }

        private void EnsureSettings()
        {
            if (flame == null)
            {
                flame = new FlameParticleSettings();
            }

            if (smoke == null)
            {
                smoke = new SmokeTrailSettings();
            }

            if (spark == null)
            {
                spark = new SparkParticleSettings();
            }

            if (lightTrail == null)
            {
                lightTrail = new LightTrailSettings();
            }

            if (tailGlow == null)
            {
                tailGlow = new TailGlowSettings();
            }
        }

        private static T EnsureComponent<T>(GameObject target) where T : Component
        {
            var component = target.GetComponent<T>();
            return component != null ? component : target.AddComponent<T>();
        }

        private static void PlayParticleIfEmitting(ParticleSystem particle, ParticleSystemRenderer renderer)
        {
            if (particle == null)
            {
                return;
            }

            var emission = particle.emission;
            if (renderer != null && renderer.enabled && emission.enabled)
            {
                particle.Play(true);
                return;
            }

            particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private static void StopParticle(ParticleSystem particle)
        {
            if (particle != null)
            {
                particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        private static bool UsesFlameParticle(MissileVisualTrailMode mode)
        {
            return mode == MissileVisualTrailMode.Light || mode == MissileVisualTrailMode.LightAndSmoke;
        }

        private static bool UsesLightTrail(MissileVisualTrailMode mode)
        {
            return mode == MissileVisualTrailMode.Light || mode == MissileVisualTrailMode.LightAndSmoke;
        }

        private static bool UsesSmokeTrail(MissileVisualTrailMode mode)
        {
            return mode == MissileVisualTrailMode.Smoke || mode == MissileVisualTrailMode.LightAndSmoke;
        }

        private static bool UsesSparkParticle(MissileVisualTrailMode mode)
        {
            return mode == MissileVisualTrailMode.Light || mode == MissileVisualTrailMode.LightAndSmoke;
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

        private static Material GetDefaultSpriteMaterial()
        {
            if (defaultSpriteMaterial == null)
            {
                var shader = Shader.Find("Sprites/Default");
                if (shader != null)
                {
                    defaultSpriteMaterial = new Material(shader);
                }
            }

            return defaultSpriteMaterial;
        }

        private static Material GetSmokeMaterial()
        {
            if (smokeMaterial == null)
            {
                var shader = Shader.Find("Sprites/Default");
                if (shader != null)
                {
                    smokeMaterial = new Material(shader)
                    {
                        mainTexture = GetSmokeTexture()
                    };

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
        private class FlameParticleSettings
        {
            [Min(0.05f)] public float duration = 0.45f;
            [Min(0f)] public float lifetimeMin = 0.08f;
            [Min(0f)] public float lifetimeMax = 0.22f;
            [Min(0f)] public float startSpeedMin = 0.02f;
            [Min(0f)] public float startSpeedMax = 0.08f;
            [Min(0f)] public float sizeRadiusScaleMin = 0.65f;
            [Min(0f)] public float sizeRadiusScaleMax = 1.85f;
            [Min(0f)] public float emissionRate = 54f;
            [Min(0f)] public float shapeRadiusScale = 0.32f;
            [Min(0f)] public float offsetRadiusScale = 0.86f;
            [Min(0f)] public float sideDrift = 0.05f;
            public float backwardSpeedMin = -0.38f;
            public float backwardSpeedMax = -0.12f;
            [Range(0f, 1f)] public float startAlpha = 0.9f;
            [Range(0f, 1f)] public float midAlpha = 0.62f;
            [Range(0f, 1f)] public float coreToWhite = 0.55f;
            [Range(0f, 2f)] public float startSizeMultiplier = 1f;
            [Range(0f, 2f)] public float midSizeMultiplier = 0.72f;
            [Range(0f, 2f)] public float endSizeMultiplier = 0.06f;
            [Min(1f)] public int maxParticles = 96;
            public int sortingOrder = 23;
            public Color edgeColor = new Color(1f, 0.22f, 0.06f, 1f);
            public Material material;
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
            [Min(0f)] public int capVertices = 4;
            public int sortingOrder = 21;
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
        private class SparkParticleSettings
        {
            [Min(0.05f)] public float duration = 0.65f;
            [Min(0f)] public float lifetimeMin = 0.16f;
            [Min(0f)] public float lifetimeMax = 0.42f;
            [Min(0f)] public float startSpeedMin = 0.18f;
            [Min(0f)] public float startSpeedMax = 0.72f;
            [Min(0f)] public float sizeRadiusScaleMin = 0.08f;
            [Min(0f)] public float sizeRadiusScaleMax = 0.22f;
            [Min(0f)] public float emissionRate = 10f;
            [Min(0f)] public float shapeRadiusScale = 0.28f;
            [Min(0f)] public float offsetRadiusScale = 0.9f;
            [Min(0f)] public float sideVelocity = 0.52f;
            public float backwardSpeedMin = -0.68f;
            public float backwardSpeedMax = -0.24f;
            [Range(0f, 1f)] public float startAlpha = 0.95f;
            [Range(0f, 1f)] public float midAlpha = 0.42f;
            [Range(0f, 1f)] public float colorToWhite = 0.7f;
            [Range(0f, 1f)] public float endSizeMultiplier = 0.08f;
            [Min(1f)] public int maxParticles = 36;
            public int sortingOrder = 24;
            public Color edgeColor = new Color(1f, 0.44f, 0.08f, 1f);
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
