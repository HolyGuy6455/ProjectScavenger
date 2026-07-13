using System;
using UnityEngine;

[Serializable]
public class Action_Log : BTActionLogic
{
    public string message = "Hello from Action_Log!";

    public override string GetDescription(AIBrain brain) => $"'{message}' 텍스트를 콘솔에 출력하고 무조건 Success를 반환합니다.";

    public override NodeState Evaluate(AIBrain brain)
    {
        Debug.Log(message);
        return NodeState.SUCCESS;
    }
}
