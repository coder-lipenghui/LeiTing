using LeiTing.Audio;
using LeiTing.Bullets;
using LeiTing.Config;
using LeiTing.Core;
using LeiTing.Enemy;
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

        [MenuItem("LeiTing/Setup/Create Demo Scene Skeleton")]
        public static void CreateDemoSceneSkeleton()
        {
            EnsureSceneLoaded();

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

        private static void AddIfMissing<T>(GameObject target) where T : Component
        {
            if (target.GetComponent<T>() == null)
            {
                target.AddComponent<T>();
            }
        }
    }
}
