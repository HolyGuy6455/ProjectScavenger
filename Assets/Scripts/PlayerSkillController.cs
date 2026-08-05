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

            bool hasReloadFunc = false;
            if (data.offFunction == 5 || data.offFunction == 6)
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
                int gunToEquip = newState.lastGunId > 0 ? newState.lastGunId : charData.GetGunId(0);
                
                GunData assignedGun = DataManager.Instance.GetData<GunData>("Gun", gunToEquip);
                if (assignedGun != null)
                {
                    // 3. 잔탄 복원 (없으면 EquipGun 내부에서 maxAmmo로 설정됨)
                    int ammoGroup = assignedGun.gunGroup > 0 ? assignedGun.gunGroup : assignedGun.id;
                    int savedAmmo = -1;
                    if (newState.gunAmmo.TryGetValue(ammoGroup, out int val))
                    {
                        savedAmmo = val;
                    }

                    playerShooter.EquipGun(assignedGun, savedAmmo);
                    newState.lastGunId = gunToEquip; // 초기화 시 기록
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
                    skill.UpdateContinuous(Time.deltaTime, GetShooter());
                }
                
                skill.UpdateInstant(GetShooter());
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

            int restoredAmmo = -1;
            if (state != null)
            {
                int newAmmoGroup = newGun.gunGroup > 0 ? newGun.gunGroup : newGun.id;
                if (state.gunAmmo.TryGetValue(newAmmoGroup, out int savedAmmo))
                {
                    restoredAmmo = savedAmmo;
                }
            }

            if (playerShooter != null) playerShooter.EquipGun(newGun, restoredAmmo);
            
            // 새 무기 상태 업데이트
            if (state != null)
            {
                state.lastGunId = gunId;
            }
            
            Debug.Log($"[PlayerSkillController] 무기 전환: {newGun.gunName}");
        }
    }

    private void ToggleWeapon(Shooter playerShooter)
    {
        if (playerShooter == null || playerShooter.CurrentGunData == null || currentCharData.gun == null || currentCharData.gun.Length == 0) return;

        int currentGunId = playerShooter.CurrentGunData.id;
        int nextGunId = 0;

        for (int i = 0; i < currentCharData.gun.Length; i++)
        {
            if (currentCharData.gun[i] == currentGunId)
            {
                // Find next non-zero gun
                for (int j = 1; j <= currentCharData.gun.Length; j++)
                {
                    int checkIndex = (i + j) % currentCharData.gun.Length;
                    if (currentCharData.gun[checkIndex] != 0)
                    {
                        nextGunId = currentCharData.gun[checkIndex];
                        break;
                    }
                }
                break;
            }
        }

        if (nextGunId == 0) // Fallback
        {
            foreach (int g in currentCharData.gun)
            {
                if (g != 0)
                {
                    nextGunId = g;
                    break;
                }
            }
        }

        if (nextGunId > 0 && nextGunId != currentGunId)
        {
            SwitchWeapon(playerShooter, nextGunId);
        }
        else
        {
            Debug.Log("[PlayerSkillController] 교체할 다음 무기가 없습니다.");
        }
    }
}
