using System;
using UnityEngine;

/// <summary>
/// 지정한 종류의 자극을 위치 기억으로 받아들이고 처리 완료 표시를 합니다.
/// 자극이 없어도 Success를 반환하므로 Sequence 앞부분에 놓을 수 있습니다.
/// </summary>
[Serializable]
public class BTActRememberStimulus : BTActionLogic
{
    public string pendingBoolKey = "HasPendingStimulus";
    public string stimulusTypeKey = "StimulusType";
    public string stimulusPositionKey = "StimulusPosition";
    public AIStimulusType acceptedType = AIStimulusType.Damage;
    public string outPositionKey = "Player/LastPos";
    public string hasPositionBoolKey = "HasLastPos";

    public override string GetDescription(AIBrain brain)
    {
        return $"미처리 {acceptedType} 자극의 발생 위치를 '{outPositionKey}'에 기억하고 "
            + $"'{hasPositionBoolKey}'를 켠 뒤 '{pendingBoolKey}'를 끕니다.\n"
            + "자극이 없거나 다른 종류이면 그대로 Success를 반환합니다. "
            + "다른 종류의 자극은 별도 BT가 처리할 수 있도록 남겨둡니다.";
    }

    public override NodeState Evaluate(AIBrain brain)
    {
        if (brain == null) return NodeState.FAILURE;

        var pending = brain.GetVariable<BTSharedBoolean>(pendingBoolKey);
        if (pending == null)
        {
            Debug.LogError($"[BTActRememberStimulus] Bool 키를 찾을 수 없습니다: {pendingBoolKey}");
            return NodeState.FAILURE;
        }
        if (!pending.value) return NodeState.SUCCESS;

        var type = brain.GetVariable<BTSharedNumeric>(stimulusTypeKey);
        if (type == null)
        {
            Debug.LogError($"[BTActRememberStimulus] Numeric 키를 찾을 수 없습니다: {stimulusTypeKey}");
            return NodeState.FAILURE;
        }
        if (type.value != (int)acceptedType) return NodeState.SUCCESS;

        var position = brain.GetVariable<BTSharedVector3>(stimulusPositionKey);
        var outPosition = brain.GetVariable<BTSharedVector3>(outPositionKey);
        var hasPosition = brain.GetVariable<BTSharedBoolean>(hasPositionBoolKey);
        if (position == null || outPosition == null || hasPosition == null)
        {
            Debug.LogError("[BTActRememberStimulus] 위치 또는 위치 기억 여부의 Blackboard 키가 없거나 타입이 다릅니다.");
            return NodeState.FAILURE;
        }

        // 오브젝트의 현재 위치를 다시 읽지 않고, 자극 당시 저장한 위치를 사용합니다.
        outPosition.value = position.value;
        hasPosition.value = true;
        pending.value = false;
        return NodeState.SUCCESS;
    }
}
