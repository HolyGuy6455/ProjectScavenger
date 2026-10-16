using UnityEngine;

/// <summary>공통 비용·쿨타임 처리를 사용하고, 실행은 char002의 지뢰 스킬에 위임합니다.</summary>
[System.Serializable]
public sealed class Char002ClaymoreSkillInstance : SkillInstance
{
    public Char002ClaymoreSkillInstance(int id) : base(id) { }

    public override bool ExecuteSkillBeginFunc(int funcCode, Shooter playerShooter, params float[] funcValues)
    {
        if (playerShooter == null) return false;
        var skill = playerShooter.GetComponent<Char002ClaymoreSkillNetwork>();
        if (skill != null) return skill.TryUse(skillId);
        Debug.LogError("[Char002ClaymoreSkillInstance] Player requires Char002ClaymoreSkillNetwork.", playerShooter);
        return false;
    }
}
