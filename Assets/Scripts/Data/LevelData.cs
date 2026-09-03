using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class LevelData : IData
{
    public int level;
    public int requiredItemId;
    public int requiredItemAmount;

    public int GetId()
    {
        return level;
    }
}

[System.Serializable]
public class LevelDataList : IDataList<LevelData>
{
    public List<LevelData> levels = new List<LevelData>();

    public List<LevelData> GetList()
    {
        return levels;
    }
}
