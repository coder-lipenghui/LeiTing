using UnityEngine;

namespace LeiTing.Effects
{
    [DisallowMultipleComponent]
    public class ExplosionEffect : MonoBehaviour
    {
        private const int TextureSize = 32;
        private static Sprite explosionSprite;

        [SerializeField] private float lifetime = 0.35f;
        [SerializeField] private float startScale = 0.3f;
        [SerializeField] private float endScale = 1.4f;
        [SerializeField] private Color startColor = new Color(1f, 0.86f, 0.22f, 0.95f);
        [SerializeField] private Color endColor = new Color(1f, 0.2f, 0.05f, 0f);

        private SpriteRenderer spriteRenderer;
        private float age;

        public static void Spawn(Vector3 position, float scale = 1f)
        {
            var effectObject = new GameObject("ExplosionEffect");
            effectObject.transform.position = position;

            var effect = effectObject.AddComponent<ExplosionEffect>();
            effect.startScale *= scale;
            effect.endScale *= scale;
        }

        private void Awake()
        {
            spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = GetExplosionSprite();
            spriteRenderer.sortingOrder = 60;
            transform.localScale = Vector3.one * startScale;
        }

        private void Update()
        {
            age += Time.deltaTime;
            var t = Mathf.Clamp01(age / Mathf.Max(0.01f, lifetime));
            transform.localScale = Vector3.one * Mathf.Lerp(startScale, endScale, EaseOut(t));
            spriteRenderer.color = Color.Lerp(startColor, endColor, t);

            if (t >= 1f)
            {
                Destroy(gameObject);
            }
        }

        private static float EaseOut(float t)
        {
            return 1f - (1f - t) * (1f - t);
        }

        private static Sprite GetExplosionSprite()
        {
            if (explosionSprite == null)
            {
                explosionSprite = CreateExplosionSprite();
            }

            return explosionSprite;
        }

        private static Sprite CreateExplosionSprite()
        {
            var texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Bilinear;

            var clear = new Color(0f, 0f, 0f, 0f);
            var center = new Vector2((TextureSize - 1) * 0.5f, (TextureSize - 1) * 0.5f);

            for (var y = 0; y < TextureSize; y++)
            {
                for (var x = 0; x < TextureSize; x++)
                {
                    var distance = Vector2.Distance(new Vector2(x, y), center) / (TextureSize * 0.5f);
                    if (distance > 1f)
                    {
                        texture.SetPixel(x, y, clear);
                        continue;
                    }

                    var alpha = Mathf.Clamp01(1f - distance);
                    var color = Color.Lerp(new Color(1f, 0.16f, 0.02f, 0.5f), new Color(1f, 0.96f, 0.36f, 1f), alpha);
                    color.a *= alpha;
                    texture.SetPixel(x, y, color);
                }
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, TextureSize, TextureSize), new Vector2(0.5f, 0.5f), TextureSize);
        }
    }
}
