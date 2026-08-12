using System.Collections.Generic;
using UnityEngine;

public class BTSequence : BTNode
{
    [SerializeReference]
    public List<BTNode> children = new List<BTNode>();

    public BTSequence()
    {
    }

    public BTSequence(List<BTNode> children)
    {
        this.children = children;
    }

    public override void ResetState()
    {
        base.ResetState();
        foreach (var child in children)
        {
            if (child != null) child.ResetState();
        }
    }

    public override NodeState Evaluate(AIBrain brain)
    {
        foreach (BTNode child in children)
        {
            switch (child.Evaluate(brain))
            {
                case NodeState.FAILURE:
                    state = NodeState.FAILURE;
                    return state;
                case NodeState.SUCCESS:
                    continue;
                case NodeState.RUNNING:
                    state = NodeState.RUNNING;
                    return state;
                default:
                    state = NodeState.SUCCESS;
                    return state;
            }
        }

        state = NodeState.SUCCESS;
        return state;
    }
}
