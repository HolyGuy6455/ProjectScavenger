using UnityEngine;
using UnityEngine.UI;
using System;

namespace Scavenger.UI
{
    public class UI_StorageTab : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private GameObject selectedBackground;
        
        [Tooltip("Inspector에서 이 탭이 어떤 카테고리인지 지정해주세요.")]
        [SerializeField] private ItemCategory tabCategory;
        public ItemCategory TabCategory => tabCategory;

        private Action<ItemCategory> _onClickCallback;

        private void Awake()
        {
            if (button == null) button = GetComponent<Button>();
            if (button != null)
            {
                button.onClick.AddListener(OnTabClicked);
            }
        }

        public void Setup(Action<ItemCategory> onClick)
        {
            _onClickCallback = onClick;
        }

        public void SetSelected(bool isSelected)
        {
            if (selectedBackground != null)
            {
                selectedBackground.SetActive(isSelected);
            }
        }

        private void OnTabClicked()
        {
            _onClickCallback?.Invoke(tabCategory);
        }
    }
}
