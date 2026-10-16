/// <summary>
/// Temporary ultimate for characters whose cinematic is ready but gameplay effects are not.
/// BaseUltimateSkill plays the animation and camera; server events intentionally do nothing.
/// </summary>
public sealed class PresentationOnlyUltimateSkill : BaseUltimateSkill
{
    public override void OnServerExecute(SkillData data, int eventId)
    {
    }
}
