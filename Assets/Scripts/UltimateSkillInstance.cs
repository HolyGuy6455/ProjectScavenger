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

    public UltimateSkillData Data => ultimateSkillId > 0 && DataManager.Instance != null ? DataManager.Instance.GetData<UltimateSkillData>("UltimateSkill", ultimateSkillId) : null;

    public bool IsReady => currentCooldown <= 0f;

    public void Use()
    {
        var data = Data;
        if (data != null)
        {
            currentCooldown = data.cooldown;
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
