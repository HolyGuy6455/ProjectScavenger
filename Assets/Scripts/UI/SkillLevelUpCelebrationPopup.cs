using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SkillLevelUpCelebrationPopup : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI levelAmount;
    [SerializeField] private Image skillIcon;
    [SerializeField] private TextMeshProUGUI skillName;
    [SerializeField] private TextMeshProUGUI skillDescription;

    public void Open(SkillData newSkillData)
    {
        if (newSkillData == null) return;

        if (levelAmount != null)
        {
            levelAmount.text = $"Lv.{newSkillData.lvl}";
        }

        if (skillName != null)
        {
            skillName.text = newSkillData.name;
        }

        if (skillDescription != null)
        {
            skillDescription.text = newSkillData.description;
        }

        if (skillIcon != null)
        {
            string iconPath = System.IO.Path.GetFileNameWithoutExtension(newSkillData.resourceIcon);
            Sprite loadedSprite = Resources.Load<Sprite>($"skill/{iconPath}");
            if (loadedSprite == null) loadedSprite = Resources.Load<Sprite>(iconPath);
            skillIcon.sprite = loadedSprite;
        }

        gameObject.SetActive(true);
    }
}
