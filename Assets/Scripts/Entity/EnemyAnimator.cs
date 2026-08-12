using UnityEngine;

/// <summary>
/// 몬스터의 이동 속도에 따라 자동으로 걷기/대기 애니메이션을 전환해주는 스크립트입니다.
/// 행동 트리(BT)와 애니메이션(연출)을 분리하기 위해 사용됩니다.
/// </summary>
public class EnemyAnimator : MonoBehaviour
{
    private Rigidbody _rb;
    private Animator _animator;
    private EnemySkill[] _skills;

    // 이전 프레임의 상태를 기억하여 Play가 매 프레임 불리는 것을 방지
    private bool _wasMoving = false;

    private void Awake()
    {
        _rb = GetComponentInParent<Rigidbody>();
        _animator = GetComponentInChildren<Animator>();
        _skills = GetComponentsInParent<EnemySkill>();
    }

    private void Update()
    {
        if (_animator == null || _rb == null) return;

        // 1. 현재 스킬(돌진 등)을 사용 중이라면 애니메이션 제어 권한을 스킬에게 넘김
        foreach (var skill in _skills)
        {
            if (skill.IsExecuting)
            {
                // 스킬 실행 중에는 여기서 기본 이동 애니메이션 처리를 하지 않음
                return;
            }
        }

        // 2. 평상시 이동 감지 (y축 속도 제외)
        Vector3 flatVelocity = new Vector3(_rb.linearVelocity.x, 0, _rb.linearVelocity.z);
        bool isMoving = flatVelocity.sqrMagnitude > 0.1f;

        // 상태가 변했을 때만 애니메이션 전환 (Animator 파라미터를 쓰지 않고 직접 State Name을 호출하는 직관적 방식)
        if (isMoving != _wasMoving)
        {
            if (isMoving)
            {
                _animator.Play("Run", 0); // 걸을 때 재생할 상태 이름
            }
            else
            {
                _animator.Play("Idle", 0); // 대기 시 재생할 상태 이름
            }
            _wasMoving = isMoving;
        }
    }
}
