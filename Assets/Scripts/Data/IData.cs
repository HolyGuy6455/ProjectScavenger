using System.Collections.Generic;

public interface IData
{
    int GetId();
}

public interface IDataList<T> where T : IData
{
    List<T> GetList();
}
