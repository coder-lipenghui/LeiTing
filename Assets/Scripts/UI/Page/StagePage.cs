using System.Collections.Generic;
using LeiTing.Config;
using LeiTing.Core;
using LeiTing.Progress;
using Luban.SimpleJSON;
using UnityEngine;
using UnityEngine.UI;

namespace LeiTing.UI
{
    public class StagePage : BasePage
    {
        private const int ColumnCount = 3;
        private const int VisibleRowCount = 3;
        private const float CellWidth = 280f;
        private const float CellHeight = 230f;
        private const float GridSpacingX = 28f;
        private const float GridSpacingY = 28f;
        private const int GridPaddingLeft = 22;
        private const int GridPaddingRight = 22;
        private const int GridPaddingTop = 18;
        private const int GridPaddingBottom = 18;
        private const float ScrollViewportWidth = 960f;
        private const float StartButtonWidth = 520f;
        private const float StartButtonHeight = 118f;

        [SerializeField] private Button startButton;
        [SerializeField] private Button backButton;
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private RectTransform contentRoot;
        [SerializeField] private Text selectedStageText;
        [Header("Replaceable Sprites")]
        [SerializeField] private Sprite pageBackgroundSprite;
        [SerializeField] private Sprite scrollBackgroundSprite;
        [SerializeField] private Sprite levelNormalSprite;
        [SerializeField] private Sprite levelSelectedSprite;
        [SerializeField] private Sprite levelLockedSprite;
        [SerializeField] private Sprite startButtonSprite;
        [SerializeField] private Sprite backButtonSprite;

        private readonly List<LevelItemView> levelItems = new List<LevelItemView>();
        private int selectedLevelNumber = 1;
        private int levelCount;
        private bool battleStartRequested;

        public override void OnCreate()
        {
            BuildView();
            BindEvents();
            RefreshLevelList(true);
            Debug.Log($"[UIStage] OnCreate complete. levelCount={levelCount}, selectedLevel={selectedLevelNumber}");
        }

        public override void OnShow()
        {
            battleStartRequested = false;
            if (startButton != null)
            {
                startButton.interactable = true;
            }

            RefreshLevelList(true);
            Debug.Log($"[UIStage] OnShow. selectedLevel={selectedLevelNumber}, levelCount={levelCount}");
        }

        public override void OnHide()
        {
            battleStartRequested = false;
            if (startButton != null)
            {
                startButton.interactable = true;
            }
        }

        private void BuildView()
        {
            ClearChildren();
            UIFactory.Stretch(RectTransform);

            var backdrop = UIFactory.CreatePanel("StageBackdrop", transform, new Color(0.018f, 0.026f, 0.055f, 0.98f));
            UIFactory.Stretch(backdrop.rectTransform);
            ApplySprite(backdrop, pageBackgroundSprite, Image.Type.Simple);
            backdrop.raycastTarget = true;

            var title = UIFactory.CreateText("Title", transform, "选择关卡", 54f, TextAnchor.MiddleCenter, Color.white);
            var titleRect = title.rectTransform;
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.anchoredPosition = new Vector2(0f, -86f);
            titleRect.sizeDelta = new Vector2(0f, 76f);

            backButton = UIFactory.CreateButton("BtnBack", transform, "返回", new Color(0.12f, 0.18f, 0.28f, 0.96f), out _, out _);
            ApplySprite(backButton.targetGraphic as Image, backButtonSprite, Image.Type.Sliced);
            var backRect = backButton.GetComponent<RectTransform>();
            backRect.anchorMin = new Vector2(0f, 1f);
            backRect.anchorMax = new Vector2(0f, 1f);
            backRect.pivot = new Vector2(0f, 1f);
            backRect.anchoredPosition = new Vector2(40f, -42f);
            backRect.sizeDelta = new Vector2(170f, 76f);

            selectedStageText = UIFactory.CreateText("TxtSelectedStage", transform, string.Empty, 34f, TextAnchor.MiddleCenter, new Color(0.72f, 0.88f, 1f, 1f));
            var selectedRect = selectedStageText.rectTransform;
            selectedRect.anchorMin = new Vector2(0.5f, 1f);
            selectedRect.anchorMax = new Vector2(0.5f, 1f);
            selectedRect.pivot = new Vector2(0.5f, 1f);
            selectedRect.anchoredPosition = new Vector2(0f, -162f);
            selectedRect.sizeDelta = new Vector2(600f, 52f);

            BuildScrollView();

            startButton = UIFactory.CreateButton("BtnStart", transform, "开始", new Color(1f, 0.72f, 0.18f, 0.98f), out _, out _);
            ApplySprite(startButton.targetGraphic as Image, startButtonSprite, Image.Type.Sliced);
            var startRect = startButton.GetComponent<RectTransform>();
            startRect.anchorMin = new Vector2(0.5f, 0f);
            startRect.anchorMax = new Vector2(0.5f, 0f);
            startRect.pivot = new Vector2(0.5f, 0f);
            startRect.anchoredPosition = new Vector2(0f, 76f);
            startRect.sizeDelta = new Vector2(StartButtonWidth, StartButtonHeight);
        }

