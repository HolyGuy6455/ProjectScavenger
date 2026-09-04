using UnityEngine;
using System.Collections.Generic;

public class SkillPanel : MonoBehaviour
{
    [SerializeField] private List<SkillSlot> slots = new List<SkillSlot>();

    private void OnEnable()
    {
        CharacterStateManager.Instance.OnCharacterChanged += HandleCharacterChanged;
    }

    private void OnDisable()
    {
        CharacterStateManager.Instance.OnCharacterChanged -= HandleCharacterChanged;
    }

    private System.Collections.IEnumerator Start()
    {
        // 씬 로딩 직후(첫 스폰 시) PlayerSkillController가 뜰 때까지 대기
        while (PlayerSkillController.Instance == null)
        {
            yield return null;
        }

        // 대기가 끝나고 뷰가 열렸을 때, 이미 선택된 캐릭터가 있다면 강제로 한 번 갱신해줌 (초기화 버그 픽스)
        if (CharacterStateManager.Instance.CurrentCharData != null)
        {
            HandleCharacterChanged(CharacterStateManager.Instance.CurrentCharData);
        }
    }

    private void HandleCharacterChanged(CharData charData)
    {
        if (PlayerSkillController.Instance == null)
        {
            Debug.LogWarning("[SkillPanel] PlayerSkillController.Instance가 없습니다.");
            return;
        }

        CharacterState state = PlayerSkillController.Instance.GetSkillState(charData.id);
        
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
