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

            if (groupName.IndexOf(',') >= 0 || groupName.IndexOf('|') >= 0)
            {
                return ResolveCompositeFirePoints(groupName);
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

        private Transform[] ResolveCompositeFirePoints(string groupName)
        {
            var points = new List<Transform>();
            var names = groupName.Split(new[] { ',', '|' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var rawName in names)
            {
                var name = rawName.Trim();
                if (string.IsNullOrEmpty(name))
                {
                    continue;
                }

                var resolved = ResolveFirePoints(name);
                for (var index = 0; index < resolved.Length; index++)
                {
                    if (resolved[index] != null && !points.Contains(resolved[index]))
                    {
                        points.Add(resolved[index]);
                    }
                }
            }

            return points.ToArray();
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
