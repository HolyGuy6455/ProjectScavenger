using System;
using UnityEngine;

[Serializable]
public class BTActCopyPos : BTActionLogic
{
    public string targetObjectKey = "";
    public string outPositionKey = "";

    public override string GetDescription(AIBrain brain)
    {
        string targetStr = GetKeyValueString<BTSharedGameObject>(brain, targetObjectKey);
        string posStr = GetKeyValueString<BTSharedVector3>(brain, outPositionKey);
        return $"{targetStr}의 현재 위치 좌표를 {posStr}에 저장하고 무조건 Success를 반환합니다.";
    }

    public override NodeState Evaluate(AIBrain brain)
    {
        var targetProp = brain.GetVariable<BTSharedGameObject>(targetObjectKey);
        var outPosProp = brain.GetVariable<BTSharedVector3>(outPositionKey);

        if (targetProp == null || targetProp.value == null || outPosProp == null)
        {
            Debug.LogError("[BTActCopyPos] Blackboard key is missing, target object is null, or out key is not Vector3.");
            return NodeState.FAILURE;
        }

        outPosProp.value = targetProp.value.transform.position;
        return NodeState.SUCCESS;
    }
}
