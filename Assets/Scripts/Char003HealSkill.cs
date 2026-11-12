using UnityEngine;

/// <summary>char003의 응급처치. 캐릭터 프리팹에 위치하며 Player 루트의 공용 BuffController에 지속 회복을 부여합니다.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterAnimationPresenter))]
public sealed class Char003HealSkill : BaseCharacterSkill
{
    // 데이터 테이블의 양수 Buff ID와 구별하는 스킬 생성 효과의 ID입니다.
    // 응급처치 첫 레벨의 Skill ID를 사용하여 세 레벨 모두 같은 효과로 갱신합니다.
    private const int RecoveryBuffId = -37;

    private bool CanUse => isActiveAndEnabled && IsSpawned && !IsPlayerUnavailable
        && Player.GetLocalDeck().GetCharId(Player.ActiveSlotIndex) == 3
        && Player.GetActiveCharacterModel() == gameObject
        && !Player.IsInputLocked && !Player.IsRolling
        && !SkillRouter.IsReloading();

    public override bool HandlesSkill(SkillData data)
        => data != null && data.charId == 3 && data.func == 4;

    public bool TryUse(int skillId)
    {
        var data = DataManager.Instance != null ? DataManager.Instance.GetData<SkillData>("Skill", skillId) : null;
        return TryUseSkill(data);
    }

    public override bool TryUseSkill(SkillData data)
    {
        if (!IsOwner || !CanUse || !ValidateSkillData(data)) return false;
        if (!PlayAnimation()) return false;
        SkillRouter.RequestCharacterSkill(data.id);
        return true;
    }

    public override bool TryAcceptOnServer(SkillData data, Vector3 target, Vector3 origin)
    {
        if (!CanUse || !ValidateSkillData(data) || Buffs == null) return false;

        Buffs.AddSharedBuff(new BuffData
        {
            id = RecoveryBuffId,
            name = data.name,
            resourceIcon = data.resourceIcon,
            buffType = (int)BuffType.HealOverTime,
            duration = data.duration,
            value = data.funcValue[0],
            tickInterval = data.funcValue[1]
        });
        return true;
    }

    public override void OnClientAccepted(SkillData data, Vector3 target, bool isOwner)
    {
        if (isOwner || IsPlayerUnavailable) return;
        PlayAnimation();
    }

    private bool PlayAnimation()
    {
        var presenter = GetComponent<CharacterAnimationPresenter>();
        if (presenter != null && presenter.PlayUpperBodySkill()) return true;
        Debug.LogError("[Char003HealSkill] Cannot play char003's upper-body Skill animation.", this);
        return false;
    }

    private bool ValidateSkillData(SkillData data)
    {
        if (HandlesSkill(data) && IsPositiveFinite(data.duration)
            && data.funcValue != null && data.funcValue.Length >= 2
            && IsPositiveFinite(data.funcValue[0]) && IsPositiveFinite(data.funcValue[1]))
            return true;
        Debug.LogError($"[Char003HealSkill] Invalid recovery SkillData {data?.id}: duration, heal amount and interval must be positive.", this);
        return false;
    }

    private static bool IsPositiveFinite(float value)
        => value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);
}
