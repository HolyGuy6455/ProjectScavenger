using UnityEngine;

/// <summary>상반신 투척이 Idle로 전환될 때 소품과 입력 상태를 정리합니다.</summary>
public sealed class Char004ThrowStateExit : StateMachineBehaviour
{
    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        => animator.GetComponent<Char004ThrowAnimation>()?.OnThrowStateExit();
}
