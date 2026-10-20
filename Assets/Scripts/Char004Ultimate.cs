using UnityEngine;

/// <summary>화염병의 점화 연출만 재생한 뒤 조준으로 넘깁니다. 투척은 조준 확정 후 별도로 실행합니다.</summary>
public sealed class Char004Ultimate : BaseUltimateSkill
{
    [SerializeField] private Renderer[] cinematicProps;
    private PlayerController Player => GetComponentInParent<PlayerController>();
    private Char004ThrowSkill ThrowSkill => GetComponentInParent<Char004ThrowSkill>();

    public override void OnServerBegin(SkillData data)
    {
        base.OnServerBegin(data);
        ThrowSkill?.MarkUltimatePreparedOnServer(data.id);
    }

    public override void OnClientExecute(SkillData data, bool isOwner)
    {
        if (IsPresentationPlaying) return;
        int previousVersion = PresentationVersion;
        base.OnClientExecute(data, isOwner);
        if (!IsPresentationPlaying || PresentationVersion == previousVersion)
        {
            if (isOwner) GetComponentInParent<SkillAimController>()?.ForceCancel();
            return;
        }
        Player?.SetCinematicInputLocked(this, true);
        SetCinematicPropsVisible(true);
    }

    // Ignition is visual only. This animation never launches a projectile.
    public override void OnServerExecute(SkillData data, int eventId) { }

    protected override void OnUltimateEndClient()
    {
        Player?.SetCinematicInputLocked(this, false);
        SetCinematicPropsVisible(false);
        ThrowSkill?.MarkUltimateReady(currentData.id);
    }

    protected override void OnUltimateCancelledClient()
    {
        Player?.SetCinematicInputLocked(this, false);
        SetCinematicPropsVisible(false);
        if (Player != null && Player.IsOwner) GetComponentInParent<SkillAimController>()?.ForceCancel();
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        Player?.SetCinematicInputLocked(this, false);
        SetCinematicPropsVisible(false);
    }

    private void SetCinematicPropsVisible(bool visible)
    {
        if (cinematicProps == null) return;
        foreach (var prop in cinematicProps)
            if (prop != null) prop.forceRenderingOff = !visible;
    }
}
