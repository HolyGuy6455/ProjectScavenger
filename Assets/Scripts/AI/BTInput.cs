using System;
using UnityEngine;

[Serializable]
public abstract class BTInput
{
    public bool useDirectValue = false;
    public string blackboardKey = "";

    public abstract Type BlackboardVariableType { get; }
    public abstract Type ValueType { get; }
    public abstract object DirectValue { get; set; }
}

[Serializable]
public abstract class BTInput<T, TVariable> : BTInput where TVariable : BTSharedVariable
{
    public T directValue;

    public override Type BlackboardVariableType => typeof(TVariable);
    public override Type ValueType => typeof(T);
    public override object DirectValue
    {
        get => directValue;
        set => directValue = (T)value;
    }

    public bool TryGetValue(AIBrain brain, out T value)
    {
        if (useDirectValue)
        {
            value = directValue;
            return true;
        }

        value = default;
        if (brain == null || string.IsNullOrEmpty(blackboardKey)) return false;

        var variable = brain.GetVariable<TVariable>(blackboardKey);
        if (variable == null) return false;

        value = ReadValue(variable);
        return true;
    }

    protected abstract T ReadValue(TVariable variable);
}

[Serializable]
public class BTStringInput : BTInput<string, BTSharedString>
{
    public BTStringInput()
    {
        directValue = "";
    }

    protected override string ReadValue(BTSharedString variable) => variable.value;
}

[Serializable]
public class BTNumericInput : BTInput<double, BTSharedNumeric>
{
    protected override double ReadValue(BTSharedNumeric variable) => variable.value;
}

[Serializable]
public class BTBooleanInput : BTInput<bool, BTSharedBoolean>
{
    protected override bool ReadValue(BTSharedBoolean variable) => variable.value;
}

[Serializable]
public class BTVector3Input : BTInput<Vector3, BTSharedVector3>
{
    protected override Vector3 ReadValue(BTSharedVector3 variable) => variable.value;
}

[Serializable]
public class BTGameObjectInput : BTInput<GameObject, BTSharedGameObject>
{
    protected override GameObject ReadValue(BTSharedGameObject variable) => variable.value;
}
