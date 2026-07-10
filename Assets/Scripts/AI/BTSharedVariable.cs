using System;
using UnityEngine;

[Serializable]
public abstract class BTSharedVariable
{
    public string key = "New Key";
}

[Serializable]
public class BTSharedNumeric : BTSharedVariable
{
    public double value;
}

[Serializable]
public class BTSharedBoolean : BTSharedVariable
{
    public bool value;
}

[Serializable]
public class BTSharedGameObject : BTSharedVariable
{
    public GameObject value;
}
