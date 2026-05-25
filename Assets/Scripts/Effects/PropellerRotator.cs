using LeiTing.Core;
using UnityEngine;

namespace LeiTing.Effects
{
    [DisallowMultipleComponent]
    public class PropellerRotator : MonoBehaviour
    {
        [SerializeField] private float degreesPerSecond = -1200f;
        [SerializeField] private bool useUnscaledTime;
        [SerializeField] private bool spinOnlyDuringGameplay = true;

        private void Update()
        {
            if (spinOnlyDuringGameplay
                && GameManager.Instance != null
                && GameManager.Instance.CurrentState != GameState.Playing)
            {
                return;
            }

            var deltaTime = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            transform.Rotate(0f, 0f, degreesPerSecond * deltaTime, Space.Self);
        }
    }
}
