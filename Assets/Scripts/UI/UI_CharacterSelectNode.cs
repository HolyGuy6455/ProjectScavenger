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
                // 확장자 제거 및 경로 조정
                string spritePath = data.resourceSlot.Replace(".png", "");
                if (!spritePath.StartsWith("char/")) spritePath = "char/" + spritePath;

                Sprite faceSprite = Resources.Load<Sprite>(spritePath);
                if (faceSprite != null)
                {
                    slotImage.sprite = faceSprite;
                    slotImage.enabled = true;
                }
                else
                {
                    Debug.LogWarning($"[UI_CharacterSelectNode] Slot Sprite not found at: {spritePath}");
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