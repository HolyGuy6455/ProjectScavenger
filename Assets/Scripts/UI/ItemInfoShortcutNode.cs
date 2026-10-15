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

        public void Setup(string shortcut, string shortcutValue)
        {
            _shortcut = shortcut?.Trim() ?? string.Empty;
            _shortcutValue = shortcutValue?.Trim() ?? string.Empty;

            bool isStage = _shortcut == "STAGE_SELECT";
            if (routeNameText != null)
            {
                routeNameText.text = isStage
                    ? (string.IsNullOrEmpty(_shortcutValue) ? "스테이지에서 획득" : $"스테이지 {_shortcutValue}에서 획득")
                    : "획득 경로";
            }
            if (routeDescriptionText != null) routeDescriptionText.text = isStage ? "탐험 보상" : string.Empty;
            if (routeIconImage != null) routeIconImage.enabled = isStage;
            if (shortcutButton != null) shortcutButton.interactable = !string.IsNullOrEmpty(_shortcut);
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
