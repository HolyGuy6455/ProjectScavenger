using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SkillSlot : MonoBehaviour
{
    [SerializeField] private Image icon;
    [SerializeField] private TextMeshProUGUI cooltimeCounter;

    private SkillInstance currentSkill;
    private int slotIndex;

    private void Awake()
    {
        Button btn = GetComponentInChildren<Button>();
        if (btn != null)
        {
            btn.onClick.AddListener(OnButtonClicked);
        }
    }

    private void OnButtonClicked()
    {
        if (PlayerSkillController.Instance != null && currentSkill != null && currentSkill.skillId != 0)
        {
            PlayerSkillController.Instance.TryUseSkill(slotIndex);
        }
    }

    public void Setup(SkillInstance skill, int index)
    {
        currentSkill = skill;
        slotIndex = index;
        if (skill == null || skill.skillId == 0)
        {
            icon.enabled = false;
            if (cooltimeCounter != null) cooltimeCounter.gameObject.SetActive(false);
            return;
        }

        icon.enabled = true;
        
        SkillData data = skill.Data;
        Sprite loadedSprite = null;

        if (data != null && !string.IsNullOrEmpty(data.resourceIcon))
        {
            string nameWithoutExt = System.IO.Path.GetFileNameWithoutExtension(data.resourceIcon);
            loadedSprite = Resources.Load<Sprite>(nameWithoutExt);
        }

        if (loadedSprite != null)
        {
            icon.sprite = loadedSprite;
        }
        else
        {
            // 리소스가 없거나 실패한 경우 디폴트 이미지 세팅
            icon.sprite = Resources.Load<Sprite>("skill_reload_nothing");
        }
    }

    private void Update()
    {
        if (currentSkill == null || currentSkill.skillId == 0) return;

        float cd = currentSkill.currentCooldown;
        if (cd > 0f)
        {
            if (cooltimeCounter != null) cooltimeCounter.gameObject.SetActive(true);
            icon.color = Color.gray;

            if (cooltimeCounter != null)
            {
                if (cd >= 10f)
                {
                    cooltimeCounter.text = Mathf.CeilToInt(cd).ToString();
                }
                else
                {
                    cooltimeCounter.text = cd.ToString("F1");
                }
            }
        }
        else
        {
            if (cooltimeCounter != null) cooltimeCounter.gameObject.SetActive(false);
            icon.color = Color.white;
        }
    }
}
