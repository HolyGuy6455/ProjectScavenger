using System;
using UnityEngine;

[Serializable]
public class BTActCheckBool : BTActionLogic
{
    public string targetBoolKey = "";
    public bool successIfTrue = true;

    public override string GetDescription(AIBrain brain)
    {
        string boolStr = GetKeyValueString<BTSharedBoolean>(brain, targetBoolKey);
        string conditionStr = successIfTrue ? "True" : "False";
        return $"{boolStr} 값이 {conditionStr}이면 Success, 아니면 Failed를 반환합니다.";
    }

    public override NodeState Evaluate(AIBrain brain)
    {
        var boolProp = brain.GetVariable<BTSharedBoolean>(targetBoolKey);

        if (boolProp == null)
        {
            Debug.LogError("[BTActCheckBool] Blackboard key is missing or of incorrect type!");
            return NodeState.FAILURE;
        }

        bool conditionMet = successIfTrue ? boolProp.value : !boolProp.value;

        if (conditionMet)
        {
            return NodeState.SUCCESS;
        }
        else
        {
            return NodeState.FAILURE;
        }
    }
}
