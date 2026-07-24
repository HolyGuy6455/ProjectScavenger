using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class EntityMovement : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 4f;
    [SerializeField] private float rotationSpeed = 720f;
    [SerializeField] private float stoppingDistance = 0.1f;

    private Rigidbody _rb;
    private Vector3 _targetPosition;
    private bool _isMoving = false;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        // Rigidbody 설정 (물리 충돌 등 원치 않는 회전 방지)
        _rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
    }

    /// <summary>
    /// 외부(AI 등)에서 목표 좌표를 지정해 이동시킵니다.
    /// </summary>
    public void MoveTo(Vector3 targetPos)
    {
        _targetPosition = targetPos;
        _isMoving = true;
    }

    /// <summary>
    /// 이동을 멈춥니다.
    /// </summary>
    public void Stop()
    {
        _isMoving = false;
        _rb.linearVelocity = Vector3.zero;
        _rb.angularVelocity = Vector3.zero;
    }

    private void FixedUpdate()
    {
        if (!_isMoving) return;

        Vector3 dir = _targetPosition - _rb.position;
        dir.y = 0f; // y축(높이) 이동은 무시하고 평면 이동만 처리

        float dist = dir.magnitude;
        if (dist <= stoppingDistance)
        {
            Stop();
            return;
        }

        Vector3 moveDir = dir.normalized;
        
        // 1. 이동
        _rb.MovePosition(_rb.position + moveDir * (moveSpeed * Time.fixedDeltaTime));

        // 2. 회전
        if (moveDir.sqrMagnitude > 0.001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(moveDir);
            _rb.rotation = Quaternion.RotateTowards(_rb.rotation, targetRotation, rotationSpeed * Time.fixedDeltaTime);
        }
    }
}
