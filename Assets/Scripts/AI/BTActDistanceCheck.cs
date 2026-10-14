using System;
using UnityEngine;

[Serializable]
public class BTActDistanceCheck : BTActionLogic
{
    public string targetPositionKey = "";
    public string mySelfKey = "";
    public string distanceKey = "";
    public bool successIfCloser = true;
    [Tooltip("높이 차이를 제외합니다. SmartMoveTo의 도착 판정과 같은 수평 거리를 사용합니다.")]
    public bool horizontalOnly = false;

    public override string GetDescription(AIBrain brain)
    {
        string mySelfStr = GetKeyValueString<BTSharedGameObject>(brain, mySelfKey);
        string posStr = GetKeyValueString<BTSharedVector3>(brain, targetPositionKey);
        string distStr = GetKeyValueString<BTSharedNumeric>(brain, distanceKey);

        string conditionStr = successIfCloser ? "이하면" : "이상이면";
        string distanceType = horizontalOnly ? "수평 거리" : "거리";

        return $"{mySelfStr}로 부터 좌표 {posStr}까지의 {distanceType}가 {distStr} {conditionStr} Success, 아니면 Failed를 반환합니다.";
    }

    public override NodeState Evaluate(AIBrain brain)
    {
        var targetPosProp = brain.GetVariable<BTSharedVector3>(targetPositionKey);
        var mySelfProp = brain.GetVariable<BTSharedGameObject>(mySelfKey);
        var distanceProp = brain.GetVariable<BTSharedNumeric>(distanceKey);

        if (targetPosProp == null || mySelfProp == null || distanceProp == null)
        {
            Debug.LogError("[BTActDistanceCheck] Blackboard keys are missing or of incorrect type.");
            return NodeState.FAILURE;
        }

        if (mySelfProp.value == null)
        {
            Debug.LogError("[BTActDistanceCheck] MySelf GameObject is null in the Blackboard.");
            return NodeState.FAILURE;
        }

        Vector3 targetPos = targetPosProp.value;
        Vector3 myPos = mySelfProp.value.transform.position;

        Vector3 offset = targetPos - myPos;
        if (horizontalOnly) offset.y = 0f;
        float distance = offset.magnitude;

        bool conditionMet = successIfCloser ? (distance <= (float)distanceProp.value) : (distance >= (float)distanceProp.value);

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