        private void BuildScrollView()
        {
            var scrollRoot = UIFactory.CreateRect("Scroll View", transform);
            scrollRoot.anchorMin = new Vector2(0.5f, 0.5f);
            scrollRoot.anchorMax = new Vector2(0.5f, 0.5f);
            scrollRoot.pivot = new Vector2(0.5f, 0.5f);
            scrollRoot.anchoredPosition = new Vector2(0f, 80f);
            scrollRoot.sizeDelta = new Vector2(ScrollViewportWidth, CalculateVisibleContentHeight());

            var background = scrollRoot.gameObject.AddComponent<Image>();
            background.color = new Color(0.035f, 0.052f, 0.095f, 0.58f);
            ApplySprite(background, scrollBackgroundSprite, Image.Type.Sliced);
            background.raycastTarget = true;

            var viewport = UIFactory.CreatePanel("Viewport", scrollRoot, new Color(1f, 1f, 1f, 0.001f));
            UIFactory.Stretch(viewport.rectTransform);
            viewport.raycastTarget = true;
            viewport.gameObject.AddComponent<RectMask2D>();

            contentRoot = UIFactory.CreateRect("Content", viewport.rectTransform);
            contentRoot.anchorMin = new Vector2(0f, 1f);
            contentRoot.anchorMax = new Vector2(1f, 1f);
            contentRoot.pivot = new Vector2(0.5f, 1f);
            contentRoot.anchoredPosition = Vector2.zero;
            contentRoot.sizeDelta = new Vector2(0f, CalculateVisibleContentHeight());

            var grid = contentRoot.gameObject.AddComponent<GridLayoutGroup>();
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = ColumnCount;
            grid.childAlignment = TextAnchor.UpperCenter;
            grid.spacing = new Vector2(GridSpacingX, GridSpacingY);
            grid.padding = CreateGridPadding();
            grid.cellSize = new Vector2(CellWidth, CellHeight);

            scrollRect = scrollRoot.gameObject.AddComponent<ScrollRect>();
            scrollRect.content = contentRoot;
            scrollRect.viewport = viewport.rectTransform;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.inertia = true;
            scrollRect.scrollSensitivity = 36f;
        }

        private void BindEvents()
        {
            if (startButton != null)
            {
                startButton.onClick.RemoveListener(OnClickStart);
                startButton.onClick.AddListener(OnClickStart);
            }

            if (backButton != null)
            {
                backButton.onClick.RemoveListener(OnClickBack);
                backButton.onClick.AddListener(OnClickBack);
            }
        }

        private void RefreshLevelList(bool selectLatestProgress)
        {
            var levels = ResolveLevels();
            levelCount = levels.Count;

            if (levelCount <= 0)
            {
                ClearGeneratedLevelItems();
                selectedLevelNumber = 0;
                UpdateSelectedStageText();
                SetStartButtonState(false);
                RebuildScrollContent();
                return;
            }

            BuildLevelItems(levels);

            var latestUnlocked = GetLatestUnlockedLevel();
            if (selectLatestProgress || selectedLevelNumber < 1 || selectedLevelNumber > levelCount || selectedLevelNumber > latestUnlocked)
            {
                selectedLevelNumber = latestUnlocked;
            }

            GameManager.RequestLevel(selectedLevelNumber);
            RefreshLevelItemStates();
            ScrollToSelectedLevel();
        }

        private List<LevelConfig> ResolveLevels()
        {
            var configManager = ConfigManager.Instance;
            if (configManager != null && !configManager.IsLoaded)
            {
                configManager.LoadDefaultConfig();
            }

            var levels = configManager != null && configManager.Config != null
                ? configManager.Config.levels
                : null;

            if ((levels == null || levels.Count == 0) && LubanConfigLoader.TryLoad(out var fallbackConfig))
            {
                levels = fallbackConfig.levels;
            }

            if ((levels == null || levels.Count == 0) && RuntimeRemoteResourceManager.CanUseResourcesFallback)
            {
                levels = LoadLevelsDirectlyFromResources();
            }

            if (levels == null || levels.Count == 0)
            {
                Debug.LogWarning("[UIStage] No level data found. Expected Resources/Luban/leiting_tblevel.json.");
                return new List<LevelConfig>();
            }

            Debug.Log($"[UIStage] Loaded {levels.Count} levels for stage selection.");
            return levels;
        }

