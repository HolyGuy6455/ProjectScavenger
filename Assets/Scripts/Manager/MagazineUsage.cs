using UnityEngine;

public class MagazineUsage : IItemUsage
{
    public bool IsConsumable => false; // 수량이 0이 되어도 빈 탄창으로 남음
    public bool UseImmediately => true; // 클릭 시 즉시 사용(탄창 교체)됨

    public bool Execute(InventoryItem item, GameObject target)
    {
        if (target == null) return false;

        Shooter shooter = target.GetComponent<Shooter>();
        if (shooter == null)
        {
            Debug.LogWarning("[MagazineUsage] Target does not have a Shooter component.");
            return false;
        }

        if (shooter.CurrentGunData == null)
        {
            Debug.LogWarning("[MagazineUsage] Shooter does not have an equipped gun.");
            return false;
        }

        // 총기의 탄약과 인벤토리의 탄창 탄약을 맞바꿈
        int tempAmmo = shooter.CurrentAmmo;
        shooter.CurrentAmmo = item.Quantity;
        item.Quantity = tempAmmo;

        Debug.Log($"[MagazineUsage] 탄창 교환 완료. Shooter 잔탄: {shooter.CurrentAmmo}, 인벤토리 탄창 잔탄: {item.Quantity}");

        return true;
    }
}
