using LeiTing.Config;
using LeiTing.Core;
using UnityEngine;

namespace LeiTing.Stage
{
    [DisallowMultipleComponent]
    public class BackgroundScroller : MonoBehaviour
    {
        private const string TileAName = "BackgroundTile_A";
        private const string TileBName = "BackgroundTile_B";
        private const float TileOverlap = 0.02f;

        [SerializeField] private Sprite backgroundSprite;
        [SerializeField] private Camera gameplayCamera;
        [SerializeField] private float scrollSpeed = 2.1f;
        [SerializeField] private int sortingOrder = -10;

        private Transform tileA;
        private Transform tileB;
        private SpriteRenderer rendererA;
        private SpriteRenderer rendererB;
        private float tileHeight;

        public void Configure(Sprite sprite, float speed)
        {
            if (sprite != null)
            {
                backgroundSprite = sprite;
            }

            scrollSpeed = Mathf.Max(0f, speed);
            BuildTiles();
        }

        private void Awake()
        {
            gameplayCamera = gameplayCamera != null ? gameplayCamera : Camera.main;
            BuildTiles();
        }

        private void Start()
        {
            ApplyConfig();
            FitToCamera();
        }

        private void LateUpdate()
        {
            if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameState.Playing)
            {
                return;
            }

            if (tileA == null || tileB == null)
            {
                BuildTiles();
            }

            if (tileHeight <= 0f)
            {
                FitToCamera();
            }

            var distance = scrollSpeed * Time.deltaTime;
            tileA.localPosition += Vector3.down * distance;
            tileB.localPosition += Vector3.down * distance;

            RecycleTile(tileA, tileB);
            RecycleTile(tileB, tileA);
        }

        private void OnValidate()
        {
            scrollSpeed = Mathf.Max(0f, scrollSpeed);

            if (!Application.isPlaying)
            {
                return;
            }

            BuildTiles();
            FitToCamera();
        }

        private void ApplyConfig()
        {
            if (ConfigManager.Instance == null || !ConfigManager.Instance.IsLoaded || ConfigManager.Instance.Config.background == null)
            {
                return;
            }

            if (ConfigManager.Instance.Config.background.scrollSpeed > 0f)
            {
                scrollSpeed = ConfigManager.Instance.Config.background.scrollSpeed;
            }
        }

        private void BuildTiles()
        {
            var ownRenderer = GetComponent<SpriteRenderer>();

            if (backgroundSprite == null && ownRenderer != null)
            {
                backgroundSprite = ownRenderer.sprite;
            }

            if (ownRenderer != null)
            {
                ownRenderer.enabled = false;
            }

            tileA = GetOrCreateTile(TileAName, out rendererA);
            tileB = GetOrCreateTile(TileBName, out rendererB);

            ConfigureRenderer(rendererA);
            ConfigureRenderer(rendererB);
            FitToCamera();
        }

        private Transform GetOrCreateTile(string tileName, out SpriteRenderer spriteRenderer)
        {
            var child = transform.Find(tileName);

            if (child == null)
            {
                var tileObject = new GameObject(tileName);
                child = tileObject.transform;
                child.SetParent(transform);
                child.localRotation = Quaternion.identity;
            }

            child.localScale = Vector3.one;
            spriteRenderer = child.GetComponent<SpriteRenderer>();

            if (spriteRenderer == null)
            {
                spriteRenderer = child.gameObject.AddComponent<SpriteRenderer>();
            }

            return child;
        }

        private void ConfigureRenderer(SpriteRenderer spriteRenderer)
        {
            if (spriteRenderer == null)
            {
                return;
            }

            spriteRenderer.sprite = backgroundSprite;
            spriteRenderer.sortingOrder = sortingOrder;
        }

        private void FitToCamera()
        {
            if (backgroundSprite == null || gameplayCamera == null || tileA == null || tileB == null)
            {
                return;
            }

            var cameraHeight = gameplayCamera.orthographicSize * 2f;
            var cameraWidth = cameraHeight * gameplayCamera.aspect;
            var spriteSize = backgroundSprite.bounds.size;

            if (spriteSize.x <= 0f || spriteSize.y <= 0f)
            {
                return;
            }

            var scale = Mathf.Max(cameraWidth / spriteSize.x, cameraHeight / spriteSize.y);
            tileHeight = spriteSize.y * scale;

            tileA.localScale = new Vector3(scale, scale, 1f);
            tileB.localScale = new Vector3(scale, scale, 1f);
            tileA.localPosition = Vector3.zero;
            tileB.localPosition = new Vector3(0f, tileHeight - TileOverlap, 0f);
        }

        private void RecycleTile(Transform tile, Transform otherTile)
        {
            if (tile == null || otherTile == null || gameplayCamera == null || tileHeight <= 0f)
            {
                return;
            }

            var bottomLimit = -gameplayCamera.orthographicSize - tileHeight * 0.5f;

            if (tile.localPosition.y <= bottomLimit)
            {
                tile.localPosition = otherTile.localPosition + new Vector3(0f, tileHeight - TileOverlap, 0f);
            }
        }

    }
}
