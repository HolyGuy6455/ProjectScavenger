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

        // item.Data.valueList에서 체력 회복량 추출
        // JSON 예시: { "key": 2001, "value": 30, "subValue": 0 }
        int healAmount = 0;
        if (item.Data.valueList != null && item.Data.valueList.Count > 0)
        {
            // 여기서는 임시로 첫 번째 value를 회복량으로 사용합니다. 
            // 필요에 따라 key값을 비교하여(예: key == 2001) 정확한 값을 가져올 수 있습니다.
            var healData = item.Data.valueList.FirstOrDefault(v => v.key == 2001);
            if (healData != null)
            {
                healAmount = healData.value;
            }
            else
            {
                // 2001(회복키)이 없다면 첫번째 값을 기본으로 사용
                healAmount = item.Data.valueList[0].value;
            }
        }
        
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
