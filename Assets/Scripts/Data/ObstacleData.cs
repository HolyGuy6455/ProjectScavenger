using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class ObstacleData : IData
{
    public int id;
    public int hp;
    public int lootTable;
    public string prefabName;

    public int GetId()
    {
        return id;
    }
}

[System.Serializable]
public class ObstacleDataList : IDataList<ObstacleData>
{
    public List<ObstacleData> obstacles = new List<ObstacleData>();

    public List<ObstacleData> GetList()
    {
        return obstacles;
    }
}
