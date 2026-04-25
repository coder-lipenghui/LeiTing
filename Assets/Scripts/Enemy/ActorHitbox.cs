using System;
using LeiTing.Bullets;
using UnityEngine;

namespace LeiTing.Enemy
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider2D))]
    public class ActorHitbox : MonoBehaviour
    {
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
    }
}
