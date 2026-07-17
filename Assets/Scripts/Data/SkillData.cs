using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class SkillData : IData
{
    public int id;
    public string name;
    public string resourceIcon;
    public int func1;
    public int func2;
    public float cooltime;

    public int GetId()
    {
        return id;
    }
}

[System.Serializable]
public class SkillDataList : IDataList<SkillData>
{
    public List<SkillData> skills = new List<SkillData>();

    public List<SkillData> GetList()
    {
        return skills;
    }
}
