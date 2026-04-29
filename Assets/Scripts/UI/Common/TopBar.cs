using UnityEngine;
using UnityEngine.UI;

namespace LeiTing.UI
{
    public class TopBar : MonoBehaviour
    {
        [SerializeField] private Text coinText;
        [SerializeField] private Text diamondText;
        [SerializeField] private Text scoreText;

        private bool built;

        public void BuildDefaultView()
        {
            if (built)
            {
                return;
            }

            built = true;

            var rootRect = GetComponent<RectTransform>();
            if (rootRect == null)
            {
                rootRect = gameObject.AddComponent<RectTransform>();
            }

            var background = gameObject.GetComponent<Image>() ?? gameObject.AddComponent<Image>();
            background.color = new Color(0.02f, 0.035f, 0.07f, 0.88f);

            var layout = gameObject.GetComponent<HorizontalLayoutGroup>() ?? gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(24, 24, 12, 12);
            layout.spacing = 18f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlHeight = true;
            layout.childControlWidth = false;
            layout.childForceExpandHeight = true;
            layout.childForceExpandWidth = false;

            CreateAvatar();
            coinText = CreateResourceText("Coin", "COIN 1200");
            diamondText = CreateResourceText("Diamond", "GEM 80");
            scoreText = CreateResourceText("Score", "SCORE 0");
        }

        public void UpdatePlayerInfo(PlayerInfo data)
        {
            if (data == null)
            {
                return;
            }

            UpdateCoin(data.coin);
            UpdateDiamond(data.diamond);
            UpdateScore(data.score);
        }

        public void UpdateCoin(int value)
        {
            BuildDefaultView();

            if (coinText != null)
            {
                coinText.text = $"COIN {value}";
            }
        }

        public void UpdateDiamond(int value)
        {
            BuildDefaultView();

            if (diamondText != null)
            {
                diamondText.text = $"GEM {value}";
            }
        }

        public void UpdateScore(int value)
        {
            BuildDefaultView();

            if (scoreText != null)
            {
                scoreText.text = $"SCORE {value}";
            }
        }

        private void CreateAvatar()
        {
            var rect = UIFactory.CreateRect("Avatar", transform);
            rect.sizeDelta = new Vector2(72f, 72f);

            var image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(0.1f, 0.68f, 1f, 0.95f);

            var label = UIFactory.CreateText("Icon", rect, "P", 34f, TextAnchor.MiddleCenter, Color.white);
            UIFactory.Stretch(label.rectTransform);
        }

        private Text CreateResourceText(string name, string text)
        {
            var rect = UIFactory.CreateRect(name, transform);
            rect.sizeDelta = new Vector2(220f, 72f);

            var background = rect.gameObject.AddComponent<Image>();
            background.color = new Color(0.06f, 0.09f, 0.14f, 0.72f);

            var layoutElement = rect.gameObject.AddComponent<LayoutElement>();
            layoutElement.minWidth = 180f;
            layoutElement.preferredWidth = 220f;
            layoutElement.minHeight = 72f;

            var label = UIFactory.CreateText("Label", rect, text, 27f, TextAnchor.MiddleCenter, UIFactory.TextColor);
            UIFactory.Stretch(label.rectTransform);
            return label;
        }
    }
}
