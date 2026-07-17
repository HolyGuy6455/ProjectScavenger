using UnityEngine;
using System.Linq;

public class FoodUsage : IItemUsage
{
    public bool IsConsumable => true;
    public bool UseImmediately => true;

    public bool Execute(InventoryItem item, GameObject target)
    {
        if (target == null) return false;

        Health health = target.GetComponent<Health>();
        if (health == null)
        {
            Debug.LogWarning("[FoodUsage] Target does not have a Health component.");
            return false;
        }

        if (health.CurrentHealth >= health.MaxHealth)
        {
            Debug.Log("체력이 이미 가득 차 있어 감자를 먹을 수 없습니다!");
            return false;
        }

        // item.Data.value1에서 체력 회복량 추출
        int healAmount = item.Data.value1;

        if (healAmount <= 0)
        {
            Debug.LogWarning("[FoodUsage] 회복량 데이터가 없습니다.");
            return false;
        }

        health.Heal(healAmount);
        Debug.Log($"감자를 먹고 체력을 {healAmount}만큼 회복했습니다! (현재 체력: {health.CurrentHealth}/{health.MaxHealth})");
        
        return true; // 성공적으로 사용됨
    }
}
