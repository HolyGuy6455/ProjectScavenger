using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Scavenger.UI
{
    public class DefaultConfirmPopup : MonoBehaviour
    {
        [Header("안내 문구")]
        [SerializeField] private TextMeshProUGUI descriptionText;
        [SerializeField] private TextMeshProUGUI messageText;

        [Header("버튼")]
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button cancelButton;
        [SerializeField] private TextMeshProUGUI confirmButtonText;
        [SerializeField] private TextMeshProUGUI cancelButtonText;

        private Action _onConfirm;
        private Action _onCancel;
        private bool _isAwaitingResponse;

        private void OnEnable()
        {
            _isAwaitingResponse = true;
            if (confirmButton != null) confirmButton.onClick.AddListener(Confirm);
            if (cancelButton != null) cancelButton.onClick.AddListener(Cancel);
        }

        private void OnDisable()
        {
            if (confirmButton != null) confirmButton.onClick.RemoveListener(Confirm);
            if (cancelButton != null) cancelButton.onClick.RemoveListener(Cancel);
            ClearCallbacks();
        }

        // 매니저가 비활성 상태의 캐시된 팝업에도 데이터를 전달할 수 있습니다.
        public void Configure(string message, Action onConfirm, Action onCancel = null,
            string description = null, string confirmLabel = "확인", string cancelLabel = "취소")
        {
            _onConfirm = onConfirm;
            _onCancel = onCancel;
            _isAwaitingResponse = true;

            if (messageText != null) messageText.text = message ?? string.Empty;
            if (descriptionText != null)
            {
                descriptionText.text = description ?? string.Empty;
                descriptionText.gameObject.SetActive(!string.IsNullOrWhiteSpace(description));
            }
            if (confirmButtonText != null)
                confirmButtonText.text = string.IsNullOrEmpty(confirmLabel) ? "확인" : confirmLabel;
            if (cancelButtonText != null)
                cancelButtonText.text = string.IsNullOrEmpty(cancelLabel) ? "취소" : cancelLabel;
        }

        public void Open(string message, Action onConfirm, Action onCancel = null,
            string description = null, string confirmLabel = "확인", string cancelLabel = "취소")
        {
            Configure(message, onConfirm, onCancel, description, confirmLabel, cancelLabel);
            gameObject.SetActive(true);
        }

        public void Confirm() => Complete(true);

        public void Cancel() => Complete(false);

        // 코드에서 닫을 때는 확인/취소 콜백을 실행하지 않습니다.
        public void Close()
        {
            ClearCallbacks();
            if (OutGameUIManager.Instance != null)
                OutGameUIManager.Instance.ClosePopup(gameObject);
            else
                gameObject.SetActive(false);
        }

        private void Complete(bool confirmed)
        {
            if (!_isAwaitingResponse || !isActiveAndEnabled) return;

            Action callback = confirmed ? _onConfirm : _onCancel;
            // 콜백에서 다음 팝업을 열어도 현재 팝업의 닫기 처리가 영향을 주지 않게 합니다.
            Close();
            callback?.Invoke();
        }

        private void ClearCallbacks()
        {
            _isAwaitingResponse = false;
            _onConfirm = null;
            _onCancel = null;
        }
    }
}
