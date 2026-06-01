using System.Collections.Generic;
using LeiTing.Core;
using UnityEngine;

namespace LeiTing.Stage
{
    [DisallowMultipleComponent]
    public sealed class ReadyCloudLayer : MonoBehaviour
    {
        private const int CloudCountPerSide = 4;
        private const int CloudSortingOrder = 14;
        private const float BaseSpeed = 1.15f;
        private const float MinScale = 1.05f;
        private const float MaxScale = 1.85f;

        private static Sprite cloudSprite;

        [SerializeField] private Camera gameplayCamera;

        private readonly List<CloudView> clouds = new List<CloudView>();
        private bool cloudsVisible;

        public static ReadyCloudLayer GetOrCreate()
        {
            var existing = FindObjectOfType<ReadyCloudLayer>();
            if (existing != null)
            {
                return existing;
            }

            var layerObject = new GameObject("ReadyCloudLayer");
            var gameRoot = GameObject.Find("GameRoot");
            if (gameRoot != null)
            {
                layerObject.transform.SetParent(gameRoot.transform, false);
            }

            return layerObject.AddComponent<ReadyCloudLayer>();
        }

        private void Awake()
        {
            gameplayCamera = gameplayCamera != null ? gameplayCamera : Camera.main;
            BuildClouds();
            ResetClouds(true);
            SetCloudsVisible(false);
        }

        private void LateUpdate()
        {
            gameplayCamera = gameplayCamera != null ? gameplayCamera : Camera.main;
            var state = GameManager.Instance != null ? GameManager.Instance.CurrentState : GameState.Boot;
            var showClouds = state == GameState.Ready;

            if (showClouds != cloudsVisible)
            {
                SetCloudsVisible(showClouds);
                if (showClouds)
                {
                    ResetClouds(true);
                }
            }

            if (!showClouds || gameplayCamera == null)
            {
                return;
            }

            GetCameraBounds(out _, out _, out var bottom, out _);
            foreach (var cloud in clouds)
            {
                if (cloud?.root == null)
                {
                    continue;
                }

                cloud.root.position += Vector3.down * (cloud.speed * Time.deltaTime);
                if (cloud.root.position.y < bottom - cloud.height)
                {
                    ResetCloud(cloud, false);
                }
            }
        }

        private void BuildClouds()
        {
            if (clouds.Count > 0)
            {
                return;
            }

            for (var side = 0; side < 2; side++)
            {
                for (var index = 0; index < CloudCountPerSide; index++)
                {
                    clouds.Add(CreateCloud(side == 0));
                }
            }
        }

        private CloudView CreateCloud(bool leftSide)
        {
            var cloudObject = new GameObject(leftSide ? "ReadyCloud_Left" : "ReadyCloud_Right");
            cloudObject.transform.SetParent(transform, false);

            var renderer = cloudObject.AddComponent<SpriteRenderer>();
            renderer.sprite = GetCloudSprite();
            renderer.sortingOrder = CloudSortingOrder;
            renderer.color = new Color(1f, 1f, 1f, 0.18f);

            return new CloudView
            {
                root = cloudObject.transform,
                renderer = renderer,
                leftSide = leftSide
            };
        }

        private void ResetClouds(bool spreadVertically)
        {
            foreach (var cloud in clouds)
            {
                ResetCloud(cloud, spreadVertically);
            }
        }

        private void ResetCloud(CloudView cloud, bool spreadVertically)
        {
            if (cloud == null || cloud.root == null || gameplayCamera == null)
            {
                return;
            }

            GetCameraBounds(out var left, out var right, out var bottom, out var top);
            var width = right - left;
            var height = top - bottom;
            var sideOffset = Random.Range(width * 0.04f, width * 0.24f);
            var x = cloud.leftSide ? left + sideOffset : right - sideOffset;
            var y = spreadVertically
                ? Random.Range(bottom + height * 0.08f, top + height * 0.75f)
                : top + Random.Range(0.2f, height * 0.45f);
            var scale = Random.Range(MinScale, MaxScale);

            cloud.root.position = new Vector3(x, y, 0f);
            cloud.root.localRotation = Quaternion.identity;
            cloud.root.localScale = new Vector3(scale, scale, 1f);
            cloud.speed = BaseSpeed * Random.Range(0.65f, 1.2f);
            cloud.height = GetCloudSprite().bounds.size.y * scale;

            if (cloud.renderer != null)
            {
                cloud.renderer.flipX = !cloud.leftSide;
                cloud.renderer.color = new Color(1f, 1f, 1f, Random.Range(0.14f, 0.28f));
            }
        }

        private void SetCloudsVisible(bool visible)
        {
            cloudsVisible = visible;
            foreach (var cloud in clouds)
            {
                if (cloud?.renderer != null)
                {
                    cloud.renderer.enabled = visible;
                }
            }
        }

        private void GetCameraBounds(out float left, out float right, out float bottom, out float top)
        {
            var cameraHeight = gameplayCamera.orthographicSize * 2f;
            var cameraWidth = cameraHeight * gameplayCamera.aspect;
            var cameraPosition = gameplayCamera.transform.position;
            left = cameraPosition.x - cameraWidth * 0.5f;
            right = cameraPosition.x + cameraWidth * 0.5f;
            bottom = cameraPosition.y - cameraHeight * 0.5f;
            top = cameraPosition.y + cameraHeight * 0.5f;
        }

        private static Sprite GetCloudSprite()
        {
            if (cloudSprite == null)
            {
                cloudSprite = CreateCloudSprite();
            }

            return cloudSprite;
        }

        private static Sprite CreateCloudSprite()
        {
            const int width = 160;
            const int height = 92;
            const float pixelsPerUnit = 80f;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            var circles = new[]
            {
                new CloudCircle(new Vector2(45f, 42f), 34f),
                new CloudCircle(new Vector2(76f, 52f), 42f),
                new CloudCircle(new Vector2(112f, 40f), 32f),
                new CloudCircle(new Vector2(78f, 34f), 50f)
            };

            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var point = new Vector2(x, y);
                    var alpha = 0f;
                    foreach (var circle in circles)
                    {
                        var distance = Vector2.Distance(point, circle.center) / circle.radius;
                        alpha = Mathf.Max(alpha, Mathf.Pow(Mathf.Clamp01(1f - distance), 1.45f));
                    }

                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), pixelsPerUnit);
        }

        private sealed class CloudView
        {
            public Transform root;
            public SpriteRenderer renderer;
            public bool leftSide;
            public float speed;
            public float height;
        }

        private readonly struct CloudCircle
        {
            public readonly Vector2 center;
            public readonly float radius;

            public CloudCircle(Vector2 center, float radius)
            {
                this.center = center;
                this.radius = radius;
            }
        }
    }
}
