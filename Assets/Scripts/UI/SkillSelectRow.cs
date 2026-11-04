using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

public class SkillSelectRow : MonoBehaviour
{
    [SerializeField] private Image skillIcon;
    [SerializeField] private TextMeshProUGUI skillNameText;
    [SerializeField] private TextMeshProUGUI skillLevelText;
    [SerializeField] private TextMeshProUGUI skillDescText;
    [SerializeField] private GameObject selectedBorder;
    
    [Header("Buttons")]
    [SerializeField] private GameObject selectButton;
    [SerializeField] private TextMeshProUGUI selectButtonText;
    [SerializeField] private GameObject unlockButton;
    [SerializeField] private GameObject levelUpButton;
    
    private Button _btnSelect;
    private Button _btnUnlock;
    private Button _btnLevelUp;
    
    private SkillData _currentSkill;
    
    public event Action<SkillData> OnSelectClicked;
    public event Action<SkillData> OnActionClicked;

    private void Awake()
    {
        if (selectButton != null)
        {
            _btnSelect = selectButton.GetComponentInChildren<Button>();
            if (_btnSelect != null) _btnSelect.onClick.AddListener(() => OnSelectClicked?.Invoke(_currentSkill));
        }
        if (unlockButton != null)
        {
            _btnUnlock = unlockButton.GetComponentInChildren<Button>();
            if (_btnUnlock != null) _btnUnlock.onClick.AddListener(() => OnActionClicked?.Invoke(_currentSkill));
        }
        if (levelUpButton != null)
        {
            _btnLevelUp = levelUpButton.GetComponentInChildren<Button>();
            if (_btnLevelUp != null) _btnLevelUp.onClick.AddListener(() => OnActionClicked?.Invoke(_currentSkill));
        }
    }

    public void Setup(SkillData skillData, bool isUnlocked, bool isEquipped, bool canLevelUp)
    {
        _currentSkill = skillData;
        
        if (skillIcon != null)
        {
            Sprite loadedSprite = SpriteManager.Instance.LoadSprite(skillData.resourceIcon);
            skillIcon.sprite = loadedSprite;
        }

        if (skillNameText != null)
        {
            Scavenger.UI.LocalizedText.ApplyFont(skillNameText);
            skillNameText.text = skillData.GetLocalizedName();
            float prefW = skillNameText.preferredWidth;
            skillNameText.rectTransform.sizeDelta = new Vector2(Mathf.Min(prefW + 4f, 470f), skillNameText.rectTransform.sizeDelta.y);
        }
        if (skillLevelText != null)
        {
            Scavenger.UI.LocalizedText.ApplyFont(skillLevelText);
            skillLevelText.text = isUnlocked ? $"Lv. {skillData.lvl}" : "Lv. 0";
        }
        if (skillDescText != null)
        {
            Scavenger.UI.LocalizedText.ApplyFont(skillDescText);
            skillDescText.text = skillData.GetLocalizedDescription();
        }

        if (selectedBorder != null) selectedBorder.SetActive(isEquipped);

        if (isEquipped)
        {
            if (_btnSelect != null) _btnSelect.interactable = false;
            if (selectButtonText != null)
            {
                Scavenger.UI.LocalizedText.ApplyFont(selectButtonText);
                selectButtonText.text = LocalizationManager.Get("UI_SKILLSELECT_BTN_EQUIPPED");
            }
        }
        else
        {
            if (_btnSelect != null) _btnSelect.interactable = isUnlocked;
            if (selectButtonText != null)
            {
                Scavenger.UI.LocalizedText.ApplyFont(selectButtonText);
                selectButtonText.text = LocalizationManager.Get("UI_SKILLSELECT_BTN_EQUIP");
            }
        }

        // 해금 여부에 따른 버튼 표시/숨김 제어
        if (selectButton != null) selectButton.SetActive(isUnlocked);
        
        if (isUnlocked)
        {
            if (unlockButton != null) unlockButton.SetActive(false);
            if (levelUpButton != null)
            {
                levelUpButton.SetActive(true);
                if (_btnLevelUp != null) _btnLevelUp.interactable = canLevelUp;
            }
        }
        else
        {
            if (unlockButton != null) unlockButton.SetActive(true);
            if (levelUpButton != null) levelUpButton.SetActive(false);
        }
    }
}
