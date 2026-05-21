using System.Collections.Generic;
using LeiTing.Config;
using LeiTing.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LeiTing.UI
{
    public class StagePage : BasePage
    {
        private const int ColumnCount = 2;
        private const float DefaultCellWidth = 360f;
        private const float DefaultCellHeight = 300f;

        [SerializeField] private Button startButton;
        [SerializeField] private Button backButton;
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private RectTransform contentRoot;
        [SerializeField] private RectTransform levelItemTemplate;
        [SerializeField] private TMP_Text selectedStageText;

        private readonly List<LevelItemView> levelItems = new List<LevelItemView>();
        private int selectedLevelNumber = 1;
        private int levelCount;

        public override void OnCreate()
        {
            if (transform.childCount == 0)
            {
                BuildDefaultView();
            }

            BindPrefabView();
            BindEvents();
            RefreshLevelList(true);
            Debug.Log($"[UIStage] OnCreate complete. startButton={startButton != null}, backButton={backButton != null}, scrollRect={scrollRect != null}, contentRoot={contentRoot != null}, levelTemplate={levelItemTemplate != null}");
        }

        public override void OnShow()
        {
            RefreshLevelList(true);
            Debug.Log($"[UIStage] OnShow. selectedLevel={selectedLevelNumber}, levelCount={levelCount}");
        }

        public override void OnHide()
        {
            if (startButton != null)
            {
                startButton.interactable = true;
            }
        }

        private void BuildDefaultView()
        {
            UIFactory.Stretch(RectTransform);

            var backdrop = UIFactory.CreatePanel("StageBackdrop", transform, new Color(0.015f, 0.025f, 0.055f, 0.98f));
            UIFactory.Stretch(backdrop.rectTransform);

            var title = UIFactory.CreateText("Title", transform, "选择关卡", 56f, TextAnchor.MiddleCenter, Color.white);
            var titleRect = title.rectTransform;
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.anchoredPosition = new Vector2(0f, -128f);
            titleRect.sizeDelta = new Vector2(0f, 92f);

            backButton = UIFactory.CreateButton("BtnBack", transform, "返回", new Color(0.12f, 0.18f, 0.26f, 0.92f), out _, out _);
            var backRect = backButton.GetComponent<RectTransform>();
            backRect.anchorMin = new Vector2(0f, 1f);
            backRect.anchorMax = new Vector2(0f, 1f);
            backRect.pivot = new Vector2(0f, 1f);
            backRect.anchoredPosition = new Vector2(42f, -48f);
            backRect.sizeDelta = new Vector2(168f, 72f);

            var scrollRoot = UIFactory.CreateRect("Scroll View", transform);
            scrollRoot.anchorMin = new Vector2(0.5f, 0.5f);
            scrollRoot.anchorMax = new Vector2(0.5f, 0.5f);
            scrollRoot.pivot = new Vector2(0.5f, 0.5f);
            scrollRoot.anchoredPosition = new Vector2(0f, 60f);
            scrollRoot.sizeDelta = new Vector2(860f, 1180f);

            var viewport = UIFactory.CreatePanel("Viewport", scrollRoot, new Color(0f, 0f, 0f, 0f));
            UIFactory.Stretch(viewport.rectTransform);
            var mask = viewport.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            contentRoot = UIFactory.CreateRect("Content", viewport.rectTransform);
            contentRoot.anchorMin = new Vector2(0f, 1f);
            contentRoot.anchorMax = new Vector2(1f, 1f);
            contentRoot.pivot = new Vector2(0.5f, 1f);
            contentRoot.anchoredPosition = Vector2.zero;
            contentRoot.sizeDelta = new Vector2(0f, 300f);

            scrollRect = scrollRoot.gameObject.AddComponent<ScrollRect>();
            scrollRect.content = contentRoot;
            scrollRect.viewport = viewport.rectTransform;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;

            startButton = UIFactory.CreateButton("BtnStart", transform, "开始", new Color(1f, 0.72f, 0.18f, 0.96f), out _, out _);
            var startRect = startButton.GetComponent<RectTransform>();
            startRect.anchorMin = new Vector2(0.5f, 0f);
            startRect.anchorMax = new Vector2(0.5f, 0f);
            startRect.pivot = new Vector2(0.5f, 0f);
            startRect.anchoredPosition = new Vector2(0f, 74f);
            startRect.sizeDelta = new Vector2(536f, 128f);
        }

        private void BindPrefabView()
        {
            UIFactory.Stretch(RectTransform);

            startButton = startButton != null
                ? startButton
                : UIFactory.FindComponentInChildren<Button>(transform, "BtnStart");
            backButton = backButton != null
                ? backButton
                : UIFactory.FindComponentInChildren<Button>(transform, "BtnBack");
            scrollRect = scrollRect != null
                ? scrollRect
                : GetComponentInChildren<ScrollRect>(true);
            contentRoot = contentRoot != null
                ? contentRoot
                : scrollRect != null
                    ? scrollRect.content
                    : UIFactory.FindComponentInChildren<RectTransform>(transform, "Content");
            levelItemTemplate = levelItemTemplate != null
                ? levelItemTemplate
                : UIFactory.FindComponentInChildren<RectTransform>(transform, "RenderItem");
            selectedStageText = selectedStageText != null
                ? selectedStageText
                : UIFactory.FindComponentInChildren<TMP_Text>(transform, "TxtStage");

            UIFactory.ApplyButtonTextFont(startButton);
            UIFactory.ApplyButtonTextFont(backButton);
            SetButtonLabel(startButton, "开始");
            SetButtonLabel(backButton, "返回");
            ConfigureContentLayout();
        }

        private void BindEvents()
        {
            if (startButton != null)
            {
                startButton.onClick.RemoveListener(OnClickStart);
                startButton.onClick.AddListener(OnClickStart);
                Debug.Log($"[UIStage] BtnStart bound. activeInHierarchy={startButton.gameObject.activeInHierarchy}, interactable={startButton.interactable}, targetGraphic={startButton.targetGraphic != null}");
            }
            else
            {
                Debug.LogWarning("[UIStage] BtnStart not found, cannot bind start battle button.");
            }

            if (backButton != null)
            {
                backButton.onClick.RemoveListener(OnClickBack);
                backButton.onClick.AddListener(OnClickBack);
                Debug.Log($"[UIStage] BtnBack bound. activeInHierarchy={backButton.gameObject.activeInHierarchy}, interactable={backButton.interactable}, targetGraphic={backButton.targetGraphic != null}");
            }
            else
            {
                Debug.LogWarning("[UIStage] BtnBack not found, cannot bind return button.");
            }
        }

        private void RefreshLevelList(bool selectLatestProgress)
        {
            var levels = ResolveLevels();
            levelCount = levels.Count;

            if (levelCount <= 0)
            {
                ClearGeneratedLevelItems();
                SetStartButtonState(false);
                return;
            }

            if (levelItems.Count != levelCount)
            {
                BuildLevelItems(levels);
            }

            var latestUnlocked = GetLatestUnlockedLevel();
            if (selectLatestProgress || selectedLevelNumber < 1 || selectedLevelNumber > levelCount || selectedLevelNumber > latestUnlocked)
            {
                selectedLevelNumber = latestUnlocked;
            }

            GameManager.RequestLevel(selectedLevelNumber);
            RefreshLevelItemStates();
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

            return levels != null ? levels : new List<LevelConfig>();
        }

        private void BuildLevelItems(IReadOnlyList<LevelConfig> levels)
        {
            ClearGeneratedLevelItems();
            ConfigureContentLayout();

            if (levelItemTemplate != null)
            {
                levelItemTemplate.gameObject.SetActive(false);
            }

            for (var index = 0; index < levels.Count; index++)
            {
                var levelNumber = index + 1;
                var itemObject = levelItemTemplate != null
                    ? Instantiate(levelItemTemplate.gameObject, contentRoot)
                    : CreateFallbackLevelItem(contentRoot);

                itemObject.name = $"Level_{levelNumber:00}";
                itemObject.SetActive(true);

                var itemView = CreateLevelItemView(itemObject, levelNumber, levels[index]);
                var capturedLevelNumber = levelNumber;
                foreach (var button in itemView.buttons)
                {
                    if (button == null)
                    {
                        continue;
                    }

                    button.onClick.RemoveAllListeners();
                    button.onClick.AddListener(() => SelectLevel(capturedLevelNumber));
                }

                levelItems.Add(itemView);
            }
        }

        private GameObject CreateFallbackLevelItem(Transform parent)
        {
            var button = UIFactory.CreateButton("LevelItem", parent, string.Empty, new Color(0.08f, 0.13f, 0.2f, 0.9f), out _, out _);
            var rect = button.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(DefaultCellWidth, DefaultCellHeight);
            return button.gameObject;
        }

        private LevelItemView CreateLevelItemView(GameObject itemObject, int levelNumber, LevelConfig levelConfig)
        {
            var root = itemObject.GetComponent<RectTransform>();
            var rootImage = itemObject.GetComponent<Image>() ?? itemObject.AddComponent<Image>();
            var rootButton = itemObject.GetComponent<Button>() ?? itemObject.AddComponent<Button>();
            rootButton.targetGraphic = rootImage;
            rootButton.transition = Selectable.Transition.ColorTint;
            rootButton.colors = UIFactory.CreateButtonColors(Color.white);

            var outline = itemObject.GetComponent<Outline>() ?? itemObject.AddComponent<Outline>();
            outline.effectColor = new Color(1f, 0.82f, 0.18f, 0.95f);
            outline.effectDistance = new Vector2(5f, -5f);
            outline.enabled = false;

            var canvasGroup = itemObject.GetComponent<CanvasGroup>() ?? itemObject.AddComponent<CanvasGroup>();
            UIFactory.ApplyFontsInChildren(itemObject.transform);

            var view = new LevelItemView
            {
                root = root,
                rootImage = rootImage,
                outline = outline,
                canvasGroup = canvasGroup,
                buttons = itemObject.GetComponentsInChildren<Button>(true),
                tmpTexts = itemObject.GetComponentsInChildren<TMP_Text>(true),
                texts = itemObject.GetComponentsInChildren<Text>(true),
                lockObject = FindChild(itemObject.transform, "img_lock") ?? FindChild(itemObject.transform, "level_lock"),
                levelNumber = levelNumber
            };

            SetLevelItemText(view, levelConfig);
            return view;
        }

        private void SetLevelItemText(LevelItemView view, LevelConfig levelConfig)
        {
            var levelName = levelConfig != null && !string.IsNullOrEmpty(levelConfig.displayName)
                ? levelConfig.displayName
                : $"第 {view.levelNumber} 关";
            var stageNumber = view.levelNumber.ToString("00");
            var assignedStageText = false;

            foreach (var text in view.tmpTexts)
            {
                if (text == null)
                {
                    continue;
                }

                if (text.name == "TxtStage" || !assignedStageText && ShouldUseAsStageText(text.text))
                {
                    text.text = $"STAGE\n{stageNumber}";
                    assignedStageText = true;
                }
                else if (ContainsSlash(text.text))
                {
                    text.text = $"{view.levelNumber}/{levelCount}";
                }
                else if (string.IsNullOrEmpty(text.text))
                {
                    text.text = levelName;
                }
            }

            foreach (var text in view.texts)
            {
                if (text == null)
                {
                    continue;
                }

                if (string.IsNullOrEmpty(text.text))
                {
                    text.text = levelName;
                }
                else if (ContainsSlash(text.text))
                {
                    text.text = $"{view.levelNumber}/{levelCount}";
                }
            }
        }

        private static bool ShouldUseAsStageText(string text)
        {
            return string.IsNullOrEmpty(text)
                || text.Contains("STAGE")
                || text == "01"
                || text == "1";
        }

        private static bool ContainsSlash(string text)
        {
            return !string.IsNullOrEmpty(text) && text.Contains("/");
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

                if (item.canvasGroup != null)
                {
                    item.canvasGroup.alpha = unlocked ? 1f : 0.42f;
                }

                if (item.rootImage != null)
                {
                    item.rootImage.color = unlocked
                        ? selected ? new Color(1f, 0.96f, 0.68f, 1f) : Color.white
                        : new Color(0.48f, 0.48f, 0.48f, 1f);
                }

                if (item.outline != null)
                {
                    item.outline.enabled = selected && unlocked;
                }

                if (item.lockObject != null)
                {
                    item.lockObject.gameObject.SetActive(!unlocked);
                }

                foreach (var button in item.buttons)
                {
                    if (button != null)
                    {
                        button.interactable = unlocked;
                    }
                }
            }

            if (selectedStageText != null)
            {
                selectedStageText.text = selectedLevelNumber.ToString("00");
                UIFactory.ApplyTextFont(selectedStageText);
            }

            SetStartButtonState(selectedLevelNumber <= latestUnlocked);
        }

        private void SetStartButtonState(bool interactable)
        {
            if (startButton != null)
            {
                startButton.interactable = interactable;
            }
        }

        private int GetLatestUnlockedLevel()
        {
            return GameManager.GetMaxUnlockedLevel(levelCount);
        }

        private void OnClickStart()
        {
            Debug.Log($"[UIStage] BtnStart clicked. selectedLevel={selectedLevelNumber}, latestUnlocked={GetLatestUnlockedLevel()}");

            if (selectedLevelNumber < 1 || selectedLevelNumber > GetLatestUnlockedLevel())
            {
                Debug.LogWarning($"[UIStage] Selected level is locked or invalid: {selectedLevelNumber}");
                return;
            }

            if (startButton != null)
            {
                startButton.interactable = false;
            }

            GameSceneManager.GetOrCreate().EnterBattle(selectedLevelNumber);
        }

        private static void OnClickBack()
        {
            Debug.Log("[UIStage] BtnBack clicked, return to UIHall.");
            UIManager.Instance?.ReturnStageToHall();
        }

        private void ConfigureContentLayout()
        {
            if (contentRoot == null)
            {
                return;
            }

            var grid = contentRoot.GetComponent<GridLayoutGroup>() ?? contentRoot.gameObject.AddComponent<GridLayoutGroup>();
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = ColumnCount;
            grid.childAlignment = TextAnchor.UpperCenter;
            grid.spacing = new Vector2(42f, 42f);
            grid.padding = new RectOffset(12, 12, 12, 80);
            grid.cellSize = ResolveCellSize();

            var fitter = contentRoot.GetComponent<ContentSizeFitter>() ?? contentRoot.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        private Vector2 ResolveCellSize()
        {
            if (levelItemTemplate != null)
            {
                var size = levelItemTemplate.rect.size;
                if (size.x > 0f && size.y > 0f)
                {
                    return size;
                }
            }

            return new Vector2(DefaultCellWidth, DefaultCellHeight);
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

        private static void DestroyLevelItem(GameObject item)
        {
            if (item == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(item);
            }
            else
            {
                DestroyImmediate(item);
            }
        }

        private static Transform FindChild(Transform root, string childName)
        {
            if (root == null || string.IsNullOrEmpty(childName))
            {
                return null;
            }

            foreach (var child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child != null && child.name == childName)
                {
                    return child;
                }
            }

            return null;
        }

        private static void SetButtonLabel(Button button, string label)
        {
            if (button == null)
            {
                return;
            }

            var tmpText = button.GetComponentInChildren<TMP_Text>(true);
            if (tmpText != null)
            {
                tmpText.text = label;
                UIFactory.ApplyTextFont(tmpText);
                return;
            }

            var text = button.GetComponentInChildren<Text>(true);
            if (text != null)
            {
                text.text = label;
                UIFactory.ApplyTextFont(text);
            }
        }

        private sealed class LevelItemView
        {
            public RectTransform root;
            public Image rootImage;
            public Outline outline;
            public CanvasGroup canvasGroup;
            public Button[] buttons;
            public TMP_Text[] tmpTexts;
            public Text[] texts;
            public Transform lockObject;
            public int levelNumber;
        }
    }
}
