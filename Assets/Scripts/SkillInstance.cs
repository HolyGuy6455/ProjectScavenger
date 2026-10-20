using UnityEngine;

[System.Serializable]
public class SkillInstance
{
    public int skillId;
    public float currentCooldown;
    public bool isToggledOn;
    public bool isCharging;
    public bool isContinuousRunning;
    public float continuousTimer;
    public int remainAmount;
    public bool isInstantOffExecuted;

    public SkillInstance(int id)
    {
        skillId = id;
        currentCooldown = 0f;
        isToggledOn = false;
        isCharging = false;
        isContinuousRunning = false;
        continuousTimer = 0f;
        isInstantOffExecuted = false;
        
        var data = Data;
        remainAmount = data != null ? data.remain : 0;
    }

    public SkillData Data => skillId > 0 && DataManager.Instance != null ? DataManager.Instance.GetData<SkillData>("Skill", skillId) : null;

    public bool IsReady => currentCooldown <= 0f;

    public virtual bool CanCancelAim => true;
    public virtual bool DeferAimCost => false;
    public virtual float AimRange => 0f;
    public virtual float AimRadius => 0f;
    public virtual bool TryBeginAim(Shooter shooter) => true;
    public virtual bool TryExecuteAim(Shooter shooter, Vector3 target) => false;
    public virtual void OnAimCancelled(Shooter shooter) { }

    public void Use(CharacterState state, Shooter shooter)
    {
        var data = Data;
        if (data == null || data.activeType == SkillActiveType.Passive) return;

        // 준비 버튼의 재입력은 비용이나 실행 함수 없이 취소만 처리합니다.
        var aim = shooter != null ? shooter.GetComponent<SkillAimController>() : null;
        if (data.activeType == SkillActiveType.Aim && aim != null && aim.IsPreparedSkill(this))
        {
            aim.CancelPreparation();
            return;
        }

        if (currentCooldown > 0f && data.activeType != SkillActiveType.Toggle) 
        {
            Debug.Log($"[SkillInstance] 쿨타임 중입니다: {currentCooldown:F1}초 남음");
            return;
        }

        bool isTurningOffToggle = (data.activeType == SkillActiveType.Toggle && isToggledOn);
        if (!isTurningOffToggle)
        {
            if (remainAmount <= 0)
            {
                Debug.Log($"[SkillInstance] 스킬 사용 횟수/탄창이 부족합니다.");
                return;
            }

            if (data.func != 0 && !CanExecuteSkillFunc(data.func, shooter))
            {
                Debug.Log($"[SkillInstance] 스킬 실행 조건을 만족하지 않습니다.");
                return;
            }
        }

        switch (data.activeType)
        {
            case SkillActiveType.Aim:
                if (aim != null) aim.Prepare(this, state);
                return; // 비용과 쿨타임은 조준을 확정했을 때만 적용합니다.
            case SkillActiveType.Instant:
                if (!ExecuteSkillBeginFunc(data.func, shooter, data.funcValue)) return;
                ReduceRemain(shooter);
                isInstantOffExecuted = false;
                currentCooldown = data.cooltime;
                break;
            case SkillActiveType.Charge:
                isCharging = true;
                ExecuteSkillBeginFunc(data.func, shooter, data.funcValue);
                ReduceRemain(shooter);
                break;
            case SkillActiveType.Toggle:
                isToggledOn = !isToggledOn;
                if (isToggledOn)
                {
                    ExecuteSkillBeginFunc(data.func, shooter, data.funcValue);
                    ReduceRemain(shooter);
                }
                else
                {
                    ExecuteSkillFinishFunc(data.func, shooter, data.funcValue);
                }
                currentCooldown = data.cooltime;
                break;
            case SkillActiveType.Continuous:
                if (!isContinuousRunning)
                {
                    if (CanExecuteSkillFunc(data.func, shooter))
                    {
                        // The first shell includes the opening pose; ammunition is committed at Finish.
                        if (data.func == 6 && !ExecuteSkillBeginFunc(data.func, shooter, data.funcValue)) return;
                        isContinuousRunning = true;
                        continuousTimer = data.func == 6 ? 0f : data.duration;
                    }
                }
                break;
            default:
                ExecuteSkillBeginFunc(data.func, shooter, data.funcValue);
                ReduceRemain(shooter);
                currentCooldown = data.cooltime;
                break;
        }

        if (state != null)
        {
            var player = shooter != null ? shooter.GetComponent<PlayerController>() : null;
            if (player != null) player.ModifyCharacterSurvivalStats(state.charId, 0f, -data.thirst);
            else state.currentThirst -= data.thirst;
        }
    }

