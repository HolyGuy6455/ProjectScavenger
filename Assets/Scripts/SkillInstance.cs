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

    public void Use(CharacterState state, Shooter shooter)
    {
        var data = Data;
        if (data == null || data.activeType == SkillActiveType.Passive) return;

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
            case SkillActiveType.Instant:
                ExecuteSkillBeginFunc(data.func, shooter, data.funcValue);
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
                        isContinuousRunning = true;
                        continuousTimer = data.duration;
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
            state.currentThirst -= data.thirst;
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

    public void ExecuteSkillBeginFunc(int funcCode, Shooter playerShooter, params float[] funcValues)
    {
        if (funcCode == 0) return;

        switch (funcCode)
        {
            case 1: // 궁극기
                if (PlayerSkillController.Instance != null)
                {
                    PlayerSkillController.Instance.CmdUseUltimateServerRpc();
                }
                break;
            case 5: // 재장전 (전체)
                if (playerShooter != null) playerShooter.TriggerReloadAnimation();
                break;
            case 6: // 1발 재장전
                if (playerShooter != null) playerShooter.ReloadAmount(1);
                break;
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
                    var data = Data;
                    if (data != null && data.remainCostType == RemainCostType.Ammo && playerShooter.CurrentGunData != null && remainAmount < playerShooter.CurrentGunData.maxAmmo)
                    {
                        playerShooter.ReloadAmount(remainAmount);
                    }
                    else
                    {
                        playerShooter.Reload();
                    }
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
                return playerShooter.CurrentGunData != null && playerShooter.CurrentAmmo < playerShooter.CurrentGunData.maxAmmo;
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
                if (shooter != null && shooter.CurrentGunData != null)
                {
                    int ammoCost = shooter.CurrentGunData.maxAmmo - shooter.CurrentAmmo;
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
