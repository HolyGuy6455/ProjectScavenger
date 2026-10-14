using UnityEngine;
using Unity.Netcode;

/// <summary>
/// 실제 위치 변화를 관찰하여 이동 방향과 걷기/대기 애니메이션을 갱신합니다.
/// 행동 트리(BT)와 애니메이션(연출)을 분리하기 위해 사용됩니다.
/// </summary>
public class EnemyAnimator : NetworkBehaviour, IShooterAnimationReceiver
{
    [Header("Movement Animation")]
    [SerializeField] private string movementStateName = "Run";
    [Tooltip("이동을 관찰할 루트. 비워두면 부모 Rigidbody의 Transform을 사용합니다.")]
    [SerializeField] private Transform movementRoot;
    [Tooltip("정면(+Z)의 기준. 비워두면 이동 루트를 사용합니다.")]
    [SerializeField] private Transform facingReference;
    [SerializeField, Min(0.001f)] private float movementSpeedThreshold = 0.1f;
    [SerializeField, Min(0f)] private float movementStopDelay = 0.1f;
    [Tooltip("한 프레임에 이 거리 이상 이동하면 순간이동으로 간주합니다.")]
    [SerializeField, Min(0.001f)] private float teleportDistanceThreshold = 5f;

    [Header("Shooting Animations")]
    [SerializeField] private string shootStateName = "";
    [SerializeField, Min(0)] private int shootLayer = 1;
    [SerializeField] private string shootEmptyStateName = "";
    [SerializeField] private string reloadStateName = "";
    [SerializeField, Min(0)] private int reloadLayer = 0;

    private static readonly int MoveXHash = Animator.StringToHash("MoveX");
    private static readonly int MoveZHash = Animator.StringToHash("MoveZ");

    private Animator _animator;
    private EnemySkill[] _skills;
    private RuntimeAnimatorController _parameterController;
    private bool _hasMoveX;
    private bool _hasMoveZ;
    private Vector3 _previousPosition;
    private Vector3 _lastWorldDirection;
    private float _timeSinceMovement;
    private bool _isMoving;
    private bool _wasSkillExecuting;
    private bool _isSkillAnimationPlaying;
    private bool _isDead;

    // 이전 프레임의 상태를 기억하여 Play가 매 프레임 불리는 것을 방지
    private bool _wasMoving = false;

    private void Awake()
    {
        if (movementRoot == null)
        {
            var body = GetComponentInParent<Rigidbody>();
            movementRoot = body != null ? body.transform : transform;
        }
        if (facingReference == null) facingReference = movementRoot;

        _animator = GetComponentInChildren<Animator>();
        _skills = GetComponentsInParent<EnemySkill>();
        CacheMovementParameters();
        ResetMovementTracking();
    }

    private void OnEnable()
    {
        ResetMovementTracking();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        _isDead = false;
        _isSkillAnimationPlaying = false;
        ResetMovementTracking();
    }

    /// <summary>외부에서 위치를 재설정한 직후 호출하면 해당 변위를 걷기로 처리하지 않습니다.</summary>
    public void ResetMovementTracking()
    {
        _previousPosition = movementRoot != null ? movementRoot.position : transform.position;
        _lastWorldDirection = Vector3.zero;
        _timeSinceMovement = movementStopDelay;
        _isMoving = false;
    }

    private void LateUpdate()
    {
        TickMovement(Time.deltaTime);
    }

    private void TickMovement(float deltaTime)
    {
        if (_isDead || _animator == null || movementRoot == null) return;

        // NetworkTransform으로 이동하는 원격 개체도 같은 방식으로 관찰합니다.
        Vector3 displacement = movementRoot.position - _previousPosition;
        _previousPosition = movementRoot.position;
        displacement.y = 0f;
        if (deltaTime <= 0f) return;

        if (displacement.sqrMagnitude >= teleportDistanceThreshold * teleportDistanceThreshold)
        {
            ResetMovementTracking();
        }
        else if (displacement.magnitude / deltaTime > movementSpeedThreshold)
        {
            _lastWorldDirection = displacement.normalized;
            _timeSinceMovement = 0f;
            _isMoving = true;
        }
        else
        {
            // 물리/네트워크 갱신 사이에 변위가 없는 프레임도 짧게 이어서 재생합니다.
            _timeSinceMovement += deltaTime;
            _isMoving = _isMoving && _timeSinceMovement < movementStopDelay;
        }

        UpdateMovementParameters();

        // 원격 클라이언트에서도 전신 스킬이 끝날 때까지 이동 상태 전환을 기다립니다.
        if (_isSkillAnimationPlaying) return;

        // 스킬 중에도 위치 이력을 갱신해야 종료 후 누적 변위를 이동으로 오인하지 않습니다.
        foreach (var skill in _skills)
        {
            if (skill != null && skill.IsExecuting)
            {
                _wasSkillExecuting = true;
                return;
            }
        }

        // 방향만 바뀔 때는 Blend Tree 파라미터만 갱신하여 재생 시간을 유지합니다.
        if (_isMoving != _wasMoving || _wasSkillExecuting)
        {
            PlayConfiguredAnimation(_isMoving ? movementStateName : "Idle", 0);
            _wasMoving = _isMoving;
        }
        _wasSkillExecuting = false;
    }

