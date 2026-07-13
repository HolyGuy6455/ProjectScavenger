using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 쿼터뷰 캐릭터 이동 스크립트
/// Unity Input System을 통해 키보드(WASD/화살표), 게임패드(XInput), 안드로이드 가상 조이스틱을 지원합니다.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class PlayerMovement : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float rotationSpeed = 720f;
    
    [Header("Combat")]
    [SerializeField] private Shooter shooter;

    private Rigidbody _rb;
    private PlayerInputActions _inputActions;
    private Vector2 _keyboardInput;
    private Vector2 _joystickInput;
    private Vector2 _joystickRotationInput;
    public Vector2 JoystickRotationInput => _joystickRotationInput;
    private bool _isShooting = false;
    private SectorIndicator _sectorIndicator;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _inputActions = new PlayerInputActions();
    }

    private void OnEnable()
    {
        _inputActions.Player.Enable();
        _inputActions.Player.Move.performed += OnMove;
        _inputActions.Player.Move.canceled += OnMove;
    }

    private void OnDisable()
    {
        _inputActions.Player.Move.performed -= OnMove;
        _inputActions.Player.Move.canceled -= OnMove;
        _inputActions.Player.Disable();
    }

    private void Start()
    {
        // 0. 부채꼴 인디케이터 동적 생성 (바닥과 겹치지 않게 살짝 위인 Y=0.1에 배치)
        GameObject indicatorObj = new GameObject("SectorIndicator");
        indicatorObj.transform.SetParent(transform);
        indicatorObj.transform.localPosition = new Vector3(0f, 0.1f, 0f);
        _sectorIndicator = indicatorObj.AddComponent<SectorIndicator>();

        // 임시 테스트 코드: 시작할 때 DataManager에서 gun_002 데이터를 가져와 장착합니다.
        if (shooter != null)
        {
            GunData startingGun = DataManager.Instance.GetData<GunData>("Gun", 1);
            if (startingGun != null)
            {
                shooter.EquipGun(startingGun);
                Debug.Log($"[{gameObject.name}] {startingGun.gunName} 장착 완료!");
            }
            else
            {
                Debug.LogWarning("DataManager에서 gun_002 데이터를 찾을 수 없습니다.");
            }
        }
    }

    private void OnMove(InputAction.CallbackContext ctx)
    {
        _keyboardInput = ctx.ReadValue<Vector2>();
    }

    /// <summary>
    /// 안드로이드 가상 조이스틱 등 외부에서 입력값을 주입할 때 사용합니다.
    /// </summary>
    public void SetMoveInput(Vector2 input)
    {
        _joystickInput = input;
    }

    /// <summary>
    /// 안드로이드 가상 회전 조이스틱 등 외부에서 회전값을 주입할 때 사용합니다.
    /// </summary>
    public void SetRotationInput(Vector2 input)
    {
        _joystickRotationInput = input;
    }

    private Transform FindClosestEnemyInSector(Vector3 forwardDir, float radius, float angle)
    {
        Transform closestEnemy = null;
        float minDistance = float.MaxValue;

        // 향후 최적화를 위해 Physics.OverlapSphereNonAlloc 또는 레이어 마스크를 사용할 수 있습니다.
        Collider[] colliders = Physics.OverlapSphere(_rb.position, radius);
        
        foreach (Collider col in colliders)
        {
            if (col.CompareTag("Enemy"))
            {
                Vector3 dirToTarget = col.transform.position - _rb.position;
                dirToTarget.y = 0f;
                float distance = dirToTarget.magnitude;

                if (distance > 0.001f)
                {
                    Vector3 dirNormalized = dirToTarget / distance;
                    float angleToTarget = Vector3.Angle(forwardDir, dirNormalized);

                    // angle은 전체 부채꼴 각도이므로 절반 각도와 비교합니다.
                    if (angleToTarget <= angle * 0.5f)
                    {
                        if (distance < minDistance)
                        {
                            minDistance = distance;
                            closestEnemy = col.transform;
                        }
                    }
                }
            }
        }

        return closestEnemy;
    }

    private void FixedUpdate()
    {
        // 1. 이동 처리
        // 키보드 입력과 조이스틱 입력을 합산한 후, 최대 크기가 1을 넘지 않도록 제한합니다.
        Vector2 combinedInput = _keyboardInput + _joystickInput;
        if (combinedInput.sqrMagnitude > 1f)
        {
            combinedInput.Normalize();
        }

        Vector3 moveDir = new Vector3(combinedInput.x, 0f, combinedInput.y);
        _rb.MovePosition(_rb.position + moveDir * (moveSpeed * Time.fixedDeltaTime));

        // 2. 회전 처리
        Vector3 lookDir = Vector3.zero;

        // 우측 회전 조이스틱 입력이 있는 경우 우선 처리 (발사 방향)
        if (_joystickRotationInput.sqrMagnitude > 0.001f)
        {
            Vector3 inputDir = new Vector3(_joystickRotationInput.x, 0f, _joystickRotationInput.y).normalized;

            // GunData에서 탐색 범위와 각도를 가져옵니다. 총이 없으면 기본값을 사용합니다.
            float searchRadius = shooter != null && shooter.CurrentGunData != null ? shooter.CurrentGunData.SearchRadius : 15f;
            float searchAngle = shooter != null && shooter.CurrentGunData != null ? shooter.CurrentGunData.SearchAngle : 90f;

            // 인디케이터 그리기
            if (_sectorIndicator != null)
            {
                _sectorIndicator.DrawSector(searchRadius, searchAngle, inputDir);
            }

            Transform targetEnemy = FindClosestEnemyInSector(inputDir, searchRadius, searchAngle);

            if (targetEnemy != null)
            {
                // 부채꼴 내에 적이 있으면 적을 향해 바라보고 사격
                Vector3 dirToEnemy = targetEnemy.position - _rb.position;
                dirToEnemy.y = 0f;
                lookDir = dirToEnemy.normalized;

                if (shooter != null)
                {
                    if (!_isShooting)
                    {
                        _isShooting = true;
                        shooter.SetTrigger(true, lookDir);
                    }
                    else
                    {
                        shooter.UpdateAimDirection(lookDir);
                    }
                }
            }
            else
            {
                // 부채꼴 내에 적이 없으면 조이스틱 방향만 바라보고 사격 중지
                lookDir = inputDir;

                if (shooter != null && _isShooting)
                {
                    _isShooting = false;
                    shooter.SetTrigger(false);
                }
            }
        }
        else
        {
            // 우측 조이스틱 입력을 놓았을 때 인디케이터 숨기기
            if (_sectorIndicator != null)
            {
                _sectorIndicator.Hide();
            }

            // 우측 조이스틱 입력을 놓았을 때 총기 발사 중지
            if (shooter != null && _isShooting)
            {
                _isShooting = false;
                shooter.SetTrigger(false);
            }

            // 우측 조이스틱 입력은 없지만 이동 입력이 있는 경우 이동 방향으로 회전
            if (combinedInput.sqrMagnitude > 0.001f)
            {
                lookDir = new Vector3(combinedInput.x, 0f, combinedInput.y);
            }
        }

        if (lookDir.sqrMagnitude > 0.001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(lookDir, Vector3.up);
            _rb.MoveRotation(Quaternion.RotateTowards(_rb.rotation, targetRotation, rotationSpeed * Time.fixedDeltaTime));
        }
    }
}
