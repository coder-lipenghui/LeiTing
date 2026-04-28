using System;
using LeiTing.Config;
using LeiTing.Core;
using UnityEditor;
using UnityEngine;

namespace LeiTing.EditorTools
{
    public sealed class LevelSelectorWindow : EditorWindow
    {
        private const string ConfigPath = "Assets/Resources/Configs/GameConfig.json";
        private const int FallbackLevelCount = 12;
        private const int ButtonColumns = 3;

        private GameConfig config;
        private Vector2 scrollPosition;

        [MenuItem("LeiTing/Test/Level Selector")]
        public static void Open()
        {
            var window = GetWindow<LevelSelectorWindow>("Level Selector");
            window.minSize = new Vector2(360f, 420f);
            window.RefreshConfig();
            window.Show();
        }

        private void OnEnable()
        {
            RefreshConfig();
        }

        private void OnGUI()
        {
            DrawToolbar();

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            DrawStatus();
            DrawLevelGrid();
            EditorGUILayout.EndScrollView();
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label("关卡测试入口", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();

                if (GUILayout.Button("刷新配置", EditorStyles.toolbarButton, GUILayout.Width(72f)))
                {
                    RefreshConfig();
                }
            }
        }

        private void DrawStatus()
        {
            var requestedLevel = Mathf.Clamp(GameManager.RequestedLevelNumber, 1, GetLevelCount());
            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField("下一次 Play 进入", FormatLevelTitle(requestedLevel), EditorStyles.boldLabel);

            if (EditorApplication.isPlaying && GameManager.Instance != null)
            {
                EditorGUILayout.LabelField("当前运行关卡", FormatLevelTitle(GameManager.Instance.CurrentLevelNumber));
            }
            else
            {
                EditorGUILayout.HelpBox("未运行时选择关卡，会在下一次进入 Play 时生效。运行中选择关卡，会立刻重载到所选关卡。", MessageType.Info);
            }

            using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
            {
                if (GUILayout.Button("从选中关卡开始 Play", GUILayout.Height(32f)))
                {
                    EditorApplication.isPlaying = true;
                }
            }
        }

        private void DrawLevelGrid()
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("选择关卡", EditorStyles.boldLabel);

            var levelCount = GetLevelCount();
            for (var index = 0; index < levelCount; index += ButtonColumns)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    for (var column = 0; column < ButtonColumns; column++)
                    {
                        var levelNumber = index + column + 1;
                        if (levelNumber > levelCount)
                        {
                            GUILayout.FlexibleSpace();
                            continue;
                        }

                        var isSelected = levelNumber == Mathf.Clamp(GameManager.RequestedLevelNumber, 1, levelCount);
                        var buttonStyle = isSelected ? EditorStyles.miniButtonMid : EditorStyles.miniButton;
                        if (GUILayout.Button(FormatButtonLabel(levelNumber), buttonStyle, GUILayout.Height(58f)))
                        {
                            SelectLevel(levelNumber);
                        }
                    }
                }
            }
        }

        private void SelectLevel(int levelNumber)
        {
            GameManager.RequestLevel(levelNumber);

            if (EditorApplication.isPlaying && GameManager.Instance != null)
            {
                GameManager.Instance.LoadLevel(levelNumber);
                return;
            }

            Debug.Log($"LeiTing test level selected: {FormatLevelTitle(levelNumber)}");
            Repaint();
        }

        private void RefreshConfig()
        {
            var configAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(ConfigPath);
            config = configAsset != null ? JsonUtility.FromJson<GameConfig>(configAsset.text) : null;
            Repaint();
        }

        private int GetLevelCount()
        {
            return config != null && config.levels != null && config.levels.Count > 0
                ? config.levels.Count
                : FallbackLevelCount;
        }

        private string FormatButtonLabel(int levelNumber)
        {
            var bossName = ResolveBossName(levelNumber);
            return string.IsNullOrEmpty(bossName)
                ? $"第 {levelNumber} 关"
                : $"第 {levelNumber} 关\n{bossName}";
        }

        private string FormatLevelTitle(int levelNumber)
        {
            var level = ResolveLevel(levelNumber);
            var levelName = level != null && !string.IsNullOrEmpty(level.displayName)
                ? level.displayName
                : $"第 {levelNumber} 关";
            var bossName = ResolveBossName(levelNumber);
            return string.IsNullOrEmpty(bossName) ? levelName : $"{levelName} / {bossName}";
        }

        private string ResolveBossName(int levelNumber)
        {
            var level = ResolveLevel(levelNumber);
            var bossId = level != null && !string.IsNullOrEmpty(level.bossId)
                ? level.bossId
                : $"boss_{levelNumber:00}";
            var enemies = config != null ? config.enemies : null;

            if (enemies == null)
            {
                return bossId;
            }

            foreach (var enemy in enemies)
            {
                if (enemy != null && string.Equals(enemy.id, bossId, StringComparison.OrdinalIgnoreCase))
                {
                    return string.IsNullOrEmpty(enemy.displayName) ? bossId : enemy.displayName;
                }
            }

            return bossId;
        }

        private LevelConfig ResolveLevel(int levelNumber)
        {
            var levels = config != null ? config.levels : null;
            if (levels == null || levels.Count == 0)
            {
                return null;
            }

            var index = Mathf.Clamp(levelNumber - 1, 0, levels.Count - 1);
            return levels[index];
        }
    }
}
