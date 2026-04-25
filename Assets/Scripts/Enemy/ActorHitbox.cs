using System;
using System.Collections.Generic;
using LeiTing.Bullets;
using UnityEngine;

namespace LeiTing.Enemy
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider2D))]
    public class ActorHitbox : MonoBehaviour
    {
        private static readonly Dictionary<ulong, int> processedHits = new Dictionary<ulong, int>();

        [SerializeField] private float damageMultiplier = 1f;

        private EnemyController enemyOwner;
        private BossController bossOwner;

        private void Awake()
        {
            enemyOwner = GetComponentInParent<EnemyController>();
            bossOwner = GetComponentInParent<BossController>();

            var hitbox = GetComponent<Collider2D>();
            hitbox.isTrigger = true;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            var bullet = other != null ? other.GetComponent<BulletProjectile>() : null;
            if (bullet == null || !string.Equals(bullet.Owner, "Player", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (HasProcessedHitThisFrame(bullet))
            {
                return;
            }

            var damage = Mathf.Max(1, Mathf.RoundToInt(bullet.Damage * Mathf.Max(0.01f, damageMultiplier)));
            if (bossOwner != null)
            {
                bossOwner.TakeDamage(damage);
            }
            else if (enemyOwner != null)
            {
                enemyOwner.TakeDamage(damage);
            }
        }

        private bool HasProcessedHitThisFrame(BulletProjectile bullet)
        {
            var ownerId = bossOwner != null ? bossOwner.GetInstanceID() : enemyOwner != null ? enemyOwner.GetInstanceID() : 0;
            var key = ((ulong)(uint)ownerId << 32) | (uint)bullet.GetInstanceID();

            if (processedHits.TryGetValue(key, out var frame) && frame == Time.frameCount)
            {
                return true;
            }

            processedHits[key] = Time.frameCount;
            return false;
        }
    }
}
