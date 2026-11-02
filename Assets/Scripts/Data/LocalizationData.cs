using System;
using System.Collections.Generic;

public enum LanguageCode
{
    KO = 0,
    EN = 1,
    JA = 2
}

[Serializable]
public class LocalizationEntry
{
    public string key;
    public string value;
}

[Serializable]
public class LocalizationDataList
{
    public List<LocalizationEntry> entries = new List<LocalizationEntry>();
}

