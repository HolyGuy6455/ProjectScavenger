using System.Collections.Generic;
using Scavenger.Network.DTOs;

[System.Serializable]
public class QuestReward
{
    public string Type;
    public int Id;
    public int Count;
}

[System.Serializable]
public class QuestData : IData
{
    public int id;
    public string isComplete;
    public string Name;
    public string Desc;
    public QuestCycleType CycleType;
    public int PreQuestId;
    public int TargetType;
    public int TargetId;
    public int TargetCount;
    public QuestReward[] Rewards;
    public string Shortcut;
    public string ShortcutValue;
    public string StartDate;
    public string EndDate;

    public int GetId()
    {
        return id;
    }
}

[System.Serializable]
public class QuestDataList : IDataList<QuestData>
{
    public List<QuestData> quests = new List<QuestData>();

    public List<QuestData> GetList()
    {
        return quests;
    }
}
