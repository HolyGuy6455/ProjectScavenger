using UnityEngine;
using System.Collections.Generic;

public class SkillPanel : MonoBehaviour
{
    [SerializeField] private List<SkillSlot> slots = new List<SkillSlot>();

    private void OnEnable()
    {
        CharSelectorUI.OnCharacterChanged += HandleCharacterChanged;
    }

    private void OnDisable()
    {
        CharSelectorUI.OnCharacterChanged -= HandleCharacterChanged;
    }

    private void HandleCharacterChanged(CharData charData)
    {
        if (PlayerSkillController.Instance == null)
        {
            Debug.LogWarning("[SkillPanel] PlayerSkillController.Instance가 없습니다.");
            return;
        }

        CharacterSkillState state = PlayerSkillController.Instance.GetSkillState(charData.id);
        
        if (state == null || state.normalSkills == null)
        {
            for (int i = 0; i < slots.Count; i++) slots[i].Setup(null, i);
            return;
        }

        for (int i = 0; i < slots.Count; i++)
        {
            if (i < state.normalSkills.Length)
            {
                slots[i].Setup(state.normalSkills[i], i);
            }
            else
            {
                slots[i].Setup(null, i);
            }
        }
    }
}