        private static List<LevelConfig> LoadLevelsDirectlyFromResources()
        {
            var source = Resources.Load<TextAsset>("Luban/leiting_tblevel");
            if (source == null)
            {
                return null;
            }

            var node = JSON.Parse(source.text);
            if (node == null || !node.IsArray)
            {
                return null;
            }

            var levels = new List<LevelConfig>(node.Count);
            foreach (JSONNode row in node.Children)
            {
                if (row == null || !row.IsObject)
                {
                    continue;
                }

                levels.Add(new LevelConfig
                {
                    id = row["id"],
                    displayName = row["displayName"],
                    backgroundSpritePath = row["backgroundSpritePath"],
                    backgroundScrollSpeed = row["backgroundScrollSpeed"].IsNumber ? row["backgroundScrollSpeed"].AsFloat : 0f,
                    bgmPath = row["bgmPath"]
                });
            }

            return levels;
        }

        private void BuildLevelItems(IReadOnlyList<LevelConfig> levels)
        {
            ClearGeneratedLevelItems();

            for (var index = 0; index < levels.Count; index++)
            {
                var levelNumber = index + 1;
                var itemView = CreateLevelItem(levelNumber, levels[index]);
                var capturedLevelNumber = levelNumber;
                itemView.button.onClick.AddListener(() => SelectLevel(capturedLevelNumber));
                levelItems.Add(itemView);
            }

            RebuildScrollContent();
        }

        private LevelItemView CreateLevelItem(int levelNumber, LevelConfig levelConfig)
        {
            var button = UIFactory.CreateButton($"Level_{levelNumber:00}", contentRoot, string.Empty, Color.white, out var label, out var image);
            var rect = button.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(CellWidth, CellHeight);

            ApplyLevelImage(image, levelNormalSprite, new Color(0.09f, 0.14f, 0.24f, 0.96f));
            button.colors = CreateLevelButtonColors();

            if (label != null)
            {
                label.raycastTarget = false;
                label.fontSize = 28;
                label.fontStyle = FontStyle.Bold;
                label.alignment = TextAnchor.MiddleCenter;
                label.text = FormatLevelLabel(levelNumber, levelConfig);
                label.color = Color.white;
            }

            var numberText = UIFactory.CreateText("TxtStageNumber", rect, levelNumber.ToString("00"), 52f, TextAnchor.MiddleCenter, Color.white);
            var numberRect = numberText.rectTransform;
            numberRect.anchorMin = new Vector2(0f, 0.48f);
            numberRect.anchorMax = new Vector2(1f, 1f);
            numberRect.offsetMin = Vector2.zero;
            numberRect.offsetMax = new Vector2(0f, -16f);
            numberText.raycastTarget = false;

            if (label != null)
            {
                var labelRect = label.rectTransform;
                labelRect.anchorMin = new Vector2(0f, 0.2f);
                labelRect.anchorMax = new Vector2(1f, 0.48f);
                labelRect.offsetMin = new Vector2(12f, 0f);
                labelRect.offsetMax = new Vector2(-12f, 0f);
            }

            var achievementStars = CreateAchievementStarTexts(rect);

            var lockOverlay = UIFactory.CreatePanel("LockedOverlay", rect, new Color(0f, 0f, 0f, 0.48f));
            UIFactory.Stretch(lockOverlay.rectTransform);
            lockOverlay.raycastTarget = false;

            var lockText = UIFactory.CreateText("TxtLocked", lockOverlay.rectTransform, "未解锁", 30f, TextAnchor.MiddleCenter, new Color(0.85f, 0.9f, 1f, 1f));
            UIFactory.Stretch(lockText.rectTransform);
            lockText.raycastTarget = false;

            var outline = button.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(1f, 0.78f, 0.12f, 1f);
            outline.effectDistance = new Vector2(5f, -5f);
            outline.enabled = false;

            return new LevelItemView
            {
                root = rect,
                button = button,
                image = image,
                titleText = label,
                numberText = numberText,
                achievementStars = achievementStars,
                lockOverlay = lockOverlay.gameObject,
                outline = outline,
                levelNumber = levelNumber
            };
        }

