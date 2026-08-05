using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class PlayerInventory : NetworkBehaviour
{
    public static PlayerInventory Instance { get; private set; }

    public const int MaxSlots = 8;
    
    private Dictionary<int, CharacterInventory> charInventories = new Dictionary<int, CharacterInventory>();
    private CharacterInventory currentInventory;

    public InventoryItem[] Items => currentInventory?.Items;

    public event Action OnInventoryChanged;
    public event Action<int> OnSlotSelected;

    public int SelectedSlotIndex 
    { 
        get => currentInventory?.SelectedSlotIndex ?? -1; 
        private set 
        {
            if (currentInventory != null) currentInventory.SelectedSlotIndex = value;
        }
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        // 멀티플레이어 환경에서는 내 캐릭터(IsOwner)의 인벤토리만 Instance로 등록하여 로컬 UI가 참조하도록 합니다.
        if (IsOwner)
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[PlayerInventory] 로컬 플레이어 인벤토리가 이미 존재하여 기존 Instance를 덮어씁니다.");
            }
            Instance = this;
        }
    }

    private void OnEnable()
    {
        CharacterStateManager.Instance.OnCharacterChanged += HandleCharacterChanged;
    }

    private void OnDisable()
    {
        CharacterStateManager.Instance.OnCharacterChanged -= HandleCharacterChanged;
    }

    private void HandleCharacterChanged(CharData charData)
    {
        if (!charInventories.TryGetValue(charData.id, out CharacterInventory inv))
        {
            inv = new CharacterInventory(charData.id, MaxSlots);
            charInventories[charData.id] = inv;
            
            // Set current context first so AddItem works properly
            currentInventory = inv;

            // Initialize given items
            if (charData.item != null && charData.itemAmount != null)
            {
                for (int i = 0; i < charData.item.Length; i++)
                {
                    if (charData.item[i] != 0)
                    {
                        int amount = i < charData.itemAmount.Length ? charData.itemAmount[i] : 1;
                        AddItem(charData.item[i], amount); 
                    }
                }
            }
        }
        else
        {
            currentInventory = inv;
        }

        OnInventoryChanged?.Invoke();
        OnSlotSelected?.Invoke(SelectedSlotIndex);
    }

    public bool AddItemToSlot(int itemId, int totalQuantity, int targetSlotIndex)
    {
        if (targetSlotIndex < 0 || targetSlotIndex >= MaxSlots) 
            return AddItem(itemId, totalQuantity);

        ItemData data = DataManager.Instance.GetData<ItemData>("Item", itemId);
        if (data == null) return false;

        int remaining = totalQuantity;
        int safeMaxQuantity = data.maxQuantity > 0 ? data.maxQuantity : 1;

        InventoryItem targetItem = Items[targetSlotIndex];

        // 타겟 슬롯이 비어있는 경우
        if (targetItem == null || targetItem.IsEmpty)
        {
            int addAmount = Mathf.Min(safeMaxQuantity, remaining);
            Items[targetSlotIndex] = new InventoryItem(data, addAmount);
            remaining -= addAmount;
        }
        // 타겟 슬롯에 같은 아이템이 있는 경우 중첩
        else if (targetItem.Data.id == itemId)
        {
            int spaceLeft = safeMaxQuantity - targetItem.Quantity;
            if (spaceLeft > 0)
            {
                int addAmount = Mathf.Min(spaceLeft, remaining);
                targetItem.Quantity += addAmount;
                remaining -= addAmount;
            }
        }
        
        // 타겟 슬롯에 우선 넣고도 수량이 남았다면, 남은 수량은 빈자리/다른 슬롯에 분배
        if (remaining > 0)
        {
            // AddItem을 호출하면 OnInventoryChanged가 거기서 호출됩니다.
            return AddItem(itemId, remaining);
        }

        OnInventoryChanged?.Invoke();
        return true;
    }

    public bool AddItem(int itemId, int totalQuantity)
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
            if (Items[i] != null && !Items[i].IsEmpty && Items[i].Data.id == itemId)
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
        if (fromIndex == toIndex) return;

        InventoryItem source = Items[fromIndex];
        InventoryItem target = Items[toIndex];

        // 타겟 슬롯과 소스 슬롯에 같은 종류의 아이템이 있다면 중첩(Stacking)을 시도합니다.
        if (source != null && !source.IsEmpty && target != null && !target.IsEmpty && source.Data.id == target.Data.id)
        {
            int safeMax = source.Data.maxQuantity > 0 ? source.Data.maxQuantity : 1;
            int spaceLeft = safeMax - target.Quantity;
            
            if (spaceLeft > 0)
            {
                int moveAmount = Mathf.Min(spaceLeft, source.Quantity);
                target.Quantity += moveAmount;
                source.Quantity -= moveAmount;

                if (source.Quantity <= 0)
                {
                    Items[fromIndex] = null;
                }
                OnInventoryChanged?.Invoke();
                return;
            }
        }

        // 그 외의 경우 (다른 종류의 아이템이거나 꽉 찬 경우 등) 자리를 서로 맞바꿉니다.
        Items[fromIndex] = target;
        Items[toIndex] = source;

        // 장착된 슬롯의 위치가 바뀌면 SelectedSlotIndex도 따라갑니다.
        if (SelectedSlotIndex == fromIndex) SelectedSlotIndex = toIndex;
        else if (SelectedSlotIndex == toIndex) SelectedSlotIndex = fromIndex;

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

        bool anySuccess = false;
        bool anyConsumable = false;
        bool hasUsages = false;

        if (item.Data.func != null)
        {
            foreach (int func in item.Data.func)
            {
                if (func == 0) continue;
                hasUsages = true;

                IItemUsage usage = ItemUsageManager.Instance.GetUsage(func);
                if (usage == null)
                {
                    Debug.LogWarning($"[PlayerInventory] 알 수 없는 아이템 효과(func)입니다: {func}");
                    continue;
                }

                if (usage.UseImmediately)
                {
                    // 즉시 사용 (예: 감자)
                    bool success = usage.Execute(item, gameObject);
                    if (success)
                    {
                        anySuccess = true;
                        if (usage.IsConsumable) anyConsumable = true;
                    }
                }
                else
                {
                    /*
                    // 선택/장착 (예: 탄창)
                    if (SelectedSlotIndex == index)
                    {
                        // 이미 선택된 슬롯을 클릭하면 장착 해제
                        SelectedSlotIndex = -1;
                        Shooter shooter = GetComponent<Shooter>();
                        if (shooter != null) shooter.UnequipMagazine();
                        Debug.Log("탄창 장착이 해제되었습니다.");
                    }
                    else
                    {
                        // 새 슬롯 장착
                        SelectedSlotIndex = index;
                        usage.Execute(item, gameObject);
                    }

                    OnSlotSelected?.Invoke(SelectedSlotIndex);
                    OnInventoryChanged?.Invoke();
                    */
                }
            }
        }

        if (!hasUsages)
        {
            return;
        }

        if (anySuccess)
        {
            item.Quantity--;
            
            if (item.Quantity <= 0 && anyConsumable)
            {
                Items[index] = null;
            }

            OnInventoryChanged?.Invoke();
        }
    }

    /// <summary>
    /// 외부(예: Shooter)에서 수량을 소모하고 UI를 갱신할 때 호출합니다.
    /// </summary>
    public void NotifyItemChanged(int index)
    {
        if (index < 0 || index >= MaxSlots) return;

        InventoryItem item = Items[index];
        if (item != null && item.Quantity <= 0)
        {
            bool isConsumable = false;
            if (item.Data.func != null)
            {
                foreach (int func in item.Data.func)
                {
                    if (func == 0) continue;
                    IItemUsage usage = ItemUsageManager.Instance?.GetUsage(func);
                    if (usage != null && usage.IsConsumable)
                    {
                        isConsumable = true;
                        break;
                    }
                }
            }

            if (isConsumable)
            {
                Items[index] = null;
                if (SelectedSlotIndex == index) SelectedSlotIndex = -1;
            }
        }
        OnInventoryChanged?.Invoke();
    }

    /// <summary>
    /// 지정된 슬롯의 아이템을 전부 월드(바닥)로 버립니다(생성합니다).
    /// </summary>
    public void DropItem(int index, Vector3 dropPosition)
    {
        if (index < 0 || index >= MaxSlots) return;

        InventoryItem item = Items[index];
        if (item == null || item.IsEmpty) return;

        if (ItemInstanceManager.Instance != null)
        {
            ItemInstanceManager.Instance.SpawnFieldItem(item.Data.id, item.Quantity, dropPosition);
        }

        // 슬롯 비우기
        Items[index] = null;
        if (SelectedSlotIndex == index) SelectedSlotIndex = -1;

        OnInventoryChanged?.Invoke();
    }
}
