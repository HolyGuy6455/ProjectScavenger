using UnityEngine;



[System.Serializable]
public class CharacterState
{
    public int charId;
    public SkillInstance[] normalSkills;
    
    public float currentHealth;
    public float maxHealth;
    
    public float currentHunger;
    public float maxHunger;

    public float currentThirst;
    public float maxThirst;
    
    public System.Collections.Generic.List<AmmoGroup> savedChamber = new System.Collections.Generic.List<AmmoGroup>();

    public CharacterState(CharData charData)
    {
        charId = charData.id;
        
        currentHealth = 100f;
        maxHealth = 100f;
        
        currentHunger = 100f;
        maxHunger = 100f;

        currentThirst = 100f;
        maxThirst = 100f;
        
        // Initialize empty array; will be populated by SetEquippedSkills
        normalSkills = new SkillInstance[4];
    }

    public bool UpdateSurvivalStats(float deltaTime)
    {
        if (currentHealth <= 0f) return false;
        bool changed = false;
        
        float oldHealth = currentHealth;
        float oldHunger = currentHunger;
        float oldThirst = currentThirst;

        float hungerPercentage = maxHunger > 0 ? (currentHunger / maxHunger) * 100f : 0f;
        float thirstPercentage = maxThirst > 0 ? (currentThirst / maxThirst) * 100f : 0f;

        // 목마름이 90이상이면 1초에 목마름을 0.1씩 깎는다.
        if (thirstPercentage >= 90f)
        {
            currentThirst -= 0.1f * deltaTime;
        }

        // 배고픔이 90이상이면 1초에 배고픔을 0.1씩 깎는다.
        if (hungerPercentage >= 90f)
        {
            currentHunger -= 0.1f * deltaTime;
        }

        // 배고픔이 0%라면 1초에 체력을 1씩 깎는다.
        if (currentHunger <= 0f)
        {
            currentHunger = 0f;
            currentHealth -= 1f * deltaTime;
        }
        else if (currentHealth < maxHealth) // 체력이 가득 차있지 않으면 배고픔을 소비해 체력을 회복한다.
        {
            if (hungerPercentage >= 30f)
            {
                // 배고픔이 30% 이상이라면 1초에 배고픔 1씩 소비해 체력을 1씩 채워준다.
                currentHunger -= 1f * deltaTime;
                currentHealth += 1f * deltaTime;
            }
            else
            {
                // 배고픔이 30% 이하라면 1초에 배고픔 1씩 소비해 체력을 0.5씩 채워준다.
                currentHunger -= 1f * deltaTime;
                currentHealth += 0.5f * deltaTime;
            }
        }

        currentThirst = Mathf.Clamp(currentThirst, 0f, maxThirst);
        currentHunger = Mathf.Clamp(currentHunger, 0f, maxHunger);
        currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);

        // 변경 사항이 있는지 확인
        if (Mathf.Abs(oldHealth - currentHealth) > 0.001f || 
            Mathf.Abs(oldHunger - currentHunger) > 0.001f ||
            Mathf.Abs(oldThirst - currentThirst) > 0.001f)
        {
            changed = true;
        }

        return changed;
    }

    public void UpdateAllCooldowns(float deltaTime)
    {
        float cooldownValue = deltaTime;
        float thirstPercentage = maxThirst > 0 ? (currentThirst / maxThirst) * 100f : 0f;

        if (thirstPercentage <= 0f)
        {
            cooldownValue *= 0f;
        }
        else if (thirstPercentage <= 30f)
        {
            cooldownValue *= 0.5f;
        }

        foreach (var skill in normalSkills)
        {
            if (skill != null) skill.UpdateCooldown(cooldownValue);
        }
    }

    public void SetEquippedSkills(int[] equippedSkills)
    {
        if (equippedSkills == null) return;
        
        normalSkills = new SkillInstance[4];
        for (int i = 0; i < 4; i++)
        {
            int skillId = i < equippedSkills.Length ? equippedSkills[i] : 0;
            if (skillId > 0)
            {
                var skillData = DataManager.Instance.GetData<SkillData>("Skill", skillId);
                if (skillData != null)
                {
                    normalSkills[i] = new SkillInstance(skillId);
                }
            }
        }
    }
}
