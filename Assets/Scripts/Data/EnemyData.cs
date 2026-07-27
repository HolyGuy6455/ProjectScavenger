using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class EnemyData : IData
{
    public int id;
    public int hp;
    public int lootTable;
    public int gun;
    public string prefabName;

    public int GetId()
    {
        return id;
    }
}

[System.Serializable]
public class EnemyDataList : IDataList<EnemyData>
{
    public List<EnemyData> enemies = new List<EnemyData>();

    public List<EnemyData> GetList()
    {
        return enemies;
    }
}