    public void ReleaseUse(Shooter shooter)
    {
        var data = Data;
        if (data != null && data.activeType == SkillActiveType.Charge)
        {
            if (isCharging)
            {
                isCharging = false;
                currentCooldown = data.cooltime; // 손을 뗐을 때 쿨타임 시작
                ExecuteSkillFinishFunc(data.func, shooter, data.funcValue);
            }
        }
    }

    public bool CommitAim(CharacterState state, Shooter shooter, Vector3 target)
    {
        var data = Data;
        if (data == null || data.activeType != SkillActiveType.Aim || !IsReady
            || !CanExecuteSkillFunc(data.func, shooter) || !TryExecuteAim(shooter, target)) return false;
        if (!DeferAimCost) ConsumeAimCost(state, shooter);
        return true;
    }

    public void ConsumeAimCost(CharacterState state, Shooter shooter)
    {
        var data = Data;
        if (data == null) return;
        ReduceRemain(shooter);
        currentCooldown = data.cooltime;
        if (state != null)
            shooter.GetComponent<PlayerController>()?.ModifyCharacterSurvivalStats(state.charId, 0f, -data.thirst);
    }

    /// <summary>Abandons pending effects without firing release/completion logic or refunding costs.</summary>
    public void CancelActiveAction(Shooter shooter = null)
    {
        if (isContinuousRunning && Data?.func == 6) shooter?.CancelReloadAnimation();
        isCharging = false;
        isContinuousRunning = false;
        isToggledOn = false;
        continuousTimer = 0f;
        isInstantOffExecuted = true;
    }

    public virtual bool ExecuteSkillBeginFunc(int funcCode, Shooter playerShooter, params float[] funcValues)
    {
        if (funcCode == 0) return true;

        switch (funcCode)
        {
            case 1: // 궁극기
                if (PlayerSkillController.Instance != null)
                {
                    PlayerSkillController.Instance.CmdUseUltimateServerRpc(skillId);
                }
                break;
            case 3: // 구르기
                if (PlayerController.LocalInstance != null)
                {
                    // Data.duration: 구르기 시간, funcValues[0]: 이동 거리, funcValues[1]: 무적 시간
                    PlayerController.LocalInstance.StartRoll(Data.duration, funcValues[0], funcValues[1]);
                }
                break;
            case 5: // 재장전 (전체)
                if (playerShooter != null) playerShooter.TriggerReloadAnimation();
                break;
            case 6: // 재장전 (1발)
                return playerShooter != null && Data != null
                    && playerShooter.TriggerReloadAnimation(Data.duration, !isContinuousRunning);
        }
        return true;
    }

    private void RefundChamberAmmo(Shooter playerShooter)
    {
        if (playerShooter == null || playerShooter.chamberAmmoQueue == null) return;
        if (PlayerSkillController.Instance == null || CharacterStateManager.Instance == null || CharacterStateManager.Instance.CurrentCharData == null) return;

        CharacterState state = PlayerSkillController.Instance.GetSkillState(CharacterStateManager.Instance.CurrentCharData.id);
        if (state == null || state.normalSkills == null) return;

        foreach (var group in playerShooter.chamberAmmoQueue)
        {
            if (group.ammoId <= 0) continue;

            // 이 ammoId를 장전하는 스킬을 찾아서 remainAmount 환불
            foreach (var skill in state.normalSkills)
            {
                if (skill == null) continue;
                var data = skill.Data;
                
                // 장전 스킬(func 5 또는 6)이면서 해당 ammoId를 장전하는 스킬인지 확인
                if (data != null && (data.func == 5 || data.func == 6) && data.funcValue != null && data.funcValue.Length > 1)
                {
                    if ((int)data.funcValue[1] == group.ammoId)
                    {
                        skill.remainAmount += group.count;
                        break; // 해당 묶음 환불 완료, 다음 묶음으로
                    }
                }
            }
        }
    }

    public void ExecuteSkillFinishFunc(int funcCode, Shooter playerShooter, params float[] funcValues)
    {
        if (funcCode == 0) return;

        switch (funcCode)
        {
            case 5: // 재장전 (전체)
                if (playerShooter != null)
                {
                    int ammoId = (funcValues != null && funcValues.Length > 1) ? (int)funcValues[1] : -1;
                    int amount = (funcValues != null && funcValues.Length > 0 && funcValues[0] > 0) ? (int)funcValues[0] : -1;
                    var data = Data;
                    
                    // 챔버를 비우기 전에 남은 탄환들을 각각의 스킬 인벤토리로 환불
                    RefundChamberAmmo(playerShooter);
                    
                    if (data != null && data.remainCostType == RemainCostType.Ammo && playerShooter.CurrentAmmoData != null && remainAmount < playerShooter.CurrentAmmoData.maxAmmo)
                    {
                        playerShooter.Reload(remainAmount, ammoId);
                    }
                    else
                    {
                        playerShooter.Reload(amount, ammoId);
                    }
                }
                break;
            case 6: // 재장전 (1발): 삽입 동작이 끝난 시점에만 탄약 추가
                if (playerShooter != null)
                {
                    int amount = GetShellLoadAmount(funcValues);
                    if (Data?.remainCostType == RemainCostType.Ammo) amount = Mathf.Min(amount, remainAmount);
                    int ammoId = (funcValues != null && funcValues.Length > 1) ? (int)funcValues[1] : -1;
                    playerShooter.LoadAmmo(amount, ammoId);
                }
                break;
        }
    }

