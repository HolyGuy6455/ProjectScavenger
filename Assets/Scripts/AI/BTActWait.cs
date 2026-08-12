using System;
using UnityEngine;

[Serializable]
public class BTActWait : BTActionLogic
{
    [Header("Wait Settings")]
    public float waitTimeMin = 1.0f;
    public float waitTimeMax = 1.0f;
    
    [NonSerialized] private float _timer = 0f;
    [NonSerialized] private bool _isWaiting = false;
    [NonSerialized] private float _actualWaitTime = 1.0f;

    public override string GetDescription(AIBrain brain)
    {
        if (Mathf.Approximately(waitTimeMin, waitTimeMax))
            return $"{waitTimeMin}초 동안 대기합니다.";
        return $"{waitTimeMin} ~ {waitTimeMax}초 사이로 랜덤 대기합니다.";
    }

    public override void ResetState()
    {
        base.ResetState();
        _isWaiting = false;
        _timer = 0f;
    }

    public override NodeState Evaluate(AIBrain brain)
    {
        if (!_isWaiting)
        {
            // 대기 시작 시 실제 대기 시간을 결정
            _isWaiting = true;
            _timer = 0f;
            _actualWaitTime = UnityEngine.Random.Range(waitTimeMin, waitTimeMax);
            return NodeState.RUNNING;
        }

        _timer += Time.deltaTime;
        
        if (_timer >= _actualWaitTime)
        {
            // 대기 완료
            return NodeState.SUCCESS;
        }

        return NodeState.RUNNING;
    }
}
