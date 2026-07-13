using System;
using UnityEngine;

[Serializable]
public class BTActSetBool : BTActionLogic
{
    public string targetBoolKey = "";
    public bool setToValue = true;

    public override string GetDescription(AIBrain brain)
    {
        string boolStr = GetKeyValueString<BTSharedBoolean>(brain, targetBoolKey);
        return $"{boolStr} 값을 {setToValue}로 변경하고 무조건 Success를 반환합니다.";
    }

    public override NodeState Evaluate(AIBrain brain)
    {
        var boolProp = brain.GetVariable<BTSharedBoolean>(targetBoolKey);

        if (boolProp == null)
        {
            Debug.LogError("[BTActSetBool] Blackboard key is missing or of incorrect type!");
            return NodeState.FAILURE;
        }

        boolProp.value = setToValue;
        return NodeState.SUCCESS;
    }
}
