using Unity.Netcode;
using UnityEngine;

/// <summary>Plays the authored placement motion and spawns char002's mine through PlayerSkillController.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterAnimationPresenter))]
public sealed class Char002ClaymoreSkill : BaseCharacterSkill
{
    [SerializeField] private NetworkObject minePrefab;
    [SerializeField] private Transform placementPoint;
    [SerializeField] private Renderer carriedMine;

    private int animationToken;
    private bool placementRequested;
    private int pendingAmmoId;
    private int activeSkillId;

    private CharacterAnimationPresenter Presenter => GetComponent<CharacterAnimationPresenter>();
    internal int AnimationToken => animationToken;
    public bool IsPlaying => animationToken != 0 && Presenter.IsCurrentPlayback(animationToken);

    public override bool HandlesSkill(SkillData data)
        => data != null && data.charId == 2 && data.func == 7;

    public bool TryUse(int skillId)
    {
        var data = DataManager.Instance != null ? DataManager.Instance.GetData<SkillData>("Skill", skillId) : null;
        return TryUseSkill(data);
    }

    public override bool TryUseSkill(SkillData data)
    {
        if (!isActiveAndEnabled || !IsOwner || !IsSpawned || IsPlayerUnavailable
            || Player.IsInputLocked || SkillRouter.IsReloading())
            return false;
        if (Player.GetLocalDeck().GetCharId(Player.ActiveSlotIndex) != 2
            || Player.GetActiveCharacterModel() != gameObject)
        {
            Debug.LogError("[Char002ClaymoreSkill] Active character must be char002 with its claymore presentation.", this);
            return false;
        }
        if (!ValidateSkillData(data) || GetAmmoId(data) == 0 || !TryPlay(data.duration))
            return false;

        activeSkillId = data.id;
        SkillRouter.RequestCharacterSkill(data.id);
        return true;
    }

    public bool TryPlay(float duration)
    {
        var player = Player;
        if (!isActiveAndEnabled || player == null || player.IsInputLocked || player.IsRolling
            || Presenter.IsFullBodyPlaying || Presenter.IsDeathPresented) return false;
        if (minePrefab == null || placementPoint == null || carriedMine == null)
        {
            Debug.LogError("[Char002ClaymoreSkill] Mine prefab, placement point and carried mesh must be assigned.", this);
            return false;
        }

        animationToken = Presenter.PlaySkill(duration);
        if (animationToken == 0)
        {
            Debug.LogError("[Char002ClaymoreSkill] Cannot play the Base Layer Skill state.", this);
            return false;
        }
        placementRequested = false;
        carriedMine.enabled = true;
        player.SetCinematicInputLocked(this, true);
        return true;
    }

    public override bool TryAcceptOnServer(SkillData data, Vector3 target, Vector3 origin)
    {
        if (pendingAmmoId != 0 || IsPlayerUnavailable || !ValidateSkillData(data)) return false;
        // 호스트 소유자는 이미 로컬 애니메이션을 시작하여 입력을 잠근 상태입니다.
        if (Player.IsInputLocked && !IsPlaying) return false;
        pendingAmmoId = GetAmmoId(data);
        return pendingAmmoId != 0;
    }

    public override void OnClientAccepted(SkillData data, Vector3 target, bool isOwner)
    {
        if (isOwner || IsPlayerUnavailable || data == null) return;
        if (!TryPlay(data.duration))
            Debug.LogError("[Char002ClaymoreSkill] Cannot play the remote character's claymore animation.", this);
    }

    public override void OnClientRejected(SkillData data)
    {
        if (IsOwner) Cancel();
    }

    // AnimationEvent on Char002_skill at the authored release frame (84).
    public void PlaceClaymore()
    {
        if (!IsPlaying || placementRequested) return;
        placementRequested = true;
        carriedMine.enabled = false;
        OnPlacementAnimationEvent();
    }

