using UnityEngine;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using Unity.Netcode;

public class PlayerSkillController : NetworkBehaviour
{
    public static PlayerSkillController Instance { get; private set; }

    private List<int> partyCharIds = new List<int>();
    private CharData currentCharData;
    private Shooter cachedShooter;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        
        // 멀티플레이어 환경에서는 내 캐릭터(IsOwner)의 스킬 컨트롤러만 Instance로 등록합니다.
        if (IsOwner)
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[PlayerSkillController] 로컬 플레이어 스킬 컨트롤러가 이미 존재하여 덮어씁니다.");
            }
            Instance = this;

            if (CharacterStateManager.Instance.CurrentCharData != null)
            {
                HandleCharacterChanged(CharacterStateManager.Instance.CurrentCharData);
            }
        }
    }

    private void Start()
    {
    }

    public CharacterState GetSkillState(int charId)
    {
        if (CharacterStateManager.Instance != null)
        {
            return CharacterStateManager.Instance.GetState(charId);
        }
        return null;
    }

    public bool IsReloading()
    {
        if (currentCharData == null) return false;
        CharacterState state = GetSkillState(currentCharData.id);
        if (state == null) return false;

        foreach (var skill in state.normalSkills)
        {
            if (skill == null || skill.skillId == 0) continue;
            SkillData data = skill.Data;
            if (data == null) continue;

            bool hasReloadFunc = false;
            if (data.func == 5 || data.func == 6)
            {
                hasReloadFunc = true;
            }

            if (hasReloadFunc)
            {
                if (data.activeType == SkillActiveType.Continuous)
                {
                    if (skill.isContinuousRunning) return true;
                }
                else if (data.activeType == SkillActiveType.Instant)
                {
                    // Instant 스킬은 발동 직후 duration 시간 동안을 재장전 중(진행 중)으로 간주
                    float elapsed = data.cooltime - skill.currentCooldown;
                    if (skill.currentCooldown > 0f && elapsed < data.duration)
                    {
                        return true;
                    }
                }
            }
        }
        return false;
    }

    private void OnEnable()
    {
        CharacterStateManager.Instance.OnCharacterChanged += HandleCharacterChanged;
    }

    private void OnDisable()
    {
        CharacterStateManager.Instance.OnCharacterChanged -= HandleCharacterChanged;
    }

    private void HandleCharacterChanged(CharData charData)
    {
        Shooter playerShooter = GetShooter();

        // 1. 기존 캐릭터의 현재 무기 잔탄 저장
        if (currentCharData != null && playerShooter != null && playerShooter.CurrentAmmoData != null)
        {
            CharacterState oldState = GetSkillState(currentCharData.id);
            if (oldState != null)
            {
                oldState.savedChamber.Clear();
                foreach (var group in playerShooter.chamberAmmoQueue)
                {
                    oldState.savedChamber.Add(group);
                }
            }
        }

        currentCharData = charData;
        Debug.Log($"[PlayerSkillController] 선택된 캐릭터 설정됨: {charData.name}");

        // 2. 새 캐릭터의 무기 장착 (이전 상태 복원)
        if (playerShooter != null)
        {
            CharacterState newState = GetSkillState(charData.id);
            if (newState != null)
            {
                int gunToEquip = charData.GetGunId(0);
                
                AmmoData assignedGun = DataManager.Instance.GetData<AmmoData>("Ammo", gunToEquip);
                if (assignedGun != null)
                {
                    playerShooter.currentShotType = charData.shotType;
                    playerShooter.EquipGunWithChamber(assignedGun, newState.savedChamber.ToArray());
                }
            }
        }
    }

    private Shooter GetShooter()
    {
        if (cachedShooter == null) cachedShooter = FindObjectOfType<Shooter>();
        return cachedShooter;
    }

    private void Update()
    {
        if (currentCharData == null) return;

        if (IsOwner)
        {
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                HandleSkillInput(keyboard);
            }
        }

        // 현재 캐릭터의 Continuous 스킬 로직만 처리 (쿨타임 감소는 CharacterStateManager가 일괄 처리함)
        CharacterState currentState = GetSkillState(currentCharData.id);
        if (currentState != null)
        {
            foreach (var skill in currentState.normalSkills)
            {
                if (skill == null || skill.skillId == 0) continue;

                if (skill.isContinuousRunning)
                {
                    skill.UpdateContinuous(Time.deltaTime, GetShooter());
                }
                
                skill.UpdateInstant(GetShooter());
            }
        }
    }

    private void HandleSkillInput(Keyboard keyboard)
    {
        if (keyboard.qKey.wasPressedThisFrame) OnSkillPointerDown(0);
        if (keyboard.qKey.wasReleasedThisFrame) OnSkillPointerUp(0);
        if (keyboard.eKey.wasPressedThisFrame) OnSkillPointerDown(1);
        if (keyboard.eKey.wasReleasedThisFrame) OnSkillPointerUp(1);
        if (keyboard.rKey.wasPressedThisFrame) OnSkillPointerDown(2);
        if (keyboard.rKey.wasReleasedThisFrame) OnSkillPointerUp(2);
        if (keyboard.fKey.wasPressedThisFrame) OnSkillPointerDown(3);
        if (keyboard.fKey.wasReleasedThisFrame) OnSkillPointerUp(3);
    }

    [ServerRpc]
    public void CmdUseUltimateServerRpc()
    {
        // 서버(데디케이티드 서버)에는 UI가 없으므로 currentCharData나 CharacterStateManager가 
        // 클라이언트와 동일하게 세팅되어 있지 않습니다.
        // 클라이언트에서 쿨타임 검증을 마치고 보냈으므로, 여기서는 모든 클라이언트에게 연출을 지시합니다.
        RpcPlayUltimateClientRpc();
    }

    [ClientRpc]
    private void RpcPlayUltimateClientRpc()
    {
        var playerController = GetComponent<PlayerController>();
        if (playerController != null)
        {
            BaseUltimateSkill activeUlt = playerController.GetActiveUltimateSkill();
            if (activeUlt != null)
            {
                var data = DataManager.Instance.GetData<SkillData>("Skill", currentCharData.ultimateSkill);
                activeUlt.OnClientExecute(data, IsOwner);
            }
        }
    }

    /// <summary>
    /// 애니메이션 이벤트에서 호출되는 함수입니다.
    /// </summary>
    public void OnUltimateAnimationEvent(int eventId)
    {
        if (IsOwner)
        {
            CmdExecuteUltimateActionServerRpc(eventId);
        }
    }

    [ServerRpc]
    private void CmdExecuteUltimateActionServerRpc(int eventId)
    {
        var playerController = GetComponent<PlayerController>();
        if (playerController != null)
        {
            BaseUltimateSkill activeUlt = playerController.GetActiveUltimateSkill();
            if (activeUlt != null && currentCharData != null)
            {
                var data = DataManager.Instance.GetData<SkillData>("Skill", currentCharData.ultimateSkill);
                activeUlt.OnServerExecute(data, eventId);
            }
        }
    }

    public void OnSkillPointerDown(int skillSlotIndex)
    {
        if (currentCharData == null) return;
        CharacterState state = GetSkillState(currentCharData.id);
        if (state == null) return;
        if (skillSlotIndex < 0 || skillSlotIndex >= state.normalSkills.Length) return;

        SkillInstance skill = state.normalSkills[skillSlotIndex];
        if (skill == null || skill.skillId == 0) return;

        skill.Use(state, GetShooter());
    }

    public void OnSkillPointerUp(int skillSlotIndex)
    {
        if (currentCharData == null) return;
        CharacterState state = GetSkillState(currentCharData.id);
        if (state == null) return;
        if (skillSlotIndex < 0 || skillSlotIndex >= state.normalSkills.Length) return;

        SkillInstance skill = state.normalSkills[skillSlotIndex];
        if (skill == null || skill.skillId == 0) return;

        skill.ReleaseUse(GetShooter());
    }

}
