using UnityEngine;

/// <summary>
/// 모든 몬스터 스킬의 기본이 되는 추상 클래스입니다.
/// </summary>
public abstract class EnemySkill : MonoBehaviour
{
    /// <summary>
    /// Behavior Tree에서 이 스킬을 찾을 때 사용할 고유 문자열 ID입니다.
    /// 자식 클래스에서 하드코딩으로 반환합니다. (예: return "Charge";)
    /// </summary>
    public abstract string SkillId { get; }

    /// <summary>
    /// 현재 스킬이 실행 중인지 여부를 나타냅니다.
    /// 이 값이 true인 동안 BT는 RUNNING 상태를 대기합니다.
    /// </summary>
    public bool IsExecuting { get; protected set; }

    /// <summary>
    /// BT에서 스킬을 발동시킬 때 호출하는 진입점입니다.
    /// </summary>
    /// <param name="targetObj">타겟(주로 플레이어) 게임오브젝트. 타겟이 없을 수도 있습니다.</param>
    /// <param name="targetPos">스킬을 시전할 목표 좌표(예: 장판기 위치, 돌진 목표 지점). 지정되지 않으면 Vector3.zero일 수 있습니다.</param>
    public abstract void ExecuteSkill(GameObject targetObj, Vector3 targetPos);
}
