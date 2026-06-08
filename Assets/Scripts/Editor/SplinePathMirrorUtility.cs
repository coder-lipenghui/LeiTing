#if UNITY_EDITOR
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;
using Object = UnityEngine.Object;

namespace LeiTing.Editor
{
    public static class SplinePathMirrorUtility
    {
        private static readonly Vector2 DefaultWorldPivot = Vector2.zero;

        [MenuItem("LeiTing/Spline Paths/Mirror Selected Spline Horizontally")]
        public static void MirrorSelectedSplineHorizontally()
        {
            MirrorSelectedSplines(true, false, DefaultWorldPivot);
        }

        [MenuItem("LeiTing/Spline Paths/Mirror Selected Spline Horizontally", true)]
        private static bool ValidateMirrorSelectedSplineHorizontally()
        {
            return GetSelectedSplineContainers().Count > 0;
        }

        [MenuItem("LeiTing/Spline Paths/Mirror Selected Spline Vertically")]
        public static void MirrorSelectedSplineVertically()
        {
            MirrorSelectedSplines(false, true, DefaultWorldPivot);
        }

        [MenuItem("LeiTing/Spline Paths/Mirror Selected Spline Vertically", true)]
        private static bool ValidateMirrorSelectedSplineVertically()
        {
            return GetSelectedSplineContainers().Count > 0;
        }

        private static void MirrorSelectedSplines(bool mirrorX, bool mirrorY, Vector2 worldPivot)
        {
            var containers = GetSelectedSplineContainers();
            if (containers.Count == 0)
            {
                Debug.LogWarning("No selected GameObject has a SplineContainer.");
                return;
            }

            foreach (var container in containers)
            {
                MirrorContainer(container, mirrorX, mirrorY, worldPivot);
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"Mirrored {containers.Count} selected spline container(s).");
        }

        private static List<SplineContainer> GetSelectedSplineContainers()
        {
            var containers = new List<SplineContainer>();
            foreach (var gameObject in Selection.gameObjects)
            {
                if (gameObject == null)
                {
                    continue;
                }

                var container = gameObject.GetComponent<SplineContainer>();
                if (container != null && !containers.Contains(container))
                {
                    containers.Add(container);
                }
            }

            return containers;
        }

        private static void MirrorContainer(SplineContainer container, bool mirrorX, bool mirrorY, Vector2 worldPivot)
        {
            if (container == null || container.Splines == null)
            {
                return;
            }

            var actionName = mirrorX ? "Mirror Spline Horizontally" : "Mirror Spline Vertically";
            Undo.RecordObjects(new Object[] { container, container.transform }, actionName);

            var containerTransform = container.transform;
            var referenceTransform = containerTransform.parent;

            foreach (var spline in container.Splines)
            {
                MirrorSpline(containerTransform, referenceTransform, spline, mirrorX, mirrorY, worldPivot);
            }

            if (referenceTransform != null)
            {
                containerTransform.localPosition = Vector3.zero;
                containerTransform.localRotation = Quaternion.identity;
                containerTransform.localScale = Vector3.one;
            }

            EditorUtility.SetDirty(container);
            PrefabUtility.RecordPrefabInstancePropertyModifications(container);
        }

        private static void MirrorSpline(
            Transform containerTransform,
            Transform referenceTransform,
            Spline spline,
            bool mirrorX,
            bool mirrorY,
            Vector2 worldPivot)
        {
            if (spline == null)
            {
                return;
            }

            var knots = new BezierKnot[spline.Count];
            var tangentModes = new TangentMode[spline.Count];
            var tensions = new float[spline.Count];

            for (var knotIndex = 0; knotIndex < spline.Count; knotIndex++)
            {
                var knot = spline[knotIndex];
                var tangentMode = spline.GetTangentMode(knotIndex);
                tangentModes[knotIndex] = tangentMode;
                tensions[knotIndex] = spline.GetAutoSmoothTension(knotIndex);
                knots[knotIndex] = MirrorKnot(containerTransform, referenceTransform, knot, tangentMode, mirrorX, mirrorY, worldPivot);
            }

            for (var knotIndex = 0; knotIndex < spline.Count; knotIndex++)
            {
                var targetMode = ShouldBakeTangents(tangentModes[knotIndex]) ? TangentMode.Broken : tangentModes[knotIndex];
                spline.SetTangentMode(knotIndex, targetMode);
                spline.SetKnot(knotIndex, knots[knotIndex]);
                spline.SetTangentMode(knotIndex, targetMode);

                if (targetMode == TangentMode.AutoSmooth)
                {
                    spline.SetAutoSmoothTension(knotIndex, tensions[knotIndex]);
                }
            }
        }

        private static BezierKnot MirrorKnot(
            Transform containerTransform,
            Transform referenceTransform,
            BezierKnot knot,
            TangentMode tangentMode,
            bool mirrorX,
            bool mirrorY,
            Vector2 worldPivot)
        {
            var position = MirrorLocalPoint(containerTransform, referenceTransform, ToVector3(knot.Position), mirrorX, mirrorY, worldPivot);
            if (!ShouldBakeTangents(tangentMode))
            {
                return new BezierKnot(ToFloat3(position));
            }

            var tangentInEndpoint = ToVector3(knot.Position) + ToVector3(math.rotate(knot.Rotation, knot.TangentIn));
            var tangentOutEndpoint = ToVector3(knot.Position) + ToVector3(math.rotate(knot.Rotation, knot.TangentOut));
            var mirroredTangentInEndpoint = MirrorLocalPoint(containerTransform, referenceTransform, tangentInEndpoint, mirrorX, mirrorY, worldPivot);
            var mirroredTangentOutEndpoint = MirrorLocalPoint(containerTransform, referenceTransform, tangentOutEndpoint, mirrorX, mirrorY, worldPivot);

            return new BezierKnot(
                ToFloat3(position),
                ToFloat3(mirroredTangentInEndpoint - position),
                ToFloat3(mirroredTangentOutEndpoint - position),
                quaternion.identity);
        }

        private static Vector3 MirrorLocalPoint(
            Transform containerTransform,
            Transform referenceTransform,
            Vector3 localPoint,
            bool mirrorX,
            bool mirrorY,
            Vector2 worldPivot)
        {
            var worldPosition = containerTransform.TransformPoint(localPoint);
            if (referenceTransform != null)
            {
                var referencePosition = referenceTransform.InverseTransformPoint(worldPosition);
                var mirroredReferencePosition = MirrorPoint(referencePosition, mirrorX, mirrorY, Vector2.zero);
                return mirroredReferencePosition;
            }

            var mirroredWorldPosition = MirrorPoint(worldPosition, mirrorX, mirrorY, worldPivot);
            return containerTransform.InverseTransformPoint(mirroredWorldPosition);
        }

        private static bool ShouldBakeTangents(TangentMode tangentMode)
        {
            return tangentMode == TangentMode.Broken
                || tangentMode == TangentMode.Continuous
                || tangentMode == TangentMode.Mirrored;
        }

        private static Vector3 MirrorPoint(Vector3 point, bool mirrorX, bool mirrorY, Vector2 worldPivot)
        {
            if (mirrorX)
            {
                point.x = worldPivot.x * 2f - point.x;
            }

            if (mirrorY)
            {
                point.y = worldPivot.y * 2f - point.y;
            }

            return point;
        }

        private static Vector3 ToVector3(float3 value)
        {
            return new Vector3(value.x, value.y, value.z);
        }

        private static float3 ToFloat3(Vector3 value)
        {
            return new float3(value.x, value.y, value.z);
        }
    }
}
#endif
