using UnityEngine;

[System.Serializable]
public class UltimateSkillInstance
{
    public int ultimateSkillId;
    public float currentCooldown;

    public UltimateSkillInstance(int id)
    {
        ultimateSkillId = id;
        currentCooldown = 0f;
    }

    public SkillData Data => ultimateSkillId > 0 && DataManager.Instance != null ? DataManager.Instance.GetData<SkillData>("Skill", ultimateSkillId) : null;

    public bool IsReady => currentCooldown <= 0f;

    public void Use()
    {
        var data = Data;
        if (data != null)
        {
            currentCooldown = data.cooltime;
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
}
