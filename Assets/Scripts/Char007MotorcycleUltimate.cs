using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// char007 전용 오토바이 궁극기 제어 컴포넌트입니다.
/// - Intro (Ultimate): 시네마틱 카메라 + 탑승 연출 (무적, 조작 잠금)
/// - Riding (UltimateRide 1D BlendTree): 자동 전진 + 좌우 조향(방식 A), 수명 감소, 입사각/적 태그 기반 충돌 판정
/// - Crashing (UltimateCrash): 충돌 반동 + 수명 차감 + 이동 입력으로 빠른 방향 전환 (수명 0 도달 시 Crash 완료 후 End 이행)
/// - Ending (UltimateEnd): 백플립 하차 연출 후 기본 이동 복귀 (주행 중 체력 0 도달 시 End 완료 후 Death 이행)
/// </summary>
[DisallowMultipleComponent]
public sealed class Char007MotorcycleUltimate : BaseUltimateSkill, ICharacterControlOverride
{
    public enum RidePhase
    {
        None = 0,
        Intro = 1,
        Riding = 2,
        Crashing = 3,
        Ending = 4
    }

    private const string IntroStateName = "Ultimate";
    private const string RideStateName = "UltimateRide";
    private const string FallbackRideStateName = "UltimateRun";
    private const string CrashStateName = "UltimateCrash";
    private const string EndStateName = "UltimateEnd";

    [Header("Fallback Parameters (Used when SkillData values are omitted)")]
    [SerializeField] private float defaultDuration = 10f;
    [SerializeField] private float defaultCollisionDamage = 100f;
    [SerializeField] private float defaultRideSpeed = 10f;
    [SerializeField] private float defaultCrashPenaltySeconds = 2.0f;
    [SerializeField] private float defaultTurnSpeedDegPerSec = 180f;

    [Header("Collision & Control Tuning")]
    [SerializeField] private float mediumEnemyCrashAngle = 20f;
    [SerializeField] private float largeOrWallCrashAngle = 45f;
    [SerializeField] private float crashTurnSpeedDegPerSec = 540f;
    [SerializeField] private float crashBounceSpeed = 3.5f;
    [SerializeField] private float crashBounceDuration = 0.25f;
    [SerializeField] private float steerBlendSpeed = 6f;
    [SerializeField] private float targetHitCooldown = 0.5f;
    [SerializeField] private float crashFallbackDuration = 1.2f;
    [SerializeField] private float endFallbackDuration = 1.6f;

    private RidePhase _phase = RidePhase.None;
    private Coroutine _sequenceRoutine;
    private PlayerController _ownerPlayer;
    private Health _ownerHealth;
    private CharacterAnimationPresenter _presenter;
    private CharacterCinematicController _cinematicController;
    private int _cameraToken;
    private int _playbackToken;

    private float _remainingLifetime;
    private float _collisionDamage;
    private float _rideSpeed;
    private float _crashPenaltySeconds;
    private float _turnSpeedDegPerSec;
    private float _currentSteer;
    private float _phaseElapsed;
    private float _currentPhaseDuration;
    private Vector3 _crashSurfaceNormal;
    private float _nextWallCrashAllowedTime;

    private bool _isDeferringDeath;
    private Action _pendingDeathCallback;

    private readonly List<(Collider playerCol, Collider otherCol, float restoreTime)> _ignoredCollisions =
        new List<(Collider, Collider, float)>();

    public RidePhase CurrentPhase => _phase;
    public float RemainingLifetime => _remainingLifetime;
    public override bool IsPresentationPlaying => _phase != RidePhase.None;

    // ICharacterControlOverride 구현
    public bool IsControlOverrideActive =>
        _phase == RidePhase.Riding || _phase == RidePhase.Crashing || _phase == RidePhase.Ending;
    public bool BlocksCharacterSwitch => _phase != RidePhase.None;
    public bool BlocksRoll => _phase != RidePhase.None;
    public bool BlocksHitAnimation => _phase != RidePhase.None;
    public bool IsDeferringDeathAnimation => _isDeferringDeath;

