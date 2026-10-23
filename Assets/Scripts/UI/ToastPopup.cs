using System.Collections;
using System.Collections.Generic;
using Scavenger.Audio;
using TMPro;
using UnityEngine;

namespace Scavenger.UI
{
    [RequireComponent(typeof(CanvasGroup))]
    public class ToastPopup : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI messageText;
        [Tooltip("ShowToast에서 시간을 생략했을 때 완전히 나타난 문구를 유지하는 시간(초). 페이드 시간은 별도입니다.")]
        [SerializeField, Min(0.01f)] private float defaultDuration = 2f;

        [Header("Fade")]
        [SerializeField, Min(0f)] private float fadeInDuration = 0.2f;
        [SerializeField, Min(0f)] private float fadeOutDuration = 0.2f;

        private const int MaxPendingCount = 2;
        private const double MaxPendingAgeSeconds = 5d;
        private readonly Queue<ToastRequest> _pending = new Queue<ToastRequest>();
        private Coroutine _hideRoutine;
        private string _currentMessage;
        private CanvasGroup _canvasGroup;
        private bool _finishHoldEarly;

        private struct ToastRequest
        {
            public string Message;
            public float Duration;
            public double QueuedAt;
        }

        private void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
            _canvasGroup.alpha = 0f;
        }

        private void OnDisable()
        {
            StopHideTimer();
            _pending.Clear();
            _currentMessage = null;
            _finishHoldEarly = false;
            if (_canvasGroup != null) _canvasGroup.alpha = 0f;
        }

        // 시간을 생략하거나 유효하지 않은 값을 전달하면 Inspector의 기본값을 사용합니다.
        public void Show(string message, float? duration = null)
        {
            if (string.IsNullOrWhiteSpace(message)) return;
            RemoveExpiredRequests();

            // 중복 요청은 현재 표시 시간과 대기 시작 시각을 갱신하지 않습니다.
            if (message == _currentMessage) return;
            foreach (var pending in _pending)
                if (pending.Message == message) return;

            float displayDuration = duration ?? defaultDuration;
            if (!IsValidDuration(displayDuration)) displayDuration = defaultDuration;
            if (!IsValidDuration(displayDuration)) displayDuration = 2f;
            var request = new ToastRequest
            {
                Message = message, Duration = displayDuration,
                QueuedAt = Time.realtimeSinceStartupAsDouble
            };

            if (_hideRoutine != null)
            {
                if (_pending.Count >= MaxPendingCount) _pending.Dequeue();
                _pending.Enqueue(request);
                // 새 요청을 받은 현재 토스트의 유지 시간만 줄입니다. 진행 중인 페이드는 끝까지 재생합니다.
                _finishHoldEarly = true;
                return;
            }

            if (!gameObject.activeSelf)
                gameObject.SetActive(true);
            if (isActiveAndEnabled)
                _hideRoutine = StartCoroutine(DisplayMessages(request));
        }

        // 강제로 닫거나 비활성화하면 즉시 숨기고 대기 중인 메시지도 함께 버립니다.
        public void Hide()
        {
            gameObject.SetActive(false);
        }

        private IEnumerator DisplayMessages(ToastRequest request)
        {
            while (true)
            {
                _canvasGroup.alpha = 0f;
                _currentMessage = request.Message;
                _finishHoldEarly = false;
                if (messageText != null) messageText.text = request.Message;
                transform.SetAsLastSibling();
                SoundManager.Play(SoundKey.UIBack);
                // 표시 시간과 대기 만료 모두 Time.timeScale의 영향을 받지 않습니다.
                yield return FadeTo(1f, fadeInDuration);
                double holdUntil = Time.realtimeSinceStartupAsDouble + request.Duration;
                while (!_finishHoldEarly && Time.realtimeSinceStartupAsDouble < holdUntil)
                    yield return null;
                yield return FadeTo(0f, fadeOutDuration);

                // 완전히 투명해진 뒤에만 다음 문구를 선택합니다.
                RemoveExpiredRequests();
                if (_pending.Count == 0) break;
                request = _pending.Dequeue();
            }
            _hideRoutine = null;
            Hide();
        }

        private IEnumerator FadeTo(float targetAlpha, float duration)
        {
            if (IsValidDuration(duration))
            {
                float startAlpha = _canvasGroup.alpha;
                float elapsed = 0f;
                while (elapsed < duration)
                {
                    _canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, Mathf.SmoothStep(0f, 1f, elapsed / duration));
                    yield return null;
                    elapsed += Time.unscaledDeltaTime;
                }
            }
            _canvasGroup.alpha = targetAlpha;
        }

        private void RemoveExpiredRequests()
        {
            double now = Time.realtimeSinceStartupAsDouble;
            while (_pending.Count > 0 && now - _pending.Peek().QueuedAt > MaxPendingAgeSeconds)
                _pending.Dequeue();
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
