using System.Collections.Generic;

[System.Serializable]
public class UserCurrencyData : IData
{
    public int id;
    public string name;
    public string ui_resources;

    public int GetId()
    {
        return id;
    }
}

[System.Serializable]
public class UserCurrencyDataList : IDataList<UserCurrencyData>
{
    public List<UserCurrencyData> items = new List<UserCurrencyData>();

    public List<UserCurrencyData> GetList()
    {
        return items;
    }
}
