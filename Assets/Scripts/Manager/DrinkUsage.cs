using UnityEngine;
using System.Linq;

public class DrinkUsage : IItemUsage
{
    public bool IsConsumable => true;
    public bool UseImmediately => true;

    public bool Execute(InventoryItem item, GameObject target)
    {
        if (target == null) return false;

        if (CharacterStateManager.Instance == null)
        {
            Debug.LogWarning("[DrinkUsage] CharacterStateManager is missing.");
            return false;
        }

        int charId = CharacterStateManager.Instance.CurrentCharId;
        var state = CharacterStateManager.Instance.GetState(charId);
        
        if (state == null)
        {
            Debug.LogWarning("[DrinkUsage] CharacterState not found.");
            return false;
        }

        if (state.currentThirst >= state.maxThirst)
        {
            Debug.Log("목마름이 이미 가득 차 있어 마실 수 없습니다!");
            return false;
        }

        // item.Data.value에서 목마름 회복량 추출 (func == 1 인 인덱스 찾기)
        int healAmount = 0;
        if (item.Data.func != null && item.Data.value != null)
        {
            for (int i = 0; i < item.Data.func.Length; i++)
            {
                if (item.Data.func[i] == 1)
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
            Debug.LogWarning("[DrinkUsage] 회복량 데이터가 없습니다.");
            return false;
        }

        CharacterStateManager.Instance.ModifyCurrentCharacterThirst(healAmount);
        Debug.Log($"음료를 마시고 목마름을 {healAmount}만큼 회복했습니다! (현재 목마름: {state.currentThirst}/{state.maxThirst})");
        
        return true; // 성공적으로 사용됨
    }
}
