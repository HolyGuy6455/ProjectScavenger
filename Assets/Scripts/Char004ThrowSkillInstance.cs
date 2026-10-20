using UnityEngine;

/// <summary>조준과 투척은 전용 컴포넌트가 처리하고, 비용은 공통 Aim 실행 시점에 적용합니다.</summary>
[System.Serializable]
public sealed class Char004ThrowSkillInstance : SkillInstance
{
    public Char004ThrowSkillInstance(int id) : base(id) { }

    public override float AimRange => Data?.funcValue != null && Data.funcValue.Length > 0 ? Data.funcValue[0] : 0f;
    public override float AimRadius => Data?.funcValue != null && Data.funcValue.Length > 1 ? Data.funcValue[1] : 0f;
    public override bool CanCancelAim => Data?.func != 1;
    public override bool DeferAimCost => true;

    public override bool TryBeginAim(Shooter shooter)
        => GetThrowSkill(shooter)?.TryPrepare(skillId) == true;

    public override bool TryExecuteAim(Shooter shooter, Vector3 target)
        => GetThrowSkill(shooter)?.TryThrow(skillId, target) == true;

    public override void OnAimCancelled(Shooter shooter) => GetThrowSkill(shooter)?.CancelAim();

    private static Char004ThrowSkill GetThrowSkill(Shooter shooter)
    {
        if (shooter == null) return null;
        var skill = shooter.GetComponent<Char004ThrowSkill>();
        if (skill == null) Debug.LogError("[Char004ThrowSkillInstance] Player requires Char004ThrowSkill.", shooter);
        return skill;
    }
}
