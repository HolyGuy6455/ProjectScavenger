using System;
using UnityEngine;

[Serializable]
public class Action_Log : BTActionLogic
{
    public string message = "Hello from Action_Log!";

    public override NodeState Evaluate(AIBrain brain)
    {
        Debug.Log(message);
        return NodeState.SUCCESS;
    }
}
