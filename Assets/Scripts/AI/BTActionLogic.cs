using System;

[Serializable]
public abstract class BTActionLogic
{
    public virtual string GetDescription(AIBrain brain) { return "No description available."; }

    protected string GetKeyValueString<T>(AIBrain brain, string key) where T : BTSharedVariable
    {
        if (string.IsNullOrEmpty(key)) return "[비어있음]";
        if (brain == null) return key;
        var prop = brain.GetVariable<T>(key);
        if (prop == null) return key;

        if (prop is BTSharedNumeric num) return $"{key}({num.value})";
        if (prop is BTSharedBoolean bln) return $"{key}({bln.value})";
        if (prop is BTSharedGameObject go) return $"{key}({(go.value != null ? go.value.name : "null")})";
        if (prop is BTSharedString str) return $"{key}({str.value})";
        if (prop is BTSharedVector3 vec) return $"{key}({vec.value})";
        return key;
    }

    public virtual void ResetState() { }

    public abstract NodeState Evaluate(AIBrain brain);
}
