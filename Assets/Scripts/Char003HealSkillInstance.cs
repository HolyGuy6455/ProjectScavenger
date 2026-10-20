using UnityEngine;

/// <summary>응급처치의 비용과 쿨타임은 공통 처리하고, 회복 효과는 전용 스킬에 위임합니다.</summary>
[System.Serializable]
public sealed class Char003HealSkillInstance : SkillInstance
{
    public Char003HealSkillInstance(int id) : base(id) { }

    public override bool ExecuteSkillBeginFunc(int funcCode, Shooter playerShooter, params float[] funcValues)
    {
        if (playerShooter == null) return false;
        var skill = playerShooter.GetComponent<Char003HealSkill>();
        if (skill != null) return skill.TryUse(skillId);
        Debug.LogError("[Char003HealSkillInstance] Player requires Char003HealSkill.", playerShooter);
        return false;
    }
}
