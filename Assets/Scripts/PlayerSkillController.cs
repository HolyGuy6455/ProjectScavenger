using UnityEngine;
using System.Collections.Generic;
using UnityEngine.InputSystem;

public class PlayerSkillController : MonoBehaviour
{
    private Dictionary<int, CharacterSkillState> charSkillStates = new Dictionary<int, CharacterSkillState>();
    private CharData currentCharData;

    private void Start()
    {
        InitializeSkillStates();
    }

    private void InitializeSkillStates()
    {
        if (DataManager.Instance == null) return;

        List<CharData> allChars = DataManager.Instance.GetAllData<CharData>("Char");
        foreach (var charData in allChars)
        {
            charSkillStates[charData.id] = new CharacterSkillState(charData);
        }
    }

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
        currentCharData = charData;
        Debug.Log($"[PlayerSkillController] 선택된 캐릭터 설정됨: {charData.name}");
    }

    private void Update()
    {
        // 1. 모든 캐릭터의 쿨타임 감소 (백그라운드 포함)
        float dt = Time.deltaTime;
        foreach (var state in charSkillStates.Values)
        {
            state.UpdateAllCooldowns(dt);
        }

        if (currentCharData == null) return;

        var keyboard = Keyboard.current;
        if (keyboard != null)
        {
            // 2. 현재 캐릭터의 스킬 입력 처리 (새로운 Input System 방식)
            if (keyboard.digit1Key.wasPressedThisFrame) TryUseSkill(0);
            if (keyboard.digit2Key.wasPressedThisFrame) TryUseSkill(1);
            if (keyboard.digit3Key.wasPressedThisFrame) TryUseSkill(2);
            if (keyboard.digit4Key.wasPressedThisFrame) TryUseSkill(3);
        }
    }

    private void TryUseSkill(int skillSlotIndex)
    {
        if (!charSkillStates.TryGetValue(currentCharData.id, out CharacterSkillState state)) return;

        if (skillSlotIndex < 0 || skillSlotIndex >= state.normalSkills.Length) return;

        SkillInstance skill = state.normalSkills[skillSlotIndex];
        if (skill.skillId == 0) return; // 빈 슬롯

        SkillData skillData = skill.Data;
        if (skillData == null)
        {
            Debug.LogWarning($"[PlayerSkillController] ID {skill.skillId}에 해당하는 스킬 데이터를 찾을 수 없습니다.");
            return;
        }

        if (!skill.IsReady)
        {
            Debug.Log($"[PlayerSkillController] 스킬 '{skillData.name}' 쿨타임 중입니다. ({skill.currentCooldown:F1}초 남음)");
            return;
        }

        // 스킬 발동
        skill.Use();
        Debug.Log($"[PlayerSkillController] 스킬 발동! 슬롯: {skillSlotIndex + 1}, 스킬 이름: {skillData.name}");
        
        Shooter playerShooter = GetComponent<Shooter>();
        if (playerShooter != null)
        {
            ExecuteSkillFunc(skillData.func1, playerShooter);
            ExecuteSkillFunc(skillData.func2, playerShooter);
        }
    }

    private void ExecuteSkillFunc(int funcCode, Shooter playerShooter)
    {
        switch (funcCode)
        {
            case 1: // 무기 1로 전환
                SwitchWeapon(playerShooter, currentCharData.gun1st);
                break;
            case 2: // 무기 2로 전환
                SwitchWeapon(playerShooter, currentCharData.gun2nd);
                break;
            case 3: // 무기 3으로 전환 (현재 데이터 구조에는 1, 2번 무기만 존재)
                Debug.LogWarning("[PlayerSkillController] 무기 3으로 전환 기능은 아직 데이터 구조가 지원하지 않습니다.");
                break;
            case 4: // 다음 무기로 전환 (1번 <-> 2번)
                ToggleWeapon(playerShooter);
                break;
            case 5: // 재장전
                playerShooter.Reload();
                break;
        }
    }

    private void SwitchWeapon(Shooter playerShooter, int gunId)
    {
        if (gunId <= 0) return; // 무기가 없는 경우 무시

        GunData newGun = DataManager.Instance.GetData<GunData>("Gun", gunId);
        if (newGun != null && (playerShooter.CurrentGunData == null || playerShooter.CurrentGunData.id != newGun.id))
        {
            playerShooter.EquipGun(newGun);
            Debug.Log($"[PlayerSkillController] 무기 전환: {newGun.gunName}");
        }
    }

    private void ToggleWeapon(Shooter playerShooter)
    {
        if (playerShooter.CurrentGunData == null) return;

        int currentGunId = playerShooter.CurrentGunData.id;
        int nextGunId = (currentGunId == currentCharData.gun1st) ? currentCharData.gun2nd : currentCharData.gun1st;

        if (nextGunId > 0)
        {
            SwitchWeapon(playerShooter, nextGunId);
        }
        else
        {
            Debug.Log("[PlayerSkillController] 교체할 다음 무기가 없습니다.");
        }
    }
}
