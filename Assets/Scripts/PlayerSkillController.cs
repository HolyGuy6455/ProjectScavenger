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

            if (CharSelectorUI.CurrentCharData != null)
            {
                HandleCharacterChanged(CharSelectorUI.CurrentCharData);
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

            if (data.func1 == 5 || data.func2 == 5 || data.func1 == 6 || data.func2 == 6)
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
        CharSelectorUI.OnCharacterChanged += HandleCharacterChanged;
    }

    private void OnDisable()
    {
        CharSelectorUI.OnCharacterChanged -= HandleCharacterChanged;
    }

    private void HandleCharacterChanged(CharData charData)
    {
        Shooter playerShooter = GetShooter();

        // 1. 기존 캐릭터의 현재 무기 잔탄 저장
        if (currentCharData != null && playerShooter != null && playerShooter.CurrentGunData != null)
        {
            CharacterState oldState = GetSkillState(currentCharData.id);
            if (oldState != null)
            {
                int ammoGroup = playerShooter.CurrentGunData.gunGroup > 0 ? playerShooter.CurrentGunData.gunGroup : playerShooter.CurrentGunData.id;
                oldState.gunAmmo[ammoGroup] = playerShooter.CurrentAmmo;
                oldState.lastGunId = playerShooter.CurrentGunData.id;
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
                int gunToEquip = newState.lastGunId > 0 ? newState.lastGunId : charData.gun1st;
                
                GunData assignedGun = DataManager.Instance.GetData<GunData>("Gun", gunToEquip);
                if (assignedGun != null)
                {
                    playerShooter.EquipGun(assignedGun);
                    newState.lastGunId = gunToEquip; // 초기화 시 기록

                    // 3. 잔탄 복원 (없으면 EquipGun에서 설정된 maxAmmo 유지)
                    int ammoGroup = assignedGun.gunGroup > 0 ? assignedGun.gunGroup : assignedGun.id;
                    if (newState.gunAmmo.TryGetValue(ammoGroup, out int savedAmmo))
                    {
                        playerShooter.CurrentAmmo = savedAmmo;
                    }
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

        var keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.digit1Key.wasPressedThisFrame) OnSkillPointerDown(0);
            if (keyboard.digit1Key.wasReleasedThisFrame) OnSkillPointerUp(0);
            
            if (keyboard.digit2Key.wasPressedThisFrame) OnSkillPointerDown(1);
            if (keyboard.digit2Key.wasReleasedThisFrame) OnSkillPointerUp(1);
            
            if (keyboard.digit3Key.wasPressedThisFrame) OnSkillPointerDown(2);
            if (keyboard.digit3Key.wasReleasedThisFrame) OnSkillPointerUp(2);
            
            if (keyboard.digit4Key.wasPressedThisFrame) OnSkillPointerDown(3);
            if (keyboard.digit4Key.wasReleasedThisFrame) OnSkillPointerUp(3);
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
                    skill.continuousTimer += Time.deltaTime;
                    if (skill.continuousTimer >= skill.Data.duration)
                    {
                        skill.continuousTimer = 0f;

                        if (CheckCanExecuteAllFuncs(skill.Data))
                        {
                            if (skill.Data.func1 != 0) ExecuteSkillFunc(skill.Data.func1, GetShooter());
                            if (skill.Data.func2 != 0) ExecuteSkillFunc(skill.Data.func2, GetShooter());

                            // 실행 직후 조건을 다시 확인하여 (예: 장전 완료) 더 이상 실행할 필요 없으면 즉시 종료
                            if (!CheckCanExecuteAllFuncs(skill.Data))
                            {
                                skill.isContinuousRunning = false;
                                skill.currentCooldown = skill.Data.cooltime;
                            }
                        }
                        else
                        {
                            skill.isContinuousRunning = false;
                            skill.currentCooldown = skill.Data.cooltime; // 종료 후 쿨타임 시작
                        }
                    }
                }
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
        
        SkillData data = skill.Data;
        if (data == null || data.activeType == SkillActiveType.Passive) return;

        if (skill.currentCooldown > 0f && data.activeType != SkillActiveType.Toggle) 
        {
            Debug.Log($"[PlayerSkillController] 쿨타임 중입니다: {skill.currentCooldown:F1}초 남음");
            return;
        }

        switch (data.activeType)
        {
            case SkillActiveType.Instant:
                ExecuteSkillFunc(data.func1, GetShooter());
                ExecuteSkillFunc(data.func2, GetShooter());
                skill.currentCooldown = data.cooltime;
                break;
            case SkillActiveType.Charge:
                skill.isCharging = true;
                ExecuteSkillFunc(data.func1, GetShooter());
                ExecuteSkillFunc(data.func2, GetShooter());
                break;
            case SkillActiveType.Toggle:
                skill.isToggledOn = !skill.isToggledOn;
                if (skill.isToggledOn)
                {
                    ExecuteSkillFunc(data.func1, GetShooter());
                    ExecuteSkillFunc(data.func2, GetShooter());
                }
                else
                {
                    ExecuteSkillFunc(data.offFunc1, GetShooter());
                    ExecuteSkillFunc(data.offFunc2, GetShooter());
                }
                skill.currentCooldown = data.cooltime; // 토글 시마다 쿨타임 시작
                break;
            case SkillActiveType.Continuous:
                if (!skill.isContinuousRunning)
                {
                    if (CheckCanExecuteAllFuncs(data))
                    {
                        skill.isContinuousRunning = true;
                        skill.continuousTimer = data.duration; // 즉시 1회 발동시키기 위함
                    }
                }
                break;
            default: // 하위 호환
                ExecuteSkillFunc(data.func1, GetShooter());
                ExecuteSkillFunc(data.func2, GetShooter());
                skill.currentCooldown = data.cooltime;
                break;
        }
    }

    public void OnSkillPointerUp(int skillSlotIndex)
    {
        if (currentCharData == null) return;
        CharacterState state = GetSkillState(currentCharData.id);
        if (state == null) return;
        if (skillSlotIndex < 0 || skillSlotIndex >= state.normalSkills.Length) return;

        SkillInstance skill = state.normalSkills[skillSlotIndex];
        if (skill == null || skill.skillId == 0) return;

        SkillData data = skill.Data;
        if (data != null && data.activeType == SkillActiveType.Charge)
        {
            if (skill.isCharging)
            {
                skill.isCharging = false;
                skill.currentCooldown = data.cooltime; // 손을 뗐을 때 쿨타임 시작
            }
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
            case 3: // 무기 3으로 전환
                Debug.LogWarning("[PlayerSkillController] 무기 3으로 전환 기능은 아직 데이터 구조가 지원하지 않습니다.");
                break;
            case 4: // 다음 무기로 전환 (1번 <-> 2번)
                ToggleWeapon(playerShooter);
                break;
            case 5: // 재장전 (전체)
                if (playerShooter != null) playerShooter.Reload();
                break;
            case 6: // 1발 재장전
                if (playerShooter != null) playerShooter.ReloadAmount(1);
                break;
        }
    }

    private bool CheckCanExecuteAllFuncs(SkillData data)
    {
        if (data.func1 == 0 && data.func2 == 0) return false;

        bool canExec1 = data.func1 != 0 ? CanExecuteSkillFunc(data.func1) : true;
        bool canExec2 = data.func2 != 0 ? CanExecuteSkillFunc(data.func2) : true;

        // 하나라도 실행 불가능한 조건이 되면 전체 스킬이 멈추도록(AND) 처리
        return canExec1 && canExec2;
    }

    private bool CanExecuteSkillFunc(int funcCode)
    {
        if (funcCode == 0) return false;
        
        Shooter playerShooter = GetShooter();
        if (playerShooter == null) return false;

        switch (funcCode)
        {
            case 5: // 재장전 (전체)
            case 6: // 재장전 (1발)
                return playerShooter.CurrentGunData != null && playerShooter.CurrentAmmo < playerShooter.CurrentGunData.maxAmmo;
            default:
                // 무기 교체 등은 항상 발동 가능
                return true;
        }
    }

    private void SwitchWeapon(Shooter playerShooter, int gunId)
    {
        if (gunId <= 0 || currentCharData == null) return;

        GunData newGun = DataManager.Instance.GetData<GunData>("Gun", gunId);
        if (newGun != null && (playerShooter.CurrentGunData == null || playerShooter.CurrentGunData.id != newGun.id))
        {
            CharacterState state = GetSkillState(currentCharData.id);

            // 현재 무기 잔탄 저장
            if (playerShooter.CurrentGunData != null && state != null)
            {
                int oldAmmoGroup = playerShooter.CurrentGunData.gunGroup > 0 ? playerShooter.CurrentGunData.gunGroup : playerShooter.CurrentGunData.id;
                state.gunAmmo[oldAmmoGroup] = playerShooter.CurrentAmmo;
            }

            if (playerShooter != null) playerShooter.EquipGun(newGun);
            
            // 새 무기 상태 업데이트 및 잔탄 복원
            if (state != null)
            {
                state.lastGunId = gunId;
                int newAmmoGroup = newGun.gunGroup > 0 ? newGun.gunGroup : newGun.id;
                if (state.gunAmmo.TryGetValue(newAmmoGroup, out int savedAmmo))
                {
                    if (playerShooter != null) playerShooter.CurrentAmmo = savedAmmo;
                }
            }
            
            Debug.Log($"[PlayerSkillController] 무기 전환: {newGun.gunName}");
        }
    }

    private void ToggleWeapon(Shooter playerShooter)
    {
        if (playerShooter == null || playerShooter.CurrentGunData == null) return;

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
