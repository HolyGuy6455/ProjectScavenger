using UnityEngine;

/// <summary>이동과 사격을 멈추고 재장전 동작이 끝난 뒤 탄약을 보충합니다.</summary>
[RequireComponent(typeof(EntityMovement), typeof(Shooter), typeof(EnemyAnimator))]
public class EnemyReloadSkill : EnemySkill
{
    public override string SkillId => "Reload";

    [SerializeField] private string reloadStateName = "Reload";
    [Tooltip("재장전 애니메이션의 실제 재생 시간에 맞춥니다. 이 시간이 끝나야 탄약이 보충됩니다.")]
    [SerializeField, Min(0f)] private float reloadDuration = 3f;

    private EntityMovement _movement;
    private Shooter _shooter;
    private EnemyAnimator _enemyAnimator;
    private Health _health;
    private bool _movementWasEnabled;
    private float _finishTime;

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
        if (_health != null && _health.IsDead) return;
        if (_shooter.CurrentAmmoData == null
            || _shooter.CurrentAmmo >= _shooter.CurrentAmmoData.maxAmmo) return;

        foreach (var skill in GetComponents<EnemySkill>())
        {
            if (skill != this && skill.IsExecuting) return;
        }

        if (!_shooter.BeginReload()) return;

        IsExecuting = true;
        _movementWasEnabled = _movement.enabled;
        _movement.Stop();
        _movement.enabled = false;

        if (!_enemyAnimator.PlaySkillAnimation(reloadStateName))
        {
            Debug.LogWarning($"[EnemyReloadSkill] 애니메이션 상태를 찾을 수 없습니다: {reloadStateName}", this);
            CancelSkill();
            return;
        }

        _finishTime = Time.time + Mathf.Max(0f, reloadDuration);
    }

    private void LateUpdate()
    {
        if (!IsExecuting) return;

        if (!_shooter.IsSpawned || !_shooter.IsServer || !_shooter.IsOwner
            || !_shooter.IsReloading || (_health != null && _health.IsDead))
        {
            CancelSkill();
            return;
        }

        if (Time.time >= _finishTime) FinishSkill(true);
    }

    public void CancelSkill()
    {
        FinishSkill(false);
    }

    private void FinishSkill(bool refill)
    {
        if (!IsExecuting) return;

        if (refill) _shooter.CompleteReload();
        else _shooter.CancelReload();

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
