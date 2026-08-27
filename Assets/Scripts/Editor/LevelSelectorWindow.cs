using System;
using System.Collections.Generic;
using System.Globalization;
using LeiTing.Config;
using LeiTing.Core;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LeiTing.EditorTools
{
    public sealed class LevelSelectorWindow : EditorWindow
    {
        private const int ButtonColumns = 3;
        private const float TimelineTailSeconds = 60f;
        private const float TimelineMinimumSeconds = 60f;
        private const float EventPreviewLeadSeconds = 5f;
        private const string PendingPlayRequestKey = "LeiTing.Editor.PendingQuickBattleStart";
        private const string QuickCommandControlName = "LeiTingQuickBattleCommand";

        private static readonly Color TimelineBackgroundColor = new Color(0.12f, 0.14f, 0.18f, 1f);
        private static readonly Color WaveMarkerColor = new Color(0.32f, 0.78f, 0.48f, 1f);
        private static readonly Color EventMarkerColor = new Color(0.28f, 0.7f, 1f, 1f);
        private static readonly Color BossMarkerColor = new Color(1f, 0.52f, 0.18f, 1f);
        private static readonly Color CurrentTimeColor = new Color(1f, 1f, 1f, 0.92f);

        private readonly List<TimelineMarker> timelineMarkers = new List<TimelineMarker>();
        private GameConfig config;
        private Vector2 scrollPosition;
        private string quickCommand;
        private string startTimeText;
        private string validationMessage;
        private float selectedStartTime;
        private bool timelineDragging;
        private float timelineDragStartTime;

        private enum TimelineMarkerKind
        {
            Wave,
            Event,
            Boss
        }

        private sealed class TimelineMarker
        {
            public float time;
            public string label;
            public TimelineMarkerKind kind;
        }

        [MenuItem("LeiTing/Test/Level Selector")]
        public static void Open()
        {
            var window = GetWindow<LevelSelectorWindow>("关卡快速测试");
            window.minSize = new Vector2(420f, 560f);
            window.RefreshConfig();
            window.Show();
        }

        private void OnEnable()
        {
            EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
            selectedStartTime = Mathf.Max(0f, GameManager.RequestedBattleStartTime);
            SyncInputText();
            RefreshConfig();
        }

        private void OnDisable()
        {
            EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
        }

        private void OnInspectorUpdate()
        {
            if (EditorApplication.isPlaying)
            {
                Repaint();
            }
        }

        private void OnGUI()
        {
            DrawToolbar();

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            DrawQuickCommand();
            DrawStatus();
            DrawTimeline();
            DrawLevelGrid();
            EditorGUILayout.EndScrollView();
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label("关卡快速测试", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();

                if (GUILayout.Button("刷新配置", EditorStyles.toolbarButton, GUILayout.Width(72f)))
                {
                    RefreshConfig();
                }
            }
        }

        private void DrawQuickCommand()
        {
            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField("快捷指令", EditorStyles.boldLabel);

            var submitWithKeyboard = false;
            using (new EditorGUILayout.HorizontalScope())
            {
                GUI.SetNextControlName(QuickCommandControlName);
                quickCommand = EditorGUILayout.TextField(quickCommand ?? string.Empty);
                submitWithKeyboard = Event.current.type == EventType.KeyDown
                    && (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter)
                    && GUI.GetNameOfFocusedControl() == QuickCommandControlName;

                if (GUILayout.Button(EditorApplication.isPlaying ? "重载" : "开始", GUILayout.Width(72f)))
                {
                    RunQuickCommand();
                }
            }

            EditorGUILayout.LabelField("格式：关卡#分:秒，例如 2#1:35；也支持 2#95。", EditorStyles.miniLabel);

            if (submitWithKeyboard)
            {
                Event.current.Use();
                RunQuickCommand();
            }

            if (!string.IsNullOrEmpty(validationMessage))
            {
                EditorGUILayout.HelpBox(validationMessage, MessageType.Warning);
            }
        }

        private void DrawStatus()
        {
            var levelCount = GetLevelCount();
            if (levelCount <= 0)
            {
                EditorGUILayout.HelpBox("未加载到 Luban 关卡配置。请运行 Luban/gen.sh 后刷新。", MessageType.Error);
                return;
            }

            var requestedLevel = GetRequestedLevelNumber();
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField(
                "下一次 Play 进入",
                $"{FormatLevelTitle(requestedLevel)} / {FormatStageTime(selectedStartTime)}",
                EditorStyles.boldLabel);

            if (EditorApplication.isPlaying && GameManager.Instance != null)
            {
                var elapsedTime = FormatStageTime(GameManager.Instance.BattleElapsedTime);
                var contentTime = FormatStageTime(GameManager.Instance.BattleTimelineTime);
                var timeText = GameManager.Instance.IsBattleTimelinePaused
                    ? $"总时间 {elapsedTime} / 刷怪时间 {contentTime}（Boss 暂停）"
                    : elapsedTime;
                EditorGUILayout.LabelField(
                    "当前运行",
                    $"{FormatLevelTitle(GameManager.Instance.CurrentLevelNumber)} / {timeText}");
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "未运行时修改的是下一次 Play 的起点；运行中拖动时间轴并松手，会从目标时间干净重载关卡。",
                    MessageType.Info);
            }
        }

        private void DrawTimeline()
        {
            if (GetLevelCount() <= 0)
            {
                return;
            }

            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField("测试时间轴", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PrefixLabel("起始时间");
                var enteredTime = EditorGUILayout.TextField(startTimeText ?? FormatStageTime(selectedStartTime));
                if (!string.Equals(enteredTime, startTimeText, StringComparison.Ordinal))
                {
                    startTimeText = enteredTime;
                    if (TryParseTime(startTimeText, out var parsedTime, out _))
                    {
                        SetSelectedStartTime(parsedTime, false);
                        validationMessage = null;
                    }
                }

                if (GUILayout.Button(EditorApplication.isPlaying ? "从此处重载" : "从此处开始", GUILayout.Width(104f)))
                {
                    CommitTimeAndStart();
                }
            }

            BuildTimelineMarkers(GetRequestedLevelNumber());
            var timelineEnd = ResolveTimelineEndTime();
            DrawTimelineSlider(timelineEnd);

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("00:00", EditorStyles.miniLabel, GUILayout.Width(52f));
                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField(FormatStageTime(timelineEnd), EditorStyles.miniLabel, GUILayout.Width(52f));
            }

            DrawEventNavigation();
            EditorGUILayout.LabelField("绿色=普通波次　蓝色=关卡事件　橙色=Boss　白色=当前运行时间", EditorStyles.miniLabel);
        }

        private void DrawTimelineSlider(float timelineEnd)
        {
            var sliderRect = GUILayoutUtility.GetRect(20f, 24f, GUILayout.ExpandWidth(true));
            var trackRect = new Rect(sliderRect.x, sliderRect.y + 7f, sliderRect.width, 10f);
            EditorGUI.DrawRect(trackRect, TimelineBackgroundColor);

            foreach (var marker in timelineMarkers)
            {
                DrawTimelineMarker(trackRect, timelineEnd, marker);
            }

            if (EditorApplication.isPlaying && GameManager.Instance != null)
            {
                DrawCurrentTimeMarker(trackRect, timelineEnd, GameManager.Instance.BattleElapsedTime);
            }

            var currentEvent = Event.current;
            if (currentEvent.type == EventType.MouseDown && sliderRect.Contains(currentEvent.mousePosition))
            {
                timelineDragging = true;
                timelineDragStartTime = selectedStartTime;
            }

            EditorGUI.BeginChangeCheck();
            var newTime = GUI.HorizontalSlider(sliderRect, selectedStartTime, 0f, timelineEnd);
            if (EditorGUI.EndChangeCheck())
            {
                SetSelectedStartTime(newTime, true);
            }

            var releasedAfterDrag = timelineDragging && currentEvent.rawType == EventType.MouseUp;
            if (releasedAfterDrag)
            {
                timelineDragging = false;
                if (!Mathf.Approximately(timelineDragStartTime, selectedStartTime))
                {
                    ApplyTimelineSelection();
                }
            }
        }

        private void DrawTimelineMarker(Rect trackRect, float timelineEnd, TimelineMarker marker)
        {
            if (marker == null || timelineEnd <= 0f)
            {
                return;
            }

            var normalizedTime = Mathf.Clamp01(marker.time / timelineEnd);
            var x = Mathf.Lerp(trackRect.xMin, trackRect.xMax, normalizedTime);
            var markerRect = new Rect(x - 1f, trackRect.y - 3f, 2f, trackRect.height + 6f);
            EditorGUI.DrawRect(markerRect, ResolveMarkerColor(marker.kind));
            GUI.Label(
                new Rect(x - 5f, trackRect.y - 5f, 10f, trackRect.height + 10f),
                new GUIContent(string.Empty, $"{FormatStageTime(marker.time)}  {marker.label}"));
        }

        private static void DrawCurrentTimeMarker(Rect trackRect, float timelineEnd, float currentTime)
        {
            if (timelineEnd <= 0f)
            {
                return;
            }

            var normalizedTime = Mathf.Clamp01(currentTime / timelineEnd);
            var x = Mathf.Lerp(trackRect.xMin, trackRect.xMax, normalizedTime);
            EditorGUI.DrawRect(new Rect(x - 1f, trackRect.y - 5f, 2f, trackRect.height + 10f), CurrentTimeColor);
        }

        private void DrawEventNavigation()
        {
            var previousMarker = FindPreviousMarker(selectedStartTime);
            var nextMarker = FindNextMarker(selectedStartTime);
            var previewMarker = nextMarker ?? FindMarkerAtTime(selectedStartTime) ?? FindLastMarker();

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(previousMarker == null))
                {
                    if (GUILayout.Button("上一个事件"))
                    {
                        SelectMarker(previousMarker, false);
                    }
                }

                using (new EditorGUI.DisabledScope(previewMarker == null))
                {
                    if (GUILayout.Button("提前 5 秒测试"))
                    {
                        SelectMarker(previewMarker, true);
                    }
                }

                using (new EditorGUI.DisabledScope(nextMarker == null))
                {
                    if (GUILayout.Button("下一个事件"))
                    {
                        SelectMarker(nextMarker, false);
                    }
                }
            }

            if (nextMarker != null)
            {
                EditorGUILayout.LabelField(
                    $"下一事件：{FormatStageTime(nextMarker.time)}  {nextMarker.label}",
                    EditorStyles.miniLabel);
            }
        }

        private void DrawLevelGrid()
        {
            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField("选择关卡", EditorStyles.boldLabel);

            var levelCount = GetLevelCount();
            if (levelCount <= 0)
            {
                return;
            }

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

                        var isSelected = levelNumber == GetRequestedLevelNumber();
                        var buttonStyle = isSelected ? EditorStyles.miniButtonMid : EditorStyles.miniButton;
                        if (GUILayout.Button(FormatButtonLabel(levelNumber), buttonStyle, GUILayout.Height(58f)))
                        {
                            SelectLevel(levelNumber);
                        }
                    }
                }
            }
        }

        private void RunQuickCommand()
        {
            if (!TryParseQuickCommand(quickCommand, out var levelNumber, out var startTime, out var error))
            {
                validationMessage = error;
                return;
            }

            validationMessage = null;
            GameManager.RequestLevel(levelNumber);
            SetSelectedStartTime(startTime, true);
            StartOrReloadSelectedBattle();
        }

        private void CommitTimeAndStart()
        {
            if (!TryParseTime(startTimeText, out var startTime, out var error))
            {
                validationMessage = error;
                return;
            }

            validationMessage = null;
            SetSelectedStartTime(startTime, true);
            StartOrReloadSelectedBattle();
        }

        private void SelectLevel(int levelNumber)
        {
            GameManager.RequestLevel(levelNumber);
            SyncInputText();
            validationMessage = null;

            if (EditorApplication.isPlaying)
            {
                StartOrReloadSelectedBattle();
                return;
            }

            Debug.Log($"LeiTing test level selected: {FormatLevelTitle(levelNumber)} at {FormatStageTime(selectedStartTime)}");
            Repaint();
        }

        private void SelectMarker(TimelineMarker marker, bool usePreviewLead)
        {
            if (marker == null)
            {
                return;
            }

            var targetTime = usePreviewLead ? Mathf.Max(0f, marker.time - EventPreviewLeadSeconds) : marker.time;
            SetSelectedStartTime(targetTime, true);
            ApplyTimelineSelection();
        }

        private void ApplyTimelineSelection()
        {
            GameManager.RequestLevel(GetRequestedLevelNumber());
            GameManager.RequestBattleStartTime(selectedStartTime);

            if (EditorApplication.isPlaying)
            {
                StartOrReloadSelectedBattle();
            }

            Repaint();
        }

        private void StartOrReloadSelectedBattle()
        {
            var levelNumber = GetRequestedLevelNumber();
            GameManager.RequestLevel(levelNumber);
            GameManager.RequestBattleStartTime(selectedStartTime);
            SyncInputText();

            if (!EditorApplication.isPlaying)
            {
                SessionState.SetBool(PendingPlayRequestKey, true);
                EditorApplication.isPlaying = true;
                return;
            }

            if (GameManager.Instance != null)
            {
                GameManager.Instance.LoadLevelForTesting(levelNumber, selectedStartTime);
                return;
            }

            GameSceneManager.GetOrCreate().EnterBattleForTesting(levelNumber, selectedStartTime);
        }

        private void HandlePlayModeStateChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(PendingPlayRequestKey, false))
            {
                return;
            }

            SessionState.SetBool(PendingPlayRequestKey, false);
            EditorApplication.delayCall += EnterRequestedBattleAfterPlayStarts;
        }

        private static void EnterRequestedBattleAfterPlayStarts()
        {
            if (!EditorApplication.isPlaying
                || GameSceneManager.IsBattleSceneName(SceneManager.GetActiveScene().name))
            {
                return;
            }

            GameSceneManager.GetOrCreate().EnterBattleForTesting(
                GameManager.RequestedLevelNumber,
                GameManager.RequestedBattleStartTime);
        }

        private void SetSelectedStartTime(float startTime, bool normalizeInputText)
        {
            selectedStartTime = Mathf.Max(0f, Mathf.Round(startTime));
            GameManager.RequestBattleStartTime(selectedStartTime);
            quickCommand = FormatQuickCommand(GetRequestedLevelNumber(), selectedStartTime);
            if (normalizeInputText)
            {
                startTimeText = FormatStageTime(selectedStartTime);
            }
        }

        private void SyncInputText()
        {
            startTimeText = FormatStageTime(selectedStartTime);
            quickCommand = FormatQuickCommand(GetRequestedLevelNumber(), selectedStartTime);
        }

        private void RefreshConfig()
        {
            config = LubanConfigLoader.TryLoad(out var lubanConfig) ? lubanConfig : null;
            BuildTimelineMarkers(GetRequestedLevelNumber());
            Repaint();
        }

        private void BuildTimelineMarkers(int levelNumber)
        {
            timelineMarkers.Clear();
            if (config == null)
            {
                return;
            }

            if (config.waves != null)
            {
                foreach (var wave in config.waves)
                {
                    if (wave == null || !IsWaveForLevel(wave, levelNumber))
                    {
                        continue;
                    }

                    var bossName = ResolveBossName(wave);
                    timelineMarkers.Add(new TimelineMarker
                    {
                        time = Mathf.Max(0f, wave.startTime),
                        label = string.IsNullOrEmpty(bossName) ? "敌机波次" : bossName,
                        kind = string.IsNullOrEmpty(bossName) ? TimelineMarkerKind.Wave : TimelineMarkerKind.Boss
                    });
                }
            }

            if (config.stageEvents != null)
            {
                foreach (var stageEvent in config.stageEvents)
                {
                    if (stageEvent == null || !IsStageEventForLevel(stageEvent, levelNumber))
                    {
                        continue;
                    }

                    timelineMarkers.Add(new TimelineMarker
                    {
                        time = Mathf.Max(0f, stageEvent.startTime),
                        label = ResolveStageEventLabel(stageEvent),
                        kind = TimelineMarkerKind.Event
                    });
                }
            }

            timelineMarkers.Sort((left, right) => left.time.CompareTo(right.time));
        }

        private float ResolveTimelineEndTime()
        {
            var lastMarkerTime = timelineMarkers.Count > 0 ? timelineMarkers[timelineMarkers.Count - 1].time : 0f;
            var requiredTime = Mathf.Max(TimelineMinimumSeconds, lastMarkerTime + TimelineTailSeconds, selectedStartTime + 10f);
            return Mathf.Ceil(requiredTime / 30f) * 30f;
        }

        private TimelineMarker FindPreviousMarker(float time)
        {
            for (var index = timelineMarkers.Count - 1; index >= 0; index--)
            {
                if (timelineMarkers[index].time < time - 0.01f)
                {
                    return timelineMarkers[index];
                }
            }

            return null;
        }

        private TimelineMarker FindNextMarker(float time)
        {
            foreach (var marker in timelineMarkers)
            {
                if (marker.time > time + 0.01f)
                {
                    return marker;
                }
            }

            return null;
        }

        private TimelineMarker FindMarkerAtTime(float time)
        {
            foreach (var marker in timelineMarkers)
            {
                if (Mathf.Abs(marker.time - time) <= 0.01f)
                {
                    return marker;
                }
            }

            return null;
        }

        private TimelineMarker FindLastMarker()
        {
            return timelineMarkers.Count > 0 ? timelineMarkers[timelineMarkers.Count - 1] : null;
        }

        private int GetLevelCount()
        {
            return config != null && config.levels != null && config.levels.Count > 0
                ? config.levels.Count
                : 0;
        }

        private int GetRequestedLevelNumber()
        {
            var levelCount = GetLevelCount();
            return levelCount > 0
                ? Mathf.Clamp(GameManager.RequestedLevelNumber, 1, levelCount)
                : Mathf.Max(1, GameManager.RequestedLevelNumber);
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
            var bossId = ResolveBossId(levelNumber);
            return ResolveEnemyDisplayName(bossId);
        }

        private string ResolveBossName(WaveConfig wave)
        {
            if (wave?.spawns == null)
            {
                return string.Empty;
            }

            foreach (var spawn in wave.spawns)
            {
                if (spawn != null && IsBossId(spawn.enemyId))
                {
                    return ResolveEnemyDisplayName(spawn.enemyId);
                }
            }

            return string.Empty;
        }

        private string ResolveEnemyDisplayName(string enemyId)
        {
            if (string.IsNullOrEmpty(enemyId))
            {
                return string.Empty;
            }

            var enemies = config != null ? config.enemies : null;
            if (enemies != null)
            {
                foreach (var enemy in enemies)
                {
                    if (enemy != null && string.Equals(enemy.id, enemyId, StringComparison.OrdinalIgnoreCase))
                    {
                        return string.IsNullOrEmpty(enemy.displayName) ? enemyId : enemy.displayName;
                    }
                }
            }

            return enemyId;
        }

        private string ResolveBossId(int levelNumber)
        {
            if (config?.waves == null)
            {
                return string.Empty;
            }

            var bossId = string.Empty;
            var bossStartTime = float.NegativeInfinity;

            foreach (var wave in config.waves)
            {
                if (wave == null || wave.spawns == null || !IsWaveForLevel(wave, levelNumber))
                {
                    continue;
                }

                foreach (var spawn in wave.spawns)
                {
                    if (spawn != null && IsBossId(spawn.enemyId) && wave.startTime >= bossStartTime)
                    {
                        bossId = spawn.enemyId;
                        bossStartTime = wave.startTime;
                    }
                }
            }

            return bossId;
        }

        private bool IsWaveForLevel(WaveConfig wave, int levelNumber)
        {
            return wave != null && IsConfigForLevel(wave.levelId, levelNumber);
        }

        private bool IsStageEventForLevel(StageEventConfig stageEvent, int levelNumber)
        {
            return stageEvent != null && IsConfigForLevel(stageEvent.levelId, levelNumber);
        }

        private bool IsConfigForLevel(string levelId, int levelNumber)
        {
            if (string.IsNullOrEmpty(levelId))
            {
                return true;
            }

            var level = ResolveLevel(levelNumber);
            return string.Equals(levelId, level?.id, StringComparison.OrdinalIgnoreCase)
                || string.Equals(levelId, levelNumber.ToString(), StringComparison.OrdinalIgnoreCase)
                || string.Equals(levelId, $"level_{levelNumber}", StringComparison.OrdinalIgnoreCase)
                || string.Equals(levelId, $"level_{levelNumber:00}", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsBossId(string enemyId)
        {
            return !string.IsNullOrEmpty(enemyId)
                && enemyId.StartsWith("boss", StringComparison.OrdinalIgnoreCase);
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

        private static string ResolveStageEventLabel(StageEventConfig stageEvent)
        {
            if (stageEvent == null)
            {
                return "关卡事件";
            }

            if (!string.IsNullOrEmpty(stageEvent.message))
            {
                return stageEvent.message;
            }

            if (!string.IsNullOrEmpty(stageEvent.id)
                && stageEvent.id.StartsWith("spawn_trophy", StringComparison.OrdinalIgnoreCase))
            {
                return "奖杯";
            }

            return stageEvent.clearEnemyBullets ? "清除弹幕" : "关卡事件";
        }

        private static Color ResolveMarkerColor(TimelineMarkerKind kind)
        {
            switch (kind)
            {
                case TimelineMarkerKind.Boss:
                    return BossMarkerColor;
                case TimelineMarkerKind.Event:
                    return EventMarkerColor;
                default:
                    return WaveMarkerColor;
            }
        }

        private bool TryParseQuickCommand(string command, out int levelNumber, out float startTime, out string error)
        {
            levelNumber = 0;
            startTime = 0f;
            error = null;

            var normalized = command != null ? command.Trim() : string.Empty;
            var separatorIndex = normalized.IndexOf('#');
            if (separatorIndex <= 0 || separatorIndex != normalized.LastIndexOf('#'))
            {
                error = "快捷指令格式不正确，请输入类似 2#1:35。";
                return false;
            }

            var levelText = normalized.Substring(0, separatorIndex).Trim();
            if (!int.TryParse(levelText, NumberStyles.Integer, CultureInfo.InvariantCulture, out levelNumber))
            {
                error = "# 前面必须是关卡数字。";
                return false;
            }

            var levelCount = GetLevelCount();
            if (levelNumber < 1 || levelNumber > levelCount)
            {
                error = $"关卡必须在 1 到 {levelCount} 之间。";
                return false;
            }

            var timeText = normalized.Substring(separatorIndex + 1).Trim();
            return TryParseTime(timeText, out startTime, out error);
        }

        private static bool TryParseTime(string value, out float time, out string error)
        {
            time = 0f;
            error = null;
            var normalized = value != null ? value.Trim() : string.Empty;
            if (string.IsNullOrEmpty(normalized))
            {
                error = "请输入起始时间，例如 1:35 或 95。";
                return false;
            }

            var timeParts = normalized.Split(':');
            if (timeParts.Length == 1)
            {
                if (!float.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out time) || time < 0f)
                {
                    error = "秒数必须是大于等于 0 的数字。";
                    return false;
                }

                time = Mathf.Round(time);
                return true;
            }

            if (timeParts.Length != 2
                || !int.TryParse(timeParts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var minutes)
                || !int.TryParse(timeParts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds)
                || minutes < 0
                || seconds < 0
                || seconds >= 60)
            {
                error = "时间格式不正确，请使用 分:秒，例如 1:35。";
                return false;
            }

            time = minutes * 60f + seconds;
            return true;
        }

        private static string FormatQuickCommand(int levelNumber, float startTime)
        {
            return $"{Mathf.Max(1, levelNumber)}#{FormatStageTime(startTime)}";
        }

        private static string FormatStageTime(float time)
        {
            var totalSeconds = Mathf.Max(0, Mathf.FloorToInt(time));
            return $"{totalSeconds / 60:00}:{totalSeconds % 60:00}";
        }
    }
}