        private static Text[] CreateAchievementStarTexts(RectTransform parent)
        {
            var root = UIFactory.CreateRect("AchievementStars", parent);
            root.anchorMin = new Vector2(0f, 0f);
            root.anchorMax = new Vector2(1f, 0.18f);
            root.offsetMin = new Vector2(40f, 10f);
            root.offsetMax = new Vector2(-40f, 0f);

            var stars = new Text[LevelProgressService.AchievementCount];
            for (var index = 0; index < stars.Length; index++)
            {
                var starText = UIFactory.CreateText($"Star_{index + 1}", root, "★", 28f, TextAnchor.MiddleCenter, UIFactory.MutedTextColor);
                var rect = starText.rectTransform;
                rect.anchorMin = new Vector2(index / (float)stars.Length, 0f);
                rect.anchorMax = new Vector2((index + 1) / (float)stars.Length, 1f);
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                starText.raycastTarget = false;
                stars[index] = starText;
            }

            return stars;
        }

        private static string FormatLevelLabel(int levelNumber, LevelConfig levelConfig)
        {
            if (levelConfig != null && !string.IsNullOrEmpty(levelConfig.displayName))
            {
                return levelConfig.displayName;
            }

            return $"第 {levelNumber} 关";
        }

        private void SelectLevel(int levelNumber)
        {
            if (levelNumber < 1 || levelNumber > GetLatestUnlockedLevel())
            {
                return;
            }

            selectedLevelNumber = levelNumber;
            GameManager.RequestLevel(selectedLevelNumber);
            RefreshLevelItemStates();
        }

        private void RefreshLevelItemStates()
        {
            var latestUnlocked = GetLatestUnlockedLevel();

            foreach (var item in levelItems)
            {
                var unlocked = item.levelNumber <= latestUnlocked;
                var selected = item.levelNumber == selectedLevelNumber;

                item.button.interactable = unlocked;
                ApplyLevelItemVisual(item, unlocked, selected);
                item.outline.enabled = selected && unlocked;
                item.lockOverlay.SetActive(!unlocked);

                var textColor = unlocked ? Color.white : new Color(0.68f, 0.72f, 0.78f, 1f);
                item.titleText.color = textColor;
                item.numberText.color = textColor;
                UpdateAchievementStars(item, unlocked);
            }

            UpdateSelectedStageText();
            SetStartButtonState(selectedLevelNumber >= 1 && selectedLevelNumber <= latestUnlocked);
        }

        private void UpdateSelectedStageText()
        {
            if (selectedStageText == null)
            {
                return;
            }

            selectedStageText.text = selectedLevelNumber > 0
                ? $"当前选择：第 {selectedLevelNumber} 关"
                : "暂无关卡";
        }

        private void SetStartButtonState(bool interactable)
        {
            if (startButton != null)
            {
                startButton.interactable = interactable;
            }
        }

        private static void UpdateAchievementStars(LevelItemView item, bool unlocked)
        {
            if (item?.achievementStars == null)
            {
                return;
            }

            var record = unlocked ? LevelProgressService.GetRecord(item.levelNumber) : null;
            for (var index = 0; index < item.achievementStars.Length; index++)
            {
                var star = item.achievementStars[index];
                if (star == null)
                {
                    continue;
                }

                var earned = record != null && (record.achievementMask & (1 << index)) != 0;
                star.color = earned
                    ? new Color(1f, 0.78f, 0.18f, 1f)
                    : new Color(0.28f, 0.36f, 0.48f, unlocked ? 0.92f : 0.46f);
            }
        }

        private int GetLatestUnlockedLevel()
        {
            return GameManager.GetMaxUnlockedLevel(levelCount);
        }

        private void OnClickStart()
        {
            if (battleStartRequested)
            {
                return;
            }

            if (selectedLevelNumber < 1 || selectedLevelNumber > GetLatestUnlockedLevel())
            {
                Debug.LogWarning($"[UIStage] Selected level is locked or invalid: {selectedLevelNumber}");
                return;
            }

            if (!StaminaService.TryConsume(StaminaService.BattleCost))
            {
                Debug.LogWarning("[UIStage] Not enough stamina to enter battle.");
                return;
            }

            battleStartRequested = true;
            if (startButton != null)
            {
                startButton.interactable = false;
            }

            GameSceneManager.GetOrCreate().EnterBattle(selectedLevelNumber);
        }

        private static void OnClickBack()
        {
            UIManager.Instance?.ReturnStageToHall();
        }

