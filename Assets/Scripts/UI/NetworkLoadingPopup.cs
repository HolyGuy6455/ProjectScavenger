using UnityEngine;

namespace Scavenger.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasGroup))]
    public class NetworkLoadingPopup : MonoBehaviour
    {
        [SerializeField] private RectTransform spinner;
        [Tooltip("스피너의 초당 시계 방향 회전 각도")]
        [SerializeField, Min(0f)] private float rotationSpeed = 360f;

        [Header("Fade")]
        [SerializeField, Min(0f)] private float fadeInDuration = 0.2f;
        [SerializeField, Min(0f)] private float fadeOutDuration = 0.2f;

        private CanvasGroup _canvasGroup;
        private float _visibility;
        private bool _showing;

        private void OnEnable()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
            transform.SetAsLastSibling();
            if (spinner != null) spinner.localRotation = Quaternion.identity;
            _visibility = fadeInDuration > 0f ? 0f : 1f;
            _canvasGroup.alpha = _visibility;
            _canvasGroup.blocksRaycasts = true;
            _showing = true;
        }

        public void Show()
        {
            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
                return;
            }

            // 페이드아웃 도중 다시 요청해도 현재 투명도에서 이어갑니다.
            _showing = true;
            _canvasGroup.blocksRaycasts = true;
        }

        public void Hide()
        {
            _showing = false;
            // 통신이 끝나면 입력은 즉시 돌려주고, 화면만 부드럽게 지웁니다.
            _canvasGroup.blocksRaycasts = false;
            if (fadeOutDuration <= 0f || _visibility <= 0f) gameObject.SetActive(false);
        }

        private void Update()
        {
            // 게임이 일시 정지되어도 네트워크 로딩 표시는 계속 회전합니다.
            if (spinner != null) spinner.Rotate(0f, 0f, -rotationSpeed * Time.unscaledDeltaTime);

            float target = _showing ? 1f : 0f;
            float duration = _showing ? fadeInDuration : fadeOutDuration;
            _visibility = duration > 0f
                ? Mathf.MoveTowards(_visibility, target, Time.unscaledDeltaTime / duration)
                : target;
            _canvasGroup.alpha = Mathf.SmoothStep(0f, 1f, _visibility);
            if (!_showing && _visibility <= 0f) gameObject.SetActive(false);
        }

        private void OnDisable()
        {
            _visibility = 0f;
            _showing = false;
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 0f;
                _canvasGroup.blocksRaycasts = false;
            }
        }
    }
}
