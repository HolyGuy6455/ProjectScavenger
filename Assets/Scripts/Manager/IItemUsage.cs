using UnityEngine;

public interface IItemUsage
{
    /// <summary>
    /// 아이템 수량이 0이 되었을 때 슬롯에서 삭제될지 여부
    /// </summary>
    bool IsConsumable { get; }

    /// <summary>
    /// 클릭 시 즉시 사용할지(true), 장착/선택만 할지(false) 여부
    /// </summary>
    bool UseImmediately { get; }

    /// <summary>
    /// 아이템 효과를 실행합니다.
    /// </summary>
    /// <param name="item">사용할 아이템 인스턴스</param>
    /// <param name="target">효과를 받을 대상 (현재는 플레이어 고정)</param>
    /// <returns>사용 성공 시 true (수량 감소), 실패 시 false</returns>
    bool Execute(InventoryItem item, GameObject target);
}
