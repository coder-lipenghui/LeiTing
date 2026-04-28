using System.Collections;
using UnityEngine;

namespace LeiTing.UI
{
    public abstract class BasePopup : MonoBehaviour
    {
        public string PopupName { get; private set; }
        public bool IsClosing { get; private set; }

        protected object popupData;
        private RectTransform rectTransform;

        public RectTransform RectTransform
        {
            get
            {
                if (rectTransform == null)
                {
                    rectTransform = GetComponent<RectTransform>();
                }

                return rectTransform;
            }
        }

        public void Configure(string popupName)
        {
            PopupName = popupName;
            rectTransform = GetComponent<RectTransform>();
        }

        public virtual void OnOpen(object data = null)
        {
            popupData = data;
            IsClosing = false;
        }

        public virtual void OnClose()
        {
            IsClosing = true;
        }

        public virtual IEnumerator PlayOpenAnim()
        {
            transform.localScale = Vector3.zero;

            const float duration = 0.15f;
            var timer = 0f;

            while (timer < duration)
            {
                timer += Time.deltaTime;
                var t = Mathf.Clamp01(timer / duration);
                transform.localScale = Vector3.Lerp(Vector3.zero, Vector3.one, EaseOutBack(t));
                yield return null;
            }

            transform.localScale = Vector3.one;
        }

        public virtual IEnumerator PlayCloseAnim()
        {
            var startScale = transform.localScale;

            const float duration = 0.12f;
            var timer = 0f;

            while (timer < duration)
            {
                timer += Time.deltaTime;
                var t = Mathf.Clamp01(timer / duration);
                transform.localScale = Vector3.Lerp(startScale, Vector3.zero, t);
                yield return null;
            }

            transform.localScale = Vector3.zero;
        }

        private static float EaseOutBack(float value)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(value - 1f, 3f) + c1 * Mathf.Pow(value - 1f, 2f);
        }
    }
}
