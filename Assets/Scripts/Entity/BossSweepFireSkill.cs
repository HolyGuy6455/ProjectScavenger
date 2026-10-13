using UnityEngine;

/// <summary>제자리에서 준비/훑기 애니메이션을 재생하며 남은 탄약을 모두 발사합니다.</summary>
[RequireComponent(typeof(EntityMovement), typeof(Shooter), typeof(EnemyAnimator))]
public class BossSweepFireSkill : EnemySkill
{
    public override string SkillId => "SweepFire";

    [SerializeField] private string prepareStateName = "Base Layer.Boss_SweepPrepare";

    private EntityMovement _movement;
    private Shooter _shooter;
    private EnemyAnimator _enemyAnimator;
    private Health _health;
    private bool _movementWasEnabled;

    private void Awake()
    {
        _movement = GetComponent<EntityMovement>();
        _shooter = GetComponent<Shooter>();
        _enemyAnimator = GetComponent<EnemyAnimator>();
        _health = GetComponent<Health>();
    }

    public override void ExecuteSkill(GameObject targetObj, Vector3 targetPos)
    {
        if (IsExecuting || !isActiveAndEnabled) return;
        if (!_shooter.IsSpawned || !_shooter.IsServer || !_shooter.IsOwner) return;
        if (_shooter.IsReloading || _shooter.CurrentAmmoData == null || _shooter.CurrentAmmo <= 0) return;
        if (_health != null && _health.IsDead) return;

        IsExecuting = true;
        _movementWasEnabled = _movement.enabled;
        _movement.Stop();
        _movement.enabled = false;

        if (!_enemyAnimator.PlaySkillAnimation(prepareStateName))
        {
            Debug.LogWarning($"[BossSweepFireSkill] 애니메이션 상태를 찾을 수 없습니다: {prepareStateName}", this);
            CancelSkill();
            return;
        }

        // 준비 동작부터 발사합니다. SweepFire 전환은 Animator Controller가 담당합니다.
        _shooter.SetTrigger(true, _shooter.FirePoint.forward);
    }

    private void LateUpdate()
    {
        if (!IsExecuting) return;

        if (!_shooter.IsSpawned || !_shooter.IsServer || !_shooter.IsOwner
            || _shooter.CurrentAmmo <= 0 || (_health != null && _health.IsDead))
        {
            CancelSkill();
            return;
        }

        // 애니메이션으로 회전한 총구를 따라 사격 방향도 갱신합니다.
        _shooter.UpdateAimDirection(_shooter.FirePoint.forward);
    }

    public void CancelSkill()
    {
        if (!IsExecuting) return;

        _shooter.SetTrigger(false);
        _movement.Stop();
        _movement.enabled = _movementWasEnabled;
        _enemyAnimator.EndSkillAnimation();
        IsExecuting = false;
    }

    private void OnDisable()
    {
        CancelSkill();
    }
}
