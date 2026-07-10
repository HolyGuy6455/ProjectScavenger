using System;
using System.Collections.Generic;

[Serializable]
public abstract class BTNode
{
    public string nodeName = "New Node";
    
    protected NodeState state;
    public NodeState State { get { return state; } }
    
    // Abstract method that must be implemented by derived nodes
    public abstract NodeState Evaluate(AIBrain brain);
}
