using LeiTing.Enemy;
using LeiTing.Missiles;
using UnityEditor;
using UnityEngine;

namespace LeiTing.EditorTools
{
    public static class PrefabSetupUtility
    {
        private const string MissilePrefabRoot = "Assets/Prefabs/Missiles";
        private const string EnemyPrefabRoot = "Assets/Prefabs/Enemies";

        [MenuItem("LeiTing/Setup/Create Missile And Helicopter Prefabs")]
        public static void CreateMissileAndHelicopterPrefabs()
        {
            EnsureFolder("Assets/Prefabs", "Missiles");

            CreateMissilePrefab(
                "missile_01_straight",
                "Assets/Art/Sprites/Bullets/missile_01.png",
                0.16f,
                new Color(1f, 0.76f, 0.23f, 1f));
            CreateMissilePrefab(
                "missile_03_weak_homing",
                "Assets/Art/Sprites/Bullets/missile_03.png",
                0.17f,
                new Color(1f, 0.25f, 0.45f, 1f));
            CreateMissilePrefab(
                "missile_09_lock_dash",
                "Assets/Art/Sprites/Bullets/missile_09.png",
                0.19f,
                new Color(0.82f, 0.92f, 1f, 1f));
            CreateMissilePrefab(
                "missile_11_explode",
                "Assets/Art/Sprites/Bullets/missile_11.png",
                0.18f,
                new Color(1f, 0.22f, 0.12f, 1f));

            for (var index = 1; index <= 6; index++)
            {
                CreateHelicopterBossPrefab(index);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Missile and helicopter prefabs generated.");
        }

        private static void CreateMissilePrefab(string prefabName, string spritePath, float radius, Color trailColor)
        {
            var root = new GameObject(prefabName);
            SetLayerRecursively(root, "EnemyMissile");

            var body = root.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            var collider = root.AddComponent<CircleCollider2D>();
            collider.isTrigger = true;
            collider.radius = radius;

            root.AddComponent<MissileController>();

            var trail = root.AddComponent<TrailRenderer>();
            trail.time = 0.24f;
            trail.startWidth = radius * 0.78f;
            trail.endWidth = 0.01f;
            trail.startColor = new Color(trailColor.r, trailColor.g, trailColor.b, 0.62f);
            trail.endColor = new Color(trailColor.r, trailColor.g, trailColor.b, 0f);
            trail.sortingOrder = 21;

            var visual = CreateChild(root.transform, "Visual", Vector3.zero);
            var renderer = visual.gameObject.AddComponent<SpriteRenderer>();
            renderer.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
            renderer.sortingOrder = 22;

            PrefabUtility.SaveAsPrefabAsset(root, $"{MissilePrefabRoot}/{prefabName}.prefab");
            UnityEngine.Object.DestroyImmediate(root);
        }

        private static void CreateHelicopterBossPrefab(int index)
        {
            var prefabName = $"boss_helicopter_{index:00}";
            var root = new GameObject(prefabName);
            SetLayerRecursively(root, "Enemy");

            var body = root.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            var collider = root.AddComponent<CircleCollider2D>();
            collider.isTrigger = true;
            collider.radius = Mathf.Lerp(0.62f, 0.86f, (index - 1) / 5f);

            root.AddComponent<ActorMounts>();
            root.AddComponent<BossController>();

            var visual = CreateChild(root.transform, "Visual", Vector3.zero);
            visual.localScale = Vector3.one * Mathf.Lerp(1.0f, 1.35f, (index - 1) / 5f);
            var renderer = visual.gameObject.AddComponent<SpriteRenderer>();
            renderer.sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/Art/Animations/Enemies/helicopter_{index:00}.png");
            renderer.flipY = true;
            renderer.sortingOrder = 25;

            var firePoints = CreateChild(root.transform, "FirePoints", Vector3.zero);
            AddFirePointGroup(firePoints, "center", new Vector2(0f, -0.72f));
            AddFirePointGroup(firePoints, "scatter_2", new Vector2(-0.48f, -0.42f), new Vector2(0.48f, -0.42f));
            AddFirePointGroup(firePoints, "missile_straight_2", new Vector2(-0.7f, -0.2f), new Vector2(0.7f, -0.2f));
            AddFirePointGroup(firePoints, "missile_homing_2", new Vector2(-0.86f, -0.05f), new Vector2(0.86f, -0.05f));
            AddFirePointGroup(firePoints, "missile_lock_2", new Vector2(-0.6f, -0.68f), new Vector2(0.6f, -0.68f));
            AddFirePointGroup(firePoints, "missile_explode_2", new Vector2(-0.34f, -0.9f), new Vector2(0.34f, -0.9f));
            AddFirePointGroup(firePoints, "missile_straight_3", new Vector2(-0.84f, -0.2f), new Vector2(0f, -0.62f), new Vector2(0.84f, -0.2f));
            AddFirePointGroup(firePoints, "missile_homing_3", new Vector2(-0.96f, 0.02f), new Vector2(0f, -0.52f), new Vector2(0.96f, 0.02f));
            AddFirePointGroup(firePoints, "missile_lock_3", new Vector2(-0.68f, -0.66f), new Vector2(0f, -0.92f), new Vector2(0.68f, -0.66f));
            AddFirePointGroup(firePoints, "missile_explode_3", new Vector2(-0.52f, -0.92f), new Vector2(0f, -1.04f), new Vector2(0.52f, -0.92f));

            PrefabUtility.SaveAsPrefabAsset(root, $"{EnemyPrefabRoot}/{prefabName}.prefab");
            UnityEngine.Object.DestroyImmediate(root);
        }

        private static Transform CreateChild(Transform parent, string name, Vector3 localPosition)
        {
            var child = new GameObject(name).transform;
            child.SetParent(parent, false);
            child.localPosition = localPosition;
            child.localRotation = Quaternion.identity;
            child.localScale = Vector3.one;
            SetLayerRecursively(child.gameObject, parent.gameObject.layer);
            return child;
        }

        private static void AddFirePointGroup(Transform firePointsRoot, string groupName, params Vector2[] localPositions)
        {
            if (localPositions == null || localPositions.Length == 0)
            {
                return;
            }

            var group = CreateChild(firePointsRoot, groupName, localPositions.Length == 1 ? (Vector3)localPositions[0] : Vector3.zero);
            if (localPositions.Length == 1)
            {
                return;
            }

            for (var index = 0; index < localPositions.Length; index++)
            {
                CreateChild(group, $"p{index + 1}", localPositions[index]);
            }
        }

        private static void EnsureFolder(string parent, string folderName)
        {
            var path = $"{parent}/{folderName}";
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, folderName);
            }
        }

        private static void SetLayerRecursively(GameObject target, string layerName)
        {
            var layer = LayerMask.NameToLayer(layerName);
            if (layer < 0)
            {
                return;
            }

            SetLayerRecursively(target, layer);
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
