using System;
using UnityEngine;
using UnityEngine.UI;

namespace Scavenger.UI
{
    public class UI_CharacterSelectNode : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Image slotImage; 
        [SerializeField] private GameObject selectedBorder; 
        [SerializeField] private Button nodeButton;

        private CharData _charData;
        private Action<UI_CharacterSelectNode, CharData> _onSelectedCallback;

        public void Initialize(CharData data, Action<UI_CharacterSelectNode, CharData> onSelected)
        {
            _charData = data;
            _onSelectedCallback = onSelected;

            if (slotImage != null && data != null && !string.IsNullOrEmpty(data.resourceSlot))
            {
                Sprite faceSprite = SpriteManager.Instance.LoadSprite(data.resourceSlot);
                if (faceSprite != null)
                {
                    slotImage.sprite = faceSprite;
                    slotImage.enabled = true;
                }
                else
                {
                    slotImage.sprite = null;
                    slotImage.enabled = false;
                }
            }

            if (nodeButton != null)
            {
                nodeButton.onClick.RemoveAllListeners();
                nodeButton.onClick.AddListener(OnClickNode);
            }

            SetSelected(false);
        }

        private void OnClickNode()
        {
            _onSelectedCallback?.Invoke(this, _charData);
        }

        public void SetSelected(bool isSelected)
        {
            if (selectedBorder != null)
            {
                selectedBorder.SetActive(isSelected);
            }
        }
    }
}
