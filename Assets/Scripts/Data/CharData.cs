using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class CharData : IData
{
    public string id;
    public string name;
    public string resourceSlot;
    public string resourceStand;
    public string gunData;
    public int[] givenItem;

    public string GetId()
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
