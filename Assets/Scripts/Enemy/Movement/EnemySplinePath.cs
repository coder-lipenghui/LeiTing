using System;
using System.Collections.Generic;
using LeiTing.Core;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.Splines;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace LeiTing.Enemy.Movement
{
    [DisallowMultipleComponent]
    public class EnemySplinePath : MonoBehaviour
    {
        private const string SplinePathPrefabFolder = "Assets/Prefabs/SplinePaths";
        private const string RuntimeRootName = "RuntimeSplinePaths";

        private static readonly Dictionary<string, EnemySplinePath> loadedPaths =
            new Dictionary<string, EnemySplinePath>(StringComparer.OrdinalIgnoreCase);

        private static Transform runtimeRoot;

        [Serializable]
        private struct SplineAlias
        {
#pragma warning disable 0649
            [FormerlySerializedAs("pathId")]
            public string splineId;
            public SplineContainer splineContainer;
            public int splineIndex;
#pragma warning restore 0649
        }

        [SerializeField] private string pathId;
        [SerializeField] private int splineIndex;
        [SerializeField] private SplineAlias[] aliases = Array.Empty<SplineAlias>();
        [SerializeField] private bool drawDesignFrame = true;
        [SerializeField] private Vector2Int designResolution = new Vector2Int(1080, 1920);
        [SerializeField] private float pixelsPerUnit = 100f;
        [SerializeField] private bool drawPreviewPath = true;
        [SerializeField] private bool drawAllSplines = true;
        [SerializeField] private Vector2 previewOffset = Vector2.zero;
        [SerializeField] private int previewSegments = 48;

        public string PathId => string.IsNullOrWhiteSpace(pathId) ? gameObject.name : pathId.Trim();
        public int SplineIndex => Mathf.Max(0, splineIndex);
        public SplineContainer Container => GetComponent<SplineContainer>();

        [ContextMenu("Auto Rebuild Aliases")]
        public int RebuildSequentialAliases()
        {
            var childContainers = GetChildSplineContainers();
            if (childContainers.Count > 0)
            {
                aliases = new SplineAlias[childContainers.Count];

                for (var i = 0; i < childContainers.Count; i++)
                {
                    aliases[i].splineId = childContainers[i].gameObject.name;
                    aliases[i].splineContainer = childContainers[i];
                    aliases[i].splineIndex = 0;
                }

                return childContainers.Count;
            }

            var container = Container;
            var count = container != null && container.Splines != null ? container.Splines.Count : 0;
            aliases = new SplineAlias[count];

            for (var i = 0; i < count; i++)
            {
                aliases[i].splineId = $"spline{i + 1}";
                aliases[i].splineContainer = container;
                aliases[i].splineIndex = i;
            }

            return count;
        }

        public static bool TryResolve(
            string pathId,
            string splineId,
            int requestedSplineIndex,
            out SplineContainer container,
            out int resolvedSplineIndex)
        {
            container = null;
            resolvedSplineIndex = 0;

            if (string.IsNullOrWhiteSpace(pathId) || string.IsNullOrWhiteSpace(splineId))
            {
                return false;
            }

            var normalizedPathId = pathId.Trim();
            var normalizedSplineId = splineId.Trim();

            if (TryResolveScenePath(normalizedPathId, normalizedSplineId, requestedSplineIndex, out container, out resolvedSplineIndex))
            {
                return true;
            }

            return TryResolveLoadedPath(normalizedPathId, normalizedSplineId, requestedSplineIndex, out container, out resolvedSplineIndex);
        }

        private static bool TryResolveScenePath(
            string normalizedPathId,
            string normalizedSplineId,
            int requestedSplineIndex,
            out SplineContainer container,
            out int resolvedSplineIndex)
        {
            container = null;
            resolvedSplineIndex = 0;

            var paths = UnityEngine.Object.FindObjectsOfType<EnemySplinePath>();
            foreach (var path in paths)
            {
                if (path == null
                    || IsRuntimeLoadedPath(path)
                    || !path.MatchesPath(normalizedPathId)
                    || !path.TryResolveSpline(normalizedSplineId, requestedSplineIndex, out var candidateContainer, out var candidateSplineIndex))
                {
                    continue;
                }

                container = candidateContainer;
                resolvedSplineIndex = candidateSplineIndex;
                return true;
            }

            return false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRuntimeState()
        {
            loadedPaths.Clear();
            runtimeRoot = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void DestroyExistingRuntimeRoot()
        {
            var rootObject = GameObject.Find(RuntimeRootName);
            if (rootObject != null)
            {
                DestroyInstance(rootObject);
            }

            runtimeRoot = null;
        }

        private static bool TryResolveLoadedPath(
            string normalizedPathId,
            string normalizedSplineId,
            int requestedSplineIndex,
            out SplineContainer container,
            out int resolvedSplineIndex)
        {
            container = null;
            resolvedSplineIndex = 0;

            var path = LoadPath(normalizedPathId);
            if (path == null)
            {
                return false;
            }

            return path.TryResolveSpline(normalizedSplineId, requestedSplineIndex, out container, out resolvedSplineIndex);
        }

        private static EnemySplinePath LoadPath(string normalizedPathId)
        {
            if (loadedPaths.TryGetValue(normalizedPathId, out var cachedPath) && cachedPath != null)
            {
                return cachedPath;
            }

            var prefab = LoadPathPrefab(normalizedPathId);
            if (prefab == null)
            {
                return null;
            }

            var instance = UnityEngine.Object.Instantiate(prefab, RuntimeRoot);
            instance.name = prefab.name;

            var path = instance.GetComponent<EnemySplinePath>() ?? instance.GetComponentInChildren<EnemySplinePath>(true);
            if (path == null)
            {
                DestroyInstance(instance);
                return null;
            }

            loadedPaths[normalizedPathId] = path;
            return path;
        }

        private static GameObject LoadPathPrefab(string pathId)
        {
            var assetPath = ResolvePrefabAssetPath(pathId);

#if UNITY_EDITOR
            if (RuntimeRemoteResourceManager.CanUseEditorLocalAssets)
            {
                var editorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                if (editorPrefab != null)
                {
                    return editorPrefab;
                }
            }
#endif

            return RuntimeAssetCatalog.LoadPrefab(assetPath)
                ?? RuntimeAssetCatalog.LoadPrefab(pathId)
                ?? Resources.Load<GameObject>(NormalizeResourcesPath(assetPath))
                ?? Resources.Load<GameObject>($"SplinePaths/{pathId}");
        }

        private static string ResolvePrefabAssetPath(string pathId)
        {
            var normalized = pathId.Trim().Replace("\\", "/");
            if (normalized.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                return HasExtension(normalized) ? normalized : $"{normalized}.prefab";
            }

            return $"{SplinePathPrefabFolder}/{normalized}.prefab";
        }

        private static string NormalizeResourcesPath(string assetPath)
        {
            const string resourcesSegment = "/Resources/";
            var normalized = assetPath.Replace("\\", "/");
            var resourcesIndex = normalized.IndexOf(resourcesSegment, StringComparison.OrdinalIgnoreCase);

            if (resourcesIndex >= 0)
            {
                normalized = normalized.Substring(resourcesIndex + resourcesSegment.Length);
            }

            var extensionIndex = normalized.LastIndexOf(".", StringComparison.Ordinal);
            if (extensionIndex >= 0)
            {
                normalized = normalized.Substring(0, extensionIndex);
            }

            return normalized;
        }

        private static bool HasExtension(string path)
        {
            var extensionIndex = path.LastIndexOf(".", StringComparison.Ordinal);
            var slashIndex = path.LastIndexOf("/", StringComparison.Ordinal);
            return extensionIndex > slashIndex;
        }

        private static bool IsRuntimeLoadedPath(EnemySplinePath path)
        {
            if (path == null)
            {
                return false;
            }

            var candidate = path.transform;
            while (candidate != null)
            {
                if (string.Equals(candidate.name, RuntimeRootName, StringComparison.Ordinal))
                {
                    return true;
                }

                candidate = candidate.parent;
            }

            return false;
        }

        private static Transform RuntimeRoot
        {
            get
            {
                if (runtimeRoot != null)
                {
                    return runtimeRoot;
                }

                var rootObject = GameObject.Find(RuntimeRootName);
                if (rootObject == null)
                {
                    rootObject = new GameObject(RuntimeRootName)
                    {
                        hideFlags = HideFlags.DontSave
                    };
                }

                runtimeRoot = rootObject.transform;
                return runtimeRoot;
            }
        }

        private static void DestroyInstance(GameObject instance)
        {
            if (instance == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(instance);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        private static bool TryResolveContainer(
            SplineContainer candidate,
            int requestedSplineIndex,
            int defaultSplineIndex,
            out SplineContainer container,
            out int resolvedSplineIndex)
        {
            container = null;
            resolvedSplineIndex = 0;

            if (candidate == null || candidate.Splines == null || candidate.Splines.Count == 0)
            {
                return false;
            }

            var index = requestedSplineIndex >= 0 ? requestedSplineIndex : defaultSplineIndex;
            if (index < 0 || index >= candidate.Splines.Count)
            {
                return false;
            }

            container = candidate;
            resolvedSplineIndex = index;
            return true;
        }

        private static bool Matches(string candidate, string expected)
        {
            return string.Equals(candidate?.Trim(), expected, StringComparison.OrdinalIgnoreCase);
        }

        private bool MatchesPath(string expectedPathId)
        {
            return Matches(PathId, expectedPathId) || Matches(gameObject.name, expectedPathId);
        }

        private bool TryResolveSpline(
            string splineId,
            int requestedSplineIndex,
            out SplineContainer container,
            out int resolvedSplineIndex)
        {
            if (TryResolveAliasedSpline(splineId, requestedSplineIndex, out container, out resolvedSplineIndex))
            {
                return true;
            }

            if (TryResolveChildSpline(splineId, requestedSplineIndex, out container, out resolvedSplineIndex))
            {
                return true;
            }

            return TryResolveOwnSpline(splineId, requestedSplineIndex, out container, out resolvedSplineIndex);
        }

        private bool TryResolveAliasedSpline(
            string splineId,
            int requestedSplineIndex,
            out SplineContainer container,
            out int resolvedSplineIndex)
        {
            if (aliases != null)
            {
                foreach (var alias in aliases)
                {
                    if (!Matches(alias.splineId, splineId))
                    {
                        continue;
                    }

                    var candidate = alias.splineContainer != null ? alias.splineContainer : Container;
                    if (TryResolveContainer(candidate, requestedSplineIndex, Mathf.Max(0, alias.splineIndex), out container, out resolvedSplineIndex))
                    {
                        return true;
                    }
                }
            }

            container = null;
            resolvedSplineIndex = 0;
            return false;
        }

        private bool TryResolveChildSpline(
            string splineId,
            int requestedSplineIndex,
            out SplineContainer container,
            out int resolvedSplineIndex)
        {
            foreach (var childContainer in GetChildSplineContainers())
            {
                if (!Matches(childContainer.gameObject.name, splineId))
                {
                    continue;
                }

                if (TryResolveContainer(childContainer, requestedSplineIndex, 0, out container, out resolvedSplineIndex))
                {
                    return true;
                }
            }

            container = null;
            resolvedSplineIndex = 0;
            return false;
        }

        private bool TryResolveOwnSpline(
            string splineId,
            int requestedSplineIndex,
            out SplineContainer container,
            out int resolvedSplineIndex)
        {
            if (Matches(gameObject.name, splineId)
                && TryResolveContainer(Container, requestedSplineIndex, 0, out container, out resolvedSplineIndex))
            {
                return true;
            }

            if (TryParseSequentialSplineId(splineId, out var parsedIndex))
            {
                return TryResolveContainer(Container, requestedSplineIndex, parsedIndex, out container, out resolvedSplineIndex);
            }

            container = null;
            resolvedSplineIndex = 0;
            return false;
        }

        private List<SplineContainer> GetChildSplineContainers()
        {
            var result = new List<SplineContainer>();
            var ownContainer = Container;
            var containers = GetComponentsInChildren<SplineContainer>(true);
            foreach (var container in containers)
            {
                if (container == null || container == ownContainer || container.Splines == null || container.Splines.Count == 0)
                {
                    continue;
                }

                result.Add(container);
            }

            return result;
        }

        private static bool TryParseSequentialSplineId(string splineId, out int splineIndex)
        {
            splineIndex = 0;
            const string prefix = "spline";
            var trimmed = splineId?.Trim();
            if (string.IsNullOrEmpty(trimmed)
                || !trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                || !int.TryParse(trimmed.Substring(prefix.Length), out var oneBasedIndex)
                || oneBasedIndex <= 0)
            {
                return false;
            }

            splineIndex = oneBasedIndex - 1;
            return true;
        }

        private void Reset()
        {
            if (string.IsNullOrWhiteSpace(pathId))
            {
                pathId = gameObject.name;
            }
        }

        private void OnValidate()
        {
            splineIndex = Mathf.Max(0, splineIndex);
            pixelsPerUnit = Mathf.Max(1f, pixelsPerUnit);
            designResolution.x = Mathf.Max(1, designResolution.x);
            designResolution.y = Mathf.Max(1, designResolution.y);
            previewSegments = Mathf.Clamp(previewSegments, 2, 256);

            if (aliases == null)
            {
                return;
            }

            for (var i = 0; i < aliases.Length; i++)
            {
                aliases[i].splineIndex = Mathf.Max(0, aliases[i].splineIndex);
            }
        }

        private void OnDrawGizmos()
        {
            if (drawDesignFrame)
            {
                DrawDesignFrame();
            }

            if (drawPreviewPath)
            {
                DrawPreviewPath();
            }
        }

        private void DrawDesignFrame()
        {
            if (pixelsPerUnit <= 0f)
            {
                return;
            }

            var halfWidth = designResolution.x * 0.5f / pixelsPerUnit;
            var halfHeight = designResolution.y * 0.5f / pixelsPerUnit;
            var z = transform.position.z;
            var topLeft = new Vector3(-halfWidth, halfHeight, z);
            var topRight = new Vector3(halfWidth, halfHeight, z);
            var bottomRight = new Vector3(halfWidth, -halfHeight, z);
            var bottomLeft = new Vector3(-halfWidth, -halfHeight, z);

            var previousColor = Gizmos.color;
            Gizmos.color = new Color(1f, 1f, 1f, 0.45f);
            Gizmos.DrawLine(topLeft, topRight);
            Gizmos.DrawLine(topRight, bottomRight);
            Gizmos.DrawLine(bottomRight, bottomLeft);
            Gizmos.DrawLine(bottomLeft, topLeft);
            Gizmos.color = previousColor;
        }

        private void DrawPreviewPath()
        {
            var childContainers = GetChildSplineContainers();
            if (childContainers.Count > 0)
            {
                DrawPreviewContainers(childContainers);
                return;
            }

            var container = Container;
            if (container == null || container.Splines == null || container.Splines.Count == 0)
            {
                return;
            }

            DrawPreviewContainer(container);
        }

        private void DrawPreviewContainers(IReadOnlyList<SplineContainer> containers)
        {
            var previousColor = Gizmos.color;
            Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.9f);

            if (drawAllSplines)
            {
                foreach (var container in containers)
                {
                    DrawAllSplines(container);
                }

                Gizmos.color = previousColor;
                return;
            }

            var containerIndex = Mathf.Clamp(SplineIndex, 0, containers.Count - 1);
            var selectedContainer = containers[containerIndex];
            if (selectedContainer != null && selectedContainer.Splines != null && selectedContainer.Splines.Count > 0)
            {
                DrawPreviewSpline(selectedContainer, selectedContainer.Splines[0]);
            }

            Gizmos.color = previousColor;
        }

        private void DrawPreviewContainer(SplineContainer container)
        {
            var previousColor = Gizmos.color;
            Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.9f);

            if (drawAllSplines)
            {
                DrawAllSplines(container);
                Gizmos.color = previousColor;
                return;
            }

            var index = Mathf.Clamp(SplineIndex, 0, container.Splines.Count - 1);
            DrawPreviewSpline(container, container.Splines[index]);
            Gizmos.color = previousColor;
        }

        private void DrawAllSplines(SplineContainer container)
        {
            if (container == null || container.Splines == null)
            {
                return;
            }

            for (var i = 0; i < container.Splines.Count; i++)
            {
                DrawPreviewSpline(container, container.Splines[i]);
            }
        }

        private void DrawPreviewSpline(SplineContainer container, Spline spline)
        {
            var previousPoint = EvaluatePreviewPoint(container, spline, 0f);
            Gizmos.DrawSphere(previousPoint, 0.08f);
            for (var i = 1; i <= previewSegments; i++)
            {
                var t = i / (float)previewSegments;
                var point = EvaluatePreviewPoint(container, spline, t);
                Gizmos.DrawLine(previousPoint, point);
                previousPoint = point;
            }

            Gizmos.DrawSphere(previousPoint, 0.08f);
        }

        private Vector3 EvaluatePreviewPoint(SplineContainer container, Spline spline, float t)
        {
            var position = SplineUtility.EvaluatePosition(spline, t);
            var worldPosition = container.transform.TransformPoint(new Vector3(position.x, position.y, position.z));
            return worldPosition + new Vector3(previewOffset.x, previewOffset.y, 0f);
        }
    }
}
