using System;
using UnityEngine;

[Serializable]
public class BTActFindTarget_LineOfSight : BTActionLogic
{
    public string targetTag = "Player";
    [Min(0f)] public float maxDistance = 20f;
    [Range(0f, 360f)] public float fieldOfView = 120f; // 정면 기준 좌우를 합한 수평 시야각
    public LayerMask obstacleLayer; // 장애물로 판정할 레이어 (예: Wall, Default 등)
    public string mySelfKey = "";
    public string outGameObjectKey = "";
    
    public bool returnRunningIfNotFound = true;

    public override string GetDescription(AIBrain brain)
    {
        string mySelfStr = GetKeyValueString<BTSharedGameObject>(brain, mySelfKey);
        string waitStr = returnRunningIfNotFound ? "\n(시야 내에 타겟이 없으면 대기합니다)" : "";
        return $"{mySelfStr}의 거리 {maxDistance} 이내, 정면 기준 수평 총 시야각 {fieldOfView}° 안에 있는 태그 '{targetTag}' 중 장애물에 가리지 않은 가장 가까운 오브젝트를 '{outGameObjectKey}'에 저장합니다.\n(장애물 레이어: {obstacleLayer.value}, 찾지 못하면 출력값을 비웁니다){waitStr}";
    }

    public override NodeState Evaluate(AIBrain brain)
    {
        var outGoProp = brain.GetVariable<BTSharedGameObject>(outGameObjectKey);
        var mySelfProp = brain.GetVariable<BTSharedGameObject>(mySelfKey);
        GameObject mySelf = mySelfProp != null ? mySelfProp.value : null;

        // 탐색 실패나 잘못된 설정 때문에 이전 타겟이 계속 남지 않게 한다.
        if (outGoProp != null) outGoProp.value = null;

        if (outGoProp == null || mySelfProp == null)
        {
            Debug.LogError("[BTActFindTarget_LineOfSight] Blackboard keys are missing or of incorrect type.");
            return NodeState.FAILURE;
        }

        if (mySelf == null)
        {
            Debug.LogError("[BTActFindTarget_LineOfSight] MySelf GameObject is null.");
            return NodeState.FAILURE;
        }

        if (string.IsNullOrEmpty(targetTag))
            return returnRunningIfNotFound ? NodeState.RUNNING : NodeState.FAILURE;

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
            return returnRunningIfNotFound ? NodeState.RUNNING : NodeState.FAILURE;
        }

        Vector3 myPos = mySelf.transform.position;
        Vector3 horizontalForward = Vector3.ProjectOnPlane(mySelf.transform.forward, Vector3.up);
        float maxDistSqr = Mathf.Max(0f, maxDistance) * Mathf.Max(0f, maxDistance);
        float halfFieldOfView = Mathf.Clamp(fieldOfView, 0f, 360f) * 0.5f;
        // 시야 판정을 위해 발 밑이 아닌 가슴/머리 높이로 약간 보정 (필요시 사용)
        Vector3 rayStartPos = myPos + Vector3.up * 1.0f; 

        GameObject closestObj = null;
        float closestDistSqr = float.MaxValue;

        foreach (var target in potentialTargets)
        {
            if (target == null) continue;

            Vector3 targetPos = target.transform.position + Vector3.up * 1.0f;
            Vector3 toTarget = targetPos - rayStartPos;
            float distSqr = toTarget.sqrMagnitude;

            // 거리와 수평 시야각을 먼저 거른 뒤 필요한 대상만 장애물을 검사한다.
            if (distSqr > maxDistSqr || distSqr >= closestDistSqr) continue;

            Vector3 horizontalDirection = Vector3.ProjectOnPlane(toTarget, Vector3.up);
            if (halfFieldOfView < 180f && horizontalDirection.sqrMagnitude > 0.000001f
                && Vector3.Angle(horizontalForward, horizontalDirection) > halfFieldOfView)
                continue;

            if (Physics.Linecast(rayStartPos, targetPos, obstacleLayer)) continue;

            closestDistSqr = distSqr;
            closestObj = target;
        }

        if (closestObj == null)
        {
            return returnRunningIfNotFound ? NodeState.RUNNING : NodeState.FAILURE;
        }

        outGoProp.value = closestObj;
        return NodeState.SUCCESS;
    }
}