    public bool CanExecuteSkillFunc(int funcCode, Shooter playerShooter)
    {
        if (remainAmount <= 0) return false;
        if (funcCode == 0) return false;
        if (playerShooter == null) return false;

        switch (funcCode)
        {
            case 5: // 재장전 (전체)
            case 6: // 재장전 (1발)
                return playerShooter.CurrentAmmoData != null && playerShooter.CurrentAmmo < playerShooter.CurrentAmmoData.maxAmmo;
            default:
                return true;
        }
    }

    public void UpdateInstant(Shooter shooter)
    {
        var data = Data;
        if (data == null || data.activeType != SkillActiveType.Instant) return;

        if (!isInstantOffExecuted && currentCooldown > 0f)
        {
            float elapsed = data.cooltime - currentCooldown;
            if (elapsed >= data.duration)
            {
                ExecuteSkillFinishFunc(data.func, shooter, data.funcValue);
                isInstantOffExecuted = true;
            }
        }
    }

    public void UpdateContinuous(float deltaTime, Shooter shooter)
    {
        if (!isContinuousRunning) return;
        
        var data = Data;
        if (data == null) return;

        continuousTimer += deltaTime;
        if (continuousTimer >= data.duration)
        {
            continuousTimer = 0f;

            if (data.func == 6)
            {
                if (!CanExecuteSkillFunc(data.func, shooter))
                {
                    FinishContinuousReload(shooter, data);
                    return;
                }

                int space = shooter.CurrentAmmoData.maxAmmo - shooter.CurrentAmmo;
                int loaded = Mathf.Min(GetShellLoadAmount(data.funcValue), space);
                if (data.remainCostType == RemainCostType.Ammo) loaded = Mathf.Min(loaded, remainAmount);
                // A client receives the updated ammo queue later, so decide fullness before the RPC.
                bool fillsMagazine = loaded >= space;
                ExecuteSkillFinishFunc(data.func, shooter, data.funcValue);
                if (data.remainCostType == RemainCostType.Ammo) remainAmount -= loaded;
                else ReduceRemain(shooter);

                if (fillsMagazine || !CanExecuteSkillFunc(data.func, shooter)
                    || !ExecuteSkillBeginFunc(data.func, shooter, data.funcValue))
                    FinishContinuousReload(shooter, data);
                return;
            }

            if (CanExecuteSkillFunc(data.func, shooter))
            {
                ExecuteSkillBeginFunc(data.func, shooter, data.funcValue);
                ReduceRemain(shooter);

                // 실행 직후 조건을 다시 확인하여 (예: 장전 완료) 더 이상 실행할 필요 없으면 즉시 종료
                if (!CanExecuteSkillFunc(data.func, shooter))
                {
                    isContinuousRunning = false;
                    currentCooldown = data.cooltime;
                }
            }
            else
            {
                isContinuousRunning = false;
                currentCooldown = data.cooltime; // 종료 후 쿨타임 시작
            }
        }
    }

    private static int GetShellLoadAmount(float[] values) =>
        values != null && values.Length > 0 && values[0] >= 1f ? (int)values[0] : 1;

    private void FinishContinuousReload(Shooter shooter, SkillData data)
    {
        isContinuousRunning = false;
        currentCooldown = data.cooltime;
        shooter?.FinishReloadAnimation();
    }

    public void UpdateCooldown(float deltaTime)
    {
        if (currentCooldown > 0f)
        {
            currentCooldown -= deltaTime;
            if (currentCooldown < 0f) currentCooldown = 0f;
        }
    }

    private void ReduceRemain(Shooter shooter)
    {
        var data = Data;
        if (data == null) return;

        switch (data.remainCostType)
        {
            case RemainCostType.OneByOne:
                remainAmount -= 1;
                break;
            case RemainCostType.Ammo:
                if (shooter != null && shooter.CurrentAmmoData != null)
                {
                    int ammoCost = shooter.CurrentAmmoData.maxAmmo - shooter.CurrentAmmo;
                    if (ammoCost > 0)
                    {
                        remainAmount -= ammoCost;
                    }
                }
                break;
            case RemainCostType.Infinity:
            case RemainCostType.None:
                break;
        }

        if (remainAmount < 0) remainAmount = 0;
    }
}
