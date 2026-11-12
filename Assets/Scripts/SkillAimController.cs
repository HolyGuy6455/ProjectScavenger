using UnityEngine;

/// <summary>스킬 버튼으로 준비하고, 오른쪽 조이스틱의 실제 손 떼기로 목표를 확정합니다.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerController), typeof(PlayerSkillController))]
public sealed class SkillAimController : MonoBehaviour, ICharacterControlOverride
{
    private PlayerController player;
    private Shooter shooter;
    private PlayerSkillController skills;
    private SkillInstance prepared;
    private CharacterState state;
    private Vector3 target;
    private bool hasTarget;
    private bool gestureHeld;
    private bool waitForRelease;
    private bool executing;
    private bool executionCommitted;

    public bool IsPrepared => prepared != null && !executing;
    public bool IsExecuting => executing;
    public bool BlocksWeapon => prepared != null || waitForRelease;
    public bool BlocksOtherActions => executing || (prepared != null && !prepared.CanCancelAim);
    public bool IsPreparedSkill(SkillInstance skill) => IsPrepared && ReferenceEquals(prepared, skill);

    public bool IsControlOverrideActive => BlocksWeapon || BlocksOtherActions;
    public bool BlocksCharacterSwitch => BlocksOtherActions;
    public bool BlocksRoll => BlocksOtherActions;
    public bool BlocksHitAnimation => false;
    public bool IsDeferringDeathAnimation => false;

    public bool TryOverrideMovement(
        PlayerController playerController,
        Rigidbody rb,
        Vector2 rawMoveInput,
        float baseMoveSpeed,
        float fixedDeltaTime,
        out Vector3 worldMoveDirection,
        out Vector2 animInput)
    {
        worldMoveDirection = Vector3.zero;
        animInput = Vector2.zero;
        return false; // 이동은 기존 PlayerController의 8방향 이동을 그대로 사용
    }

    public bool TryOverrideAimAndRotation(
        PlayerController playerController,
        Rigidbody rb,
        Vector3 worldMoveDirection,
        Vector2 rotationInput,
        float baseRotationSpeed,
        float fixedDeltaTime)
    {
        if (!BlocksWeapon) return false;
        playerController.StopWeaponForSkillAim();
        if (UpdateTarget(rotationInput, out Vector3 lookDir, out Vector3 aimPoint, out float aimRadius))
        {
            playerController.DrawAimCircleIndicator(aimPoint, aimRadius);
        }
        if (lookDir.sqrMagnitude > 0.001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(lookDir, Vector3.up);
            rb.MoveRotation(Quaternion.RotateTowards(rb.rotation, targetRotation, baseRotationSpeed * fixedDeltaTime));
        }
        return true;
    }

    public void OnBeforeRoll(PlayerController playerController)
    {
        CancelPreparation();
    }

    public void OnCharacterCollision(PlayerController playerController, Collision collision) { }

    public bool TryHandleCustomDeathSequence(PlayerController playerController, System.Action playDefaultDeath)
    {
        return false;
    }

    private void Awake()
    {
        player = GetComponent<PlayerController>();
        shooter = GetComponent<Shooter>();
        skills = GetComponent<PlayerSkillController>();
    }

    private void OnEnable() => skills.ActiveActionsCancelled += ForceCancel;
    private void OnDisable()
    {
        skills.ActiveActionsCancelled -= ForceCancel;
        ForceCancel();
        OnAimPointerCancel();
    }

    public bool Prepare(SkillInstance skill, CharacterState ownerState)
    {
        if (!player.IsOwner || !player.IsSpawned || player.IsInputLocked || player.IsRolling
            || skills.IsReloading() || BlocksOtherActions || skill == null || !skill.IsReady) return false;
        if (!(skill.AimRange > 0f) || !(skill.AimRadius >= 0f)) return false;
        ForceCancel();
        prepared = skill;
        state = ownerState;
        waitForRelease = gestureHeld; // 이미 총을 조준하던 손은 한 번 떼고 새 조준을 시작합니다.
        player.StopWeaponForSkillAim();
        if (skill.TryBeginAim(shooter)) return true;
        ForceCancel();
        return false;
    }

    public void CancelPreparation()
    {
        if (IsPrepared && prepared.CanCancelAim) ForceCancel();
    }

    public void ForceCancel()
    {
        var old = prepared;
        prepared = null;
        state = null;
        executing = false;
        executionCommitted = false;
        hasTarget = false;
        waitForRelease = gestureHeld;
        old?.OnAimCancelled(shooter);
        if (player != null) player.StopWeaponForSkillAim();
    }

    public void FinishExecution()
    {
        prepared = null;
        state = null;
        executing = false;
        executionCommitted = false;
        hasTarget = false;
    }

    public void ConfirmExecution()
    {
        if (!executing || prepared == null || executionCommitted) return;
        executionCommitted = true;
        prepared.ConsumeAimCost(state, shooter);
    }

    public void OnAimPointerDown()
    {
        gestureHeld = true;
        hasTarget = false;
    }

    public void OnAimPointerUp(Vector2 finalInput)
    {
        bool canConfirm = gestureHeld && !waitForRelease && IsPrepared && !player.IsInputLocked;
        if (canConfirm) UpdateTarget(finalInput, out _, out _, out _);
        gestureHeld = false;
        waitForRelease = false;
        if (!canConfirm || !hasTarget || player.IsRolling || skills.IsReloading()) return;
        executing = true;
        if (!prepared.CommitAim(state, shooter, target)) executing = false;
    }

    public void OnAimPointerCancel()
    {
        gestureHeld = false;
        waitForRelease = false;
        hasTarget = false;
    }

    private void OnApplicationFocus(bool focused)
    {
        if (!focused) OnAimPointerCancel();
    }

    public bool UpdateTarget(Vector2 input, out Vector3 direction, out Vector3 point, out float radius)
    {
        direction = Vector3.zero;
        point = target;
        radius = prepared != null ? prepared.AimRadius : 0f;
        if (executing)
        {
            direction = Vector3.ProjectOnPlane(target - transform.position, Vector3.up).normalized;
            return false;
        }
        if (!IsPrepared || !gestureHeld || waitForRelease || player.IsInputLocked
            || player.IsRolling || skills.IsReloading()) return false;
        if (input.sqrMagnitude > .001f)
        {
            var flat = new Vector3(input.x, 0f, input.y);
            target = transform.position + flat.normalized * (prepared.AimRange * Mathf.Clamp01(flat.magnitude));
            float nearest = float.PositiveInfinity;
            foreach (var hit in Physics.RaycastAll(target + Vector3.up * 10f, Vector3.down, 20f,
                         Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.transform.IsChildOf(transform) || hit.distance >= nearest || hit.normal.y < .5f
                    || Bullet.TryGetTarget(hit.collider.transform, out _)) continue;
                nearest = hit.distance;
                target.y = hit.point.y;
            }
            hasTarget = true;
        }
        if (!hasTarget) return false;
        point = target;
        direction = Vector3.ProjectOnPlane(target - transform.position, Vector3.up).normalized;
        return true;
    }
}
