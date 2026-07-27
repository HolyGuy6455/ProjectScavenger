using System.Collections.Generic;

[System.Serializable]
public class LootData : IData
{
    public int id;
    public int[] item;
    public int[] chance;

    public int GetId()
    {
        return id;
    }
}

[System.Serializable]
public class LootDataList : IDataList<LootData>
{
    public List<LootData> items = new List<LootData>();

    public List<LootData> GetList()
    {
        return items;
    }
}
