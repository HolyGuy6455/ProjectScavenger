using System;
using UnityEngine;

[Serializable]
public class BTActSmartMoveTo : BTActionLogic
{
    public string targetPositionKey = "";
    public string mySelfKey = "";

    [Min(0.1f), Tooltip("이 시간 동안 minProgressDistance만큼 움직이지 못하면 실패합니다.")]
    public float stuckTimeout = 2f;
    [Min(0.001f), Tooltip("진행으로 인정할 최소 수평 이동 거리입니다. 우회 이동도 인정합니다.")]
    public float minProgressDistance = 0.1f;
    [Min(0f), Tooltip("실패 후 이 시간 동안 Failure를 유지하고 경로 재계산을 기다립니다.")]
    public float retryDelay = 1f;

    [NonSerialized] private EntityMovement _trackedMovement;
    [NonSerialized] private Vector3 _progressPosition;
    [NonSerialized] private float _lastProgressTime;
    [NonSerialized] private float _retryAfterTime;

    public override void ResetState()
    {
        _trackedMovement = null;
        // 분기를 잠깐 벗어났다가 돌아와도 실패 직후의 재시도 제한은 유지합니다.
    }

    public override string GetDescription(AIBrain brain)
    {
        string mySelfStr = GetKeyValueString<BTSharedGameObject>(brain, mySelfKey);
        string posStr = GetKeyValueString<BTSharedVector3>(brain, targetPositionKey);
        return $"{mySelfStr}가 NavMesh 경로를 따라 좌표 {posStr}로 이동합니다. 도착하면 Success, 이동 중이면 Running입니다. 경로가 없거나 끊겨 있거나, {stuckTimeout}초 동안 수평으로 {minProgressDistance}m 움직이지 못하면 정지하고 Failure를 반환합니다. 실패 후 {retryDelay}초 동안 재시도를 기다립니다.";
    }

    public override NodeState Evaluate(AIBrain brain)
    {
        var targetPosProp = brain.GetVariable<BTSharedVector3>(targetPositionKey);
        var mySelfProp = brain.GetVariable<BTSharedGameObject>(mySelfKey);

        if (targetPosProp == null || mySelfProp == null)
        {
            Debug.LogError("[BTActSmartMoveTo] Blackboard keys are missing or of incorrect type.");
            return NodeState.FAILURE;
        }

        if (mySelfProp.value == null)
        {
            Debug.LogError("[BTActSmartMoveTo] MySelf GameObject is null.");
            return NodeState.FAILURE;
        }

        var movement = mySelfProp.value.GetComponent<EntityMovement>();
        if (movement == null)
        {
            Debug.LogError("[BTActSmartMoveTo] EntityMovement component is missing on MySelf GameObject.");
            return NodeState.FAILURE;
        }

        Vector3 targetPos = targetPosProp.value;
        Vector3 myPos = mySelfProp.value.transform.position;

        // 수평 거리만 계산 (y축 무시)
        Vector2 targetPos2D = new Vector2(targetPos.x, targetPos.z);
        Vector2 myPos2D = new Vector2(myPos.x, myPos.z);
        float distance = Vector2.Distance(myPos2D, targetPos2D);

        // 도착 판정 (임의의 0.15 거리 이내면 도착한 것으로 간주)
        if (distance <= 0.15f)
        {
            movement.Stop();
            ResetState();
            _retryAfterTime = 0f;
            return NodeState.SUCCESS;
        }

        if (Time.time < _retryAfterTime)
        {
            movement.Stop();
            return NodeState.FAILURE;
        }

        if (!movement.SmartMoveTo(targetPos)) return FailMovement(movement);

        Vector3 progress = myPos - _progressPosition;
        progress.y = 0f;
        float requiredProgress = Mathf.Max(0.001f, minProgressDistance);
        if (_trackedMovement != movement || progress.sqrMagnitude >= requiredProgress * requiredProgress)
        {
            _trackedMovement = movement;
            _progressPosition = myPos;
            _lastProgressTime = Time.time;
        }
        else if (Time.time - _lastProgressTime >= Mathf.Max(0.1f, stuckTimeout))
        {
            return FailMovement(movement);
        }

        return NodeState.RUNNING;
    }

    private NodeState FailMovement(EntityMovement movement)
    {
        movement.Stop();
        ResetState();
        _retryAfterTime = Time.time + Mathf.Max(0f, retryDelay);
        return NodeState.FAILURE;
    }
}
