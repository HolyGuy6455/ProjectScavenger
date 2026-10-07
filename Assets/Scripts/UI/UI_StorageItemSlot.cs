using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

namespace Scavenger.UI
{
    public class UI_StorageItemSlot : MonoBehaviour
    {
        [SerializeField] private Image iconImage;
        [SerializeField] private TextMeshProUGUI amountText;
        [SerializeField] private TextMeshProUGUI nameText; // 슬롯에 이름이 표시되는 경우 사용
        [SerializeField] private Button button; // 슬롯 클릭 버튼
        [SerializeField] private GameObject selectedBackground; // 선택 시 표시될 배경
        [SerializeField] private GameObject rootObject; // 비어있을 때 꺼질 전체 부모 오브젝트

        private int _currentItemId;
        public int CurrentItemId => _currentItemId;

        private Action<int> _onClickCallback;

        private void Awake()
        {
            if (button == null) button = GetComponent<Button>();
            if (button != null)
            {
                button.onClick.AddListener(OnSlotClicked);
            }
        }

        public void Setup(int itemId, int amount, Action<int> onClick)
        {
            _currentItemId = itemId;
            _onClickCallback = onClick;

            if (itemId == 0 || amount <= 0 || DataManager.Instance == null)
            {
                SetEmpty();
                return;
            }

            if (rootObject != null) rootObject.SetActive(true);

            ItemData data = DataManager.Instance.GetData<ItemData>("Item", itemId);
            if (data != null)
            {
                if (iconImage != null && !string.IsNullOrEmpty(data.uiResource))
                {
                    string path = data.uiResource.Replace(".png", "");
                    Sprite sprite = Resources.Load<Sprite>($"Items/{path}");
                    if (sprite == null) sprite = Resources.Load<Sprite>(path);
                    
                    if (sprite != null)
                    {
                        iconImage.sprite = sprite;
                        iconImage.enabled = true;
                    }
                    else
                    {
                        iconImage.enabled = false;
                    }
                }

                if (nameText != null) nameText.text = data.name;
            }
            else
            {
                SetEmpty();
                return;
            }

            if (amountText != null)
            {
                amountText.gameObject.SetActive(true);
                amountText.text = amount.ToString();
            }

            if (button != null) button.interactable = true;
        }

        public void SetSelected(bool isSelected)
        {
            if (selectedBackground != null)
            {
                selectedBackground.SetActive(isSelected);
            }
        }

        private void SetEmpty()
        {
            if (rootObject != null) rootObject.SetActive(false);
            SetSelected(false);
        }

        private void OnSlotClicked()
        {
            _onClickCallback?.Invoke(_currentItemId);
        }
    }
}
