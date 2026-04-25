using UnityEngine;

namespace LeiTing.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CircleCollider2D))]
    public class PlayerHitbox : MonoBehaviour
    {
        [SerializeField] private PlayerController owner;

        private CircleCollider2D hitbox;

        public CircleCollider2D Collider => hitbox != null ? hitbox : GetComponent<CircleCollider2D>();

        public void Configure(PlayerController controller, float radius, Vector2 offset)
        {
            owner = controller;
            hitbox = Collider;
            hitbox.isTrigger = true;
            hitbox.radius = Mathf.Max(0.01f, radius);
            hitbox.offset = Vector2.zero;
            transform.localPosition = offset;
        }

        private void Awake()
        {
            hitbox = Collider;

            if (owner == null)
            {
                owner = GetComponentInParent<PlayerController>();
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (owner != null)
            {
                owner.HandleHitboxTrigger(other);
            }
        }
    }
}
