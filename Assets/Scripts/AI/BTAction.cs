using System;
using UnityEngine;

[Serializable]
public class BTAction : BTNode
{
    [SerializeReference]
    public BTActionLogic logic;

    [NonSerialized] private int _lastEvalFrame = -1;

    public override void ResetState()
    {
        base.ResetState();
        if (logic != null) logic.ResetState();
    }

    public override NodeState Evaluate(AIBrain brain)
    {
        if (logic != null)
        {
            // 만약 이전 프레임에 평가되지 않았다면 (새로 진입했거나 다른 브랜치에서 넘어온 경우)
            if (_lastEvalFrame < Time.frameCount - 1)
            {
                logic.ResetState();
            }
            _lastEvalFrame = Time.frameCount;

            state = logic.Evaluate(brain);
            return state;
        }
        
        state = NodeState.FAILURE;
        return state;
    }
}
