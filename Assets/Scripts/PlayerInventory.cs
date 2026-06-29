using System;
using UnityEngine;

public class PlayerInventory : MonoBehaviour
{
    public static PlayerInventory Instance { get; private set; }

    public const int MaxSlots = 8;
    public InventoryItem[] Items { get; private set; } = new InventoryItem[MaxSlots];

    public event Action OnInventoryChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        // 임시로 아이템 지급 (테스트용)
        AddItem("item_001", 1);
        AddItem("item_002", 3);
    }

    public bool AddItem(string itemId, int totalQuantity)
    {
        ItemData data = DataManager.Instance.GetData<ItemData>("Item", itemId);
        if (data == null)
        {
            Debug.LogWarning($"[PlayerInventory] Item '{itemId}' not found in DataManager.");
            return false;
        }

        int remaining = totalQuantity;

        // 1단계: 이미 존재하는 동일 아이템의 슬롯 중 빈 자리가 있는지 확인하여 보충
        for (int i = 0; i < MaxSlots; i++)
        {
            if (Items[i] != null && !Items[i].IsEmpty && Items[i].Data.itemId == itemId)
            {
                int safeMaxQuantity = data.maxQuantity > 0 ? data.maxQuantity : 1;
                int spaceLeft = safeMaxQuantity - Items[i].Quantity;
                if (spaceLeft > 0)
                {
                    int addAmount = Mathf.Min(spaceLeft, remaining);
                    Items[i].Quantity += addAmount;
                    remaining -= addAmount;

                    if (remaining <= 0)
                    {
                        OnInventoryChanged?.Invoke();
                        return true;
                    }
                }
            }
        }

        // 2단계: 빈 슬롯을 찾아 남은 수량(remaining)을 새 인스턴스로 분배
        if (ItemInstanceManager.Instance == null)
        {
            Debug.LogError("[PlayerInventory] ItemInstanceManager is missing in the scene!");
            return false;
        }

        var newInstances = ItemInstanceManager.Instance.CreateItemInstancesByTotalQuantity(itemId, remaining);
        int instanceIndex = 0;

        for (int i = 0; i < MaxSlots; i++)
        {
            if (Items[i] == null || Items[i].IsEmpty)
            {
                Items[i] = newInstances[instanceIndex];
                instanceIndex++;
                
                if (instanceIndex >= newInstances.Count)
                {
                    OnInventoryChanged?.Invoke();
                    return true;
                }
            }
        }

        // 3단계: 슬롯이 부족해서 다 넣지 못함
        Debug.LogWarning($"[PlayerInventory] Inventory full! Added {instanceIndex} out of {newInstances.Count} stacks. The rest are lost.");
        OnInventoryChanged?.Invoke();
        return false;
    }

    public void MoveItem(int fromIndex, int toIndex)
    {
        if (fromIndex < 0 || fromIndex >= MaxSlots || toIndex < 0 || toIndex >= MaxSlots) return;

        InventoryItem temp = Items[fromIndex];
        Items[fromIndex] = Items[toIndex];
        Items[toIndex] = temp;

        OnInventoryChanged?.Invoke();
    }

    /// <summary>
    /// 지정된 인덱스의 아이템을 사용합니다.
    /// </summary>
    public void UseItem(int index)
    {
        if (index < 0 || index >= MaxSlots) return;

        InventoryItem item = Items[index];
        if (item == null || item.IsEmpty) return;

        if (ItemUsageManager.Instance == null)
        {
            Debug.LogError("[PlayerInventory] ItemUsageManager is missing in the scene!");
            return;
        }

        // 아이템 사용 효과 실행 (타겟은 플레이어 본인, gameObject 사용)
        bool success = ItemUsageManager.Instance.ExecuteItemUsage(item, gameObject);

        if (success)
        {
            item.Quantity--;
            
            if (item.Quantity <= 0)
            {
                // 다 썼으면 슬롯 비우기
                Items[index] = null;
            }

            OnInventoryChanged?.Invoke();
        }
    }
}
