using Scavenger.Manager;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// char002 전용 지뢰 설치 요청과 서버 판정을 담당합니다.
/// 모델은 스폰 후 생성되므로 RPC 컴포넌트는 Player 프리팹 루트에 미리 둡니다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerController), typeof(PlayerSkillController), typeof(Health))]
public sealed class Char002ClaymoreSkillNetwork : NetworkBehaviour
{
    private PlayerController player;
    private PlayerSkillController skills;
    private Health health;
    private int pendingAmmoId;

    private bool IsUnavailable => player.IsDeathPresented || health.IsDead || health.CurrentHealth <= 0f
        || health.IsClearProtected || (GameFlowManager.Instance != null
            && GameFlowManager.Instance.CurrentState != GameResultState.None);

    private void Awake()
    {
        player = GetComponent<PlayerController>();
        skills = GetComponent<PlayerSkillController>();
        health = GetComponent<Health>();
    }

    private void OnEnable() => skills.ActiveActionsCancelled += ClearPendingPlacement;

    private void OnDisable()
    {
        skills.ActiveActionsCancelled -= ClearPendingPlacement;
        CancelPendingPlacement();
    }

    public override void OnNetworkDespawn() => pendingAmmoId = 0;

    private void ClearPendingPlacement() => pendingAmmoId = 0;

    private Char002ClaymoreSkill GetActiveSkill()
    {
        if (player.GetLocalDeck().GetCharId(player.ActiveSlotIndex) != 2) return null;
        var model = player.GetActiveCharacterModel();
        return model != null ? model.GetComponent<Char002ClaymoreSkill>() : null;
    }

    public bool TryUse(int skillId)
    {
        if (!isActiveAndEnabled || !IsOwner || !IsSpawned || IsUnavailable || player.IsInputLocked || skills.IsReloading()) return false;
        var skill = GetActiveSkill();
        if (skill == null)
        {
            Debug.LogError("[Char002ClaymoreSkillNetwork] Active character must be char002 with its claymore presentation.", this);
            return false;
        }
        var data = GetSkillData(skillId);
        if (data == null || GetAmmoId(data) == 0 || !skill.TryPlay(data.duration)) return false;
        BeginPlacementServerRpc(skillId);
        return true;
    }

    [ServerRpc]
    private void BeginPlacementServerRpc(int skillId)
    {
        if (pendingAmmoId != 0 || IsUnavailable) return;
        var data = GetSkillData(skillId);
        var skill = GetActiveSkill();
        if (data == null || skill == null) return;
        // 호스트 소유자는 이미 로컬 애니메이션을 시작하여 입력을 잠근 상태입니다.
        if (player.IsInputLocked && !skill.IsPlaying) return;
        pendingAmmoId = GetAmmoId(data);
        if (pendingAmmoId != 0) PlayPlacementClientRpc(data.duration);
    }

    [ClientRpc]
    private void PlayPlacementClientRpc(float duration)
    {
        if (IsOwner || IsUnavailable) return;
        var skill = GetActiveSkill();
        if (skill == null || !skill.TryPlay(duration))
            Debug.LogError("[Char002ClaymoreSkillNetwork] Cannot play the remote character's claymore animation.", this);
    }

    public void OnPlacementAnimationEvent()
    {
        if (!IsOwner || !IsSpawned || IsUnavailable) return;
        if (GetActiveSkill()?.IsPlaying == true) PlaceMineServerRpc();
    }

    [ServerRpc]
    private void PlaceMineServerRpc()
    {
        if (pendingAmmoId == 0) return;
        int ammoId = pendingAmmoId;
        pendingAmmoId = 0;
        if (!IsUnavailable) GetActiveSkill()?.SpawnMineOnServer(ammoId);
    }

    public void CancelPendingPlacement()
    {
        // 전용 서버는 모델 애니메이션을 재생하지 않아도 예약된 설치를 취소해야 합니다.
        if (IsServer) pendingAmmoId = 0;
        else if (IsOwner && IsSpawned) CancelPlacementServerRpc();
    }

    [ServerRpc]
    private void CancelPlacementServerRpc() => pendingAmmoId = 0;

    private SkillData GetSkillData(int skillId)
    {
        var data = DataManager.Instance != null ? DataManager.Instance.GetData<SkillData>("Skill", skillId) : null;
        if (data != null && data.charId == 2 && data.func == 7) return data;
        Debug.LogError($"[Char002ClaymoreSkillNetwork] Missing or invalid char002 claymore SkillData: {skillId}.", this);
        return null;
    }

    private int GetAmmoId(SkillData data)
    {
        int ammoId = data.funcValue != null && data.funcValue.Length > 0 ? (int)data.funcValue[0] : 0;
        var ammo = DataManager.Instance != null ? DataManager.Instance.GetData<AmmoData>("Ammo", ammoId) : null;
        if (ammo != null && ammo.SearchRadius > 0f && ammo.SearchAngle > 0f) return ammoId;
        Debug.LogError($"[Char002ClaymoreSkillNetwork] SkillData {data.id} requires a valid AmmoData ID in funcValue[0]: {ammoId}.", this);
        return 0;
    }
}
