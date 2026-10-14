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
        [SerializeField] private Button skillSelectButton;

        [Header("Extended UI References")]
        [SerializeField] private GameObject[] roleTags; // 0: Infiltrator, 1: Striker, 2: Anchor, 3: Suppressor
        [SerializeField] private TextMeshProUGUI levelAmountText;
        [SerializeField] private Image[] skillIcons; // Size 4

        private int _slotIndex;
        private UI_CharacterDeckPanel _parentPanel;
        private CharData _currentCharData;

        public void Initialize(int slotIndex, UI_CharacterDeckPanel parentPanel)
        {
            _slotIndex = slotIndex;
            _parentPanel = parentPanel;

            if (changeButton != null)
            {
                changeButton.onClick.RemoveAllListeners();
                changeButton.onClick.AddListener(OnChangeButtonClicked);
            }

            if (skillSelectButton != null)
            {
                skillSelectButton.onClick.RemoveAllListeners();
                skillSelectButton.onClick.AddListener(OnSkillSelectButtonClicked);
            }
        }

        public void SetCharacter(CharData charData)
        {
            _currentCharData = charData;

            if (charData == null)
            {
                // 빈 슬롯 처리
                if (nameText != null) nameText.text = "Empty";
                if (levelText != null) levelText.text = "";
                if (levelAmountText != null) levelAmountText.text = "";
                if (characterImage != null) characterImage.enabled = false;
                
                if (roleTags != null)
                {
                    foreach (var tag in roleTags) if (tag != null) tag.SetActive(false);
                }
                
                if (skillIcons != null)
                {
                    foreach (var icon in skillIcons) if (icon != null) icon.enabled = false;
                }
                return;
            }

            if (nameText != null) nameText.text = charData.name;
            if (levelText != null) levelText.text = "Lv. 15";
            
            if (roleTags != null && roleTags.Length > 0)
            {
                for (int i = 0; i < roleTags.Length; i++)
                {
                    if (roleTags[i] != null) roleTags[i].SetActive((int)charData.role == (i + 1));
                }
            }

            if (UserDataManager.Instance != null)
            {
                var userChar = UserDataManager.Instance.GetCharacter(charData.id);
                if (userChar != null)
                {
                    if (levelAmountText != null) levelAmountText.text = $"Lv. {userChar.level}";
                    
                    if (skillIcons != null && skillIcons.Length == 4)
                    {
                        for (int i = 0; i < 4; i++)
                        {
                            if (skillIcons[i] == null) continue;

                            int skillId = i < userChar.equippedSkills.Length ? userChar.equippedSkills[i] : 0;
                            if (skillId <= 0 || DataManager.Instance == null)
                            {
                                skillIcons[i].enabled = false;
                                continue;
                            }

                            var skillData = DataManager.Instance.GetData<SkillData>("Skill", skillId);
                            if (skillData == null || string.IsNullOrEmpty(skillData.resourceIcon))
                            {
                                skillIcons[i].enabled = false;
                                continue;
                            }

                            Sprite icon = SkillIconLoader.Load(skillData.resourceIcon);
                            skillIcons[i].sprite = icon;
                            skillIcons[i].enabled = icon != null;
                        }
                    }
                }
            }
            
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

        private void OnChangeButtonClicked()
        {
            if (_parentPanel != null)
            {
                _parentPanel.OnDeckNodeChangeRequested(_slotIndex);
            }
        }

        private void OnSkillSelectButtonClicked()
        {
            if (_currentCharData != null && OutGameUIManager.Instance != null)
            {
                var popup = OutGameUIManager.Instance.ShowPopup<SkillSelectPopup>(OutGamePopupType.SkillSelect);
                if (popup != null)
                {
                    popup.Open(_currentCharData.id, 0);
                }
            }
        }
    }
}