using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public struct ItemAmount
{
    public int itemId;
    public int amount;
}

[System.Serializable]
public class CharData : IData
{
    public int id;
    public string name;
    public string resourceSlot;
    public string resourceStand;
    public int gunData;
    public ItemAmount[] givenItem;

    public int GetId()
    {
        return id;
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
