using System;
using System.Text;
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

        [Header("结算信息显示控制")]
        [SerializeField] private SettlementInfoBinding scoreInfo = new SettlementInfoBinding("Score", "TextScore", "本关积分：{0}");
        [SerializeField] private SettlementInfoBinding coinInfo = new SettlementInfoBinding("Coin", "TextCoin", "金币：{0}");
        [SerializeField] private SettlementInfoBinding starInfo = new SettlementInfoBinding("Star", "TextStar", "收集星星：{0}");
        [SerializeField] private SettlementInfoBinding bossInfo = new SettlementInfoBinding("Enemy", "TextEnemy", "击破：{0}");
        [SerializeField] private SettlementInfoBinding levelInfo = new SettlementInfoBinding("Level", "TextLevel", "关卡：{0}", false);
        [SerializeField] private SettlementInfoBinding totalScoreInfo = new SettlementInfoBinding("TotalScore", "TextTotalScore", "累计积分：{0}", false);
        [SerializeField] private SettlementInfoBinding enemyKillInfo = new SettlementInfoBinding("EnemyKill", "TextEnemyKill", "击毁敌机：{0}", false, "EnemyKills", "TextEnemyKills");
        [SerializeField] private SettlementInfoBinding achievementInfo = new SettlementInfoBinding("Achievement", "TextAchievement", "达成目标：{0}", false, "Achievements", "TextAchievements");
        [SerializeField] private SettlementInfoBinding hitInfo = new SettlementInfoBinding("Hit", "TextHit", "受击情况：{0}", false, "HitStatus", "TextHitStatus");

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

        public void SetContent(string title, SettlementInfo info)
        {
            if (TitleText != null)
            {
                TitleText.text = title;
            }

            var boundInfoCount = 0;
            boundInfoCount += scoreInfo.Apply(transform, info.score) ? 1 : 0;
            boundInfoCount += coinInfo.Apply(transform, info.coins) ? 1 : 0;
            boundInfoCount += starInfo.Apply(transform, info.stars) ? 1 : 0;
            boundInfoCount += bossInfo.Apply(transform, info.bossName) ? 1 : 0;
            boundInfoCount += levelInfo.Apply(transform, info.levelName) ? 1 : 0;
            boundInfoCount += totalScoreInfo.Apply(transform, info.totalScore) ? 1 : 0;
            boundInfoCount += enemyKillInfo.Apply(transform, info.enemyKills) ? 1 : 0;
            boundInfoCount += achievementInfo.Apply(transform, info.achievements) ? 1 : 0;
            boundInfoCount += hitInfo.Apply(transform, info.hitStatus) ? 1 : 0;

            if (DetailText != null)
            {
                DetailText.text = boundInfoCount > 0 ? string.Empty : BuildDetails(info);
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

        private string BuildDetails(SettlementInfo info)
        {
            var builder = new StringBuilder();
            levelInfo.AppendDetailLine(builder, info.levelName);
            bossInfo.AppendDetailLine(builder, info.bossName);
            scoreInfo.AppendDetailLine(builder, info.score);
            totalScoreInfo.AppendDetailLine(builder, info.totalScore);
            coinInfo.AppendDetailLine(builder, info.coins);
            enemyKillInfo.AppendDetailLine(builder, info.enemyKills);
            starInfo.AppendDetailLine(builder, info.stars);
            achievementInfo.AppendDetailLine(builder, info.achievements);
            hitInfo.AppendDetailLine(builder, info.hitStatus);
            return builder.ToString().TrimEnd();
        }

        [Serializable]
        public struct SettlementInfo
        {
            public string levelName;
            public string bossName;
            public string score;
            public string totalScore;
            public string coins;
            public string enemyKills;
            public string stars;
            public string achievements;
            public string hitStatus;
        }

        [Serializable]
        private sealed class SettlementInfoBinding
        {
            [Tooltip("是否显示这一项；关闭时会隐藏 Root。")]
            [SerializeField] private bool show = true;
            [Tooltip("这一项的根节点，通常包含标题、数值、装饰线等全部内容。")]
            [SerializeField] private GameObject root;
            [Tooltip("运行时写入数值的 Text。")]
            [SerializeField] private Text valueText;
            [Tooltip("Root 未手动绑定时按这个名字自动查找。")]
            [SerializeField] private string rootName;
            [Tooltip("Value Text 未手动绑定时按这个名字自动查找。")]
            [SerializeField] private string valueTextName;
            [Tooltip("没有独立节点、使用 DetailText 拼接显示时的行文本格式。")]
            [SerializeField] private string detailLineFormat;
            [Tooltip("Root 自动查找的备用名字。")]
            [SerializeField] private string alternateRootName;
            [Tooltip("Value Text 自动查找的备用名字。")]
            [SerializeField] private string alternateValueTextName;

            public SettlementInfoBinding()
            {
            }

            public SettlementInfoBinding(
                string rootName,
                string valueTextName,
                string detailLineFormat,
                bool show = true,
                string alternateRootName = "",
                string alternateValueTextName = "")
            {
                this.show = show;
                this.rootName = rootName;
                this.valueTextName = valueTextName;
                this.detailLineFormat = detailLineFormat;
                this.alternateRootName = alternateRootName;
                this.alternateValueTextName = alternateValueTextName;
            }

            public bool Apply(Transform owner, string value)
            {
                var resolvedRoot = ResolveRoot(owner);
                var resolvedText = ResolveText(owner);
                var hasBinding = resolvedRoot != null || resolvedText != null;

                if (resolvedRoot != null)
                {
                    resolvedRoot.SetActive(show);
                }
                else if (resolvedText != null)
                {
                    resolvedText.gameObject.SetActive(show);
                }

                if (show && resolvedText != null)
                {
                    resolvedText.text = value ?? string.Empty;
                }

                return hasBinding;
            }

            public void AppendDetailLine(StringBuilder builder, string value)
            {
                if (!show || builder == null)
                {
                    return;
                }

                var text = value ?? string.Empty;
                if (string.IsNullOrEmpty(detailLineFormat))
                {
                    builder.AppendLine(text);
                    return;
                }

                builder.AppendLine(detailLineFormat.Contains("{0}")
                    ? string.Format(detailLineFormat, text)
                    : detailLineFormat + text);
            }

            private GameObject ResolveRoot(Transform owner)
            {
                if (root != null)
                {
                    return root;
                }

                var found = FindTransform(owner, rootName) ?? FindTransform(owner, alternateRootName);
                root = found != null ? found.gameObject : null;
                return root;
            }

            private Text ResolveText(Transform owner)
            {
                if (valueText != null)
                {
                    return valueText;
                }

                valueText = FindComponent<Text>(owner, valueTextName) ?? FindComponent<Text>(owner, alternateValueTextName);
                return valueText;
            }

            private static Transform FindTransform(Transform owner, string childName)
            {
                if (owner == null || string.IsNullOrEmpty(childName))
                {
                    return null;
                }

                foreach (var transform in owner.GetComponentsInChildren<Transform>(true))
                {
                    if (transform != null && transform.name == childName)
                    {
                        return transform;
                    }
                }

                return null;
            }

            private static T FindComponent<T>(Transform owner, string childName) where T : Component
            {
                if (owner == null || string.IsNullOrEmpty(childName))
                {
                    return null;
                }

                foreach (var component in owner.GetComponentsInChildren<T>(true))
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
}