    protected override void OnDisable()
    {
        base.OnDisable();
        AbortAll(false);
    }

    public override void CancelPresentation()
    {
        base.CancelPresentation();
        AbortAll(false);
    }

    public override void OnServerExecute(SkillData data, int eventId)
    {
        // 오토바이 궁극기의 충돌 데미지 및 넉백은 PlayerController.RequestRamHit을 통해 처리됩니다.
    }

    public override void OnClientExecute(SkillData data, bool isOwner)
    {
        currentData = data;
        ResolveParameters(data);

        _ownerPlayer = GetComponentInParent<PlayerController>();
        _ownerHealth = _ownerPlayer != null ? _ownerPlayer.GetComponent<Health>() : GetComponentInParent<Health>();
        _presenter = CharacterAnimationPresenter.GetOrCreate(gameObject);
        _cinematicController = GetComponent<CharacterCinematicController>();

        if (!isOwner)
        {
            // 원격 클라이언트는 Intro 애니메이션으로 시작하고 이후 상태 전환은 BroadcastCharacterFullBodyState로 동기화됩니다.
            _presenter?.PlayFullBody(IntroStateName);
            return;
        }

        if (_ownerPlayer == null || _presenter == null)
        {
            Debug.LogError("[Char007MotorcycleUltimate] PlayerController 또는 CharacterAnimationPresenter를 찾을 수 없습니다.", this);
            return;
        }

        AbortAll(false);
        _sequenceRoutine = StartCoroutine(UltimateLifecycleRoutine());
    }

    private void ResolveParameters(SkillData data)
    {
        _remainingLifetime = data != null && data.duration > 0f ? data.duration : defaultDuration;
        float[] fv = data != null ? data.funcValue : null;
        _collisionDamage = fv != null && fv.Length > 0 && fv[0] > 0f ? fv[0] : defaultCollisionDamage;
        _rideSpeed = fv != null && fv.Length > 1 && fv[1] > 0f ? fv[1] : defaultRideSpeed;
        _crashPenaltySeconds = fv != null && fv.Length > 2 && fv[2] >= 0f ? fv[2] : defaultCrashPenaltySeconds;
        _turnSpeedDegPerSec = fv != null && fv.Length > 3 && fv[3] > 0f ? fv[3] : defaultTurnSpeedDegPerSec;
    }

