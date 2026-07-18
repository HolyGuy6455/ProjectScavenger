using UnityEngine;
using System.Collections.Generic;

public enum SkillActiveType
{
    None = 0,
    Instant = 1,
    Charge = 2,
    Toggle = 3,
    Continuous = 4,
    Passive = 5
}

[System.Serializable]
public class SkillData : IData
{
    public int id;
    public string name;
    public string resourceIcon;
    public string resourceIcon2;
    public int func1;
    public int func2;
    public int offFunc1;
    public int offFunc2;
    public float cooltime;
    public float duration;
    public SkillActiveType activeType;

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
