using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class UltimateSkillData : IData
{
    public int id;
    public string name;
    
    // 밸런스 데이터
    public float cooldown;
    public float duration;
    public string resourceIcon;
    public float thirst;
    public float remain;
    public int remainCostType;
    public float value_1;
    public float value_2;

    public int GetId()
    {
        return id;
    }
}

[System.Serializable]
public class UltimateSkillDataList : IDataList<UltimateSkillData>
{
    public List<UltimateSkillData> ultimateSkills = new List<UltimateSkillData>();

    public List<UltimateSkillData> GetList()
    {
        return ultimateSkills;
    }
}
