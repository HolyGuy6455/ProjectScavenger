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
                Sprite iconSprite = LoadSprite(item.Data.uiResource);
                slots[i].UpdateSlot(item, iconSprite, isSelected);
            }
            else
            {
                slots[i].UpdateSlot(null, null, false);
            }
        }
    }

    private Sprite LoadSprite(string resourceName)
    {
        // 파일명에서 확장자 제거
        string nameWithoutExt = System.IO.Path.GetFileNameWithoutExtension(resourceName);
        
        // Resources/Items 폴더에 해당 이미지가 있다고 가정하고 로드
        Sprite loadedSprite = Resources.Load<Sprite>($"Items/{nameWithoutExt}");
        
        // 만약 못 찾았다면 기존처럼 루트 폴더에서도 한번 찾아봅니다
        if (loadedSprite == null)
        {
            loadedSprite = Resources.Load<Sprite>(nameWithoutExt);
        }
        
        if (loadedSprite == null)
        {
            // Debug.LogWarning($"[InventoryUI] Icon not found: {nameWithoutExt}");
        }
        
        return loadedSprite;
    }
}
