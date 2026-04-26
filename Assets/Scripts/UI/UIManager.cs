using LeiTing.Core;
using LeiTing.Player;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LeiTing.UI
{
    public class UIManager : MonoSingleton<UIManager>
    {
        private const string SingleBulletId = "player_bullet_01";
        private const string DoubleBulletId = "player_bullet_double_01";
        private const string SpreadBulletId = "player_bullet_spread_01";
        private const string PierceBulletId = "player_bullet_pierce_01";
        private const string LaserBulletId = "player_laser_01";

        private readonly WeaponButton[] weaponButtons =
        {
            new WeaponButton("单发", SingleBulletId),
            new WeaponButton("双发", DoubleBulletId),
            new WeaponButton("5散射", SpreadBulletId),
            new WeaponButton("穿透2", PierceBulletId),
            new WeaponButton("激光", LaserBulletId)
        };

        private Button[] buttons;
        private RectTransform canvasRoot;
        private Text hudText;
        private Text settlementText;
        private GameObject bossHudRoot;
        private Image bossHealthFill;
        private Text bossNameText;
        private Text bossPhaseText;
        private Text bossNoticeText;
        private Coroutine bossNoticeRoutine;
        private string selectedBulletId = SingleBulletId;

        protected override void Awake()
        {
            base.Awake();

            if (Instance == this)
            {
                EnsureWeaponTestUi();
            }
        }

        private void Start()
        {
            ApplyWeaponSelection(selectedBulletId);
        }

        private void EnsureWeaponTestUi()
        {
            EnsureEventSystem();

            var canvasObject = new GameObject("WeaponTestCanvas", typeof(RectTransform));
            canvasObject.transform.SetParent(transform, false);

            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;

            canvasObject.AddComponent<GraphicRaycaster>();
            canvasRoot = canvasObject.GetComponent<RectTransform>();

            var barObject = new GameObject("WeaponButtons", typeof(RectTransform));
            barObject.transform.SetParent(canvasObject.transform, false);

            var bar = barObject.GetComponent<RectTransform>();
            bar.anchorMin = new Vector2(0.5f, 1f);
            bar.anchorMax = new Vector2(0.5f, 1f);
            bar.pivot = new Vector2(0.5f, 1f);
            bar.anchoredPosition = new Vector2(0f, -36f);
            bar.sizeDelta = new Vector2(980f, 96f);

            var layout = barObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 12f;
            layout.padding = new RectOffset(0, 0, 0, 0);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;

            buttons = new Button[weaponButtons.Length];
            for (var index = 0; index < weaponButtons.Length; index++)
            {
                buttons[index] = CreateWeaponButton(barObject.transform, weaponButtons[index]);
            }

            RefreshButtonStates();
            CreateHud(canvasObject.transform);
            CreateBossHud(canvasObject.transform);
            CreateBossNotice(canvasObject.transform);
            CreateSettlementText(canvasObject.transform);
        }

        private Button CreateWeaponButton(Transform parent, WeaponButton weaponButton)
        {
            var buttonObject = new GameObject(weaponButton.Label, typeof(RectTransform));
            buttonObject.transform.SetParent(parent, false);

            var image = buttonObject.AddComponent<Image>();
            image.color = GetButtonColor(false);

            var button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;
            button.colors = CreateButtonColors();
            button.onClick.AddListener(() => ApplyWeaponSelection(weaponButton.BulletId));

            var layoutElement = buttonObject.AddComponent<LayoutElement>();
            layoutElement.minHeight = 72f;
            layoutElement.preferredHeight = 72f;

            var labelObject = new GameObject("Label", typeof(RectTransform));
            labelObject.transform.SetParent(buttonObject.transform, false);

            var labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            var text = labelObject.AddComponent<Text>();
            text.text = weaponButton.Label;
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = 30;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.raycastTarget = false;

            return button;
        }

        private void CreateHud(Transform parent)
        {
            var hudObject = new GameObject("HudText", typeof(RectTransform));
            hudObject.transform.SetParent(parent, false);

            var rect = hudObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(36f, -148f);
            rect.sizeDelta = new Vector2(520f, 96f);

            hudText = hudObject.AddComponent<Text>();
            hudText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            hudText.fontSize = 32;
            hudText.fontStyle = FontStyle.Bold;
            hudText.alignment = TextAnchor.UpperLeft;
            hudText.color = Color.white;
            hudText.raycastTarget = false;
        }

        private void CreateSettlementText(Transform parent)
        {
            var settlementObject = new GameObject("SettlementText", typeof(RectTransform));
            settlementObject.transform.SetParent(parent, false);

            var rect = settlementObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(760f, 260f);

            settlementText = settlementObject.AddComponent<Text>();
            settlementText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            settlementText.fontSize = 64;
            settlementText.fontStyle = FontStyle.Bold;
            settlementText.alignment = TextAnchor.MiddleCenter;
            settlementText.color = new Color(1f, 0.95f, 0.75f, 1f);
            settlementText.raycastTarget = false;
            settlementText.enabled = false;
        }

        private void CreateBossHud(Transform parent)
        {
            bossHudRoot = new GameObject("BossHud", typeof(RectTransform));
            bossHudRoot.transform.SetParent(parent, false);

            var rootRect = bossHudRoot.GetComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0.5f, 1f);
            rootRect.anchorMax = new Vector2(0.5f, 1f);
            rootRect.pivot = new Vector2(0.5f, 1f);
            rootRect.anchoredPosition = new Vector2(0f, -150f);
            rootRect.sizeDelta = new Vector2(840f, 112f);

            var nameObject = new GameObject("BossName", typeof(RectTransform));
            nameObject.transform.SetParent(bossHudRoot.transform, false);
            var nameRect = nameObject.GetComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(0f, 1f);
            nameRect.anchorMax = new Vector2(1f, 1f);
            nameRect.pivot = new Vector2(0.5f, 1f);
            nameRect.anchoredPosition = Vector2.zero;
            nameRect.sizeDelta = new Vector2(0f, 42f);

            bossNameText = nameObject.AddComponent<Text>();
            bossNameText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            bossNameText.fontSize = 32;
            bossNameText.fontStyle = FontStyle.Bold;
            bossNameText.alignment = TextAnchor.MiddleLeft;
            bossNameText.color = new Color(1f, 0.92f, 0.74f, 1f);
            bossNameText.raycastTarget = false;

            var phaseObject = new GameObject("BossPhase", typeof(RectTransform));
            phaseObject.transform.SetParent(bossHudRoot.transform, false);
            var phaseRect = phaseObject.GetComponent<RectTransform>();
            phaseRect.anchorMin = new Vector2(0f, 1f);
            phaseRect.anchorMax = new Vector2(1f, 1f);
            phaseRect.pivot = new Vector2(0.5f, 1f);
            phaseRect.anchoredPosition = new Vector2(0f, -38f);
            phaseRect.sizeDelta = new Vector2(0f, 28f);

            bossPhaseText = phaseObject.AddComponent<Text>();
            bossPhaseText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            bossPhaseText.fontSize = 22;
            bossPhaseText.fontStyle = FontStyle.Bold;
            bossPhaseText.alignment = TextAnchor.MiddleRight;
            bossPhaseText.color = new Color(0.8f, 0.96f, 1f, 1f);
            bossPhaseText.raycastTarget = false;

            var barBackObject = new GameObject("BossHealthBack", typeof(RectTransform));
            barBackObject.transform.SetParent(bossHudRoot.transform, false);
            var barBackRect = barBackObject.GetComponent<RectTransform>();
            barBackRect.anchorMin = new Vector2(0f, 0f);
            barBackRect.anchorMax = new Vector2(1f, 0f);
            barBackRect.pivot = new Vector2(0.5f, 0f);
            barBackRect.anchoredPosition = Vector2.zero;
            barBackRect.sizeDelta = new Vector2(0f, 34f);

            var backImage = barBackObject.AddComponent<Image>();
            backImage.color = new Color(0.05f, 0.06f, 0.09f, 0.88f);
            backImage.raycastTarget = false;

            var fillObject = new GameObject("BossHealthFill", typeof(RectTransform));
            fillObject.transform.SetParent(barBackObject.transform, false);
            var fillRect = fillObject.GetComponent<RectTransform>();
            fillRect.anchorMin = new Vector2(0f, 0f);
            fillRect.anchorMax = new Vector2(1f, 1f);
            fillRect.offsetMin = new Vector2(5f, 5f);
            fillRect.offsetMax = new Vector2(-5f, -5f);

            bossHealthFill = fillObject.AddComponent<Image>();
            bossHealthFill.type = Image.Type.Filled;
            bossHealthFill.fillMethod = Image.FillMethod.Horizontal;
            bossHealthFill.fillOrigin = (int)Image.OriginHorizontal.Left;
            bossHealthFill.color = new Color(1f, 0.25f, 0.18f, 0.96f);
            bossHealthFill.raycastTarget = false;

            bossHudRoot.SetActive(false);
        }

        private void CreateBossNotice(Transform parent)
        {
            var noticeObject = new GameObject("BossNotice", typeof(RectTransform));
            noticeObject.transform.SetParent(parent, false);

            var rect = noticeObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.72f);
            rect.anchorMax = new Vector2(0.5f, 0.72f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(900f, 180f);

            bossNoticeText = noticeObject.AddComponent<Text>();
            bossNoticeText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            bossNoticeText.fontSize = 58;
            bossNoticeText.fontStyle = FontStyle.Bold;
            bossNoticeText.alignment = TextAnchor.MiddleCenter;
            bossNoticeText.color = new Color(1f, 0.85f, 0.22f, 0f);
            bossNoticeText.raycastTarget = false;
            bossNoticeText.enabled = false;
        }

        private void Update()
        {
            UpdateHud();
            UpdateSettlement();
        }

        public void ShowScorePopup(Vector3 worldPosition, int amount)
        {
            if (canvasRoot == null || amount <= 0)
            {
                return;
            }

            var popupObject = new GameObject("ScorePopup", typeof(RectTransform));
            popupObject.transform.SetParent(canvasRoot, false);

            var rect = popupObject.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(180f, 52f);

            var screenPosition = Camera.main != null ? Camera.main.WorldToScreenPoint(worldPosition) : Vector3.zero;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRoot, screenPosition, null, out var localPosition);
            rect.anchoredPosition = localPosition;

            var text = popupObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = 34;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color(1f, 0.88f, 0.22f, 1f);
            text.raycastTarget = false;
            text.text = $"+{amount}";

            StartCoroutine(AnimateScorePopup(rect, text));
        }

        public void UpdateBossHud(string bossName, int currentHp, int maxHp, string phaseName)
        {
            if (bossHudRoot == null || bossHealthFill == null)
            {
                return;
            }

            bossHudRoot.SetActive(maxHp > 0 && currentHp > 0);
            bossHealthFill.fillAmount = maxHp > 0 ? Mathf.Clamp01(currentHp / (float)maxHp) : 0f;

            if (bossNameText != null)
            {
                bossNameText.text = string.IsNullOrEmpty(bossName) ? "BOSS" : bossName;
            }

            if (bossPhaseText != null)
            {
                bossPhaseText.text = string.IsNullOrEmpty(phaseName) ? string.Empty : phaseName;
            }
        }

        public void HideBossHud()
        {
            if (bossHudRoot != null)
            {
                bossHudRoot.SetActive(false);
            }
        }

        public void ShowBossPhaseNotice(string message)
        {
            if (bossNoticeText == null || string.IsNullOrEmpty(message))
            {
                return;
            }

            if (bossNoticeRoutine != null)
            {
                StopCoroutine(bossNoticeRoutine);
            }

            bossNoticeRoutine = StartCoroutine(AnimateBossNotice(message));
        }

        private IEnumerator AnimateScorePopup(RectTransform rect, Text text)
        {
            const float lifetime = 0.55f;
            var age = 0f;
            var start = rect.anchoredPosition;
            var end = start + Vector2.up * 78f;
            var startColor = text.color;
            var endColor = new Color(startColor.r, startColor.g, startColor.b, 0f);

            while (age < lifetime)
            {
                age += Time.deltaTime;
                var t = Mathf.Clamp01(age / lifetime);
                rect.anchoredPosition = Vector2.Lerp(start, end, t);
                text.color = Color.Lerp(startColor, endColor, t);
                yield return null;
            }

            if (rect != null)
            {
                Destroy(rect.gameObject);
            }
        }

        private IEnumerator AnimateBossNotice(string message)
        {
            const float fadeIn = 0.12f;
            const float hold = 1.0f;
            const float fadeOut = 0.32f;

            bossNoticeText.text = message;
            bossNoticeText.enabled = true;

            yield return FadeBossNotice(0f, 1f, fadeIn);
            yield return new WaitForSeconds(hold);
            yield return FadeBossNotice(1f, 0f, fadeOut);

            bossNoticeText.enabled = false;
            bossNoticeRoutine = null;
        }

        private IEnumerator FadeBossNotice(float from, float to, float duration)
        {
            var age = 0f;
            while (age < duration)
            {
                age += Time.deltaTime;
                var t = Mathf.Clamp01(age / Mathf.Max(0.01f, duration));
                SetBossNoticeAlpha(Mathf.Lerp(from, to, t));
                yield return null;
            }

            SetBossNoticeAlpha(to);
        }

        private void SetBossNoticeAlpha(float alpha)
        {
            if (bossNoticeText == null)
            {
                return;
            }

            var color = bossNoticeText.color;
            color.a = alpha;
            bossNoticeText.color = color;
        }

        private void UpdateHud()
        {
            if (hudText == null)
            {
                return;
            }

            var player = FindObjectOfType<PlayerController>();
            var hp = player != null ? player.CurrentHp : 0;
            var shield = player != null ? player.CurrentShield : 0;
            var stars = player != null ? player.CurrentStars : 0;
            var score = GameManager.Instance != null ? GameManager.Instance.Score : 0;
            hudText.text = $"HP {hp}  SH {shield}  STAR {stars}\nSCORE {score}";
        }

        private void UpdateSettlement()
        {
            if (settlementText == null || GameManager.Instance == null)
            {
                return;
            }

            var state = GameManager.Instance.CurrentState;
            var finished = state == GameState.Defeat || state == GameState.Victory;
            settlementText.enabled = finished;

            if (!finished)
            {
                return;
            }

            var title = state == GameState.Victory ? "CLEAR" : "GAME OVER";
            settlementText.text = $"{title}\nSCORE {GameManager.Instance.Score}";
        }

        private void ApplyWeaponSelection(string bulletId)
        {
            selectedBulletId = bulletId;

            var shooter = FindObjectOfType<PlayerShooter>();
            if (shooter != null)
            {
                shooter.SetBulletId(bulletId);
            }

            RefreshButtonStates();
        }

        private void RefreshButtonStates()
        {
            if (buttons == null)
            {
                return;
            }

            for (var index = 0; index < buttons.Length; index++)
            {
                var image = buttons[index] != null ? buttons[index].GetComponent<Image>() : null;
                if (image != null)
                {
                    image.color = GetButtonColor(weaponButtons[index].BulletId == selectedBulletId);
                }
            }
        }

        private static ColorBlock CreateButtonColors()
        {
            var colors = ColorBlock.defaultColorBlock;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.82f, 0.95f, 1f, 1f);
            colors.pressedColor = new Color(0.42f, 0.82f, 1f, 1f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(0.35f, 0.35f, 0.35f, 0.65f);
            colors.colorMultiplier = 1f;
            return colors;
        }

        private static Color GetButtonColor(bool selected)
        {
            return selected ? new Color(0.1f, 0.62f, 1f, 0.92f) : new Color(0.06f, 0.09f, 0.14f, 0.78f);
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null)
            {
                return;
            }

            var eventSystemObject = new GameObject("EventSystem");
            eventSystemObject.AddComponent<EventSystem>();
            eventSystemObject.AddComponent<StandaloneInputModule>();
        }

        private readonly struct WeaponButton
        {
            public readonly string Label;
            public readonly string BulletId;

            public WeaponButton(string label, string bulletId)
            {
                Label = label;
                BulletId = bulletId;
            }
        }
    }
}
