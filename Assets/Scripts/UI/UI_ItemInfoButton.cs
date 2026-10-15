using UnityEngine;
using UnityEngine.UI;

namespace Scavenger.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public class UI_ItemInfoButton : MonoBehaviour
    {
        [Tooltip("표시할 Item 데이터의 ID입니다. 0이면 아이템 정보 버튼을 비활성화합니다.")]
        [SerializeField, Min(0)] private int itemId;

        private Button _button;

        public int ItemId => itemId;

        private void OnEnable()
        {
            _button = GetComponent<Button>();
            _button.onClick.AddListener(OnClick);
            RefreshInteractable();
        }

        private void OnDisable()
        {
            if (_button != null) _button.onClick.RemoveListener(OnClick);
        }

        public void SetItemId(int value)
        {
            itemId = Mathf.Max(0, value);
            RefreshInteractable();
        }

        public void OnClick()
        {
            if (!isActiveAndEnabled || itemId <= 0) return;
            OutGameUIManager.Instance?.ShowItemInfoPopup(itemId);
        }

        private void RefreshInteractable()
        {
            if (_button == null) _button = GetComponent<Button>();
            _button.interactable = itemId > 0;
        }
    }
}
