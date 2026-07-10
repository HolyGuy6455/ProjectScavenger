using System;

[Serializable]
public abstract class BTActionLogic
{
    public abstract NodeState Evaluate(AIBrain brain);
}
