using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TrainCharacterLevelingCost : MonoBehaviour
{
    [SerializeField] private Image itemIcon;
    [SerializeField] private TextMeshProUGUI amountText;

    [Header("Colors")]
    [SerializeField] private Color sufficientColor = Color.white;
    [SerializeField] private Color insufficientColor = Color.red;

    public void Setup(Sprite icon, int requiredAmount, int currentAmount)
    {
        if (itemIcon != null && icon != null)
        {
            itemIcon.sprite = icon;
        }

        if (amountText != null)
        {
            amountText.text = $"{requiredAmount} / {currentAmount}";
            amountText.color = currentAmount >= requiredAmount ? sufficientColor : insufficientColor;
        }
        
        gameObject.SetActive(true);
    }
}
