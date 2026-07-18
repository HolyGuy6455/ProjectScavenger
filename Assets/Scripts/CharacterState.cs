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

    public SkillInstance(int id)
    {
        skillId = id;
        currentCooldown = 0f;
        isToggledOn = false;
        isCharging = false;
        isContinuousRunning = false;
        continuousTimer = 0f;
    }

    public SkillData Data => skillId > 0 && DataManager.Instance != null ? DataManager.Instance.GetData<SkillData>("Skill", skillId) : null;

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

[System.Serializable]
public class CharacterState
{
    public int charId;
    public SkillInstance[] normalSkills;
    public UltimateSkillInstance ultimateSkill;
    
    public float currentHealth;
    public float maxHealth;
    
    public int lastGunId;
    public System.Collections.Generic.Dictionary<int, int> gunAmmo = new System.Collections.Generic.Dictionary<int, int>();

    public CharacterState(CharData charData)
    {
        charId = charData.id;
        
        currentHealth = 100f;
        maxHealth = 100f;
        
        normalSkills = new SkillInstance[charData.skill != null ? charData.skill.Length : 0];
        for (int i = 0; i < normalSkills.Length; i++)
        {
            normalSkills[i] = new SkillInstance(charData.skill[i]);
        }

        ultimateSkill = new UltimateSkillInstance(charData.ultimateSkill);
    }

    public void UpdateAllCooldowns(float deltaTime)
    {
        foreach (var skill in normalSkills)
        {
            if (skill != null) skill.UpdateCooldown(deltaTime);
        }

        if (ultimateSkill != null)
        {
            ultimateSkill.UpdateCooldown(deltaTime);
        }
    }
}
