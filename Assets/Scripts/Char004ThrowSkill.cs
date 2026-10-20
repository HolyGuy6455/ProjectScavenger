using Scavenger.Manager;
using Unity.Netcode;
using UnityEngine;

/// <summary>char004의 조준 준비, 투척 시점과 서버 발사체 생성을 연결합니다.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerController), typeof(PlayerSkillController), typeof(Health))]
public sealed class Char004ThrowSkill : NetworkBehaviour
{
    [SerializeField] private Char004Throwable smokePrefab;
    [SerializeField] private Char004Throwable bottlePrefab;
    [SerializeField] private Char004Throwable molotovPrefab;

    private PlayerController player;
    private PlayerSkillController skills;
    private Health health;
    private int pendingSkillId;
    private Vector3 pendingTarget;
    private int preparedUltimateId;
    // Negative while waiting for this cinematic, positive only after its normal completion.
    private int localUltimateId;

    public bool BlocksCharacterChange => preparedUltimateId != 0 || pendingSkillId != 0 || localUltimateId != 0;
    private bool IsUnavailable => player.IsDeathPresented || health.IsDead || health.CurrentHealth <= 0f
        || health.IsClearProtected || (GameFlowManager.Instance != null
            && GameFlowManager.Instance.CurrentState != GameResultState.None);

    private void Awake()
    {
        player = GetComponent<PlayerController>();
        skills = GetComponent<PlayerSkillController>();
        health = GetComponent<Health>();
    }

    private void OnEnable()
    {
        skills.ActiveActionsCancelled += CancelAim;
        player.PresentationInterrupted += CancelAim;
    }

    private void OnDisable()
    {
        skills.ActiveActionsCancelled -= CancelAim;
        player.PresentationInterrupted -= CancelAim;
        CancelAim();
    }

    public override void OnNetworkDespawn()
    {
        pendingSkillId = preparedUltimateId = localUltimateId = 0;
        GetActiveAnimation()?.CancelAimVisual();
    }

    public bool TryPrepare(int skillId)
    {
        if (!IsOwner || !IsSpawned || IsUnavailable || player.IsInputLocked || player.IsRolling || skills.IsReloading()) return false;
        var data = GetSkillData(skillId);
        var animation = GetActiveAnimation();
        var prefab = GetPrefab(data);
        if (data == null || animation == null || animation.IsPlaying || prefab == null) return false;
        if (data.func != 1) return animation.ShowAim(prefab.gameObject);
        if (!(player.GetActiveUltimateSkill() is Char004Ultimate ultimate) || ultimate.IsPresentationPlaying) return false;
        localUltimateId = -skillId;
        skills.CmdUseUltimateServerRpc(skillId);
        return true;
    }

    public bool TryThrow(int skillId, Vector3 target)
    {
        if (!IsOwner || !IsSpawned || IsUnavailable || player.IsInputLocked || player.IsRolling || skills.IsReloading()) return false;
        var data = GetSkillData(skillId);
        var animation = GetActiveAnimation();
        Vector3 confirmedOrigin = player.transform.position;
        if (data == null || animation == null || !animation.CanThrow || !IsTargetInRange(data, target, confirmedOrigin)
            || (data.func == 1 && localUltimateId != skillId)) return false;
        localUltimateId = 0;
        BeginThrowServerRpc(skillId, target, confirmedOrigin);
        return true;
    }

    public void MarkUltimatePreparedOnServer(int skillId)
    {
        var data = GetSkillData(skillId);
        if (IsServer && IsSpawned && !IsUnavailable && data?.func == 1 && GetActiveAnimation() != null)
            preparedUltimateId = skillId;
    }

    public void MarkUltimateReady(int skillId)
    {
        if (IsUnavailable || (IsOwner && localUltimateId != -skillId)) return;
        var animation = GetActiveAnimation();
        if (molotovPrefab != null && animation != null && animation.ShowAim(molotovPrefab.gameObject))
        {
            if (IsOwner) localUltimateId = skillId;
        }
        else
        {
            Debug.LogError("[Char004ThrowSkill] Cannot prepare the ignited bottle after its cinematic.", this);
            if (IsOwner) GetComponent<SkillAimController>()?.ForceCancel();
        }
    }

    [ServerRpc]
    private void BeginThrowServerRpc(int skillId, Vector3 target, Vector3 confirmedOrigin)
    {
        if (pendingSkillId != 0 || IsUnavailable || player.IsInputLocked)
        {
            RejectThrowClientRpc();
            return;
        }
        var data = GetSkillData(skillId);
        var animation = GetActiveAnimation();
        if (data == null || animation == null || GetPrefab(data) == null || !IsFinite(confirmedOrigin)
            || Vector3.Distance(confirmedOrigin, player.transform.position) > MaximumReach(animation)
            || !IsTargetInRange(data, target, confirmedOrigin)
            || (data.func == 1 && preparedUltimateId != skillId))
        {
            Debug.LogError($"[Char004ThrowSkill] Invalid throw request for SkillData {skillId}.", this);
            RejectThrowClientRpc();
            return;
        }
        pendingSkillId = skillId;
        pendingTarget = target;
        if (data.func == 1) preparedUltimateId = 0;
        PlayThrowClientRpc(skillId);
    }

