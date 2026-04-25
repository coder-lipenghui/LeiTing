using LeiTing.Audio;
using LeiTing.Bullets;
using LeiTing.Config;
using LeiTing.Core;
using LeiTing.Enemy;
using LeiTing.Player;
using LeiTing.Stage;
using LeiTing.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LeiTing.EditorTools
{
    public static class DemoSceneSetup
    {
        private static readonly string[] LayerNames =
        {
            "BgLayer",
            "EnemyLayer",
            "BulletLayer_Player",
            "BulletLayer_Enemy",
            "EffectLayer",
            "UILayer"
        };

        private static readonly string[] ProjectLayerNames =
        {
            "Player",
            "Enemy",
            "PlayerBullet",
            "EnemyBullet",
            "Effect"
        };

        private const string PlayerPrefabPath = "Assets/Prefabs/Player/warplane-01.prefab";
        private const string PlayerSpritePath = "Assets/Art/Animations/Player/warplane-01.png";
        private const string BackgroundSpritePath = "Assets/Art/Sprites/Backgrounds/background-01.png";
        private const float DesignOrthographicSize = 9.6f;

        [MenuItem("LeiTing/Setup/Create Demo Scene Skeleton")]
        public static void CreateDemoSceneSkeleton()
        {
            EnsureSceneLoaded();
            EnsureProjectLayers();

            var root = GetOrCreate("GameRoot");

            foreach (var layerName in LayerNames)
            {
                GetOrCreate(layerName, root.transform);
            }

            var managers = GetOrCreate("Managers", root.transform);
            AddIfMissing<GameBootstrap>(managers);
            AddIfMissing<ConfigManager>(managers);
            AddIfMissing<GameManager>(managers);
            AddIfMissing<EnemyManager>(managers);
            AddIfMissing<BulletManager>(managers);
            AddIfMissing<BulletPatternManager>(managers);
            AddIfMissing<StageManager>(managers);
            AddIfMissing<UIManager>(managers);
            AddIfMissing<AudioManager>(managers);

            EnsureCamera();
            CreatePlayerPrefab();
            CreatePlayer(root.transform);
            CreateBackground(root.transform);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
            Debug.Log("LeiTing demo scene skeleton created.");
        }

        [MenuItem("LeiTing/Setup/Apply Demo Art Setup")]
        public static void ApplyDemoArtSetup()
        {
            CreateDemoSceneSkeleton();
        }

        private static void EnsureSceneLoaded()
        {
            var activeScene = EditorSceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(activeScene.path))
            {
                EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
            }
        }

        private static GameObject GetOrCreate(string objectName, Transform parent = null)
        {
            var found = GameObject.Find(objectName);
            if (found != null)
            {
                if (parent != null)
                {
                    found.transform.SetParent(parent);
                }

                return found;
            }

            var created = new GameObject(objectName);
            if (parent != null)
            {
                created.transform.SetParent(parent);
            }

            created.transform.localPosition = Vector3.zero;
            return created;
        }

        private static T AddIfMissing<T>(GameObject target) where T : Component
        {
            var component = target.GetComponent<T>();

            if (component == null)
            {
                component = target.AddComponent<T>();
            }

            return component;
        }

        private static void CreatePlayer(Transform root)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            var player = GameObject.Find("Player");

            if (player == null && prefab != null)
            {
                player = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                player.name = "Player";
            }

            if (player == null)
            {
                player = GetOrCreate("Player", root);
            }

            player.transform.SetParent(root);
            player.transform.position = new Vector3(0f, -3.5f, 0f);

            var playerLayer = LayerMask.NameToLayer("Player");
            if (playerLayer >= 0)
            {
                SetLayerRecursively(player, playerLayer);
            }

            var body = AddIfMissing<Rigidbody2D>(player);
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            body.freezeRotation = true;

            AddIfMissing<PlayerController>(player);
            AddIfMissing<PlayerShooter>(player);
            EnsurePlayerVisual(player.transform);
            EnsurePlayerHitbox(player.transform);
            EnsurePlayerFirePoint(player.transform);
        }

        private static void CreatePlayerPrefab()
        {
            EnsureFolder("Assets", "Prefabs");
            EnsureFolder("Assets/Prefabs", "Player");

            var prefabRoot = new GameObject("warplane-01");
            var playerLayer = LayerMask.NameToLayer("Player");
            if (playerLayer >= 0)
            {
                SetLayerRecursively(prefabRoot, playerLayer);
            }

            var body = prefabRoot.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            body.freezeRotation = true;
            prefabRoot.AddComponent<PlayerController>();
            prefabRoot.AddComponent<PlayerShooter>();

            EnsurePlayerVisual(prefabRoot.transform);
            EnsurePlayerHitbox(prefabRoot.transform);
            EnsurePlayerFirePoint(prefabRoot.transform);

            PrefabUtility.SaveAsPrefabAsset(prefabRoot, PlayerPrefabPath);
            Object.DestroyImmediate(prefabRoot);
            AssetDatabase.SaveAssets();
        }

        private static void EnsurePlayerVisual(Transform player)
        {
            var visual = player.Find("Visual");

            if (visual == null)
            {
                visual = new GameObject("Visual").transform;
                visual.SetParent(player);
            }

            visual.localPosition = Vector3.zero;
            visual.localRotation = Quaternion.identity;
            visual.localScale = Vector3.one * 0.55f;

            var spriteRenderer = AddIfMissing<SpriteRenderer>(visual.gameObject);
            spriteRenderer.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(PlayerSpritePath);
            spriteRenderer.sortingOrder = 10;
        }

        private static void EnsurePlayerHitbox(Transform player)
        {
            var hitbox = player.Find("Hitbox");

            if (hitbox == null)
            {
                hitbox = new GameObject("Hitbox").transform;
                hitbox.SetParent(player);
            }

            hitbox.localPosition = new Vector3(0f, -0.08f, 0f);
            hitbox.localRotation = Quaternion.identity;
            hitbox.localScale = Vector3.one;

            var playerHitbox = AddIfMissing<PlayerHitbox>(hitbox.gameObject);
            playerHitbox.Configure(player.GetComponent<PlayerController>(), 0.18f, new Vector2(0f, -0.08f));
        }

        private static void EnsurePlayerFirePoint(Transform player)
        {
            var firePoint = player.Find("FirePoint");

            if (firePoint == null)
            {
                firePoint = new GameObject("FirePoint").transform;
                firePoint.SetParent(player);
            }

            firePoint.localPosition = new Vector3(0f, 0.45f, 0f);
            firePoint.localRotation = Quaternion.identity;
            firePoint.localScale = Vector3.one;
        }

        private static void CreateBackground(Transform root)
        {
            var bgLayer = GetOrCreate("BgLayer", root);
            var background = GameObject.Find("ScrollingBackground") ?? GameObject.Find("background-01") ?? new GameObject("ScrollingBackground");
            background.name = "ScrollingBackground";
            background.transform.SetParent(bgLayer.transform);
            background.transform.localPosition = Vector3.zero;
            background.transform.localRotation = Quaternion.identity;
            background.transform.localScale = Vector3.one;

            var sourceRenderer = AddIfMissing<SpriteRenderer>(background);
            sourceRenderer.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(BackgroundSpritePath);
            sourceRenderer.sortingOrder = -10;

            var scroller = AddIfMissing<BackgroundScroller>(background);
            scroller.Configure(sourceRenderer.sprite, 2.1f);
        }

        private static void EnsureCamera()
        {
            var camera = Camera.main;
            if (camera == null)
            {
                return;
            }

            camera.orthographic = true;
            camera.orthographicSize = DesignOrthographicSize;
        }

        private static void EnsureProjectLayers()
        {
            foreach (var layerName in ProjectLayerNames)
            {
                AddProjectLayer(layerName);
            }
        }

        private static void AddProjectLayer(string layerName)
        {
            if (LayerMask.NameToLayer(layerName) >= 0)
            {
                return;
            }

            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tagManager.FindProperty("layers");

            for (var index = 8; index < layers.arraySize; index++)
            {
                var layer = layers.GetArrayElementAtIndex(index);
                if (!string.IsNullOrEmpty(layer.stringValue))
                {
                    continue;
                }

                layer.stringValue = layerName;
                tagManager.ApplyModifiedProperties();
                return;
            }

            Debug.LogWarning($"No empty Unity layer slot is available for {layerName}.");
        }

        private static void EnsureFolder(string parent, string folderName)
        {
            var path = $"{parent}/{folderName}";
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, folderName);
            }
        }

        private static void SetLayerRecursively(GameObject target, int layer)
        {
            target.layer = layer;

            foreach (Transform child in target.transform)
            {
                SetLayerRecursively(child.gameObject, layer);
            }
        }
    }
}
