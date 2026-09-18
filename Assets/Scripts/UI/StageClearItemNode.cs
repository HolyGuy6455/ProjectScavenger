using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Scavenger.UI
{
    public class StageClearItemNode : MonoBehaviour
    {
        [SerializeField] private Image itemIcon;
        [SerializeField] private TextMeshProUGUI countText;
        [SerializeField] private GameObject bonusTag;
        [SerializeField] private GameObject glowEffect;

        public void Setup(ItemData itemData, int count, bool isBonus)
        {
            if (itemData != null && itemIcon != null)
            {
                // Resource loading logic
                Sprite sprite = Resources.Load<Sprite>(itemData.uiResource);
                if (sprite != null)
                {
                    itemIcon.sprite = sprite;
                    itemIcon.enabled = true;
                }
                else
                {
                    itemIcon.enabled = false;
                }
            }

            if (countText != null)
            {
                countText.text = count.ToString();
            }

            if (bonusTag != null)
            {
                bonusTag.SetActive(isBonus);
            }

            if (glowEffect != null)
            {
                glowEffect.SetActive(isBonus);
            }
        }
    }
}
