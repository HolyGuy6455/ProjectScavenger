using System;
using UnityEngine;
using UnityEngine.AI;

[Serializable]
public class BTActCalculateFleePos : BTActionLogic
{
    public string mySelfKey = "MySelf";
    public string attackerKey = "Attacker";
    public string outFleePosKey = "Target/Pos"; // 도망칠 목적지를 저장할 키
    
    public float fleeDistance = 10f; // 얼마나 멀리 도망칠지

    public override string GetDescription(AIBrain brain)
    {
        return $"[{attackerKey}]의 반대 방향으로 {fleeDistance}만큼 도망칠 위치를 계산하여 [{outFleePosKey}]에 저장합니다.";
    }

    public override NodeState Evaluate(AIBrain brain)
    {
        var mySelfProp = brain.GetVariable<BTSharedGameObject>(mySelfKey);
        var attackerProp = brain.GetVariable<BTSharedGameObject>(attackerKey);
        var outFleePosProp = brain.GetVariable<BTSharedVector3>(outFleePosKey);

        if (mySelfProp == null || mySelfProp.value == null || attackerProp == null || outFleePosProp == null)
        {
            return NodeState.FAILURE;
        }

        GameObject mySelf = mySelfProp.value;
        GameObject attacker = attackerProp.value;

        if (attacker == null)
        {
            return NodeState.FAILURE; // 공격자가 없으면 실패
        }

        Vector3 myPos = mySelf.transform.position;
        Vector3 attackerPos = attacker.transform.position;

        // 공격자로부터 나를 향하는 방향 벡터 (반대 방향)
        Vector3 fleeDir = (myPos - attackerPos).normalized;
        fleeDir.y = 0; // 수평으로만 도망치도록 보정
        
        // 위치가 완전히 같을 때의 예외 처리
        if (fleeDir == Vector3.zero) 
        {
            fleeDir = mySelf.transform.forward; 
        }

        Vector3 fleeTargetPos = myPos + (fleeDir * fleeDistance);

        // 네비메쉬 위의 유효한 위치인지 검사하여 보정 (선택 사항)
        if (NavMesh.SamplePosition(fleeTargetPos, out NavMeshHit hit, fleeDistance, NavMesh.AllAreas))
        {
            fleeTargetPos = hit.position;
        }

        outFleePosProp.value = fleeTargetPos;

        return NodeState.SUCCESS;
    }
}
