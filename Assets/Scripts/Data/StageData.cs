using System.Collections.Generic;
using UnityEngine;

public enum StageType
{
    None = 0,
    Normal = 1,   // 일반 (자유 전투)
    Currency = 2, // 재화전용 (육성재화던전)
    Event = 3     // 이벤트
}

[System.Serializable]
public class StageData : IData
{
    public int id;
    public StageType stageType;
    public string name;
    public string desc;
    public string previewImage;
    public string bgIllustration;
    public int[] mainRewards;
    public int[] mainEnemies;
    public string sceneName;

    public int GetId()
    {
        return id;
    }

    public string GetLocalizedName()
    {
        return LocalizationManager.Get(name);
    }

    public string GetLocalizedDesc()
    {
        return LocalizationManager.Get(desc);
    }
}

[System.Serializable]
public class StageDataList : IDataList<StageData>
{
    public List<StageData> stages = new List<StageData>();

    public List<StageData> GetList()
    {
        return stages;
    }
}
