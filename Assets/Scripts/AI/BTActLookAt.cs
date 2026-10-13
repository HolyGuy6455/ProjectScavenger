using System;
using UnityEngine;

[Serializable]
public class BTActLookAt : BTActionLogic
{
    public enum LookMode
    {
        Position,
        RelativeYaw,
        MovementDirection
    }

    public string mySelfKey = "";
    public LookMode mode = LookMode.Position;
    [Tooltip("Position 모드: 진입 시 이 블랙보드 좌표를 한 번 읽어 바라봅니다.")]
    public string targetPositionKey = "";
    [Tooltip("RelativeYaw 모드: 진입 시 정면 기준 회전 각도입니다. 음수는 왼쪽, 양수는 오른쪽입니다.")]
    public float yawOffset = 45f;

    [NonSerialized] private bool _hasIssuedCommand;

    public override string GetDescription(AIBrain brain)
    {
        string self = GetKeyValueString<BTSharedGameObject>(brain, mySelfKey);
        if (mode == LookMode.MovementDirection)
            return $"{self}의 별도 시선 지정을 해제하고 다시 이동 방향을 바라보게 합니다.";

        string target = mode == LookMode.RelativeYaw
            ? $"진입 시 정면에서 {yawOffset}° 회전한 방향"
            : $"좌표 {GetKeyValueString<BTSharedVector3>(brain, targetPositionKey)}";
        return $"{self}의 이동을 멈추고 {target}을 바라보도록 한 번 지시한 뒤 Success를 반환합니다. 회전·대기 시간은 뒤의 Wait로 지정합니다. 시선은 다음 명령까지 유지됩니다.";
    }

    public override void ResetState()
    {
        _hasIssuedCommand = false;
    }

    public override NodeState Evaluate(AIBrain brain)
    {
        // Sequence가 매 틱 앞에서부터 재평가되어도 뒤의 Look 명령을 덮어쓰지 않는다.
        if (_hasIssuedCommand) return NodeState.SUCCESS;

        var self = brain.GetVariable<BTSharedGameObject>(mySelfKey);
        var movement = self != null && self.value != null
            ? self.value.GetComponent<EntityMovement>() : null;
        if (movement == null)
        {
            Debug.LogError("[BTActLookAt] MySelf 또는 EntityMovement가 없습니다.");
            return NodeState.FAILURE;
        }

        if (mode == LookMode.MovementDirection)
        {
            movement.ClearLookAtPosition();
            movement.SyncMovementAndFacing = true;
        }
        else
        {
            Vector3 targetPosition;
            if (mode == LookMode.RelativeYaw)
            {
                var selfTransform = self.value.transform;
                Vector3 direction = Quaternion.AngleAxis(yawOffset, Vector3.up)
                    * Vector3.ProjectOnPlane(selfTransform.forward, Vector3.up).normalized;
                targetPosition = selfTransform.position + direction;
            }
            else
            {
                var target = brain.GetVariable<BTSharedVector3>(targetPositionKey);
                if (target == null)
                {
                    Debug.LogError("[BTActLookAt] TargetPosition 키가 없거나 Vector3 타입이 아닙니다.");
                    return NodeState.FAILURE;
                }
                targetPosition = target.value;
            }

            movement.Stop();
            movement.SyncMovementAndFacing = false;
            movement.SetLookAtPosition(targetPosition);
        }

        _hasIssuedCommand = true;
        return NodeState.SUCCESS;
    }
}
