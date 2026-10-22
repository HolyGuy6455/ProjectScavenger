using System.Collections;
using TMPro;
using UnityEngine;

namespace Scavenger.UI
{
    public class ToastPopup : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI messageText;
        [Tooltip("ShowToast에서 시간을 생략했을 때 사용하는 표시 시간(초)")]
        [SerializeField, Min(0.01f)] private float defaultDuration = 2f;

        private Coroutine _hideRoutine;
        private float? _durationOverride;

        private void OnEnable()
        {
            RestartHideTimer();
        }

        private void OnDisable()
        {
            StopHideTimer();
            _durationOverride = null;
        }

        // 시간을 생략하거나 유효하지 않은 값을 전달하면 Inspector의 기본값을 사용합니다.
        public void Show(string message, float? duration = null)
        {
            if (messageText != null) messageText.text = message ?? string.Empty;
            _durationOverride = duration;
            transform.SetAsLastSibling();

            if (!gameObject.activeSelf)
                gameObject.SetActive(true);
            else if (isActiveAndEnabled)
                RestartHideTimer();
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        private void RestartHideTimer()
        {
            StopHideTimer();
            float duration = _durationOverride ?? defaultDuration;
            if (!IsValidDuration(duration)) duration = defaultDuration;
            if (!IsValidDuration(duration)) duration = 2f;
            _hideRoutine = StartCoroutine(HideAfterDelay(duration));
        }

        private IEnumerator HideAfterDelay(float duration)
        {
            // UI 표시 시간은 Time.timeScale의 영향을 받지 않습니다.
            yield return new WaitForSecondsRealtime(duration);
            _hideRoutine = null;
            Hide();
        }

        private void StopHideTimer()
        {
            if (_hideRoutine == null) return;
            StopCoroutine(_hideRoutine);
            _hideRoutine = null;
        }

        private static bool IsValidDuration(float duration)
        {
            return duration > 0f && !float.IsNaN(duration) && !float.IsInfinity(duration);
        }
    }
}
