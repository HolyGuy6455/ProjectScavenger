using System;
using System.Collections.Generic;

[Serializable]
public abstract class BTNode
{
    public string nodeName = "New Node";
    public string nodeMemo = "";
    protected NodeState state;
    public NodeState State { get { return state; } }
    
    public virtual void ResetState()
    {
        state = NodeState.IDLE;
    }
    
    // Abstract method that must be implemented by derived nodes
    public abstract NodeState Evaluate(AIBrain brain);
}
