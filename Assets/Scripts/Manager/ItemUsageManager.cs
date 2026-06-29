using System.Collections.Generic;
using UnityEngine;

public class ItemUsageManager : MonoBehaviour
{
    public static ItemUsageManager Instance { get; private set; }

    // func 이름과 효과 구현체를 매핑하는 딕셔너리
    private Dictionary<string, IItemUsage> effectDictionary;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        InitializeEffects();
    }

    private void InitializeEffects()
    {
        effectDictionary = new Dictionary<string, IItemUsage>();

        // 여기에 새로운 아이템 효과(func)가 생길 때마다 추가해주면 됩니다.
        effectDictionary.Add("food", new FoodUsage());
        effectDictionary.Add("magazine", new MagazineUsage());
    }

    /// <summary>
    /// 아이템의 func 값에 따라 적절한 효과를 실행합니다.
    /// </summary>
    public bool ExecuteItemUsage(InventoryItem item, GameObject target)
    {
        if (item == null || item.IsEmpty) return false;

        string func = item.Data.func;
        
        if (effectDictionary.TryGetValue(func, out IItemUsage effect))
        {
            return effect.Execute(item, target);
        }
        else
        {
            Debug.LogWarning($"[ItemUsageManager] 알 수 없는 아이템 효과(func)입니다: {func}");
            return false;
        }
    }

    public IItemUsage GetUsage(string func)
    {
        if (effectDictionary.TryGetValue(func, out IItemUsage effect))
        {
            return effect;
        }
        return null;
    }
}
