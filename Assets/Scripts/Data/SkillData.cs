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

public enum RemainCostType
{
    None = 0,
    OneByOne = 1,
    Ammo = 2,
    Infinity = 3
}

[System.Serializable]
public class SkillData : IData
{
    public int id;
    public string name;
    public int charId;
    public int[] index;
    public int skillGroup;
    public int lvl;
    public string resourceIcon;
    public string resourceIcon2;
    public int func;
    public float[] funcValue;
    public float cooltime;
    public float duration;
    public SkillActiveType activeType;
    public float thirst;
    public int remain;
    public RemainCostType remainCostType;
#if UNITY_EDITOR
    public bool unlockFirst;
    public int unlockLevel;
    public int[] unlockItem;
    public int[] unlockItemAmount;
#endif

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
