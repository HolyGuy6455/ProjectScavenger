using UnityEngine;

/// <summary>
/// 유니티짱 애니메이션 이벤트(OnCallChangeFace 등)를 흡수하여
/// 에러 로그가 출력되지 않도록 하는 더미 스크립트입니다.
/// 반드시 Animator 컴포넌트가 있는 게임 오브젝트에 부착해야 합니다.
/// </summary>
public class UnityChanEventCatcher : MonoBehaviour
{
    public void OnCallChangeFace(string str)
    {
        // 표정 변경 로직 (현재는 비워둠)
    }
}
