using System.Collections.Generic;
using UnityEngine;

public enum QuantityShowType
{
    None = 0,
    Integer = 1,
    Fraction = 2
}

[System.Serializable]
public class ItemData : IData
{
    public int id;
    public string name;
    public string uiResource;
    public int maxQuantity;
    public string func;
    public int quantityShowType;
    public QuantityShowType QuantityShowType => (QuantityShowType)quantityShowType;
    
    public List<ItemValueData> valueList = new List<ItemValueData>();

    public int GetId()
    {
        return id;
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
    public List<ItemData> items = new List<ItemData>();

    public List<ItemData> GetList()
    {
        return items;
    }
}