    private void CacheMovementParameters()
    {
        _hasMoveX = false;
        _hasMoveZ = false;
        _parameterController = _animator != null ? _animator.runtimeAnimatorController : null;
        if (_parameterController == null) return;

        foreach (var parameter in _animator.parameters)
        {
            if (parameter.type != AnimatorControllerParameterType.Float) continue;
            if (parameter.nameHash == MoveXHash) _hasMoveX = true;
            if (parameter.nameHash == MoveZHash) _hasMoveZ = true;
        }
    }

    private void UpdateMovementParameters()
    {
        if (_parameterController != _animator.runtimeAnimatorController) CacheMovementParameters();
        if ((!_hasMoveX && !_hasMoveZ) || _lastWorldDirection == Vector3.zero) return;

        Vector3 forward = facingReference.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f) return;
        forward.Normalize();
        Vector3 right = Vector3.Cross(Vector3.up, forward);

        // 이동 속도와 무관한 단위 방향. 정지할 때도 마지막 방향을 유지합니다.
        if (_hasMoveX) _animator.SetFloat(MoveXHash, Vector3.Dot(_lastWorldDirection, right));
        if (_hasMoveZ) _animator.SetFloat(MoveZHash, Vector3.Dot(_lastWorldDirection, forward));
    }

    public void PlayShootAnimation()
    {
        if (_isDead || _isSkillAnimationPlaying || (IsSpawned && !IsServer)) return;

        PlayConfiguredAnimation(shootStateName, shootLayer);
        if (IsSpawned) PlayShootAnimationClientRpc();
    }

    [ClientRpc]
    private void PlayShootAnimationClientRpc()
    {
        if (IsServer || _isSkillAnimationPlaying) return;
        PlayConfiguredAnimation(shootStateName, shootLayer);
    }

    public bool PlaySkillAnimation(string stateName)
    {
        if (_isDead || (IsSpawned && !IsServer)) return false;
        if (!HasConfiguredAnimation(stateName, 0)) return false;

        PlaySkillAnimationLocal(stateName);
        if (IsSpawned) PlaySkillAnimationClientRpc(stateName);
        return true;
    }

    [ClientRpc]
    private void PlaySkillAnimationClientRpc(string stateName)
    {
        if (IsServer) return;
        PlaySkillAnimationLocal(stateName);
    }

    private void PlaySkillAnimationLocal(string stateName)
    {
        if (_isDead) return;

        _isSkillAnimationPlaying = true;
        _wasSkillExecuting = true;
        // 일반 사격의 상체 레이어가 전신 스킬의 총/팔 동작을 덮지 않도록 비웁니다.
        PlayConfiguredAnimation(shootEmptyStateName, shootLayer);
        PlayConfiguredAnimation(stateName, 0);
    }

    public void EndSkillAnimation()
    {
        if (IsSpawned && !IsServer) return;

        EndSkillAnimationLocal();
        if (IsSpawned) EndSkillAnimationClientRpc();
    }

    [ClientRpc]
    private void EndSkillAnimationClientRpc()
    {
        if (IsServer) return;
        EndSkillAnimationLocal();
    }

    private void EndSkillAnimationLocal()
    {
        if (!_isSkillAnimationPlaying) return;

        _isSkillAnimationPlaying = false;
        // 다음 AI 행동(재장전 등)을 뒤늦은 이동 상태 복귀가 덮지 않도록 즉시 복귀합니다.
        PlayConfiguredAnimation(_isMoving ? movementStateName : "Idle", 0);
        _wasMoving = _isMoving;
        _wasSkillExecuting = false;
    }

    public void PlayReloadAnimation()
    {
        if (_isDead || (IsSpawned && !IsServer)) return;

        PlayReloadAnimationLocal();
        if (IsSpawned) PlayReloadAnimationClientRpc();
    }

    [ClientRpc]
    private void PlayReloadAnimationClientRpc()
    {
        if (IsServer) return;
        PlayReloadAnimationLocal();
    }

    private void PlayReloadAnimationLocal()
    {
        if (!HasConfiguredAnimation(reloadStateName, reloadLayer)) return;

        // 재장전은 전신 동작이므로 진행 중인 상체 사격을 먼저 끝냅니다.
        PlayConfiguredAnimation(shootEmptyStateName, shootLayer);
        PlayConfiguredAnimation(reloadStateName, reloadLayer);
    }

    /// <summary>Health의 사망 이벤트가 각 클라이언트에서 호출합니다. 이후 일반 애니메이션 전환을 막습니다.</summary>
    public bool PlayDeathAnimation(string stateName)
    {
        if (_isDead) return true;

        PlayConfiguredAnimation(shootEmptyStateName, shootLayer);
        _isDead = true;
        _isSkillAnimationPlaying = false;

        if (!HasConfiguredAnimation(stateName, 0)) return false;
        _animator.Play(stateName, 0, 0f);
        return true;
    }

    private void PlayConfiguredAnimation(string stateName, int layer)
    {
        // 사망 뒤 도착한 사격/스킬 종료 RPC도 사망 모션을 덮지 못하게 합니다.
        if (!_isDead && HasConfiguredAnimation(stateName, layer))
        {
            _animator.Play(stateName, layer, 0f);
        }
    }

    private bool HasConfiguredAnimation(string stateName, int layer)
    {
        return _animator != null && !string.IsNullOrEmpty(stateName)
            && layer >= 0 && layer < _animator.layerCount
            && _animator.HasState(layer, Animator.StringToHash(stateName));
    }
}
