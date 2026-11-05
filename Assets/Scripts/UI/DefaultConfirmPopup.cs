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
        private string _customConfirmLabel;
        private string _customCancelLabel;

        private void OnEnable()
        {
            _isAwaitingResponse = true;
            if (confirmButton != null) confirmButton.onClick.AddListener(Confirm);
            if (cancelButton != null) cancelButton.onClick.AddListener(Cancel);
            LocalizationManager.OnLanguageChanged += RefreshLocalizedTexts;
            RefreshLocalizedTexts();
        }

        private void OnDisable()
        {
            if (confirmButton != null) confirmButton.onClick.RemoveListener(Confirm);
            if (cancelButton != null) cancelButton.onClick.RemoveListener(Cancel);
            LocalizationManager.OnLanguageChanged -= RefreshLocalizedTexts;
            ClearCallbacks();
        }

        private void RefreshLocalizedTexts()
        {
            if (messageText != null) LocalizedText.ApplyFont(messageText);
            if (descriptionText != null) LocalizedText.ApplyFont(descriptionText);
            if (confirmButtonText != null)
            {
                LocalizedText.ApplyFont(confirmButtonText);
                confirmButtonText.text = string.IsNullOrEmpty(_customConfirmLabel) || _customConfirmLabel == "확인"
                    ? LocalizationManager.Get("UI_COMMON_CONFIRM")
                    : _customConfirmLabel;
            }
            if (cancelButtonText != null)
            {
                LocalizedText.ApplyFont(cancelButtonText);
                cancelButtonText.text = string.IsNullOrEmpty(_customCancelLabel) || _customCancelLabel == "취소"
                    ? LocalizationManager.Get("UI_COMMON_CANCEL")
                    : _customCancelLabel;
            }
        }

        // 매니저가 비활성 상태의 캐시된 팝업에도 데이터를 전달할 수 있습니다.
        public void Configure(string message, Action onConfirm, Action onCancel = null,
            string description = null, string confirmLabel = "확인", string cancelLabel = "취소")
        {
            _onConfirm = onConfirm;
            _onCancel = onCancel;
            _isAwaitingResponse = true;
            _customConfirmLabel = confirmLabel;
            _customCancelLabel = cancelLabel;

            if (messageText != null) messageText.text = message ?? string.Empty;
            if (descriptionText != null)
            {
                descriptionText.text = description ?? string.Empty;
                descriptionText.gameObject.SetActive(!string.IsNullOrWhiteSpace(description));
            }
            RefreshLocalizedTexts();
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