    private IEnumerator UltimateLifecycleRoutine()
    {
        // 1. Intro (시네마틱 카메라 + 탑승 연출)
        _phase = RidePhase.Intro;
        _currentSteer = 0f;
        _isDeferringDeath = false;
        _pendingDeathCallback = null;

        _ownerPlayer.SetCinematicInputLocked(this, true);
        _ownerPlayer.StopWeaponForSkillAim();
        if (_ownerHealth != null) _ownerHealth.IsInvincible = true;
        if (MenuToggleController.Instance != null) MenuToggleController.Instance.SetUIVisibility(false);

        yield return null;

        _playbackToken = _presenter.PlayFullBody(IntroStateName);
        if (_playbackToken == 0)
        {
            Debug.LogError($"[Char007MotorcycleUltimate] {IntroStateName} 상태를 재생할 수 없습니다.", this);
            AbortAll(true);
            yield break;
        }

        Rigidbody rb = _ownerPlayer.GetComponent<Rigidbody>();
        _ownerPlayer.transform.rotation = Quaternion.LookRotation(Vector3.back);
        if (rb != null) rb.rotation = _ownerPlayer.transform.rotation;

        if (_cinematicController != null && _cinematicController.CinematicCamera != null)
        {
            _cameraToken = _cinematicController.BeginCinematic(IntroStateName, true);
        }

        float introDuration = 2.533f;
        yield return _presenter.WaitForDuration(_playbackToken, resolved => introDuration = resolved);
        float returnLead = _cinematicController != null
            ? Mathf.Clamp(_cinematicController.CameraReturnLeadSeconds, 0f, introDuration)
            : 0f;

        yield return new WaitForSeconds(Mathf.Max(0f, introDuration - returnLead));
        if (_cameraToken != 0)
        {
            _cinematicController?.EndCinematic(_cameraToken);
            _cameraToken = 0;
        }
        if (returnLead > 0f) yield return new WaitForSeconds(returnLead);

        if (MenuToggleController.Instance != null) MenuToggleController.Instance.SetUIVisibility(true);
        _ownerPlayer.SetCinematicInputLocked(this, false);
        // 주행 중에는 무적이 아님 (피격 데미지는 정상 적용, 피격 모션만 억제)
        if (_ownerHealth != null) _ownerHealth.IsInvincible = false;

        // 2. Riding 진입
        EnterRidingPhase();

        // 3. 주행 / 충돌 / 종료 루프
        while (_phase == RidePhase.Riding || _phase == RidePhase.Crashing || _phase == RidePhase.Ending)
        {
            UpdateExpiredIgnoredCollisions();

            if (_ownerPlayer == null || _ownerPlayer.HasCleared)
            {
                AbortAll(true);
                yield break;
            }

            float dt = Time.deltaTime;
            _phaseElapsed += dt;

            if (_phase == RidePhase.Riding)
            {
                _remainingLifetime -= dt;
                if (_remainingLifetime <= 0f)
                {
                    _remainingLifetime = 0f;
                    EnterEndingPhase(false);
                }
            }
            else if (_phase == RidePhase.Crashing)
            {
                if (_phaseElapsed >= _currentPhaseDuration)
                {
                    // Crash 애니메이션이 끝난 뒤 남은 수명이 0 이하면 End로, 남아 있으면 다시 Riding으로 전환
                    if (_remainingLifetime <= 0f)
                    {
                        EnterEndingPhase(false);
                    }
                    else
                    {
                        EnterRidingPhase();
                    }
                }
            }
            else if (_phase == RidePhase.Ending)
            {
                if (_phaseElapsed >= _currentPhaseDuration)
                {
                    CompleteSequence();
                    yield break;
                }
            }

            yield return null;
        }
    }

    private void EnterRidingPhase()
    {
        _phase = RidePhase.Riding;
        _phaseElapsed = 0f;
        _ownerPlayer.StopWeaponForSkillAim();
        int rideToken = _presenter != null ? _presenter.PlayFullBody(RideStateName) : 0;
        string stateToPlay = rideToken != 0 ? RideStateName : FallbackRideStateName;
        if (rideToken != 0)
        {
            _playbackToken = rideToken;
        }
        else if (_presenter != null)
        {
            _playbackToken = _presenter.PlayFullBody(FallbackRideStateName);
        }
        _ownerPlayer.BroadcastCharacterFullBodyState(stateToPlay);
    }

    private void EnterCrashingPhase(Vector3 surfaceNormal)
    {
        _phase = RidePhase.Crashing;
        _phaseElapsed = 0f;
        _currentSteer = 0f;
        _crashSurfaceNormal = surfaceNormal;
        _currentPhaseDuration = GetClipDurationOrDefault("ultimate_crash", crashFallbackDuration);
        if (_presenter != null)
        {
            _playbackToken = _presenter.PlayFullBody(CrashStateName);
        }
        _ownerPlayer?.BroadcastCharacterFullBodyState(CrashStateName);
    }

    private void EnterEndingPhase(bool forDeath)
    {
        _phase = RidePhase.Ending;
        _phaseElapsed = 0f;
        _currentSteer = 0f;
        _currentPhaseDuration = GetClipDurationOrDefault("ultimate_end", endFallbackDuration);
        if (_ownerHealth != null && !forDeath)
        {
            _ownerHealth.IsInvincible = true;
        }
        if (_presenter != null)
        {
            _playbackToken = _presenter.PlayFullBody(EndStateName);
        }
        _ownerPlayer?.BroadcastCharacterFullBodyState(EndStateName);
    }

