using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Scavenger.UI
{
    public class UI_CharacterDeckNode : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Image characterImage;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI levelText;
        [SerializeField] private Button changeButton;

        private int _slotIndex;
        private UI_CharacterDeckPanel _parentPanel;

        public void Initialize(int slotIndex, UI_CharacterDeckPanel parentPanel)
        {
            _slotIndex = slotIndex;
            _parentPanel = parentPanel;

            if (changeButton != null)
            {
                changeButton.onClick.RemoveAllListeners();
                changeButton.onClick.AddListener(OnChangeButtonClicked);
            }
        }

        public void SetCharacter(CharData charData)
        {
            if (charData != null)
            {
                if (nameText != null) nameText.text = charData.name;
                if (levelText != null) levelText.text = "Lv 15";
                
                if (characterImage != null && !string.IsNullOrEmpty(charData.resourceStand))
                {
                    // 확장자 제거 및 경로 조정 (예: char_001_stand.png -> char/char_001_stand)
                    string spritePath = charData.resourceStand.Replace(".png", "");
                    if (!spritePath.StartsWith("char/")) spritePath = "char/" + spritePath;

                    Sprite standSprite = Resources.Load<Sprite>(spritePath);
                    if (standSprite != null)
                    {
                        characterImage.sprite = standSprite;
                        characterImage.enabled = true;
                    }
                    else
                    {
                        Debug.LogWarning($"[UI_CharacterDeckNode] Stand Sprite not found at: {spritePath}");
                    }
                }
            }
            else
            {
                // 빈 슬롯 처리
                if (nameText != null) nameText.text = "Empty";
                if (levelText != null) levelText.text = "";
                if (characterImage != null) characterImage.enabled = false;
            }
        }

        private void OnChangeButtonClicked()
        {
            if (_parentPanel != null)
            {
                _parentPanel.OnDeckNodeChangeRequested(_slotIndex);
            }
        }
    }
}