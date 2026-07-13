using System;
using UnityEngine;

[Serializable]
public abstract class BTSharedVariable
{
    public string key = "New Key";
    public abstract bool TryCopyFrom(BTSharedVariable other);
}

[Serializable]
public class BTSharedNumeric : BTSharedVariable
{
    public double value;
    public override bool TryCopyFrom(BTSharedVariable other)
    {
        if (other is BTSharedNumeric num) { value = num.value; return true; }
        return false;
    }
}

[Serializable]
public class BTSharedBoolean : BTSharedVariable
{
    public bool value;
    public override bool TryCopyFrom(BTSharedVariable other)
    {
        if (other is BTSharedBoolean bln) { value = bln.value; return true; }
        return false;
    }
}

[Serializable]
public class BTSharedGameObject : BTSharedVariable
{
    public GameObject value;
    public override bool TryCopyFrom(BTSharedVariable other)
    {
        if (other is BTSharedGameObject go) { value = go.value; return true; }
        return false;
    }
}

[Serializable]
public class BTSharedString : BTSharedVariable
{
    public string value = "";
    public override bool TryCopyFrom(BTSharedVariable other)
    {
        if (other is BTSharedString str) { value = str.value; return true; }
        return false;
    }
}

[Serializable]
public class BTSharedVector3 : BTSharedVariable
{
    public Vector3 value;
    public override bool TryCopyFrom(BTSharedVariable other)
    {
        if (other is BTSharedVector3 vec) { value = vec.value; return true; }
        return false;
    }
}
