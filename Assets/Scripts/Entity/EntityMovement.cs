using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

[RequireComponent(typeof(Rigidbody))]
public class EntityMovement : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 4f;
    [SerializeField] private float rotationSpeed = 720f;
    [SerializeField] private float stoppingDistance = 0.1f;
    [SerializeField]
    [Tooltip("활성화하면 이동 방향을 바라봅니다. 비활성화하면 SetLookAtPosition으로 지정한 월드 위치를 바라봅니다.")]
    private bool syncMovementAndFacing = true;

    private Rigidbody _rb;
    private Vector3 _targetPosition;
    private bool _isMoving = false;
    
    // 길찾기용 변수
    private Queue<Vector3> _pathCorners = new Queue<Vector3>();
    private bool _isSmartMoving = false;

    // 방향 기반 이동 (스킬용)
    private bool _isMovingInDirection = false;
    private Vector3 _moveDirection;
    private float _customSpeed;

    private Vector3 _lookAtPosition;
    private bool _hasLookAtPosition;

    /// <summary>이동 방향을 바라볼지, 별도 위치를 바라볼지 설정합니다.</summary>
    public bool SyncMovementAndFacing
    {
        get => syncMovementAndFacing;
        set => syncMovementAndFacing = value;
    }

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        // Rigidbody 설정 (물리 충돌 등 원치 않는 회전 방지)
        _rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
    }

    /// <summary>별도 시선 대상을 해제합니다. 동기화가 꺼져 있으면 현재 방향을 유지합니다.</summary>
    public void ClearLookAtPosition()
    {
        _hasLookAtPosition = false;
    }


    /// <summary>
    /// 외부(AI 등)에서 목표 좌표를 지정해 장애물을 무시하고 직진합니다.
    /// </summary>
    public void DumbMoveTo(Vector3 targetPos)
    {
        _targetPosition = targetPos;
        _isMoving = true;
        _isSmartMoving = false;
        _isMovingInDirection = false;
    }

    /// <summary>
    /// NavMesh를 이용해 길을 찾은 뒤, 장애물을 피해 이동합니다.
    /// </summary>
    public void SmartMoveTo(Vector3 targetPos)
    {
        NavMeshPath path = new NavMeshPath();
        if (NavMesh.CalculatePath(_rb.position, targetPos, NavMesh.AllAreas, path))
        {
            _pathCorners.Clear();
            // 0번 인덱스는 현재 위치이므로 1번부터 큐에 넣습니다.
            for (int i = 1; i < path.corners.Length; i++)
            {
                _pathCorners.Enqueue(path.corners[i]);
            }
            
            _isMoving = true;
            _isSmartMoving = true;
            _isMovingInDirection = false;
            
            // 첫 번째 목적지 설정
            if (_pathCorners.Count > 0)
            {
                _targetPosition = _pathCorners.Dequeue();
            }
        }
        else
        {
            // 길을 찾을 수 없으면 일단 멍청한 이동으로 대체
            DumbMoveTo(targetPos);
        }
    }

    /// <summary>
    /// 목표물이 아닌 특정 방향으로 지정된 속도로 이동시킵니다. (돌진 스킬 등에 사용)
    /// </summary>
    public void MoveInDirection(Vector3 direction, float speed)
    {
        _moveDirection = direction.normalized;
        _customSpeed = speed;
        _isMovingInDirection = true;
        _isMoving = false;
    }

    /// <summary>
    /// 이동을 멈춥니다.
    /// </summary>
    public void Stop()
    {
        _isMoving = false;
        _isSmartMoving = false;
        _pathCorners.Clear();
        _isMovingInDirection = false;
        _rb.linearVelocity = Vector3.zero;
        _rb.angularVelocity = Vector3.zero;
    }

    /// <summary>
    /// 별도로 바라볼 월드 위치를 설정합니다.
    /// SyncMovementAndFacing이 false일 때 이동 중이나 정지 중에도 적용됩니다.
    /// </summary>
    public void SetLookAtPosition(Vector3 worldPosition)
    {
        _lookAtPosition = worldPosition;
        _hasLookAtPosition = true;
    }


    private void FixedUpdate()
    {
        Vector3 movementDirection = Vector3.zero;

        if (_isMovingInDirection)
        {
            movementDirection = _moveDirection;
            movementDirection.y = 0f;
            _rb.MovePosition(_rb.position + movementDirection * (_customSpeed * Time.fixedDeltaTime));
        }
        else if (_isMoving)
        {
            Vector3 dir = _targetPosition - _rb.position;
            dir.y = 0f;

            if (dir.magnitude <= stoppingDistance)
            {
                if (_isSmartMoving && _pathCorners.Count > 0)
                {
                    _targetPosition = _pathCorners.Dequeue();
                }
                else
                {
                    Stop();
                }
            }
            else
            {
                movementDirection = dir.normalized;
                _rb.MovePosition(_rb.position + movementDirection * (moveSpeed * Time.fixedDeltaTime));
            }
        }

        UpdateFacing(movementDirection);
    }


    private void UpdateFacing(Vector3 movementDirection)
    {
        Vector3 facingDirection;
        if (syncMovementAndFacing)
        {
            facingDirection = movementDirection;
        }
        else if (_hasLookAtPosition)
        {
            facingDirection = _lookAtPosition - _rb.position;
        }
        else
        {
            return;
        }

        facingDirection.y = 0f;
        if (facingDirection.sqrMagnitude <= 0.001f) return;

        Quaternion targetRotation = Quaternion.LookRotation(facingDirection);
        _rb.rotation = Quaternion.RotateTowards(
            _rb.rotation, targetRotation, rotationSpeed * Time.fixedDeltaTime);
    }
}
