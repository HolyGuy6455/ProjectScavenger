using System.Collections.Generic;
using UnityEngine;

public class BTSelector : BTNode
{
    [SerializeReference]
    public List<BTNode> children = new List<BTNode>();

    public BTSelector()
    {
    }

    public BTSelector(List<BTNode> children)
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
                    continue;
                case NodeState.SUCCESS:
                    state = NodeState.SUCCESS;
                    return state;
                case NodeState.RUNNING:
                    state = NodeState.RUNNING;
                    return state;
                default:
                    continue;
            }
        }

        state = NodeState.FAILURE;
        return state;
    }
}
