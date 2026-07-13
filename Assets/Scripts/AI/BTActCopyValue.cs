using System;
using UnityEngine;

[Serializable]
public class BTActCopyValue : BTActionLogic
{
    public string sourceKey = "";
    public string targetKey = "";

    public override string GetDescription(AIBrain brain)
    {
        return $"'{sourceKey}'의 값을 '{targetKey}'에 복사합니다. (타입이 다르거나 변수가 없으면 무시되며, 항상 Success를 반환합니다)";
    }

    public override NodeState Evaluate(AIBrain brain)
    {
        if (brain == null || brain.blackboard == null) return NodeState.FAILURE;

        if (string.IsNullOrEmpty(sourceKey) || string.IsNullOrEmpty(targetKey))
        {
            Debug.LogWarning("[BTActCopyValue] Source 또는 Target 키가 비어있어 복사를 무시합니다.");
            return NodeState.SUCCESS;
        }

        BTSharedVariable sourceVar = brain.blackboard.Find(x => x.key == sourceKey);
        BTSharedVariable targetVar = brain.blackboard.Find(x => x.key == targetKey);

        if (sourceVar == null || targetVar == null)
        {
            Debug.LogWarning($"[BTActCopyValue] '{sourceKey}' 또는 '{targetKey}' 변수를 찾을 수 없어 복사를 무시합니다.");
            return NodeState.SUCCESS;
        }

        bool copySuccess = targetVar.TryCopyFrom(sourceVar);
        
        if (!copySuccess)
        {
            Debug.LogWarning($"[BTActCopyValue] '{sourceKey}'와 '{targetKey}'의 타입이 달라 복사를 무시했습니다.");
        }

        return NodeState.SUCCESS;
    }
}
