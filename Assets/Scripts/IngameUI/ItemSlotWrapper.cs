using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Scavenger.UI;

public class ItemSlotWrapper : MonoBehaviour
{
    [SerializeField] private Image itemIcon;
    [SerializeField] private TextMeshProUGUI itemNameText;
    [SerializeField] private TextMeshProUGUI itemAmountText;
    [SerializeField] private UI_ItemInfoButton itemInfoButton;

    [Header("Colors")]
    [SerializeField] private Color sufficientColor = Color.white;
    [SerializeField] private Color insufficientColor = Color.red;

    public void SetData(Sprite icon, string name, int requiredAmount, int currentAmount, int itemId = 0)
    {
        itemInfoButton?.SetItemId(itemId);

        if (itemIcon != null && icon != null)
        {
            itemIcon.sprite = icon;
            itemIcon.enabled = true;
        }

        if (itemNameText != null)
        {
            LocalizedText.ApplyFont(itemNameText);
            itemNameText.text = name ?? "";
        }

        if (itemAmountText != null)
        {
            // (필요 수량) / (현재 수량)
            itemAmountText.text = $"{requiredAmount} / {currentAmount}";
            
            if (currentAmount >= requiredAmount)
            {
                itemAmountText.color = sufficientColor;
            }
            else
            {
                itemAmountText.color = insufficientColor;
            }
        }
    }

    public void SetRewardData(Sprite icon, int amount, int itemId = 0)
    {
        itemInfoButton?.SetItemId(itemId);

        if (itemIcon != null)
        {
            itemIcon.sprite = icon;
            itemIcon.enabled = icon != null;
        }

        if (itemAmountText != null)
        {
            itemAmountText.text = amount.ToString("N0");
            itemAmountText.color = sufficientColor;
        }
    }

    public void SetIconOnlyData(Sprite icon, int itemId = 0)
    {
        itemInfoButton?.SetItemId(itemId);

        if (itemIcon != null)
        {
            itemIcon.sprite = icon;
            itemIcon.enabled = icon != null;
        }

        if (itemNameText != null)
        {
            itemNameText.gameObject.SetActive(false);
        }

        if (itemAmountText != null)
        {
            itemAmountText.gameObject.SetActive(false);
        }
    }
}
