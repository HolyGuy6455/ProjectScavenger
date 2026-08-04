using UnityEngine;

[System.Serializable]
public class UltimateSkillInstance
{
    public int ultimateSkillId;

    public UltimateSkillInstance(int id)
    {
        ultimateSkillId = id;
    }

    public UltimateSkillData Data => ultimateSkillId > 0 && DataManager.Instance != null ? DataManager.Instance.GetData<UltimateSkillData>("UltimateSkill", ultimateSkillId) : null;

    // TODO: 궁극기 쿨타임/게이지 로직은 나중에 추가
    public bool IsReady => true;

    public void Use()
    {
        // TODO: 궁극기 사용 로직
    }

    public void UpdateCooldown(float deltaTime)
    {
        // TODO: 궁극기 쿨타임 업데이트
    }
}
