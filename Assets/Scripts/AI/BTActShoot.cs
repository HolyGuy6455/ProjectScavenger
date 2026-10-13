using System;
using UnityEngine;

[Serializable]
public class BTActShoot : BTActionLogic
{
    public string mySelfKey = "MySelf";
    public string targetPositionKey = "Player/LastPos";
    [Min(0f)] public float shotInterval = 0.6f;

    // 분기를 잠시 벗어났다가 돌아와도 같은 노드의 사격 간격은 유지한다.
    [NonSerialized] private float _nextShotTime;

    public override string GetDescription(AIBrain brain)
    {
        string self = GetKeyValueString<BTSharedGameObject>(brain, mySelfKey);
        string target = GetKeyValueString<BTSharedVector3>(brain, targetPositionKey);
        return $"{self}가 이동을 멈추지 않고 {target}을 계속 바라보며 {shotInterval}초 간격으로 한 발씩 사격합니다. 무기의 fireRate 제한도 적용됩니다. 사격 대기 중에도 Success를 반환하여 뒤의 이동 행동을 실행합니다.";
    }

    public override NodeState Evaluate(AIBrain brain)
    {
        var self = brain.GetVariable<BTSharedGameObject>(mySelfKey);
        var target = brain.GetVariable<BTSharedVector3>(targetPositionKey);
        if (self == null || self.value == null || target == null)
        {
            Debug.LogError("[BTActShoot] MySelf 또는 TargetPosition 블랙보드 값이 없습니다.");
            return NodeState.FAILURE;
        }

        var shooter = self.value.GetComponent<Shooter>();
        if (shooter == null)
        {
            Debug.LogError("[BTActShoot] MySelf에 Shooter가 없습니다.");
            return NodeState.FAILURE;
        }

        if (!shooter.IsSpawned || !shooter.IsServer || !shooter.IsOwner)
            return NodeState.SUCCESS;

        // 끊어 쏘는 사이에도 목표 위치와 몸의 시선은 계속 갱신한다.
        var movement = self.value.GetComponent<EntityMovement>();
        if (movement != null)
        {
            movement.SyncMovementAndFacing = false;
            movement.SetLookAtPosition(target.value);
        }

        if (Time.time < _nextShotTime) return NodeState.SUCCESS;

        Vector3 direction = target.value - shooter.FirePoint.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.000001f)
            direction = Vector3.ProjectOnPlane(self.value.transform.forward, Vector3.up);

        if (shooter.TryShoot(direction.normalized, target.value))
            _nextShotTime = Time.time + Mathf.Max(0f, shotInterval);

        return NodeState.SUCCESS;
    }
}