    private void CompleteSequence()
    {
        RestoreAllIgnoredCollisions();
        if (_ownerHealth != null) _ownerHealth.IsInvincible = false;

        Action deathCallback = _pendingDeathCallback;
        _pendingDeathCallback = null;
        _isDeferringDeath = false;
        _phase = RidePhase.None;
        _sequenceRoutine = null;

        if (deathCallback != null)
        {
            if (_presenter != null && _playbackToken != 0)
            {
                _presenter.FinishFullBody(_playbackToken, false);
            }
            _playbackToken = 0;
            deathCallback.Invoke();
        }
        else
        {
            if (_presenter != null && _playbackToken != 0)
            {
                _presenter.FinishFullBody(_playbackToken, true);
            }
            _playbackToken = 0;
            _ownerPlayer?.BroadcastCharacterFullBodyState(string.Empty, true);
        }
    }

    private void AbortAll(bool restoreLocomotion)
    {
        if (_sequenceRoutine != null)
        {
            StopCoroutine(_sequenceRoutine);
            _sequenceRoutine = null;
        }
        RestoreAllIgnoredCollisions();
        if (_cameraToken != 0)
        {
            _cinematicController?.EndCinematic(_cameraToken);
            _cameraToken = 0;
        }
        if (_ownerPlayer != null)
        {
            _ownerPlayer.SetCinematicInputLocked(this, false);
            if (MenuToggleController.Instance != null && _ownerIsLocal())
            {
                MenuToggleController.Instance.SetUIVisibility(true);
            }
        }
        if (_ownerHealth != null)
        {
            _ownerHealth.IsInvincible = false;
        }
        if (_presenter != null && _playbackToken != 0)
        {
            if (restoreLocomotion) _presenter.FinishFullBody(_playbackToken, true);
            else _presenter.CancelFullBody(_playbackToken);
        }
        _playbackToken = 0;
        _isDeferringDeath = false;
        _pendingDeathCallback = null;
        _phase = RidePhase.None;
    }

    private bool _ownerIsLocal() => _ownerPlayer != null && _ownerPlayer.IsOwner;

    public bool TryOverrideMovement(
        PlayerController player,
        Rigidbody rb,
        Vector2 rawMoveInput,
        float baseMoveSpeed,
        float fixedDeltaTime,
        out Vector3 worldMoveDirection,
        out Vector2 animInput)
    {
        worldMoveDirection = new Vector3(rawMoveInput.x, 0f, rawMoveInput.y);
        animInput = Vector2.zero;

        if (!IsControlOverrideActive || rb == null) return false;

        player.StopWeaponForSkillAim();

        if (_phase == RidePhase.Riding)
        {
            // 방식 A: 바라보는 정면으로 자동 전진 + 좌/우 조향 입력으로 SteerX 블렌딩
            float targetSteer = ResolveSteerInput(rb.transform.forward, rawMoveInput);
            _currentSteer = Mathf.MoveTowards(_currentSteer, targetSteer, steerBlendSpeed * fixedDeltaTime);

            Vector3 forward = rb.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
            forward.Normalize();

            rb.MovePosition(rb.position + forward * (_rideSpeed * fixedDeltaTime));
            worldMoveDirection = forward;
            animInput = new Vector2(_currentSteer, 1f);
            return true;
        }

        if (_phase == RidePhase.Crashing)
        {
            // 충돌 직후 짧은 반동으로 벽에서 살짝 튕겨나감
            if (_phaseElapsed < crashBounceDuration && _crashSurfaceNormal.sqrMagnitude > 0.001f)
            {
                float t = 1f - (_phaseElapsed / crashBounceDuration);
                rb.MovePosition(rb.position + _crashSurfaceNormal * (crashBounceSpeed * t * fixedDeltaTime));
            }
            animInput = Vector2.zero;
            return true;
        }

        if (_phase == RidePhase.Ending)
        {
            animInput = Vector2.zero;
            return true;
        }

        return false;
    }

