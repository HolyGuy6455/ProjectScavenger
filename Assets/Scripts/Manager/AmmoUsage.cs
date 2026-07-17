using UnityEngine;

public class AmmoUsage : IItemUsage
{
    public bool IsConsumable => false; 
    public bool UseImmediately => false;

    public bool Execute(InventoryItem item, GameObject target)
    {
        // 공갈(Dummy) 클래스: 이제 총알은 인벤토리에서 클릭해서 사용(재장전)하지 않고 스킬 시스템을 통해 소모될 예정입니다.
        Debug.Log("총알은 인벤토리에서 직접 사용할 수 없습니다. 스킬을 통해 재장전하세요.");
        return false; // 소비되지 않음
    }
}
