using Unity.Netcode;
using UnityEngine;

/// <summary>char004 프리팹에 부착되어 조준 준비, 투척 시점과 서버 발사체 생성을 연결합니다.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Char004ThrowAnimation))]
public sealed class Char004ThrowSkill : BaseCharacterSkill, ICharacterControlOverride
{
    [SerializeField] private Char004Throwable smokePrefab;
    [SerializeField] private Char004Throwable bottlePrefab;
    [SerializeField] private Char004Throwable molotovPrefab;

    private Char004ThrowAnimation cachedAnimation;
    private int pendingSkillId;
    private Vector3 pendingTarget;
    private int preparedUltimateId;
    // Negative while waiting for this cinematic, positive only after its normal completion.
    private int localUltimateId;
    private int activeSkillId;

    private Char004ThrowAnimation Animation
        => cachedAnimation != null ? cachedAnimation : (cachedAnimation = GetComponent<Char004ThrowAnimation>());

    public bool BlocksCharacterChange => preparedUltimateId != 0 || pendingSkillId != 0 || localUltimateId != 0;
    public bool IsControlOverrideActive => BlocksCharacterChange;
    public bool BlocksCharacterSwitch => BlocksCharacterChange;
    public bool BlocksRoll => false;
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
        return false;
    }

    public bool TryOverrideAimAndRotation(
        PlayerController playerController,
        Rigidbody rb,
        Vector3 worldMoveDirection,
        Vector2 rotationInput,
        float baseRotationSpeed,
        float fixedDeltaTime)
    {
        return false;
    }

    public void OnBeforeRoll(PlayerController playerController) { }

    public void OnCharacterCollision(PlayerController playerController, Collision collision) { }

    public bool TryHandleCustomDeathSequence(PlayerController playerController, System.Action playDefaultDeath)
    {
        return false;
    }

    public override bool HandlesSkill(SkillData data)
        => data != null && data.charId == 4 && (data.func == 1 || data.func == 8 || data.func == 9);

    public override float GetAimRange(SkillData data)
        => data?.funcValue != null && data.funcValue.Length > 0 ? data.funcValue[0] : 0f;

    public override float GetAimRadius(SkillData data)
        => data?.funcValue != null && data.funcValue.Length > 1 ? data.funcValue[1] : 0f;

    public override bool CanCancelAim(SkillData data)
        => data?.func != 1;

    public override bool DeferAimCost(SkillData data)
        => true;

    public bool TryPrepare(int skillId)
    {
        var data = DataManager.Instance != null ? DataManager.Instance.GetData<SkillData>("Skill", skillId) : null;
        return TryBeginAim(data);
    }

    public override bool TryBeginAim(SkillData data)
    {
        if (!isActiveAndEnabled || !IsOwner || !IsSpawned || IsPlayerUnavailable
            || Player.IsInputLocked || Player.IsRolling || SkillRouter.IsReloading())
            return false;
        if (!ValidateSkillData(data)) return false;
        var animation = Animation;
        var prefab = GetPrefab(data);
        if (animation == null || animation.IsPlaying || prefab == null) return false;
        if (data.func != 1)
        {
            if (!animation.ShowAim(prefab.gameObject)) return false;
            activeSkillId = data.id;
            return true;
        }
        if (!(Player.GetActiveUltimateSkill() is Char004Ultimate ultimate) || ultimate.IsPresentationPlaying)
            return false;
        localUltimateId = -data.id;
        activeSkillId = data.id;
        SkillRouter.CmdUseUltimateServerRpc(data.id);
        return true;
    }

    public bool TryThrow(int skillId, Vector3 target)
    {
        var data = DataManager.Instance != null ? DataManager.Instance.GetData<SkillData>("Skill", skillId) : null;
        return TryCommitAim(data, target);
    }

    public override bool TryCommitAim(SkillData data, Vector3 target)
    {
        if (!isActiveAndEnabled || !IsOwner || !IsSpawned || IsPlayerUnavailable
            || Player.IsInputLocked || Player.IsRolling || SkillRouter.IsReloading())
            return false;
        if (!ValidateSkillData(data)) return false;
        var animation = Animation;
        Vector3 confirmedOrigin = Player.transform.position;
        if (animation == null || !animation.CanThrow || !IsTargetInRange(data, target, confirmedOrigin)
            || (data.func == 1 && localUltimateId != data.id))
            return false;
        localUltimateId = 0;
        activeSkillId = data.id;
        SkillRouter.RequestCharacterSkill(data.id, target, confirmedOrigin);
        return true;
    }

    public void MarkUltimatePreparedOnServer(int skillId)
    {
        var data = DataManager.Instance != null ? DataManager.Instance.GetData<SkillData>("Skill", skillId) : null;
        if (IsServer && IsSpawned && !IsPlayerUnavailable && ValidateSkillData(data) && data.func == 1 && Animation != null)
            preparedUltimateId = skillId;
    }

    public void MarkUltimateReady(int skillId)
    {
        if (IsPlayerUnavailable || (IsOwner && localUltimateId != -skillId)) return;
        var animation = Animation;
        if (molotovPrefab != null && animation != null && animation.ShowAim(molotovPrefab.gameObject))
        {
            if (IsOwner)
            {
                localUltimateId = skillId;
                activeSkillId = skillId;
            }
        }
        else
        {
            Debug.LogError("[Char004ThrowSkill] Cannot prepare the ignited bottle after its cinematic.", this);
            if (IsOwner) AimController?.ForceCancel();
        }
    }

    public override bool TryAcceptOnServer(SkillData data, Vector3 target, Vector3 origin)
    {
        if (pendingSkillId != 0 || IsPlayerUnavailable || Player.IsInputLocked)
            return false;
        var animation = Animation;
        if (!ValidateSkillData(data) || animation == null || GetPrefab(data) == null || !IsFinite(origin)
            || Vector3.Distance(origin, Player.transform.position) > MaximumReach(animation)
            || !IsTargetInRange(data, target, origin)
            || (data.func == 1 && preparedUltimateId != data.id))
        {
            Debug.LogError($"[Char004ThrowSkill] Invalid throw request for SkillData {data?.id}.", this);
            return false;
        }
        pendingSkillId = data.id;
        pendingTarget = target;
        if (data.func == 1) preparedUltimateId = 0;
        return true;
    }

    public override void OnClientAccepted(SkillData data, Vector3 target, bool isOwner)
    {
        if (isOwner)
        {
            var aim = AimController;
            if (IsPlayerUnavailable || aim == null || !aim.IsExecuting)
            {
                CancelPendingThrow();
                return;
            }
            var ownerAnimation = Animation;
            if (!ValidateSkillData(data) || ownerAnimation == null || !ownerAnimation.TryPlay(ThrowDuration(data)))
            {
                Debug.LogError("[Char004ThrowSkill] Cannot begin the accepted throwing animation.", this);
                aim.ForceCancel();
                return;
            }
            activeSkillId = data.id;
            // Keep the ready pose while awaiting acceptance, then begin the gesture and its cost together.
            aim.ConfirmExecution();
            return;
        }
        if (IsPlayerUnavailable) return;
        var animation = Animation;
        var prefab = GetPrefab(data);
        if (!ValidateSkillData(data) || prefab == null || animation == null
            || !animation.ShowAim(prefab.gameObject) || !animation.TryPlay(ThrowDuration(data)))
            Debug.LogError("[Char004ThrowSkill] Cannot play the remote character's throwing animation.", this);
    }

    public override void OnClientRejected(SkillData data)
    {
        if (IsOwner) AimController?.ForceCancel();
    }

    public void OnReleaseAnimationEvent()
    {
        if (!IsOwner || !IsSpawned || IsPlayerUnavailable || activeSkillId == 0) return;
        var animation = Animation;
        if (animation != null && animation.IsPlaying && animation.ReleasePoint != null)
            SkillRouter?.NotifyCharacterSkillAnimationEvent(activeSkillId, 0, animation.ReleasePoint.position);
    }

    public override void OnServerAnimationEvent(SkillData data, int eventId, Vector3 worldPoint)
    {
        if (pendingSkillId == 0 || data == null || pendingSkillId != data.id) return;
        pendingSkillId = 0;
        if (IsPlayerUnavailable || !ValidateSkillData(data)) return;
        var animation = Animation;
        var prefab = GetPrefab(data);
        if (animation == null || prefab == null || !IsFinite(worldPoint)) return;
        if (Vector3.Distance(worldPoint, Player.transform.position) > MaximumReach(animation))
        {
            Debug.LogError("[Char004ThrowSkill] Throw release point lies outside the character's reach.", this);
            return;
        }
        // The owner provides the animated hand pose, while the server keeps the confirmed target.
        // This also works when a dedicated server does not evaluate character animation.
        var projectile = Instantiate(prefab, worldPoint, Quaternion.identity);
        projectile.transform.localScale = Vector3.Scale(prefab.transform.localScale, animation.transform.lossyScale);
        var networkObject = projectile.GetComponent<NetworkObject>();
        if (networkObject == null || !projectile.Initialize(pendingTarget, Player.gameObject, data))
        {
            Debug.LogError("[Char004ThrowSkill] The throwable prefab could not be initialized.", this);
            Destroy(projectile.gameObject);
            return;
        }
        networkObject.Spawn(true);
    }

    public void FinishThrow()
    {
        if (!IsOwner) return;
        CancelPendingThrow();
        activeSkillId = 0;
        AimController?.FinishExecution();
    }

    public override void OnAimCancelled() => CancelAim();

    public override void OnServerCancel() => pendingSkillId = preparedUltimateId = 0;

    public override void OnLocalAndServerCancel() => CancelAim();

    protected override void OnPresentationInterrupted() => CancelAim();

    public void CancelAim()
    {
        var animation = Animation;
        bool hadClientState = localUltimateId != 0 || activeSkillId != 0
            || (animation != null && (animation.IsPlaying || animation.CanThrow));
        int cancelId = activeSkillId != 0 ? activeSkillId : Mathf.Abs(localUltimateId);
        localUltimateId = 0;
        activeSkillId = 0;
        if (IsServer) pendingSkillId = preparedUltimateId = 0;
        else if (hadClientState && IsOwner && IsSpawned)
            SkillRouter?.RequestCancelCharacterSkill(cancelId);
        animation?.CancelAimVisual();
    }

    public void CancelPendingThrow()
    {
        int cancelId = activeSkillId != 0 ? activeSkillId : Mathf.Abs(localUltimateId);
        if (IsServer) pendingSkillId = preparedUltimateId = 0;
        else if (IsOwner && IsSpawned) SkillRouter?.RequestCancelCharacterSkill(cancelId);
    }

    private bool ValidateSkillData(SkillData data)
    {
        if (HandlesSkill(data) && data.duration > 0f
            && data.funcValue != null && data.funcValue.Length >= 2 && data.funcValue[0] > 0f)
            return true;
        Debug.LogError($"[Char004ThrowSkill] Missing or invalid char004 throwable SkillData: {data?.id}.", this);
        return false;
    }

    private Char004Throwable GetPrefab(SkillData data)
    {
        if (data == null) return null;
        var prefab = data.func == 8 ? smokePrefab : data.func == 9 ? bottlePrefab : molotovPrefab;
        if (prefab == null) Debug.LogError($"[Char004ThrowSkill] Projectile prefab is missing for SkillData {data.id}.", this);
        return prefab;
    }

    private static bool IsTargetInRange(SkillData data, Vector3 target, Vector3 origin)
    {
        if (!IsFinite(target)) return false;
        Vector3 delta = target - origin;
        delta.y = 0f;
        return delta.sqrMagnitude <= data.funcValue[0] * data.funcValue[0] + .01f;
    }

    // Ultimate.duration belongs to the ignition presentation; its subsequent toss uses the same 1s gesture.
    private static float ThrowDuration(SkillData data) => data.func == 1 ? 1f : data.duration;

    private static float MaximumReach(Char004ThrowAnimation animation)
        => Mathf.Max(2f, Mathf.Abs(animation.transform.lossyScale.y) * 1.2f);

    private static bool IsFinite(Vector3 value) => IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
    private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
