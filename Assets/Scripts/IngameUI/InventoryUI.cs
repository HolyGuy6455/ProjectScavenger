using UnityEngine;
using System.Collections.Generic;

public class InventoryUI : MonoBehaviour
{
    [SerializeField] private List<InventorySlot> slots = new List<InventorySlot>();

    private void Start()
    {
        InitializeSlots();
        
        // PlayerInventory 이벤트 구독
        if (PlayerInventory.Instance != null)
        {
            PlayerInventory.Instance.OnInventoryChanged += RefreshUI;
            RefreshUI(); // 초기 상태 반영
        }
    }

    private void OnDestroy()
    {
        if (PlayerInventory.Instance != null)
        {
            PlayerInventory.Instance.OnInventoryChanged -= RefreshUI;
        }
    }

    private void InitializeSlots()
    {
        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i] != null)
            {
                slots[i].Setup(i);
            }
        }
    }

    private void RefreshUI()
    {
        if (PlayerInventory.Instance == null) return;

        for (int i = 0; i < slots.Count && i < PlayerInventory.MaxSlots; i++)
        {
            InventoryItem item = PlayerInventory.Instance.Items[i];
            bool isSelected = (PlayerInventory.Instance.SelectedSlotIndex == i);
            
            if (item != null && !item.IsEmpty)
            {
                Sprite iconSprite = SpriteManager.Instance.LoadSprite(item.Data.uiResource);
                slots[i].UpdateSlot(item, iconSprite, isSelected);
            }
            else
            {
                slots[i].UpdateSlot(null, null, false);
            }
        }
    }
}
