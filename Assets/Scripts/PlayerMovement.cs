using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;

/// <summary>
/// 쿼터뷰 캐릭터 이동 스크립트
/// Unity Input System을 통해 키보드(WASD/화살표), 게임패드(XInput), 안드로이드 가상 조이스틱을 지원합니다.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class PlayerMovement : NetworkBehaviour
{
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float rotationSpeed = 720f;
    
    [Header("Combat")]
    [SerializeField] private Shooter shooter;

    [Header("Animation")]
    [SerializeField] private Animator _animator;

    private Rigidbody _rb;
    private PlayerInputActions _inputActions;
    private Vector2 _keyboardInput;
    private Vector2 _joystickInput;
    private Vector2 _joystickRotationInput;
    public Vector2 JoystickRotationInput => _joystickRotationInput;
    private bool _isShooting = false;
    private SectorIndicator _sectorIndicator;

    private string _currentAnimState = "Standing@loop";
    
    // 네트워크 동기화를 위한 변수 (오너가 쓰고 모두가 읽음)
    private NetworkVariable<bool> _netIsRunning = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        // Awake에서는 InputSystem을 미리 생성해둡니다.
        _inputActions = new PlayerInputActions();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        
        Debug.Log($"[PlayerMovement] OnNetworkSpawn 호출됨! (OwnerClientId: {OwnerClientId}, IsServer: {IsServer}, IsOwner: {IsOwner})");

        GameObject spawnObj = GameObject.Find("PlayerSpawnPosition");
        if (spawnObj != null)
        {
            var nt = GetComponent<Unity.Netcode.Components.NetworkTransform>();
            Vector3 spawnPos = spawnObj.transform.position;
            
            // 플레이어들이 겹치지 않게 클라이언트 ID를 기반으로 약간의 X축 간격을 줍니다.
            spawnPos.x += (OwnerClientId * 1.5f);
            
            // NetworkTransform의 권한(서버 권한 or 클라이언트 권한)을 가진 쪽에서만 텔레포트를 실행합니다.
            bool hasAuthority = (nt != null) ? nt.CanCommitToTransform : IsServer;

            if (hasAuthority)
            {
                if (nt != null) 
                {
                    nt.Teleport(spawnPos, transform.rotation, transform.localScale);
                }
                else 
                {
                    transform.position = spawnPos;
                }
            }
        }
        
        // 내 캐릭터일 때만 입력을 활성화합니다.
        if (IsOwner)
        {
            _inputActions.Player.Enable();
            _inputActions.Player.Move.performed += OnMove;
            _inputActions.Player.Move.canceled += OnMove;
            
            // 내 캐릭터일 때만 인디케이터 생성
            GameObject indicatorObj = new GameObject("SectorIndicator");
            indicatorObj.transform.SetParent(transform);
            indicatorObj.transform.localPosition = new Vector3(0f, 0.1f, 0f);
            _sectorIndicator = indicatorObj.AddComponent<SectorIndicator>();

            // 동적으로 스폰된 로컬 플레이어에 카메라 연동
            var camFollow = FindAnyObjectByType<CameraFollow>();
            if (camFollow != null)
            {
                camFollow.SetTarget(this.transform);
            }

            // 동적으로 스폰된 로컬 플레이어에 UI 조이스틱 연동
            foreach (var joystick in VirtualJoystick.AllJoysticks) 
            {
                if (joystick != null)
                {
                    joystick.SetPlayer(this);
                }
            }
        }
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        if (IsOwner)
        {
            _inputActions.Player.Move.performed -= OnMove;
            _inputActions.Player.Move.canceled -= OnMove;
            _inputActions.Player.Disable();
        }
    }

    private void Update()
    {
        // 애니메이션은 모든 클라이언트에서 동일하게 재생되어야 하므로 IsOwner 체크 밖에서 처리
        if (_animator != null)
        {
            string targetAnim = _netIsRunning.Value ? "Running@loop" : "Standing@loop";
            if (_currentAnimState != targetAnim)
            {
                _animator.CrossFade(targetAnim, 0.1f);
                _currentAnimState = targetAnim;
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
        Transform closestObstacle = null;
        float minEnemyDistance = float.MaxValue;
        float minObstacleDistance = float.MaxValue;

        // 향후 최적화를 위해 Physics.OverlapSphereNonAlloc 또는 레이어 마스크를 사용할 수 있습니다.
        Collider[] colliders = Physics.OverlapSphere(_rb.position, radius);
        
        // 레이캐스트 시작 위치 (바닥 충돌 방지 및 허리춤 높이)
        Vector3 rayOrigin = _rb.position + Vector3.up * 0.5f;

        foreach (Collider col in colliders)
        {
            if (col.CompareTag("Enemy") || col.CompareTag("Obstacle"))
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
                        bool isBlocked = false;
                        
                        // 중심점을 향해 레이캐스트를 쏴서 시야(Line of Sight) 검사
                        Vector3 targetPos = col.bounds.center;
                        Vector3 rayDir = (targetPos - rayOrigin).normalized;
                        float rayDist = Vector3.Distance(rayOrigin, targetPos);

                        if (Physics.Raycast(rayOrigin, rayDir, out RaycastHit hit, rayDist))
                        {
                            // 맞은 대상이 타겟 본인이나 플레이어 자신이 아닐 경우
                            if (hit.collider != col && hit.collider.transform.root != this.transform.root)
                            {
                                // GetComponentInParent를 사용하면 맞은 콜라이더의 부모를 거슬러 올라가며 컴포넌트를 찾습니다.
                                // IDamageable(체력)이 있다면 적이거나 파괴 가능한 엄폐물이므로 타겟팅 허용!
                                if (hit.collider.GetComponentInParent<IDamageable>() != null)
                                {
                                    isBlocked = false;
                                }
                                else
                                {
                                    // 데미지를 입지 않는 무적 벽이나 지형에 가려진 경우
                                    isBlocked = true;
                                }
                            }
                        }

                        if (!isBlocked)
                        {
                            if (col.CompareTag("Enemy"))
                            {
                                if (distance < minEnemyDistance)
                                {
                                    minEnemyDistance = distance;
                                    closestEnemy = col.transform;
                                }
                            }
                            else if (col.CompareTag("Obstacle"))
                            {
                                if (distance < minObstacleDistance)
                                {
                                    minObstacleDistance = distance;
                                    closestObstacle = col.transform;
                                }
                            }
                        }
                    }
                }
            }
        }

        Debug.Log($"[PlayerMovement] 가장 가까운 적: {closestEnemy}");
        Debug.Log($"[PlayerMovement] 가장 가까운 장애물: {closestObstacle}");

        return closestEnemy != null ? closestEnemy : closestObstacle;
    }

    private void FixedUpdate()
    {
        if (!IsOwner) return; // 위치 이동과 물리 연산, 입력 처리는 로컬 오너만 수행합니다.

        // 1. 이동 처리
        // 키보드 입력과 조이스틱 입력을 합산한 후, 최대 크기가 1을 넘지 않도록 제한합니다.
        Vector2 combinedInput = _keyboardInput + _joystickInput;
        if (combinedInput.sqrMagnitude > 1f)
        {
            combinedInput.Normalize();
        }

        // 애니메이션 동기화 변수 업데이트
        _netIsRunning.Value = combinedInput.magnitude > 0.1f;

        Vector3 moveDir = new Vector3(combinedInput.x, 0f, combinedInput.y);
        _rb.MovePosition(_rb.position + moveDir * (moveSpeed * Time.fixedDeltaTime));

        // 2. 회전 처리
        Vector3 lookDir = Vector3.zero;

        bool isReloading = PlayerSkillController.Instance != null && PlayerSkillController.Instance.IsReloading();

        // 우측 회전 조이스틱 입력이 있는 경우 우선 처리 (발사 방향), 단 장전 중이 아닐 때만
        if (_joystickRotationInput.sqrMagnitude > 0.001f && !isReloading)
        {
            Vector3 inputDir = new Vector3(_joystickRotationInput.x, 0f, _joystickRotationInput.y).normalized;
            float inputMag = Mathf.Clamp01(_joystickRotationInput.magnitude);

            // GunData에서 탐색 범위와 각도를 가져옵니다. 총이 없으면 기본값을 사용합니다.
            float searchRadius = 15f;
            float searchAngle = 90f;
            int shotType = 1;
            float explosionRadius = 5f;
            if (shooter != null && shooter.CurrentGunData != null) {
                searchRadius = shooter.CurrentGunData.SearchRadius;
                searchAngle = shooter.CurrentGunData.SearchAngle;
                shotType = shooter.CurrentGunData.shotType;
                explosionRadius = shooter.CurrentGunData.ExplosionRadius;
            }

            if (shotType == 1 || shotType == 0) // 직사형 (기본값)
            {
                float scaledSearchRadius = searchRadius * inputMag;

                // 인디케이터 그리기
                if (_sectorIndicator != null)
                {
                    _sectorIndicator.transform.localPosition = new Vector3(0f, 0.1f, 0f);
                    _sectorIndicator.DrawSector(scaledSearchRadius, searchAngle, inputDir);
                }

                Transform targetEnemy = FindClosestEnemyInSector(inputDir, scaledSearchRadius, searchAngle);

                if (targetEnemy != null)
                {
                    // 부채꼴 내에 적이 있으면 적을 향해 바라보고 사격
                    Vector3 dirToEnemy = targetEnemy.position - _rb.position;
                    
                    // 캐릭터의 회전(lookDir)은 상하로 꺾이지 않도록 수평만 적용
                    Vector3 flatDir = dirToEnemy;
                    flatDir.y = 0f;
                    lookDir = flatDir.normalized;

                    // 실제 총알이 날아갈 방향은 높이차까지 포함한 3D 방향
                    Vector3 aimDir = dirToEnemy.normalized;

                    if (shooter != null)
                    {
                        if (!_isShooting)
                        {
                            _isShooting = true;
                            shooter.SetTrigger(true, aimDir);
                        }
                        else
                        {
                            shooter.UpdateAimDirection(aimDir);
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
            else if (shotType == 2) // 포물선형
            {
                // 포물선 조준: 플레이어 중심에서 스틱 방향으로 (스틱 입력량 * 최대 사거리)만큼 떨어진 위치
                Vector3 targetPosition = _rb.position + (inputDir * searchRadius * inputMag);
                
                // 지형의 높낮이를 고려하여 위에서 아래로 Raycast를 쏘아 바닥의 실제 Y좌표를 찾습니다.
                Vector3 rayOrigin = targetPosition;
                rayOrigin.y += 10f; // 플레이어 위치보다 충분히 높은 곳에서 아래로 탐색
                
                if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 20f))
                {
                    targetPosition.y = hit.point.y;
                }
                else
                {
                    // 바닥을 찾지 못했다면(예: 허공) 플레이어의 발밑 높이로 대체
                    targetPosition.y = _rb.position.y;
                }

                if (_sectorIndicator != null)
                {
                    Vector3 localPos = transform.InverseTransformPoint(targetPosition);
                    localPos.y += 0.1f; // 바닥 높이보다 살짝 띄워서 인디케이터 표시
                    _sectorIndicator.transform.localPosition = localPos;
                    _sectorIndicator.DrawCircle(explosionRadius);
                }

                lookDir = inputDir; // 캐릭터는 입력 방향을 바라봄

                if (shooter != null)
                {
                    if (!_isShooting)
                    {
                        _isShooting = true;
                        shooter.SetTrigger(true, lookDir, targetPosition);
                    }
                    else
                    {
                        shooter.UpdateAimDirection(lookDir, targetPosition);
                    }
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
