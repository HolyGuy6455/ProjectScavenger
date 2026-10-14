using System.Collections.Generic;
using Cinemachine;
using Unity.Netcode;
using UnityEngine;

/// <summary>지역 트리거를 기다렸다가 모든 플레이어에게 보스 등장 연출을 보여줍니다.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(EnemyAnimator), typeof(Health), typeof(CharacterCinematicController))]
public class BossIntroController : NetworkBehaviour
{
    [SerializeField] private string introStateName = "Base Layer.Boss_Intro";
    [Tooltip("일반 플레이어 연출 카메라보다 높은 우선순위를 사용합니다.")]
    [SerializeField] private int cameraPriority = 100;
    [Tooltip("애니메이터가 멈추거나 잘못 설정됐을 때 조작 잠금이 남지 않도록 하는 제한 시간입니다.")]
    [SerializeField, Min(1f)] private float maxIntroDuration = 30f;

    private AIBrain _brain;
    private EntityMovement _movement;
    private EnemyAnimator _enemyAnimator;
    private Animator _animator;
    private Health _health;
    private CinemachineVirtualCamera _camera;
    private MenuToggleController _menu;
    private readonly List<PlayerController> _lockedPlayers = new List<PlayerController>();
    private bool _brainWasEnabled;
    private bool _movementWasEnabled;
    private bool _holdingBoss;
    private bool _started;
    private bool _finished;
    private bool _showingPresentation;
    private int _previousCameraPriority;
    private float _startedAt;
    private Transform _cameraFollowBeforeReturn;
    private Transform _cameraReturnTarget;

    private void Awake()
    {
        _brain = GetComponent<AIBrain>();
        _movement = GetComponent<EntityMovement>();
        _enemyAnimator = GetComponent<EnemyAnimator>();
        _animator = GetComponentInChildren<Animator>();
        _health = GetComponent<Health>();
        _camera = GetComponent<CharacterCinematicController>().CinematicCamera;
    }

    public override void OnNetworkSpawn()
    {
        if (!enabled) return;

        _started = false;
        _finished = false;
        _health.OnDeath += OnBossDeath;
        HoldBoss();
    }

    private void HoldBoss()
    {
        _holdingBoss = true;
        if (_brain != null)
        {
            _brainWasEnabled = _brain.enabled;
            _brain.enabled = false;
        }
        if (_movement != null)
        {
            _movementWasEnabled = _movement.enabled;
            var body = GetComponent<Rigidbody>();
            if (IsServer && body != null && !body.isKinematic) _movement.Stop();
            _movement.enabled = false;
        }
        if (IsServer)
        {
            var shooter = GetComponent<Shooter>();
            if (shooter != null) shooter.SetTrigger(false);
        }
    }

    /// <summary>서버의 지역 트리거가 호출합니다. 같은 보스는 한 번만 시작할 수 있습니다.</summary>
    public bool TryBeginIntro()
    {
        if (!isActiveAndEnabled || !IsSpawned || !IsServer || _started || _finished || _health.IsDead)
            return false;

        if (_camera == null || _animator == null || !_enemyAnimator.PlaySkillAnimation(introStateName))
        {
            Debug.LogWarning($"[BossIntroController] 인트로 카메라/애니메이션 설정을 확인하세요: {introStateName}", this);
            FinishIntro();
            return false;
        }

        _started = true;
        _startedAt = Time.unscaledTime;

        // 서버에서도 잠금 상태를 보관하여 이미 전송된 스킬/교체 요청을 막습니다.
        foreach (var client in NetworkManager.ConnectedClientsList)
        {
            if (client.PlayerObject != null)
                LockPlayer(client.PlayerObject.GetComponent<PlayerController>());
        }
        BeginIntroClientRpc();
        return true;
    }

    private void Update()
    {
        if (!IsSpawned || !IsServer || !_started || _finished) return;

        // 클립 길이를 별도로 적지 않아도 편집된 애니메이션의 마지막까지 기다립니다.
        var state = _animator.GetCurrentAnimatorStateInfo(0);
        if (state.IsName(introStateName) && state.normalizedTime >= 1f)
        {
            FinishIntro();
        }
        else if (Time.unscaledTime - _startedAt >= maxIntroDuration)
        {
            Debug.LogWarning("[BossIntroController] 인트로 제한 시간을 초과하여 전투로 전환합니다.", this);
            FinishIntro();
        }
    }

