using UnityEngine;

/// <summary>수프를 내미는 순간 자신의 편성 전체를 회복하고 전투 불능 캐릭터를 부활시킵니다.</summary>
public class Char03_Ultimate : BaseUltimateSkill
{
    private bool pendingRecovery;

    private void OnEnable()
    {
        var skills = GetComponentInParent<PlayerSkillController>();
        if (skills != null) skills.ActiveActionsCancelled += ClearPendingRecovery;
    }

    private void ClearPendingRecovery() => pendingRecovery = false;

    public override void OnServerBegin(SkillData data)
    {
        base.OnServerBegin(data);
        pendingRecovery = true;
    }

    public override void OnServerExecute(SkillData data, int eventId)
    {
        var player = GetComponentInParent<PlayerController>();
        if (player == null || !player.IsServer || !player.IsSpawned || eventId != 0 || !pendingRecovery) return;
        if (data == null || data.charId != 3 || data.func != 1
            || data.funcValue == null || data.funcValue.Length != 4
            || System.Array.Exists(data.funcValue, value => float.IsNaN(value) || float.IsInfinity(value) || value < 0f)
            || data.funcValue[3] <= 0f)
        {
            Debug.LogError("[Char03_Ultimate] SkillData에 체력·배고픔·목마름 회복량과 부활 체력을 지정하세요.", this);
            return;
        }

        pendingRecovery = false; // 같은 시전의 애니메이션 이벤트가 중복되어도 한 번만 적용합니다.
        player.RestoreParty(data.funcValue[0], data.funcValue[1], data.funcValue[2], data.funcValue[3]);
    }

    public override void OnClientExecute(SkillData data, bool isOwner)
    {
        int previousVersion = PresentationVersion;
        base.OnClientExecute(data, isOwner);
        if (IsPresentationPlaying && PresentationVersion != previousVersion)
            GetComponentInParent<PlayerController>()?.SetCinematicInputLocked(this, true);
    }

    protected override void OnUltimateEndClient()
        => GetComponentInParent<PlayerController>()?.SetCinematicInputLocked(this, false);

    protected override void OnUltimateCancelledClient()
    {
        ClearPendingRecovery();
        GetComponentInParent<PlayerController>()?.SetCinematicInputLocked(this, false);
    }

    protected override void OnDisable()
    {
        var skills = GetComponentInParent<PlayerSkillController>();
        if (skills != null) skills.ActiveActionsCancelled -= ClearPendingRecovery;
        ClearPendingRecovery();
        base.OnDisable();
        GetComponentInParent<PlayerController>()?.SetCinematicInputLocked(this, false);
    }
}
