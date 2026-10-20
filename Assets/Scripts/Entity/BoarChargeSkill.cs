using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 목표 방향을 고정하고 준비, 돌진, 후딜 순서로 실행하는 멧돼지 스킬입니다.
/// </summary>
[RequireComponent(typeof(EntityMovement))]
public class BoarChargeSkill : EnemySkill
{
    public enum ChargePhase { Idle, Preparing, Charging, Recovering }

    public override string SkillId => "Charge";
    public ChargePhase CurrentPhase { get; private set; }

    [Header("Charge Settings")]
    [Min(0f)] public float preparationTime = 1f;
    public float chargeSpeed = 8f;
    [Min(0f)] public float maxChargeTime = 1.5f;
    [Min(0f)] public float recoveryTime = 1.5f;

    [Header("Animations")]
    [SerializeField] private string prepareStateName = "Base Layer.Boar_ChargePrepare";
    [SerializeField] private string chargeStateName = "Base Layer.Boar_Charge";
    [SerializeField] private string recoverStateName = "Base Layer.Boar_ChargeRecover";
    
    [Header("Attack Settings")]
    public float attackDamage = 15f;
    public float attackRadius = 1.2f;
    public Vector3 attackOffset = new Vector3(0, 0.5f, 1.0f);
    public LayerMask targetLayer = ~0;

    private EntityMovement _movement;
    private Rigidbody _body;
    private Health _health;
    private EnemyAnimator _enemyAnimator;
    private Vector3 _chargeDirection;
    private float _phaseEndTime;
    private bool _movementWasEnabled;
    private bool _syncMovementAndFacingWasEnabled;
    private readonly HashSet<Health> _hitTargets = new HashSet<Health>();

    private void Awake()
    {
        _movement = GetComponent<EntityMovement>();
        _body = GetComponent<Rigidbody>();
        _health = GetComponent<Health>();
        _enemyAnimator = GetComponent<EnemyAnimator>();
    }

    public override void ExecuteSkill(GameObject targetObj, Vector3 targetPos)
    {
        if (IsExecuting || !isActiveAndEnabled || !CanExecute()) return;
        if (_movement == null || !_movement.enabled) return;

        // 준비 중 플레이어가 움직여도 시전 시점의 방향은 바꾸지 않습니다.
        _chargeDirection = targetPos - transform.position;
        _chargeDirection.y = 0f;
        if (_chargeDirection.sqrMagnitude < 0.01f)
        {
            _chargeDirection = transform.forward;
            _chargeDirection.y = 0f;
        }
        _chargeDirection = _chargeDirection.sqrMagnitude > 0.001f
            ? _chargeDirection.normalized : Vector3.forward;

        IsExecuting = true;
        _hitTargets.Clear();
        _movementWasEnabled = _movement.enabled;
        _syncMovementAndFacingWasEnabled = _movement.SyncMovementAndFacing;
        StopMovement();
        _movement.enabled = false;
        _body.rotation = Quaternion.LookRotation(_chargeDirection);
        SetPhase(ChargePhase.Preparing, preparationTime);
    }

    private bool CanExecute()
    {
        return _health != null && _health.IsSpawned && _health.IsServer && !_health.IsDead;
    }

    private void Update()
    {
        if (!IsExecuting) return;
        if (!CanExecute())
        {
            CancelSkill();
            return;
        }
        if (Time.time < _phaseEndTime) return;

        switch (CurrentPhase)
        {
            case ChargePhase.Preparing:
                if (maxChargeTime <= 0f)
                {
                    BeginRecovery();
                    break;
                }
                _movement.SyncMovementAndFacing = true;
                _movement.enabled = true;
                _movement.MoveInDirection(_chargeDirection, chargeSpeed);
                SetPhase(ChargePhase.Charging, maxChargeTime);
                PerformMeleeAttack();
                break;
            case ChargePhase.Charging:
                // 마지막 물리 이동 위치도 검사한 후 공격 판정을 닫습니다.
                PerformMeleeAttack();
                BeginRecovery();
                break;
            case ChargePhase.Recovering:
                CancelSkill();
                break;
        }
    }

    private void FixedUpdate()
    {
        if (CurrentPhase != ChargePhase.Charging) return;
        if (!CanExecute())
        {
            CancelSkill();
            return;
        }
        PerformMeleeAttack();
    }

    private void SetPhase(ChargePhase phase, float duration)
    {
        CurrentPhase = phase;
        _phaseEndTime = Time.time + Mathf.Max(0f, duration);

        string stateName = phase switch
        {
            ChargePhase.Preparing => prepareStateName,
            ChargePhase.Charging => chargeStateName,
            ChargePhase.Recovering => recoverStateName,
            _ => null
        };
        if (_enemyAnimator != null && !string.IsNullOrEmpty(stateName))
            _enemyAnimator.PlaySkillAnimation(stateName);
    }

    private void BeginRecovery()
    {
        StopMovement();
        _movement.enabled = false;
        SetPhase(ChargePhase.Recovering, recoveryTime);
    }

    public void CancelSkill()
    {
        if (!IsExecuting) return;

        StopMovement();
        _movement.SyncMovementAndFacing = _syncMovementAndFacingWasEnabled;
        _movement.enabled = _movementWasEnabled;
        _hitTargets.Clear();
        CurrentPhase = ChargePhase.Idle;
        IsExecuting = false;
        if (_enemyAnimator != null) _enemyAnimator.EndSkillAnimation();
    }

    private void StopMovement()
    {
        if (_movement == null) return;

        // Despawn 직후에는 NetworkRigidbody가 이미 kinematic으로 바뀔 수 있습니다.
        // 이 경우 속도를 쓰지 않고 남아 있는 방향 이동 명령만 0으로 만듭니다.
        if (_body != null && _body.isKinematic)
            _movement.MoveInDirection(Vector3.zero, 0f);
        else
            _movement.Stop();
    }

    private void OnDisable()
    {
        CancelSkill();
    }

    /// <summary>
    /// 돌진 중 물리 주기마다 호출합니다. 나중에 애니메이션 이벤트가 연결되어도
    /// 같은 돌진에서는 플레이어 한 명당 한 번만 피해를 줍니다.
    /// </summary>
    public void PerformMeleeAttack()
    {
        if (!IsExecuting || CurrentPhase != ChargePhase.Charging || !CanExecute()) return;

        Vector3 sphereCenter = _body.position + _body.rotation * attackOffset;
        Collider[] hitColliders = Physics.OverlapSphere(
            sphereCenter, attackRadius, targetLayer, QueryTriggerInteraction.Ignore);

        foreach (var hitCollider in hitColliders)
        {
            Health targetHealth = hitCollider.GetComponentInParent<Health>();
            if (targetHealth == null || targetHealth == _health || targetHealth.IsDead) continue;
            if (!targetHealth.CompareTag("Player")
                && targetHealth.GetComponentInParent<PlayerController>() == null) continue;
            if (!_hitTargets.Add(targetHealth)) continue;

            Vector3 hitPoint = hitCollider.ClosestPoint(sphereCenter);
            Vector3 hitNormal = (hitPoint - sphereCenter).normalized;
            targetHealth.TakeDamage(new DamageInfo(attackDamage, gameObject, hitPoint, hitNormal));
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1, 0, 0, 0.5f);
        Vector3 sphereCenter = transform.position + transform.rotation * attackOffset;
        Gizmos.DrawSphere(sphereCenter, attackRadius);
    }
}
