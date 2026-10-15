using UnityEngine;
using System.Collections.Generic;


public enum CharRole
{
    None = 0,
    Infiltrator = 1,  // 수색
    Striker = 2,      // 타격
    Anchor = 3,       // 방호
    Suppressor = 4    // 제압
}

[System.Serializable]
public class CharData : IData
{
    public int id;
    public string name;
    public string resourceSlot;
    public string resourceStand;
    public CharRole role;
    public int[] defaultSkills;
    public string gunName;
    public int shotType;
    public string resourceSilhouette;
    public int defaultAmmoId;

    [System.NonSerialized]
    public List<int> skill = new List<int>();

    [System.NonSerialized]
    public int ultimateSkill;

    public int GetId()
    {
        return id;
    }

    public int GetGunId(int index = 0)
    {
        return defaultAmmoId;
    }
}

[System.Serializable]
public class CharDataList : IDataList<CharData>
{
    public List<CharData> chars = new List<CharData>();

    public List<CharData> GetList()
    {
        return chars;
    }
}
