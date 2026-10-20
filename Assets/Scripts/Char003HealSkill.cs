using Scavenger.Manager;
using Unity.Netcode;
using UnityEngine;

/// <summary>char003의 응급처치. Player 루트에 두어 교대 후에도 공용 회복 버프를 유지합니다.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerController), typeof(PlayerSkillController), typeof(BuffController))]
public sealed class Char003HealSkill : NetworkBehaviour
{
    // 데이터 테이블의 양수 Buff ID와 구별하는 스킬 생성 효과의 ID입니다.
    // 응급처치 첫 레벨의 Skill ID를 사용하여 세 레벨 모두 같은 효과로 갱신합니다.
    private const int RecoveryBuffId = -37;

    private PlayerController player;
    private PlayerSkillController skills;
    private Health health;
    private BuffController buffs;

    private void Awake()
    {
        player = GetComponent<PlayerController>();
        skills = GetComponent<PlayerSkillController>();
        health = GetComponent<Health>();
        buffs = GetComponent<BuffController>();
    }

    private bool CanUse => isActiveAndEnabled && IsSpawned
        && player.GetLocalDeck().GetCharId(player.ActiveSlotIndex) == 3
        && !player.IsInputLocked && !player.IsRolling && !player.IsDeathPresented
        && health != null && !health.IsDead && health.CurrentHealth > 0f && !health.IsClearProtected
        && !skills.IsReloading()
        && (GameFlowManager.Instance == null || GameFlowManager.Instance.CurrentState == GameResultState.None);

    public bool TryUse(int skillId)
    {
        if (!IsOwner || !CanUse || GetSkillData(skillId) == null) return false;
        if (!PlayAnimation()) return false;
        ApplyRecoveryServerRpc(skillId);
        return true;
    }

    [ServerRpc]
    private void ApplyRecoveryServerRpc(int skillId)
    {
        if (!CanUse) return;
        var data = GetSkillData(skillId);
        if (data == null) return;

        buffs.AddSharedBuff(new BuffData
        {
            id = RecoveryBuffId,
            buffType = (int)BuffType.HealOverTime,
            duration = data.duration,
            value = data.funcValue[0],
            tickInterval = data.funcValue[1]
        });
        PlayRecoveryClientRpc();
    }

    [ClientRpc]
    private void PlayRecoveryClientRpc()
    {
        if (IsOwner || player.GetLocalDeck().GetCharId(player.ActiveSlotIndex) != 3) return;
        PlayAnimation();
    }

    private bool PlayAnimation()
    {
        var model = player.GetActiveCharacterModel();
        var presenter = model != null ? model.GetComponent<CharacterAnimationPresenter>() : null;
        if (presenter != null && presenter.PlayUpperBodySkill()) return true;
        Debug.LogError("[Char003HealSkill] Cannot play char003's upper-body Skill animation.", this);
        return false;
    }

    private SkillData GetSkillData(int skillId)
    {
        var data = DataManager.Instance != null ? DataManager.Instance.GetData<SkillData>("Skill", skillId) : null;
        if (data != null && data.charId == 3 && data.func == 4 && IsPositiveFinite(data.duration)
            && data.funcValue != null && data.funcValue.Length >= 2
            && IsPositiveFinite(data.funcValue[0]) && IsPositiveFinite(data.funcValue[1])) return data;
        Debug.LogError($"[Char003HealSkill] Invalid recovery SkillData {skillId}: duration, heal amount and interval must be positive.", this);
        return null;
    }

    private static bool IsPositiveFinite(float value)
        => value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);
}
