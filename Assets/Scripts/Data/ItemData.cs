using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class ItemData : IData
{
    public string itemId;
    public string itemName;
    public string uiResource;
    public string model;
    public int maxQuantity;
    public string func;
    public List<ItemValueData> valueList;

    public string GetId()
    {
        return itemId;
    }
}

[System.Serializable]
public class ItemValueData
{
    public int key;
    public int value;
    public int subValue;
}

[System.Serializable]
public class ItemDataList : IDataList<ItemData>
{
    public List<ItemData> items;

    public List<ItemData> GetList()
    {
        return items;
    }
}
