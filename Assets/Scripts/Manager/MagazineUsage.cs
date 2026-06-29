using UnityEngine;

public class MagazineUsage : IItemUsage
{
    public bool IsConsumable => false; // 수량이 0이 되어도 빈 탄창으로 남음
    public bool UseImmediately => false; // 클릭 시 장착(선택)만 됨

    public bool Execute(InventoryItem item, GameObject target)
    {
        if (target == null) return false;

        Shooter shooter = target.GetComponent<Shooter>();
        if (shooter == null)
        {
            Debug.LogWarning("[MagazineUsage] Target does not have a Shooter component.");
            return false;
        }

        PlayerInventory inventory = target.GetComponent<PlayerInventory>();
        if (inventory != null)
        {
            shooter.EquipMagazine(item, inventory.SelectedSlotIndex);
        }
        else
        {
            // 예외 상황: PlayerInventory가 없으면 인덱스 -1로 장착
            shooter.EquipMagazine(item, -1);
        }

        return true;
    }
}