    public void OnPlacementAnimationEvent()
    {
        if (!IsOwner || !IsSpawned || IsPlayerUnavailable || !IsPlaying || activeSkillId == 0) return;
        SkillRouter?.NotifyCharacterSkillAnimationEvent(activeSkillId);
    }

    public override void OnServerAnimationEvent(SkillData data, int eventId, Vector3 worldPoint)
    {
        if (pendingAmmoId == 0) return;
        int ammoId = pendingAmmoId;
        pendingAmmoId = 0;
        if (!IsPlayerUnavailable) SpawnMineOnServer(ammoId);
    }

    public NetworkObject SpawnMineOnServer(int ammoId)
    {
        if (!IsServer || !IsSpawned) return null;
        if (minePrefab == null || placementPoint == null)
        {
            Debug.LogError("[Char002ClaymoreSkill] Cannot place a mine without its prefab and placement point.", this);
            return null;
        }
        var ammo = DataManager.Instance != null ? DataManager.Instance.GetData<AmmoData>("Ammo", ammoId) : null;
        if (ammo == null || minePrefab.GetComponent<ClaymoreMine>() == null)
        {
            Debug.LogError($"[Char002ClaymoreSkill] Missing ClaymoreMine component or AmmoData {ammoId}.", this);
            return null;
        }
        // This root-relative point also works on a dedicated server that does not animate the hand.
        var mine = Instantiate(minePrefab, placementPoint.position, placementPoint.rotation);
        mine.transform.localScale = Vector3.Scale(minePrefab.transform.localScale, placementPoint.lossyScale);
        mine.Spawn(true);
        mine.GetComponent<ClaymoreMine>().Arm(ammo);
        return mine;
    }

    internal void OnSkillStateExit(int token)
    {
        // A cancelled state can exit while a replacement playback is entering.
        if (token == 0 || token != animationToken) return;
        if (!IsPlaying)
        {
            Cancel();
            return;
        }

        // The Animator's exit-time transition has already returned the base layer to Movement.
        Presenter.FinishSkill(animationToken);
        animationToken = 0;
        activeSkillId = 0;
        if (carriedMine != null) carriedMine.enabled = true;
        if (Player != null) Player.SetCinematicInputLocked(this, false);
    }

    public override void OnServerCancel() => pendingAmmoId = 0;

    public override void OnLocalAndServerCancel() => Cancel();

    protected override void OnPresentationInterrupted() => Cancel();

    public void Cancel()
    {
        int token = animationToken;
        int skillId = activeSkillId;
        animationToken = 0;
        activeSkillId = 0;
        // 전용 서버는 모델 애니메이션을 재생하지 않아도 예약된 설치를 취소해야 합니다.
        if (IsServer) pendingAmmoId = 0;
        if (token != 0)
        {
            if (!IsServer && IsOwner && IsSpawned)
                SkillRouter?.RequestCancelCharacterSkill(skillId);
            Presenter.CancelFullBody(token, true);
        }
        if (carriedMine != null) carriedMine.enabled = true;
        if (Player != null) Player.SetCinematicInputLocked(this, false);
    }

    private bool ValidateSkillData(SkillData data)
    {
        if (HandlesSkill(data)) return true;
        Debug.LogError($"[Char002ClaymoreSkill] Missing or invalid char002 claymore SkillData: {data?.id}.", this);
        return false;
    }

    private int GetAmmoId(SkillData data)
    {
        int ammoId = data.funcValue != null && data.funcValue.Length > 0 ? (int)data.funcValue[0] : 0;
        var ammo = DataManager.Instance != null ? DataManager.Instance.GetData<AmmoData>("Ammo", ammoId) : null;
        if (ammo != null && ammo.SearchRadius > 0f && ammo.SearchAngle > 0f) return ammoId;
        Debug.LogError($"[Char002ClaymoreSkill] SkillData {data.id} requires a valid AmmoData ID in funcValue[0]: {ammoId}.", this);
        return 0;
    }
}
