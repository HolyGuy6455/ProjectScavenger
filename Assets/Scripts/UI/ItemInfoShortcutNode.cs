using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Scavenger.UI
{
    public class ItemInfoShortcutNode : MonoBehaviour
    {
        [Header("획득 경로")]
        [SerializeField] private Image routeIconImage;
        [SerializeField] private TextMeshProUGUI routeNameText;
        [SerializeField] private TextMeshProUGUI routeDescriptionText;
        [SerializeField] private Button shortcutButton;

        private string _shortcut;
        private string _shortcutValue;

        private void OnEnable()
        {
            LocalizationManager.OnLanguageChanged += RefreshTexts;
            RefreshTexts();
        }

        private void OnDisable()
        {
            LocalizationManager.OnLanguageChanged -= RefreshTexts;
        }

        public void Setup(string shortcut, string shortcutValue)
        {
            _shortcut = shortcut?.Trim() ?? string.Empty;
            _shortcutValue = shortcutValue?.Trim() ?? string.Empty;

            bool isStage = _shortcut == "STAGE_SELECT";
            RefreshTexts();
            if (routeIconImage != null) routeIconImage.enabled = isStage;
            if (shortcutButton != null) shortcutButton.interactable = !string.IsNullOrEmpty(_shortcut);
        }

        public void RefreshTexts()
        {
            if (string.IsNullOrEmpty(_shortcut)) return;

            bool isStage = _shortcut == "STAGE_SELECT";
            if (routeNameText != null)
            {
                LocalizedText.ApplyFont(routeNameText);
                routeNameText.text = isStage
                    ? (string.IsNullOrEmpty(_shortcutValue)
                        ? LocalizationManager.Get("UI_ITEMINFO_ROUTE_STAGE_GENERAL")
                        : LocalizationManager.Get("UI_ITEMINFO_ROUTE_STAGE_FORMAT", _shortcutValue))
                    : LocalizationManager.Get("UI_ITEMINFO_ROUTE_DEFAULT");
            }
            if (routeDescriptionText != null)
            {
                LocalizedText.ApplyFont(routeDescriptionText);
                routeDescriptionText.text = isStage
                    ? LocalizationManager.Get("UI_ITEMINFO_ROUTE_DESC_STAGE")
                    : string.Empty;
            }
        }

        public void OnClickShortcut()
        {
            OutGameUIManager manager = OutGameUIManager.Instance;
            if (manager == null || string.IsNullOrEmpty(_shortcut)) return;

            ItemInfoPopup popup = GetComponentInParent<ItemInfoPopup>();
            if (popup != null) manager.ClosePopup(popup.gameObject);
            manager.GoToQuestShortcut(_shortcut, _shortcutValue);
        }
    }
}
