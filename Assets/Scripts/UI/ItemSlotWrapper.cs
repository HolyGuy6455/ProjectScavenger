using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ItemSlotWrapper : MonoBehaviour
{
    [SerializeField] private Image itemIcon;
    [SerializeField] private TextMeshProUGUI itemNameText;
    [SerializeField] private TextMeshProUGUI itemAmountText;

    [Header("Colors")]
    [SerializeField] private Color sufficientColor = Color.white;
    [SerializeField] private Color insufficientColor = Color.red;

    public void SetData(Sprite icon, string name, int requiredAmount, int currentAmount)
    {
        if (itemIcon != null && icon != null)
        {
            itemIcon.sprite = icon;
        }

        if (itemNameText != null)
        {
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
}