    public bool TryOverrideAimAndRotation(
        PlayerController player,
        Rigidbody rb,
        Vector3 worldMoveDirection,
        Vector2 rotationInput,
        float baseRotationSpeed,
        float fixedDeltaTime)
    {
        if (!IsControlOverrideActive || rb == null) return false;

        player.StopWeaponForSkillAim();

        if (_phase == RidePhase.Riding)
        {
            if (Mathf.Abs(_currentSteer) > 0.001f)
            {
                float yawDelta = _currentSteer * _turnSpeedDegPerSec * fixedDeltaTime;
                Quaternion targetRot = rb.rotation * Quaternion.Euler(0f, yawDelta, 0f);
                rb.MoveRotation(targetRot);
            }
            return true;
        }

        if (_phase == RidePhase.Crashing)
        {
            // 예외 조작: ultimate_crash 재생 중에는 이동 조작 방향으로 빠르게 제자리 회전
            if (worldMoveDirection.sqrMagnitude > 0.001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(worldMoveDirection.normalized, Vector3.up);
                rb.MoveRotation(Quaternion.RotateTowards(
                    rb.rotation,
                    targetRotation,
                    crashTurnSpeedDegPerSec * fixedDeltaTime));
            }
            return true;
        }

        if (_phase == RidePhase.Ending)
        {
            return true;
        }

        return false;
    }

    public void OnBeforeRoll(PlayerController player) { }

    public bool TryHandleCustomDeathSequence(PlayerController player, Action playDefaultDeath)
    {
        if (_phase != RidePhase.Riding && _phase != RidePhase.Crashing && _phase != RidePhase.Ending)
        {
            return false;
        }

        _isDeferringDeath = true;
        _pendingDeathCallback = playDefaultDeath;
        if (_phase != RidePhase.Ending)
        {
            EnterEndingPhase(true);
        }
        return true;
    }

