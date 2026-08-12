using System.Collections;
using UnityEngine;

/// <summary>
/// 멧돼지 전용 돌진 스킬 컴포넌트입니다.
/// </summary>
[RequireComponent(typeof(EntityMovement))]
public class BoarChargeSkill : EnemySkill
{
    // 스킬 고유 ID 하드코딩
    public override string SkillId => "Charge";

    [Header("Charge Settings")]
    public float chargeSpeed = 8f;
    public float maxChargeTime = 1.5f;
    
    [Header("Attack Settings")]
    public float attackDamage = 15f;
    public float attackRadius = 1.2f;
    public Vector3 attackOffset = new Vector3(0, 0.5f, 1.0f);
    public LayerMask targetLayer = ~0;

    private EntityMovement _movement;
    private Animator _animator;
    private Coroutine _chargeCoroutine;

    private void Awake()
    {
        _movement = GetComponent<EntityMovement>();
        _animator = GetComponentInChildren<Animator>();
    }

    public override void ExecuteSkill(GameObject targetObj, Vector3 targetPos)
    {
        if (IsExecuting) return;

        IsExecuting = true;
        
        // 1. 전달받은 목표 좌표(targetPos)를 향해 돌진 방향을 계산합니다.
        Vector3 dir = (targetPos - transform.position);
        dir.y = 0; // 평면 이동
        
        // 좌표가 세팅되지 않았거나 현재 위치와 똑같다면 그냥 앞을 보고 뜁니다.
        if (dir.sqrMagnitude < 0.01f)
        {
            dir = transform.forward;
        }
        else
        {
            dir.Normalize();
        }

        // 2. 애니메이션 실행 (스킬 전용 오버라이드)
        if (_animator != null)
        {
            _animator.Play("Attack", 0, 0f);
        }

        // 3. 코루틴으로 일정 시간 동안 직진 이동
        if (_chargeCoroutine != null) StopCoroutine(_chargeCoroutine);
        _chargeCoroutine = StartCoroutine(ChargeRoutine(dir));
    }

    private IEnumerator ChargeRoutine(Vector3 direction)
    {
        float timer = 0f;

        // 돌진 시작 전 즉시 방향 회전 (선택적)
        if (direction.sqrMagnitude > 0.001f)
        {
            transform.rotation = Quaternion.LookRotation(direction);
        }

        while (timer < maxChargeTime)
        {
            // Update 주기에 맞춰서 이동 호출 (EntityMovement 내부는 FixedUpdate로 동작)
            _movement.MoveInDirection(direction, chargeSpeed);
            
            timer += Time.deltaTime;
            yield return null;
        }

        // 돌진 완료
        _movement.Stop();
        IsExecuting = false;
    }

    /// <summary>
    /// Run 애니메이션 클립의 Animation Event에서 호출됩니다.
    /// 돌진 중 애니메이션 타이밍에 맞춰 물리 타격을 줍니다.
    /// </summary>
    public void PerformMeleeAttack()
    {
        if (!IsExecuting) return; // 돌진 중이 아닐 때는 무시

        Vector3 sphereCenter = transform.position + transform.rotation * attackOffset;
        Collider[] hitColliders = Physics.OverlapSphere(sphereCenter, attackRadius, targetLayer);

        foreach (var hitCollider in hitColliders)
        {
            if (hitCollider.gameObject == this.gameObject) continue;

            IDamageable damageable = hitCollider.GetComponent<IDamageable>();
            if (damageable != null)
            {
                Vector3 hitPoint = hitCollider.bounds.center;
                Vector3 hitNormal = (hitPoint - sphereCenter).normalized;

                DamageInfo damageInfo = new DamageInfo(attackDamage, this.gameObject, hitPoint, hitNormal);
                damageable.TakeDamage(damageInfo);
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1, 0, 0, 0.5f);
        Vector3 sphereCenter = transform.position + transform.rotation * attackOffset;
        Gizmos.DrawSphere(sphereCenter, attackRadius);
    }
}
