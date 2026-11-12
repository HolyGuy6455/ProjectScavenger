using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class EnemyData : IData
{
    public int id;
    public string name;
    public string icon;
    public int hp;
    public int lootTable;
    public string prefabName;
    public int ammoId; // 0이면 사격 무기 없음
    public string[] tags;

    public bool HasTag(string tag)
    {
        if (tags == null || string.IsNullOrWhiteSpace(tag)) return false;
        string target = tag.Trim();
        for (int i = 0; i < tags.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(tags[i])
                && string.Equals(tags[i].Trim(), target, System.StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    public int GetId()
    {
        return id;
    }

    public string GetLocalizedName()
    {
        return LocalizationManager.Get(name);
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
