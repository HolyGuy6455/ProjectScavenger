using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SkillLevelUpCelebrationPopup : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI levelAmount;
    [SerializeField] private Image skillIcon;
    [SerializeField] private TextMeshProUGUI skillName;
    [SerializeField] private TextMeshProUGUI skillDescription;

    private SkillData _currentSkillData;

    private void OnEnable()
    {
        LocalizationManager.OnLanguageChanged += RefreshLocalizedTexts;
        RefreshLocalizedTexts();
    }

    private void OnDisable()
    {
        LocalizationManager.OnLanguageChanged -= RefreshLocalizedTexts;
    }

    public void Open(SkillData newSkillData)
    {
        if (newSkillData == null) return;

        _currentSkillData = newSkillData;

        if (levelAmount != null)
        {
            levelAmount.text = newSkillData.lvl.ToString();
        }

        RefreshLocalizedTexts();

        if (skillIcon != null)
        {
            Sprite loadedSprite = SpriteManager.Instance.LoadSprite(newSkillData.resourceIcon);
            skillIcon.sprite = loadedSprite;
        }

        gameObject.SetActive(true);
    }

    private void RefreshLocalizedTexts()
    {
        if (_currentSkillData == null) return;

        if (skillName != null)
        {
            Scavenger.UI.LocalizedText.ApplyFont(skillName);
            skillName.text = _currentSkillData.GetLocalizedName();
        }

        if (skillDescription != null)
        {
            Scavenger.UI.LocalizedText.ApplyFont(skillDescription);
            skillDescription.text = _currentSkillData.GetLocalizedDescription();
        }
    }
}