    public void OnCharacterCollision(PlayerController player, Collision collision)
    {
        if (_phase != RidePhase.Riding || collision == null || collision.contactCount == 0) return;

        Collider hitCollider = collision.collider;
        if (hitCollider == null || hitCollider.isTrigger || hitCollider.transform.root == player.transform.root)
        {
            return;
        }

        ContactPoint contact = collision.GetContact(0);
        // 바닥/경사면 충돌 무시
        if (Mathf.Abs(contact.normal.y) > 0.65f) return;

        Vector3 surfaceNormal = contact.normal;
        surfaceNormal.y = 0f;
        if (surfaceNormal.sqrMagnitude < 0.0001f)
        {
            surfaceNormal = player.transform.position - hitCollider.bounds.center;
            surfaceNormal.y = 0f;
        }
        if (surfaceNormal.sqrMagnitude < 0.0001f) return;
        surfaceNormal.Normalize();

        Vector3 bikeForward = player.transform.forward;
        bikeForward.y = 0f;
        if (bikeForward.sqrMagnitude < 0.0001f) return;
        bikeForward.Normalize();

        Vector3 impactDir = -surfaceNormal;
        // 이미 표면과 평행하거나 멀어지는 방향이면 충돌 판정 생략
        if (Vector3.Dot(bikeForward, impactDir) <= 0.02f) return;

        // 입사각(theta): 정면 수직 충돌 = 0도, 평행 스침 = 90도
        float incidenceAngle = Vector3.Angle(bikeForward, impactDir);
        long hitSourceKey = ((long)player.NetworkObjectId << 16) | 7007L;

        EnemyController enemy = hitCollider.GetComponentInParent<EnemyController>();
        Health targetHealth = hitCollider.GetComponentInParent<Health>();
        IDamageable damageable = hitCollider.GetComponentInParent<IDamageable>();

        if (enemy != null)
        {
            if (targetHealth != null && targetHealth.IsDead)
            {
                IgnoreTargetCollidersTemporarily(player, enemy.gameObject, targetHitCooldown);
                return;
            }

            bool isSmall = enemy.HasTag("Small");
            bool isLargeOrBoss = enemy.HasTag("Large") || enemy.HasTag("Boss");

            if (isSmall)
            {
                // 소형 적: 무조건 통과 + 충돌 데미지 + 옆으로 살짝 밀어냄
                IgnoreTargetCollidersTemporarily(player, enemy.gameObject, targetHitCooldown);
                if (targetHealth == null || targetHealth.TryConsumeLocalHitCooldown(hitSourceKey, targetHitCooldown))
                {
                    Vector3 toEnemy = enemy.transform.position - player.transform.position;
                    toEnemy.y = 0f;
                    Vector3 right = Vector3.Cross(Vector3.up, bikeForward);
                    float side = Vector3.Dot(toEnemy, right) >= 0f ? 1f : -1f;
                    Vector3 pushDir = (right * side + bikeForward * 0.35f).normalized;
                    player.RequestRamHit(
                        enemy.NetworkObject,
                        _collisionDamage,
                        contact.point,
                        contact.normal,
                        hitSourceKey,
                        targetHitCooldown,
                        pushDir,
                        8f,
                        0.2f);
                }
                return;
            }

            float crashThreshold = isLargeOrBoss ? largeOrWallCrashAngle : mediumEnemyCrashAngle;
            bool shouldCrash = incidenceAngle <= crashThreshold;

            if (targetHealth == null || targetHealth.TryConsumeLocalHitCooldown(hitSourceKey, targetHitCooldown))
            {
                Vector3 knockDir = isLargeOrBoss ? Vector3.zero : impactDir;
                float knockSpeed = isLargeOrBoss ? 0f : 3.5f;
                float knockDuration = isLargeOrBoss ? 0f : 0.12f;
                player.RequestRamHit(
                    enemy.NetworkObject,
                    _collisionDamage,
                    contact.point,
                    contact.normal,
                    hitSourceKey,
                    targetHitCooldown,
                    knockDir,
                    knockSpeed,
                    knockDuration);

                // 막타로 적을 처치하더라도 입사각 조건을 충족했으면 Crash 재생
                if (shouldCrash)
                {
                    TriggerCrash(player, surfaceNormal);
                    return;
                }
            }

            // 비스듬히 스친 경우: 수명 차감 없이 방향만 접선으로 흘려보냄
            if (!isLargeOrBoss)
            {
                IgnoreTargetCollidersTemporarily(player, enemy.gameObject, 0.25f);
            }
            DeflectAlongSurfaceTangent(player, bikeForward, surfaceNormal);
            return;
        }

        // 벽 / 지형 / 파괴 가능 엄폐물 충돌 처리
        if (damageable != null && (targetHealth == null || targetHealth.TryConsumeLocalHitCooldown(hitSourceKey, targetHitCooldown)))
        {
            NetworkObject netObj = hitCollider.GetComponentInParent<NetworkObject>();
            player.RequestRamHit(
                netObj,
                _collisionDamage,
                contact.point,
                contact.normal,
                hitSourceKey,
                targetHitCooldown,
                Vector3.zero,
                0f,
                0f);
        }

        if (incidenceAngle <= largeOrWallCrashAngle)
        {
            if (Time.time >= _nextWallCrashAllowedTime)
            {
                _nextWallCrashAllowedTime = Time.time + targetHitCooldown;
                TriggerCrash(player, surfaceNormal);
            }
        }
        else
        {
            // 입사각 45도 초과로 비스듬히 벽에 부딪힌 경우: 게이지 차감 없이 벽면 접선 방향으로 슬라이딩 회전
            DeflectAlongSurfaceTangent(player, bikeForward, surfaceNormal);
        }
    }

