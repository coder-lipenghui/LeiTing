using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace LeiTing.UI
{
    public sealed class VictorySettlementView : MonoBehaviour
    {
        [SerializeField] private Text titleText;
        [SerializeField] private Text detailText;
        [SerializeField] private Button continueButton;
        [SerializeField] private Button shareButton;
        [SerializeField] private Image continueButtonImage;

        private bool buttonsBound;

        public Text TitleText => titleText = Resolve(titleText, "TitleText");
        public Text DetailText => detailText = Resolve(detailText, "DetailText");
        public Button ContinueButton => continueButton = Resolve(continueButton, "ContinueButton");
        public Button ShareButton => shareButton = Resolve(shareButton, "ShareButton");

        public void SetContent(string title, string details)
        {
            if (TitleText != null)
            {
                TitleText.text = title;
            }

            if (DetailText != null)
            {
                DetailText.text = details;
            }
        }

        public void ApplyContinueSprite(Sprite sprite)
        {
            if (sprite == null)
            {
                return;
            }

            continueButtonImage = continueButtonImage != null
                ? continueButtonImage
                : ContinueButton != null
                    ? ContinueButton.GetComponent<Image>()
                    : null;

            if (continueButtonImage == null)
            {
                return;
            }

            continueButtonImage.sprite = sprite;
            continueButtonImage.color = Color.white;
            continueButtonImage.preserveAspect = true;
        }

        public void BindButtons(UnityAction onContinue, UnityAction onShare)
        {
            if (buttonsBound)
            {
                return;
            }

            buttonsBound = true;

            if (ContinueButton != null && onContinue != null)
            {
                ContinueButton.onClick.AddListener(onContinue);
            }

            if (ShareButton != null && onShare != null)
            {
                ShareButton.onClick.AddListener(onShare);
            }
        }

        private T Resolve<T>(T cached, string childName) where T : Component
        {
            if (cached != null)
            {
                return cached;
            }

            foreach (var component in GetComponentsInChildren<T>(true))
            {
                if (component != null && component.name == childName)
                {
                    return component;
                }
            }

            return null;
        }
    }
}
