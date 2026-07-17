using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class UltimateSkillData : IData
{
    public int id;
    public string name;

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
