using System;
using UnityEngine;

[Serializable]
public class BTAction : BTNode
{
    [SerializeReference]
    public BTActionLogic logic;

    public override NodeState Evaluate(AIBrain brain)
    {
        if (logic != null)
        {
            state = logic.Evaluate(brain);
            return state;
        }
        
        state = NodeState.FAILURE;
        return state;
    }
}
