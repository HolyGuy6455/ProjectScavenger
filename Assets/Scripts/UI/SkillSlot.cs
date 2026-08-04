using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class SkillSlot : MonoBehaviour
{
    [SerializeField] private Image icon;
    [SerializeField] private TextMeshProUGUI cooltimeCounter;
    [SerializeField] private GuageBarUI guageBar;

    private SkillInstance currentSkill;
    private int slotIndex;
    private Sprite normalSprite;
    private Sprite toggledSprite;

    private void Awake()
    {
        Button btn = GetComponentInChildren<Button>();
        if (btn != null)
        {
            EventTrigger trigger = btn.gameObject.GetComponent<EventTrigger>();
            if (trigger == null) trigger = btn.gameObject.AddComponent<EventTrigger>();

            EventTrigger.Entry downEntry = new EventTrigger.Entry();
            downEntry.eventID = EventTriggerType.PointerDown;
            downEntry.callback.AddListener((data) => { OnPointerDown(); });
            trigger.triggers.Add(downEntry);

            EventTrigger.Entry upEntry = new EventTrigger.Entry();
            upEntry.eventID = EventTriggerType.PointerUp;
            upEntry.callback.AddListener((data) => { OnPointerUp(); });
            trigger.triggers.Add(upEntry);
        }
    }

    private void OnPointerDown()
    {
        if (PlayerSkillController.Instance != null && currentSkill != null && currentSkill.skillId != 0)
        {
            PlayerSkillController.Instance.OnSkillPointerDown(slotIndex);
        }
    }

    private void OnPointerUp()
    {
        if (PlayerSkillController.Instance != null && currentSkill != null && currentSkill.skillId != 0)
        {
            PlayerSkillController.Instance.OnSkillPointerUp(slotIndex);
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
        normalSprite = null;
        toggledSprite = null;

        if (data != null)
        {
            if (!string.IsNullOrEmpty(data.resourceIcon))
            {
                string name1 = System.IO.Path.GetFileNameWithoutExtension(data.resourceIcon);
                normalSprite = Resources.Load<Sprite>(name1);
            }
            if (!string.IsNullOrEmpty(data.resourceIcon2))
            {
                string name2 = System.IO.Path.GetFileNameWithoutExtension(data.resourceIcon2);
                toggledSprite = Resources.Load<Sprite>(name2);
            }
        }

        if (normalSprite != null)
        {
            icon.sprite = normalSprite;
        }
        else
        {
            icon.sprite = Resources.Load<Sprite>("skill_reload_nothing");
        }
    }

    private void Update()
    {
        if (currentSkill == null || currentSkill.skillId == 0) return;

        if (currentSkill.isToggledOn && toggledSprite != null)
        {
            icon.sprite = toggledSprite;
        }
        else if (normalSprite != null)
        {
            icon.sprite = normalSprite;
        }
        else
        {
            icon.sprite = Resources.Load<Sprite>("skill_reload_nothing");
        }

        float cd = currentSkill.currentCooldown;
        if (guageBar != null)
        {
            float currentRemain = currentSkill.remainAmount;
            float maxRemain = currentSkill.Data != null ? currentSkill.Data.remain : 1f;
            
            if (maxRemain > 0f)
            {
                guageBar.UpdateGuage(currentRemain, maxRemain);
            }
            else
            {
                guageBar.UpdateGuage(0f, 1f);
            }
        }

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
