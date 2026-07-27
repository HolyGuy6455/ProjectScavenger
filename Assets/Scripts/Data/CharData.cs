using UnityEngine;
using System.Collections.Generic;



[System.Serializable]
public class CharData : IData
{
    public int id;
    public string name;
    public string resourceSlot;
    public string resourceStand;
    public int[] gun;
    public int[] item;
    public int[] itemAmount;
    public int[] skill;
    public int ultimateSkill;

    public int GetId()
    {
        return id;
    }

    public int GetGunId(int index = 0)
    {
        return gun != null && gun.Length > index ? gun[index] : 0;
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
