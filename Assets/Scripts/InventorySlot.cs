using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class InventorySlot : MonoBehaviour
{
    [SerializeField] private Image iconImage;
    [SerializeField] private TextMeshProUGUI quantityText;
    [SerializeField] private Button slotButton;

    public int SlotIndex { get; private set; }

    private RectTransform rectTransform;
    private float originalY;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        if (rectTransform != null)
        {
            originalY = rectTransform.anchoredPosition.y;
        }
    }

    public void Setup(int index)
    {
        SlotIndex = index;
        if (slotButton != null)
        {
            slotButton.onClick.RemoveAllListeners();
            slotButton.onClick.AddListener(OnSlotClicked);
        }
    }

    private void OnSlotClicked()
    {
        Debug.Log($"Inventory Slot {SlotIndex} clicked!");
        if (PlayerInventory.Instance != null)
        {
            PlayerInventory.Instance.UseItem(SlotIndex);
        }
    }

    public void UpdateSlot(InventoryItem item, Sprite sprite, bool isSelected)
    {
        if (item == null || item.IsEmpty)
        {
            if (iconImage != null)
            {
                iconImage.sprite = null;
                iconImage.enabled = false;
            }
            if (quantityText != null)
            {
                quantityText.text = "";
            }
            SetSelected(false);
            return;
        }

        if (iconImage != null)
        {
            iconImage.sprite = sprite;
            iconImage.enabled = sprite != null;
        }

        if (quantityText != null)
        {
            UpdateQuantityText(item);
        }
        
        SetSelected(isSelected);
    }

    private void SetSelected(bool isSelected)
    {
        if (rectTransform != null)
        {
            float targetY = isSelected ? originalY + 20f : originalY;
            rectTransform.anchoredPosition = new Vector2(rectTransform.anchoredPosition.x, targetY);
        }
    }

    private void UpdateQuantityText(InventoryItem item)
    {
        QuantityShowType showType = item.Data.QuantityShowType;

        if (showType == QuantityShowType.None)
        {
            quantityText.text = "";
        }
        else if (showType == QuantityShowType.Integer)
        {
            quantityText.text = item.Quantity.ToString();
            quantityText.color = Color.white;
        }
        else if (showType == QuantityShowType.Fraction)
        {
            int quantity = item.Quantity;
            int max = item.Data.maxQuantity;
            
            quantityText.text = $"{quantity} / {max}";

            if (max > 0)
            {
                float ratio = (float)quantity / max;
                if (ratio <= 0.1f)
                {
                    quantityText.color = new Color(1f, 0.4f, 0.4f); // 밝은 빨간색
                }
                else if (ratio <= 0.333f)
                {
                    quantityText.color = new Color(1f, 1f, 0.6f); // 옅은 노란색
                }
                else
                {
                    quantityText.color = Color.white;
                }
            }
            else
            {
                quantityText.color = Color.white;
            }
        }
    }
}
