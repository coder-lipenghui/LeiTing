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
            AddIfMissing<StageManager>(managers);
            AddIfMissing<UIManager>(managers);
            AddIfMissing<AudioManager>(managers);

            CreatePlayer(root.transform);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
            Debug.Log("LeiTing demo scene skeleton created.");
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
            var player = GetOrCreate("Player", root);
            player.transform.position = new Vector3(0f, -3.5f, 0f);

            var playerLayer = LayerMask.NameToLayer("Player");
            if (playerLayer >= 0)
            {
                player.layer = playerLayer;
            }

            var body = AddIfMissing<Rigidbody2D>(player);
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            body.freezeRotation = true;

            var hitbox = AddIfMissing<CircleCollider2D>(player);
            hitbox.isTrigger = true;
            hitbox.radius = 0.18f;

            AddIfMissing<SpriteRenderer>(player);
            AddIfMissing<PlayerController>(player);
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
    }
}
