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
            
            if (item != null && !item.IsEmpty)
            {
                Sprite iconSprite = LoadSprite(item.Data.uiResource);
                slots[i].UpdateSlot(item, iconSprite);
            }
            else
            {
                slots[i].UpdateSlot(null, null);
            }
        }
    }

    private Sprite LoadSprite(string resourceName)
    {
        // 파일명에서 확장자 제거
        string nameWithoutExt = System.IO.Path.GetFileNameWithoutExtension(resourceName);
        
        // Resources 폴더에 해당 이미지가 있다고 가정하고 로드 (예: Resources/item_stagnag)
        Sprite loadedSprite = Resources.Load<Sprite>(nameWithoutExt);
        
        if (loadedSprite == null)
        {
            // Debug.LogWarning($"[InventoryUI] Icon not found: {nameWithoutExt}");
        }
        
        return loadedSprite;
    }
}
