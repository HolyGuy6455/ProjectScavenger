using UnityEngine;
using System.Linq;

public class FoodUsage : IItemUsage
{
    public bool IsConsumable => true;
    public bool UseImmediately => true;

    public bool Execute(InventoryItem item, GameObject target)
    {
        if (target == null) return false;

        if (CharacterStateManager.Instance == null)
        {
            Debug.LogWarning("[FoodUsage] CharacterStateManager is missing.");
            return false;
        }

        int charId = CharacterStateManager.Instance.CurrentCharId;
        var state = CharacterStateManager.Instance.GetState(charId);
        
        if (state == null)
        {
            Debug.LogWarning("[FoodUsage] CharacterState not found.");
            return false;
        }

        if (state.currentHunger >= state.maxHunger)
        {
            Debug.Log("배고픔이 이미 가득 차 있어 감자를 먹을 수 없습니다!");
            return false;
        }

        // item.Data.value에서 배고픔 회복량 추출 (func == 2 인 인덱스 찾기)
        int healAmount = 0;
        if (item.Data.func != null && item.Data.value != null)
        {
            for (int i = 0; i < item.Data.func.Length; i++)
            {
                if (item.Data.func[i] == 2)
                {
                    if (i < item.Data.value.Length)
                    {
                        healAmount += item.Data.value[i];
                    }
                }
            }
        }

        if (healAmount <= 0)
        {
            Debug.LogWarning("[FoodUsage] 회복량 데이터가 없습니다.");
            return false;
        }

        CharacterStateManager.Instance.ModifyCurrentCharacterHunger(healAmount);
        Debug.Log($"감자를 먹고 배고픔을 {healAmount}만큼 회복했습니다! (현재 배고픔: {state.currentHunger}/{state.maxHunger})");
        
        return true; // 성공적으로 사용됨
    }
}
