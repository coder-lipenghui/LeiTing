using System;
using System.Collections.Generic;
using LeiTing.Config;
using LeiTing.Core;
using UnityEngine;

namespace LeiTing.Missiles
{
    public class MissileManager : MonoSingleton<MissileManager>
    {
        [SerializeField] private int initialPoolSize = 16;
        [SerializeField] private Transform missileLayer;

        private readonly Stack<MissileController> pooledMissiles = new Stack<MissileController>();
        private bool isPoolWarmed;

        public MissileController Fire(MissileConfig missileConfig, Vector2 position, Vector2 direction)
        {
            if (missileConfig == null)
            {
                return null;
            }

            EnsurePool();

            var missile = GetMissile();
            missile.transform.SetParent(GetLayerRoot(), false);
            missile.transform.position = position;
            missile.transform.rotation = Quaternion.identity;
            missile.Activate(missileConfig, direction, this);
            return missile;
        }

        public void Recycle(MissileController missile)
        {
            if (missile == null)
            {
                return;
            }

            missile.DeactivateForPool();
            missile.transform.SetParent(transform, false);
            pooledMissiles.Push(missile);
        }

        public void ClearEnemyMissiles()
        {
            ClearMissilesInLayer(GetLayerRoot());
        }

        protected override void Awake()
        {
            base.Awake();

            if (Instance == this)
            {
                EnsurePool();
            }
        }

        private void EnsurePool()
        {
            if (isPoolWarmed)
            {
                return;
            }

            CacheLayerRoot();

            var count = Mathf.Max(0, initialPoolSize);
            for (var index = 0; index < count; index++)
            {
                pooledMissiles.Push(CreateMissile());
            }

            isPoolWarmed = true;
        }

        private MissileController GetMissile()
        {
            return pooledMissiles.Count > 0 ? pooledMissiles.Pop() : CreateMissile();
        }

        private MissileController CreateMissile()
        {
            var missileObject = new GameObject("Missile");
            missileObject.transform.SetParent(transform, false);
            missileObject.SetActive(false);
            return missileObject.AddComponent<MissileController>();
        }

        private Transform GetLayerRoot()
        {
            CacheLayerRoot();
            return missileLayer;
        }

        private void CacheLayerRoot()
        {
            missileLayer = missileLayer != null ? missileLayer : FindLayerRoot("MissileLayer_Enemy");
            missileLayer = missileLayer != null ? missileLayer : FindLayerRoot("BulletLayer_Enemy");
            missileLayer = missileLayer != null ? missileLayer : transform;
        }

        private static Transform FindLayerRoot(string layerName)
        {
            var found = GameObject.Find(layerName);
            return found != null ? found.transform : null;
        }

        private void ClearMissilesInLayer(Transform layerRoot)
        {
            if (layerRoot == null)
            {
                return;
            }

            var missiles = new List<MissileController>();
            foreach (Transform child in layerRoot)
            {
                var missile = child.GetComponent<MissileController>();
                if (missile != null)
                {
                    missiles.Add(missile);
                }
            }

            foreach (var missile in missiles)
            {
                Recycle(missile);
            }
        }
    }
}
