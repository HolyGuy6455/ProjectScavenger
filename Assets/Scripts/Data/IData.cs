using System.Collections.Generic;

public interface IData
{
    string GetId();
}

public interface IDataList<T> where T : IData
{
    List<T> GetList();
}
