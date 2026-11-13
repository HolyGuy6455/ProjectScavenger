using UnityEngine;
using System.Collections.Generic;

public class InventoryUI : MonoBehaviour
{
    [SerializeField] private List<InventorySlot> slots = new List<InventorySlot>();
    private PlayerInventory _boundInventory;

    private void Start()
    {
        InitializeSlots();
        BindInventory(PlayerInventory.Instance);
    }

    private void Update()
    {
        if (_boundInventory != PlayerInventory.Instance)
        {
            BindInventory(PlayerInventory.Instance);
        }
    }

    private void OnDestroy()
    {
        if (_boundInventory != null)
        {
            _boundInventory.OnInventoryChanged -= RefreshUI;
            _boundInventory = null;
        }
    }

    private void BindInventory(PlayerInventory newInventory)
    {
        if (_boundInventory != null)
        {
            _boundInventory.OnInventoryChanged -= RefreshUI;
        }

        _boundInventory = newInventory;

        if (_boundInventory != null)
        {
            _boundInventory.OnInventoryChanged += RefreshUI;
        }

        RefreshUI();
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
        if (_boundInventory == null || _boundInventory.Items == null)
        {
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i] != null)
                {
                    slots[i].UpdateSlot(null, null, false);
                }
            }
            return;
        }

        for (int i = 0; i < slots.Count && i < PlayerInventory.MaxSlots; i++)
        {
            InventoryItem item = _boundInventory.Items[i];
            bool isSelected = (_boundInventory.SelectedSlotIndex == i);
            
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
