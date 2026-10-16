using UnityEngine;

/// <summary>Notifies the claymore skill when its Base Layer state finishes exiting.</summary>
public sealed class Char002ClaymoreStateBehaviour : StateMachineBehaviour
{
    private int animationToken;

    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        animationToken = layerIndex == 0 && animator.TryGetComponent<Char002ClaymoreSkill>(out var skill)
            ? skill.AnimationToken : 0;
    }

    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (Application.isPlaying && layerIndex == 0
            && animator.TryGetComponent<Char002ClaymoreSkill>(out var skill))
            skill.OnSkillStateExit(animationToken);
    }
}
