using System.Threading.Tasks;
using LeiTing.Core;
using UnityEngine;

namespace LeiTing.UI
{
    public class AdManager : MonoSingleton<AdManager>
    {
        public static AdManager GetOrCreate()
        {
            if (Instance != null)
            {
                return Instance;
            }

            var existing = FindObjectOfType<AdManager>();
            if (existing != null)
            {
                return existing;
            }

            var managerObject = new GameObject("AdManager");
            return managerObject.AddComponent<AdManager>();
        }

        protected override void Awake()
        {
            base.Awake();

            if (Instance == this)
            {
                DontDestroyOnLoad(gameObject);
            }
        }

        public Task<bool> ShowRewardAd()
        {
            return Task.FromResult(true);
        }
    }
}
