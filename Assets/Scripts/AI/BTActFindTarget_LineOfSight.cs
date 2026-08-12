using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class BTActFindTarget_LineOfSight : BTActionLogic
{
    public string targetTag = "Player";
    public LayerMask obstacleLayer; // 장애물로 판정할 레이어 (예: Wall, Default 등)
    public string mySelfKey = "";
    public string outGameObjectKey = "";
    
    public bool returnRunningIfNotFound = true;

    public override string GetDescription(AIBrain brain)
    {
        string mySelfStr = GetKeyValueString<BTSharedGameObject>(brain, mySelfKey);
        string waitStr = returnRunningIfNotFound ? "\n(시야 내에 타겟이 없으면 대기합니다)" : "";
        return $"{mySelfStr}의 시야(Line of Sight) 내에 있는 태그 '{targetTag}' 중 가장 가까운 오브젝트를 '{outGameObjectKey}'에 저장합니다.\n(장애물 레이어: {obstacleLayer.value}){waitStr}";
    }

    public override NodeState Evaluate(AIBrain brain)
    {
        var outGoProp = brain.GetVariable<BTSharedGameObject>(outGameObjectKey);
        var mySelfProp = brain.GetVariable<BTSharedGameObject>(mySelfKey);

        if (outGoProp == null || mySelfProp == null)
        {
            Debug.LogError("[BTActFindTarget_LineOfSight] Blackboard keys are missing or of incorrect type.");
            return NodeState.FAILURE;
        }

        if (mySelfProp.value == null)
        {
            Debug.LogError("[BTActFindTarget_LineOfSight] MySelf GameObject is null.");
            return NodeState.FAILURE;
        }

        GameObject[] potentialTargets;
        try 
        {
            potentialTargets = GameObject.FindGameObjectsWithTag(targetTag);
        } 
        catch (UnityException) 
        {
            return returnRunningIfNotFound ? NodeState.RUNNING : NodeState.FAILURE;
        }

        if (potentialTargets == null || potentialTargets.Length == 0)
        {
            outGoProp.value = null;
            return returnRunningIfNotFound ? NodeState.RUNNING : NodeState.FAILURE;
        }

        Vector3 myPos = mySelfProp.value.transform.position;
        // 시야 판정을 위해 발 밑이 아닌 가슴/머리 높이로 약간 보정 (필요시 사용)
        Vector3 rayStartPos = myPos + Vector3.up * 1.0f; 

        GameObject closestObj = null;
        float closestDistSqr = float.MaxValue;

        foreach (var target in potentialTargets)
        {
            if (target == null) continue;

            Vector3 targetPos = target.transform.position + Vector3.up * 1.0f;
            float distSqr = (targetPos - rayStartPos).sqrMagnitude;

            // 이미 찾은 가장 가까운 거리보다 멀면 시야 검사(Raycast) 할 필요도 없음 (최적화)
            if (distSqr < closestDistSqr)
            {
                // Linecast로 장애물 확인 (장애물이 없다면 false 반환)
                if (!Physics.Linecast(rayStartPos, targetPos, obstacleLayer))
                {
                    closestDistSqr = distSqr;
                    closestObj = target;
                }
            }
        }

        if (closestObj == null)
        {
            outGoProp.value = null;
            return returnRunningIfNotFound ? NodeState.RUNNING : NodeState.FAILURE;
        }

        outGoProp.value = closestObj;
        return NodeState.SUCCESS;
    }
}