    private void TriggerCrash(PlayerController player, Vector3 surfaceNormal)
    {
        _remainingLifetime = Mathf.Max(0f, _remainingLifetime - _crashPenaltySeconds);
        EnterCrashingPhase(surfaceNormal);
    }

    private static void DeflectAlongSurfaceTangent(PlayerController player, Vector3 bikeForward, Vector3 surfaceNormal)
    {
        Rigidbody rb = player.GetComponent<Rigidbody>();
        if (rb == null) return;

        Vector3 tangent = Vector3.ProjectOnPlane(bikeForward, surfaceNormal);
        tangent.y = 0f;
        if (tangent.sqrMagnitude <= 0.0001f) return;
        tangent.Normalize();

        Quaternion targetRot = Quaternion.LookRotation(tangent, Vector3.up);
        rb.MoveRotation(Quaternion.RotateTowards(rb.rotation, targetRot, 720f * Time.fixedDeltaTime));
    }

    private static float ResolveSteerInput(Vector3 bikeForward, Vector2 rawMoveInput)
    {
        if (rawMoveInput.sqrMagnitude <= 0.001f) return 0f;
        // 순수 좌/우 키 입력(A/D)이거나 전후방 입력이 거의 없을 때는 x축을 직관적인 핸들 조향으로 사용
        if (Mathf.Abs(rawMoveInput.y) <= 0.05f)
        {
            return Mathf.Clamp(rawMoveInput.x, -1f, 1f);
        }
        // 가상 조이스틱/대각선 입력 시에는 현재 바이크 진행 방향 대비 목표 월드 방향의 좌우 각도 차이로도 조향 가능하되,
        // 좌/우 입력(rawMoveInput.x)을 우선 반영
        return Mathf.Clamp(rawMoveInput.x, -1f, 1f);
    }

    private void IgnoreTargetCollidersTemporarily(PlayerController player, GameObject targetRoot, float duration)
    {
        if (player == null || targetRoot == null) return;
        var playerColliders = player.GetComponentsInChildren<Collider>();
        var targetColliders = targetRoot.GetComponentsInChildren<Collider>();
        float restoreTime = Time.time + duration;

        for (int i = 0; i < playerColliders.Length; i++)
        {
            Collider pCol = playerColliders[i];
            if (pCol == null || pCol.isTrigger) continue;
            for (int j = 0; j < targetColliders.Length; j++)
            {
                Collider tCol = targetColliders[j];
                if (tCol == null || tCol.isTrigger) continue;
                Physics.IgnoreCollision(pCol, tCol, true);
                _ignoredCollisions.Add((pCol, tCol, restoreTime));
            }
        }
    }

    private void UpdateExpiredIgnoredCollisions()
    {
        float now = Time.time;
        for (int i = _ignoredCollisions.Count - 1; i >= 0; i--)
        {
            var entry = _ignoredCollisions[i];
            if (now >= entry.restoreTime)
            {
                if (entry.playerCol != null && entry.otherCol != null)
                {
                    Physics.IgnoreCollision(entry.playerCol, entry.otherCol, false);
                }
                _ignoredCollisions.RemoveAt(i);
            }
        }
    }

    private void RestoreAllIgnoredCollisions()
    {
        for (int i = 0; i < _ignoredCollisions.Count; i++)
        {
            var entry = _ignoredCollisions[i];
            if (entry.playerCol != null && entry.otherCol != null)
            {
                Physics.IgnoreCollision(entry.playerCol, entry.otherCol, false);
            }
        }
        _ignoredCollisions.Clear();
    }

    private float GetClipDurationOrDefault(string suffix, float fallback)
    {
        var animator = GetComponentInChildren<Animator>();
        if (animator != null && animator.runtimeAnimatorController != null)
        {
            foreach (var clip in animator.runtimeAnimatorController.animationClips)
            {
                if (clip != null && clip.name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) && clip.length > 0f)
                {
                    return clip.length;
                }
            }
        }
        return fallback;
    }
}
