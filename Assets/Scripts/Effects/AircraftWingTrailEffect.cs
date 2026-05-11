using System;
using LeiTing.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace LeiTing.Effects
{
    [DisallowMultipleComponent]
    public class AircraftWingTrailEffect : MonoBehaviour
    {
        private const string DefaultRootName = "WingTrails";

        [SerializeField] private bool autoCreateMounts = true;
        [SerializeField] private string trailRootName = DefaultRootName;
        [SerializeField] private WingTrailMount[] trails = CreateDefaultTrails();

        [Header("TrailRenderer")]
        [SerializeField] private bool trailEnabled = true;
        [SerializeField] private Color trailColor = new Color(0.56f, 0.9f, 1f, 1f);
        [SerializeField, Min(0.01f)] private float duration = 0.38f;
        [SerializeField, Min(0f)] private float startWidth = 0.055f;
        [SerializeField, Min(0f)] private float endWidth = 0.005f;
        [SerializeField, Range(0f, 1f)] private float startAlpha = 0.78f;
        [SerializeField, Range(0f, 1f)] private float endAlpha = 0f;
        [SerializeField, Min(0f)] private float minVertexDistance = 0.03f;
        [SerializeField, Min(0)] private int capVertices = 4;
        [SerializeField] private int sortingOrder = 14;
        [SerializeField] private Material material = null;

        private void Awake()
        {
            EnsureConfigured();
        }

        private void OnEnable()
        {
            EnsureConfigured();
            ClearTrails();
            SetEmitting(trailEnabled);
        }

        private void OnDisable()
        {
            SetEmitting(false);
            ClearTrails();
        }

        private void Reset()
        {
            trails = CreateDefaultTrails();
            EnsureConfigured();
        }

        private void OnValidate()
        {
            EnsureDefaultMountSettings();
            ConfigureExistingTrails();
        }

        [ContextMenu("Rebuild Wing Trails")]
        public void Rebuild()
        {
            EnsureConfigured();
            ClearTrails();
        }

        public void SetEmitting(bool emitting)
        {
            if (trails == null)
            {
                return;
            }

            foreach (var trail in trails)
            {
                var trailRenderer = trail?.TrailRenderer;
                if (trailRenderer == null)
                {
                    continue;
                }

                trailRenderer.enabled = trailEnabled;
                trailRenderer.emitting = emitting && trailEnabled && isActiveAndEnabled;
            }
        }

        public void ClearTrails()
        {
            if (trails == null)
            {
                return;
            }

            foreach (var trail in trails)
            {
                trail?.TrailRenderer?.Clear();
            }
        }

        private void EnsureConfigured()
        {
            EnsureDefaultMountSettings();

            if (autoCreateMounts)
            {
                EnsureTrailRenderers();
            }

            ConfigureExistingTrails();
        }

        private void EnsureDefaultMountSettings()
        {
            trailRootName = string.IsNullOrWhiteSpace(trailRootName) ? DefaultRootName : trailRootName;
            trails = trails != null && trails.Length > 0 ? trails : CreateDefaultTrails();
        }

        private void EnsureTrailRenderers()
        {
            var root = EnsureRoot();
            foreach (var trail in trails)
            {
                if (trail == null)
                {
                    continue;
                }

                trail.AssignRenderer(EnsureTrailRenderer(root, trail));
            }
        }

        private Transform EnsureRoot()
        {
            var root = transform.Find(trailRootName);
            if (root == null)
            {
                var rootObject = new GameObject(trailRootName);
                rootObject.transform.SetParent(transform, false);
                root = rootObject.transform;
            }

            root.gameObject.layer = gameObject.layer;
            root.localPosition = Vector3.zero;
            root.localRotation = Quaternion.identity;
            root.localScale = Vector3.one;
            return root;
        }

        private TrailRenderer EnsureTrailRenderer(Transform root, WingTrailMount trail)
        {
            var trailRenderer = trail.TrailRenderer;
            if (trailRenderer != null)
            {
                return trailRenderer;
            }

            var mountName = string.IsNullOrWhiteSpace(trail.MountName) ? "WingTrail" : trail.MountName;
            var mount = root.Find(mountName);
            if (mount == null)
            {
                var mountObject = new GameObject(mountName);
                mountObject.transform.SetParent(root, false);
                mount = mountObject.transform;
            }

            mount.gameObject.layer = gameObject.layer;
            trailRenderer = mount.GetComponent<TrailRenderer>();
            if (trailRenderer == null)
            {
                trailRenderer = mount.gameObject.AddComponent<TrailRenderer>();
            }

            return trailRenderer;
        }

        private void ConfigureExistingTrails()
        {
            if (trails == null)
            {
                return;
            }

            foreach (var trail in trails)
            {
                if (trail == null || trail.TrailRenderer == null)
                {
                    continue;
                }

                ConfigureTrailRenderer(trail);
            }
        }

        private void ConfigureTrailRenderer(WingTrailMount trail)
        {
            var trailRenderer = trail.TrailRenderer;
            var mount = trailRenderer.transform;
            mount.localPosition = trail.LocalPosition;
            mount.localRotation = Quaternion.identity;
            mount.localScale = Vector3.one;
            mount.gameObject.layer = gameObject.layer;

            trailRenderer.enabled = trailEnabled;
            trailRenderer.emitting = trailEnabled && isActiveAndEnabled;
            trailRenderer.time = Mathf.Max(0.01f, duration);
            trailRenderer.startWidth = Mathf.Max(0f, startWidth);
            trailRenderer.endWidth = Mathf.Max(0f, endWidth);
            trailRenderer.startColor = WithAlpha(trailColor, startAlpha);
            trailRenderer.endColor = WithAlpha(trailColor, endAlpha);
            trailRenderer.minVertexDistance = Mathf.Max(0.001f, minVertexDistance);
            trailRenderer.numCapVertices = Mathf.Max(0, capVertices);
            trailRenderer.alignment = LineAlignment.View;
            trailRenderer.textureMode = LineTextureMode.Stretch;
            trailRenderer.shadowCastingMode = ShadowCastingMode.Off;
            trailRenderer.receiveShadows = false;
            trailRenderer.sortingOrder = sortingOrder;
            trailRenderer.sharedMaterial = material != null ? material : SpriteMaterialUtility.DefaultSpriteMaterial;
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = Mathf.Clamp01(alpha);
            return color;
        }

        private static WingTrailMount[] CreateDefaultTrails()
        {
            return new[]
            {
                new WingTrailMount("LeftWingTrail", new Vector3(-0.52f, -0.04f, 0f)),
                new WingTrailMount("RightWingTrail", new Vector3(0.52f, -0.04f, 0f)),
            };
        }

        [Serializable]
        public class WingTrailMount
        {
            [SerializeField] private string mountName;
            [SerializeField] private Vector3 localPosition;
            [SerializeField] private TrailRenderer trailRenderer;

            public WingTrailMount(string mountName, Vector3 localPosition)
            {
                this.mountName = mountName;
                this.localPosition = localPosition;
            }

            public string MountName => mountName;
            public Vector3 LocalPosition => localPosition;
            public TrailRenderer TrailRenderer => trailRenderer;

            public void AssignRenderer(TrailRenderer renderer)
            {
                trailRenderer = renderer;
            }
        }
    }
}