        private void RebuildScrollContent()
        {
            if (contentRoot == null)
            {
                return;
            }

            var height = Mathf.Max(CalculateContentHeight(levelItems.Count), CalculateVisibleContentHeight());
            contentRoot.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(contentRoot);
        }

        private void ScrollToSelectedLevel()
        {
            if (scrollRect == null || levelItems.Count <= ColumnCount * VisibleRowCount)
            {
                if (scrollRect != null)
                {
                    scrollRect.verticalNormalizedPosition = 1f;
                }

                return;
            }

            var selectedIndex = Mathf.Clamp(selectedLevelNumber - 1, 0, levelItems.Count - 1);
            var selectedRow = selectedIndex / ColumnCount;
            var rows = Mathf.Max(1, Mathf.CeilToInt(levelItems.Count / (float)ColumnCount));
            var maxFirstVisibleRow = Mathf.Max(0, rows - VisibleRowCount);
            var firstVisibleRow = Mathf.Clamp(selectedRow - VisibleRowCount / 2, 0, maxFirstVisibleRow);

            scrollRect.verticalNormalizedPosition = maxFirstVisibleRow > 0
                ? 1f - firstVisibleRow / (float)maxFirstVisibleRow
                : 1f;
        }

        private static float CalculateVisibleContentHeight()
        {
            return GridPaddingTop
                + GridPaddingBottom
                + VisibleRowCount * CellHeight
                + (VisibleRowCount - 1) * GridSpacingY;
        }

        private static float CalculateContentHeight(int itemCount)
        {
            var rows = Mathf.Max(1, Mathf.CeilToInt(itemCount / (float)ColumnCount));
            return GridPaddingTop
                + GridPaddingBottom
                + rows * CellHeight
                + Mathf.Max(0, rows - 1) * GridSpacingY;
        }

        private static RectOffset CreateGridPadding()
        {
            return new RectOffset(GridPaddingLeft, GridPaddingRight, GridPaddingTop, GridPaddingBottom);
        }

        private static ColorBlock CreateLevelButtonColors()
        {
            var colors = ColorBlock.defaultColorBlock;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 0.9f, 0.48f, 1f);
            colors.pressedColor = new Color(0.8f, 0.56f, 0.12f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = Color.white;
            colors.colorMultiplier = 1f;
            return colors;
        }

        private void ApplyLevelItemVisual(LevelItemView item, bool unlocked, bool selected)
        {
            if (item == null || item.image == null)
            {
                return;
            }

            var sprite = unlocked
                ? selected && levelSelectedSprite != null ? levelSelectedSprite : levelNormalSprite
                : levelLockedSprite != null ? levelLockedSprite : levelNormalSprite;

            ApplyLevelImage(item.image, sprite, unlocked
                ? selected ? new Color(1f, 0.78f, 0.18f, 0.98f) : new Color(0.09f, 0.14f, 0.24f, 0.96f)
                : new Color(0.13f, 0.15f, 0.18f, 0.78f));
        }

        private static void ApplySprite(Image image, Sprite sprite, Image.Type imageType)
        {
            if (image == null || sprite == null)
            {
                return;
            }

            image.sprite = sprite;
            image.type = imageType;
        }

        private static void ApplyLevelImage(Image image, Sprite sprite, Color fallbackColor)
        {
            if (image == null)
            {
                return;
            }

            if (sprite == null)
            {
                image.sprite = null;
                image.color = fallbackColor;
                return;
            }

            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.color = Color.white;
        }

        private void ClearGeneratedLevelItems()
        {
            foreach (var item in levelItems)
            {
                if (item.root != null)
                {
                    DestroyLevelItem(item.root.gameObject);
                }
            }

            levelItems.Clear();
        }

        private void ClearChildren()
        {
            for (var index = transform.childCount - 1; index >= 0; index--)
            {
                DestroyLevelItem(transform.GetChild(index).gameObject);
            }

            levelItems.Clear();
            startButton = null;
            backButton = null;
            scrollRect = null;
            contentRoot = null;
            selectedStageText = null;
        }

        private static void DestroyLevelItem(GameObject item)
        {
            if (item == null)
            {
                return;
            }

            item.SetActive(false);

            if (Application.isPlaying)
            {
                Destroy(item);
            }
            else
            {
                DestroyImmediate(item);
            }
        }

        private sealed class LevelItemView
        {
            public RectTransform root;
            public Button button;
            public Image image;
            public Text titleText;
            public Text numberText;
            public Text[] achievementStars;
            public GameObject lockOverlay;
            public Outline outline;
            public int levelNumber;
        }
    }
}
