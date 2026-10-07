using System.Collections.Generic;
using UnityEngine;

public enum QuantityShowType
{
    None = 0,
    Integer = 1,
    Fraction = 2
}

public enum ItemCategory
{
    All = 0,
    Important = 1, // 중요
    Food = 2,      // 음식
    Tool = 3,      // 도구
    Consumable = 4 // 소모품
}

[System.Serializable]
public class ItemData : IData
{
    public int id;
    public string name;
    public string Desc;
    public int category;
    public string uiResource;
    public string[] tags;
    public int maxQuantity;
    public int[] func;
    public int[] value;
    public int quantityShowType;
    public int serverFunc;
    public int serverValue;
    public QuantityShowType QuantityShowType => (QuantityShowType)quantityShowType;
    public ItemCategory Category => (ItemCategory)category;

    public int GetId()
    {
        return id;
    }
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