    [ClientRpc]
    private void PlayThrowClientRpc(int skillId)
    {
        if (IsOwner)
        {
            var aim = GetComponent<SkillAimController>();
            if (IsUnavailable || aim == null || !aim.IsExecuting)
            {
                CancelPendingThrow();
                return;
            }
            var ownerData = GetSkillData(skillId);
            var ownerAnimation = GetActiveAnimation();
            if (ownerData == null || ownerAnimation == null || !ownerAnimation.TryPlay(ThrowDuration(ownerData)))
            {
                Debug.LogError("[Char004ThrowSkill] Cannot begin the accepted throwing animation.", this);
                aim.ForceCancel();
                return;
            }
            // Keep the ready pose while awaiting acceptance, then begin the gesture and its cost together.
            aim.ConfirmExecution();
            return;
        }
        if (IsUnavailable) return;
        var data = GetSkillData(skillId);
        var animation = GetActiveAnimation();
        var prefab = GetPrefab(data);
        if (data == null || prefab == null || animation == null
            || !animation.ShowAim(prefab.gameObject) || !animation.TryPlay(ThrowDuration(data)))
            Debug.LogError("[Char004ThrowSkill] Cannot play the remote character's throwing animation.", this);
    }

    [ClientRpc]
    private void RejectThrowClientRpc()
    {
        if (IsOwner) GetComponent<SkillAimController>()?.ForceCancel();
    }

    public void OnReleaseAnimationEvent()
    {
        if (!IsOwner || !IsSpawned || IsUnavailable) return;
        var animation = GetActiveAnimation();
        if (animation != null && animation.IsPlaying && animation.ReleasePoint != null)
            ReleaseThrowServerRpc(animation.ReleasePoint.position);
    }

    [ServerRpc]
    private void ReleaseThrowServerRpc(Vector3 handPosition)
    {
        if (pendingSkillId == 0) return;
        var data = GetSkillData(pendingSkillId);
        pendingSkillId = 0;
        if (IsUnavailable || data == null) return;
        var animation = GetActiveAnimation();
        var prefab = GetPrefab(data);
        if (animation == null || prefab == null || !IsFinite(handPosition)) return;
        if (Vector3.Distance(handPosition, player.transform.position) > MaximumReach(animation))
        {
            Debug.LogError("[Char004ThrowSkill] Throw release point lies outside the character's reach.", this);
            return;
        }
        // The owner provides the animated hand pose, while the server keeps the confirmed target.
        // This also works when a dedicated server does not evaluate character animation.
        var projectile = Instantiate(prefab, handPosition, Quaternion.identity);
        projectile.transform.localScale = Vector3.Scale(prefab.transform.localScale, animation.transform.lossyScale);
        var networkObject = projectile.GetComponent<NetworkObject>();
        if (networkObject == null || !projectile.Initialize(pendingTarget, gameObject, data))
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
        GetComponent<SkillAimController>()?.FinishExecution();
    }

    public void CancelAim()
    {
        localUltimateId = 0;
        CancelPendingThrow();
        GetActiveAnimation()?.CancelAimVisual();
    }

    public void CancelPendingThrow()
    {
        if (IsServer) pendingSkillId = preparedUltimateId = 0;
        else if (IsOwner && IsSpawned) CancelThrowServerRpc();
    }

    [ServerRpc]
    private void CancelThrowServerRpc() => pendingSkillId = preparedUltimateId = 0;

    private Char004ThrowAnimation GetActiveAnimation()
    {
        if (player == null || player.GetLocalDeck().GetCharId(player.ActiveSlotIndex) != 4) return null;
        return player.GetActiveCharacterModel()?.GetComponent<Char004ThrowAnimation>();
    }

    private SkillData GetSkillData(int skillId)
    {
        var data = DataManager.Instance != null ? DataManager.Instance.GetData<SkillData>("Skill", skillId) : null;
        if (data != null && data.charId == 4 && (data.func == 1 || data.func == 8 || data.func == 9)
            && data.duration > 0f && data.funcValue != null && data.funcValue.Length >= 2 && data.funcValue[0] > 0f)
            return data;
        Debug.LogError($"[Char004ThrowSkill] Missing or invalid char004 throwable SkillData: {skillId}.", this);
        return null;
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
