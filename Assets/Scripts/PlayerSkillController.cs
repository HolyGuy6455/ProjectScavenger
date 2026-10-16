using UnityEngine;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using Unity.Netcode;
using Scavenger.Manager;

public class PlayerSkillController : NetworkBehaviour
{
    public static PlayerSkillController Instance { get; private set; }

    private List<int> partyCharIds = new List<int>();
    private CharData currentCharData;
    private Shooter cachedShooter;
    private PlayerController cachedPlayerController;
    private Health cachedHealth;

    private bool HasDied
    {
        get
        {
            if (cachedPlayerController == null) cachedPlayerController = GetComponent<PlayerController>();
            if (cachedHealth == null) cachedHealth = GetComponent<Health>();
            return (cachedPlayerController != null && cachedPlayerController.IsDeathPresented)
                || (cachedHealth != null && (cachedHealth.IsDead || cachedHealth.CurrentHealth <= 0f));
        }
    }

    private bool IsInputLocked
    {
        get
        {
            if (cachedPlayerController == null) cachedPlayerController = GetComponent<PlayerController>();
            return cachedPlayerController != null && cachedPlayerController.IsInputLocked;
        }
    }

    private bool HasGameEnded
    {
        get
        {
            if (cachedHealth == null) cachedHealth = GetComponent<Health>();
            return (cachedHealth != null && cachedHealth.IsClearProtected)
                || (GameFlowManager.Instance != null
                    && GameFlowManager.Instance.CurrentState != GameResultState.None);
        }
    }

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
        if (cachedPlayerController == null) cachedPlayerController = GetComponent<PlayerController>();
        return cachedPlayerController != null ? cachedPlayerController.GetCharacterState(charId) : null;
    }

    public bool IsReloading()
    {
        if (HasDied || currentCharData == null) return false;
        CharacterState state = GetSkillState(currentCharData.id);
        if (state == null || state.normalSkills == null) return false;

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
                    if (!skill.isInstantOffExecuted && skill.currentCooldown > 0f && elapsed < data.duration)
                    {
                        return true;
                    }
                }
            }
        }
        // Keep aiming/fire blocked while the closing motion finishes after the last shell.
        return cachedPlayerController != null && cachedPlayerController.IsReloadPoseActive();
    }

    /// <summary>Stops this player's pending skill effects; spent resources and cooldowns are retained.</summary>
    public void CancelActiveActions()
    {
        // CharacterStateManager contains the local party, not other network players' skill state.
        if (!IsOwner) return;

        if (currentCharData != null) CancelStateActions(GetSkillState(currentCharData.id));
        if (cachedPlayerController == null) cachedPlayerController = GetComponent<PlayerController>();
        if (cachedPlayerController == null) return;

        var deck = cachedPlayerController.GetLocalDeck();
        for (int slot = 0; slot < 4; slot++)
        {
            int charId = deck.GetCharId(slot);
            if (charId > 0) CancelStateActions(GetSkillState(charId));
        }
    }

    private void CancelStateActions(CharacterState state)
    {
        if (state == null || state.normalSkills == null) return;
        foreach (var skill in state.normalSkills)
        {
            skill?.CancelActiveAction(GetShooter());
        }
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
        if (!IsOwner) return;
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

    // Remote players use their own replicated selection, never the local party's UI event.
    public void RefreshRemoteCharacterData()
    {
        if (cachedPlayerController == null) cachedPlayerController = GetComponent<PlayerController>();
        if (cachedPlayerController == null || cachedPlayerController.IsOwner || DataManager.Instance == null) return;
        int charId = cachedPlayerController.GetLocalDeck().GetCharId(cachedPlayerController.ActiveSlotIndex);
        currentCharData = DataManager.Instance.GetData<CharData>("Char", charId);
    }

    private Shooter GetShooter()
    {
        if (cachedShooter == null) cachedShooter = GetComponent<Shooter>();
        return cachedShooter;
    }

    private void Update()
    {
        if (!IsOwner) return;
        if (HasDied)
        {
            CancelActiveActions();
            return;
        }
        if (currentCharData == null) return;

        var keyboard = Keyboard.current;
        if (keyboard != null)
        {
            HandleSkillInput(keyboard);
        }

        // 현재 캐릭터의 Continuous 스킬 로직만 처리 (쿨타임 감소는 CharacterStateManager가 일괄 처리함)
        CharacterState currentState = GetSkillState(currentCharData.id);
        if (currentState != null && currentState.normalSkills != null)
        {
            foreach (var skill in currentState.normalSkills)
            {
                if (skill == null || skill.skillId == 0) continue;

                // A full-body action replaces the reload pose, so do not commit its unfinished shell.
                bool reloadInterrupted = skill.isContinuousRunning && skill.Data?.func == 6
                    && (IsInputLocked || (cachedPlayerController != null && cachedPlayerController.IsRolling));
                if ((IsInputLocked && skill.isCharging) || reloadInterrupted)
                {
                    skill.CancelActiveAction(GetShooter());
                }

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
        if (HasGameEnded || HasDied || IsInputLocked) return;
        // 서버(데디케이티드 서버)에는 UI가 없으므로 currentCharData나 CharacterStateManager가 
        // 클라이언트와 동일하게 세팅되어 있지 않습니다.
        // 클라이언트에서 쿨타임 검증을 마치고 보냈으므로, 여기서는 모든 클라이언트에게 연출을 지시합니다.
        RpcPlayUltimateClientRpc();
    }

    [ClientRpc]
    private void RpcPlayUltimateClientRpc()
    {
        if (HasGameEnded || HasDied || IsInputLocked) return;
        var playerController = GetComponent<PlayerController>();
        BaseUltimateSkill activeUlt = playerController != null
            ? playerController.GetActiveUltimateSkill() : null;
        if (activeUlt == null)
        {
            Debug.LogError($"[PlayerSkillController] {name}: 궁극기 연출을 시작할 수 없습니다. PlayerController와 활성 캐릭터의 BaseUltimateSkill 설정을 확인하세요.", this);
            return;
        }
        if (DataManager.Instance == null || currentCharData == null)
        {
            Debug.LogError($"[PlayerSkillController] {name}: 궁극기 실행 시 캐릭터/스킬 데이터가 준비되지 않았습니다.", this);
            return;
        }
        var data = DataManager.Instance.GetData<SkillData>("Skill", currentCharData.ultimateSkill);
        activeUlt.OnClientExecute(data, IsOwner);
    }

    /// <summary>
    /// 애니메이션 이벤트에서 호출되는 함수입니다.
    /// </summary>
    public void OnUltimateAnimationEvent(int eventId)
    {
        if (IsOwner && CanExecuteUltimateEvent())
        {
            CmdExecuteUltimateActionServerRpc(eventId);
        }
    }

    [ServerRpc]
    private void CmdExecuteUltimateActionServerRpc(int eventId)
    {
        if (!CanExecuteUltimateEvent()) return;
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

    private bool CanExecuteUltimateEvent()
    {
        if (HasDied) return false;
        // Dedicated servers do not play the cinematic. A clear input lock must not
        // discard animation events from the ultimate already running on its client.
        if (IsServer && !IsClient && cachedHealth != null && cachedHealth.IsClearProtected) return true;
        if (!IsInputLocked) return true;

        // A clear/exit wait blocks new input, but the ultimate already in progress must finish.
        if (cachedPlayerController == null) cachedPlayerController = GetComponent<PlayerController>();
        BaseUltimateSkill activeUltimate = cachedPlayerController != null
            ? cachedPlayerController.GetActiveUltimateSkill() : null;
        return activeUltimate != null && activeUltimate.IsPresentationPlaying;
    }

    public void OnSkillPointerDown(int skillSlotIndex)
    {
        if (HasGameEnded || HasDied || IsInputLocked || currentCharData == null) return;
        CharacterState state = GetSkillState(currentCharData.id);
        if (state == null) return;
        if (skillSlotIndex < 0 || skillSlotIndex >= state.normalSkills.Length) return;

        SkillInstance skill = state.normalSkills[skillSlotIndex];
        if (skill == null || skill.skillId == 0) return;

        // Different ammo skills share the same weapon and reload animation.
        if ((skill.Data?.func == 5 || skill.Data?.func == 6) && IsReloading()) return;

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

        if (HasGameEnded || HasDied || IsInputLocked)
        {
            skill.CancelActiveAction(GetShooter());
            return;
        }
        skill.ReleaseUse(GetShooter());
    }

}
