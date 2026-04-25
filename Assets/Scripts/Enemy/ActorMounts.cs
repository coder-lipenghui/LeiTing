using System;
using System.Collections.Generic;
using UnityEngine;

namespace LeiTing.Enemy
{
    [DisallowMultipleComponent]
    public class ActorMounts : MonoBehaviour
    {
        private const string FirePointsRootName = "FirePoints";

        private readonly Dictionary<string, Transform[]> firePointCache = new Dictionary<string, Transform[]>(StringComparer.OrdinalIgnoreCase);
        private Transform firePointsRoot;

        public Transform[] GetFirePoints(string groupName)
        {
            var key = string.IsNullOrEmpty(groupName) ? "center" : groupName;
            if (firePointCache.TryGetValue(key, out var cached))
            {
                return cached;
            }

            CacheRoots();
            var points = ResolveFirePoints(key);
            firePointCache[key] = points;
            return points;
        }

        private void Awake()
        {
            CacheRoots();
        }

        private void CacheRoots()
        {
            firePointsRoot = firePointsRoot != null ? firePointsRoot : transform.Find(FirePointsRootName);
        }

        private Transform[] ResolveFirePoints(string groupName)
        {
            if (firePointsRoot == null)
            {
                return Array.Empty<Transform>();
            }

            var group = firePointsRoot.Find(groupName);
            if (group != null)
            {
                if (group.childCount == 0)
                {
                    return new[] { group };
                }

                var children = new Transform[group.childCount];
                for (var index = 0; index < group.childCount; index++)
                {
                    children[index] = group.GetChild(index);
                }

                return children;
            }

            var directPoint = FindDeepChild(firePointsRoot, groupName);
            return directPoint != null ? new[] { directPoint } : Array.Empty<Transform>();
        }

        private static Transform FindDeepChild(Transform root, string childName)
        {
            foreach (Transform child in root)
            {
                if (string.Equals(child.name, childName, StringComparison.OrdinalIgnoreCase))
                {
                    return child;
                }

                var nested = FindDeepChild(child, childName);
                if (nested != null)
                {
                    return nested;
                }
            }

            return null;
        }
    }
}
