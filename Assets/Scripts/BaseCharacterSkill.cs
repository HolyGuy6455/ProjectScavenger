using Scavenger.Manager;
using UnityEngine;

/// <summary>
/// 캐릭터 모델 프리팹(charXXX.prefab)에 부착되는 캐릭터 고유 일반·조준 스킬의 공통 베이스 클래스입니다.
/// 네트워크 동기화는 부모 Player 루트의 PlayerSkillController 공용 RPC를 통해 수행됩니다.
/// </summary>
public abstract class BaseCharacterSkill : MonoBehaviour
{
    private PlayerController cachedPlayer;
    private PlayerSkillController cachedSkillRouter;
    private Health cachedHealth;
    private BuffController cachedBuffs;
    private Shooter cachedShooter;
    private SkillAimController cachedAimController;

    protected PlayerController Player
        => cachedPlayer != null ? cachedPlayer : (cachedPlayer = GetComponentInParent<PlayerController>());

    protected PlayerSkillController SkillRouter
        => cachedSkillRouter != null ? cachedSkillRouter : (cachedSkillRouter = GetComponentInParent<PlayerSkillController>());

    protected Health Health
        => cachedHealth != null ? cachedHealth : (cachedHealth = GetComponentInParent<Health>());

    protected BuffController Buffs
        => cachedBuffs != null ? cachedBuffs : (cachedBuffs = GetComponentInParent<BuffController>());

    protected Shooter Shooter
        => cachedShooter != null ? cachedShooter : (cachedShooter = GetComponentInParent<Shooter>());

    protected SkillAimController AimController
        => cachedAimController != null ? cachedAimController : (cachedAimController = GetComponentInParent<SkillAimController>());

    public bool IsOwner => SkillRouter != null && SkillRouter.IsOwner;
    public bool IsServer => SkillRouter != null && SkillRouter.IsServer;
    public bool IsSpawned => SkillRouter != null && SkillRouter.IsSpawned;

    protected bool IsPlayerUnavailable
    {
        get
        {
            var player = Player;
            var health = Health;
            return player == null || health == null
                || player.IsDeathPresented || health.IsDead || health.CurrentHealth <= 0f
                || health.IsClearProtected
                || (GameFlowManager.Instance != null && GameFlowManager.Instance.CurrentState != GameResultState.None);
        }
    }

    protected virtual void OnEnable()
    {
        if (SkillRouter != null) SkillRouter.ActiveActionsCancelled += OnLocalAndServerCancel;
        if (Player != null) Player.PresentationInterrupted += OnPresentationInterrupted;
    }

    protected virtual void OnDisable()
    {
        if (SkillRouter != null) SkillRouter.ActiveActionsCancelled -= OnLocalAndServerCancel;
        if (Player != null) Player.PresentationInterrupted -= OnPresentationInterrupted;
        OnLocalAndServerCancel();
    }

    public abstract bool HandlesSkill(SkillData data);

    // 즉시/일반 커스텀 스킬 실행 (클라이언트 소유자 로컬 진입점)
    public virtual bool TryUseSkill(SkillData data) => false;

    // SkillActiveType.Aim 전용 훅
    public virtual float GetAimRange(SkillData data) => 0f;
    public virtual float GetAimRadius(SkillData data) => 0f;
    public virtual bool CanCancelAim(SkillData data) => true;
    public virtual bool DeferAimCost(SkillData data) => false;
    public virtual bool TryBeginAim(SkillData data) => false;
    public virtual bool TryCommitAim(SkillData data, Vector3 target) => false;
    public virtual void OnAimCancelled() { }

    // PlayerSkillController 공용 RPC 콜백
    public virtual bool TryAcceptOnServer(SkillData data, Vector3 target, Vector3 origin) => false;
    public virtual void OnClientAccepted(SkillData data, Vector3 target, bool isOwner) { }
    public virtual void OnClientRejected(SkillData data) { }
    public virtual void OnServerAnimationEvent(SkillData data, int eventId, Vector3 worldPoint) { }
    public virtual void OnServerCancel() { }
    public virtual void OnLocalAndServerCancel() => OnServerCancel();
    protected virtual void OnPresentationInterrupted() { }
}

