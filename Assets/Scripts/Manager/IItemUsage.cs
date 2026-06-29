using UnityEngine;

public interface IItemUsage
{
    /// <summary>
    /// 아이템 효과를 실행합니다.
    /// </summary>
    /// <param name="item">사용할 아이템 인스턴스</param>
    /// <param name="target">효과를 받을 대상 (현재는 플레이어 고정)</param>
    /// <returns>사용 성공 시 true (수량 감소), 실패 시 false</returns>
    bool Execute(InventoryItem item, GameObject target);
}
