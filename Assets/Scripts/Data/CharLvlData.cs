using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class CharLvlData : IData
{
    public int id;
    public int charId;
    public int level;
    public int atk;
    public int hp;

    public int GetId()
    {
        return id;
    }
}

[System.Serializable]
public class CharLvlDataList : IDataList<CharLvlData>
{
    public List<CharLvlData> charLvls = new List<CharLvlData>();

    public List<CharLvlData> GetList()
    {
        return charLvls;
    }
}
