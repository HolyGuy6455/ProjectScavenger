using System;
using UnityEngine;

[Serializable]
public class BTActMoveTo : BTActionLogic
{
    public string targetPositionKey = "";
    public string mySelfKey = "";

    public override string GetDescription(AIBrain brain)
    {
        string mySelfStr = GetKeyValueString<BTSharedGameObject>(brain, mySelfKey);
        string posStr = GetKeyValueString<BTSharedVector3>(brain, targetPositionKey);
        return $"{mySelfStr}의 EntityMovement를 이용해 좌표 {posStr}로 이동합니다. 목표에 도달하면 Success, 이동 중이면 Running을 반환합니다.";
    }

    public override NodeState Evaluate(AIBrain brain)
    {
        var targetPosProp = brain.GetVariable<BTSharedVector3>(targetPositionKey);
        var mySelfProp = brain.GetVariable<BTSharedGameObject>(mySelfKey);

        if (targetPosProp == null || mySelfProp == null)
        {
            Debug.LogError("[BTActMoveTo] Blackboard keys are missing or of incorrect type.");
            return NodeState.FAILURE;
        }

        if (mySelfProp.value == null)
        {
            Debug.LogError("[BTActMoveTo] MySelf GameObject is null.");
            return NodeState.FAILURE;
        }

        var movement = mySelfProp.value.GetComponent<EntityMovement>();
        if (movement == null)
        {
            Debug.LogError("[BTActMoveTo] EntityMovement component is missing on MySelf GameObject.");
            return NodeState.FAILURE;
        }

        Vector3 targetPos = targetPosProp.value;
        Vector3 myPos = mySelfProp.value.transform.position;

        // 수평 거리만 계산 (y축 무시)
        Vector2 targetPos2D = new Vector2(targetPos.x, targetPos.z);
        Vector2 myPos2D = new Vector2(myPos.x, myPos.z);
        float distance = Vector2.Distance(myPos2D, targetPos2D);

        // 도착 판정 (임의의 0.15 거리 이내면 도착한 것으로 간주)
        if (distance <= 0.15f)
        {
            movement.Stop();
            return NodeState.SUCCESS;
        }

        movement.MoveTo(targetPos);
        return NodeState.RUNNING;
    }
}
