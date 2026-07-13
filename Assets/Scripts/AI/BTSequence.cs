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
        bool anyChildIsRunning = false;

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
                    anyChildIsRunning = true;
                    continue;
                default:
                    state = NodeState.SUCCESS;
                    return state;
            }
        }

        state = anyChildIsRunning ? NodeState.RUNNING : NodeState.SUCCESS;
        return state;
    }
}