    private void LateUpdate()
    {
        // Cinemachine은 복귀 블렌드 중에도 출발 카메라를 갱신합니다.
        // 화면에 더 이상 사용되지 않을 때 원래 CameraMount 연결을 복구합니다.
        if (_cameraReturnTarget != null && (_camera == null || !CinemachineCore.Instance.IsLive(_camera)))
            ReleaseReturnCamera();
    }

    [ClientRpc]
    private void BeginIntroClientRpc()
    {
        if (_finished || _health.IsDead) return;

        LockPlayer(PlayerController.LocalInstance);
        _showingPresentation = true;
        if (_camera != null)
        {
            _previousCameraPriority = _camera.Priority;
            _camera.Priority = cameraPriority;
        }
        _menu = MenuToggleController.Instance;
        if (_menu != null) _menu.SetCinematicHidden(this, true);
    }

    private void LockPlayer(PlayerController player)
    {
        if (player == null || _lockedPlayers.Contains(player)) return;
        _lockedPlayers.Add(player);
        player.SetCinematicInputLocked(this, true);
    }

    private void FinishIntro()
    {
        if (_finished) return;
        _finished = true;

        if (IsSpawned && IsServer)
        {
            _enemyAnimator.EndSkillAnimation();
            EndIntroClientRpc();
        }
        CleanupLocal();
    }

    [ClientRpc]
    private void EndIntroClientRpc()
    {
        _finished = true;
        CleanupLocal();
    }

    private void CleanupLocal()
    {
        if (_showingPresentation)
        {
            if (_camera != null)
            {
                HoldReturnCamera();
                _camera.Priority = _previousCameraPriority;
            }
            if (_menu != null) _menu.SetCinematicHidden(this, false);
            _showingPresentation = false;
        }
        foreach (var player in _lockedPlayers)
        {
            if (player != null) player.SetCinematicInputLocked(this, false);
        }
        _lockedPlayers.Clear();

        if (!_holdingBoss) return;
        _holdingBoss = false;
        // 사망 처리가 꺼둔 컴포넌트는 연출 종료가 다시 켜지 않게 합니다.
        if (_health != null && _health.IsDead) return;
        if (_movement != null) _movement.enabled = _movementWasEnabled;
        if (_brain != null) _brain.enabled = _brainWasEnabled;
    }

    private void HoldReturnCamera()
    {
        if (!isActiveAndEnabled || _camera.Follow == null || _cameraReturnTarget != null) return;

        // Idle 복귀/AI 회전으로 CameraMount가 움직여도 마지막 연출 구도에서 복귀합니다.
        // 보스 부모의 움직임도 따라가지 않도록 월드에 독립된 Follow 지점을 둡니다.
        _cameraFollowBeforeReturn = _camera.Follow;
        _cameraReturnTarget = new GameObject(name + " Intro Camera Return").transform;
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(_cameraReturnTarget.gameObject, gameObject.scene);
        _cameraReturnTarget.SetPositionAndRotation(_camera.State.RawPosition, _camera.State.RawOrientation);
        _camera.Follow = _cameraReturnTarget;
    }

    private void ReleaseReturnCamera()
    {
        if (_cameraReturnTarget == null) return;
        if (_camera != null) _camera.Follow = _cameraFollowBeforeReturn;
        Destroy(_cameraReturnTarget.gameObject);
        _cameraReturnTarget = null;
        _cameraFollowBeforeReturn = null;
    }

    private void OnBossDeath(DamageInfo info)
    {
        FinishIntro();
    }

    public override void OnNetworkDespawn()
    {
        _health.OnDeath -= OnBossDeath;
        _finished = true;
        CleanupLocal();
        ReleaseReturnCamera();
    }

    private void OnDisable()
    {
        FinishIntro();
        ReleaseReturnCamera();
    }
}
