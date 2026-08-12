using UnityEngine;

/// <summary>
/// 모델링(자식 오브젝트)에 붙은 Animator의 애니메이션 이벤트를 
/// 최상위(부모) 오브젝트의 스크립트들로 전달해주는 프록시 클래스입니다.
/// </summary>
[RequireComponent(typeof(Animator))]
public class AnimationEventForwarder : MonoBehaviour
{
    /// <summary>
    /// [범용 방식] 애니메이션 이벤트에서 이 메서드를 선택하고, 
    /// String 파라미터 칸에 전달할 실제 함수 이름(예: "PerformMeleeAttack")을 적어주세요!
    /// </summary>
    public void ForwardEvent(string methodName)
    {
        if (string.IsNullOrEmpty(methodName)) return;
        
        // 주의: 이 스크립트 안에 methodName과 똑같은 이름의 함수가 있으면 무한 루프에 빠집니다!
        SendMessageUpwards(methodName, SendMessageOptions.DontRequireReceiver);
    }
}
