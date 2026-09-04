using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CharacterLevelUpCelebrationPopup : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI levelAmount;
    [SerializeField] private Image characterStandingImage;

    public void Open(int newLevel, CharData charData)
    {
        if (charData == null) return;

        if (levelAmount != null)
        {
            levelAmount.text = newLevel.ToString(); // 혹은 기획에 맞춰 "Lv. " + newLevel 로 표시
        }

        if (characterStandingImage != null)
        {
            string standPath = System.IO.Path.GetFileNameWithoutExtension(charData.resourceStand);
            Sprite loadedSprite = Resources.Load<Sprite>($"char/{standPath}");
            
            if (loadedSprite == null) 
            {
                loadedSprite = Resources.Load<Sprite>(standPath);
            }

            if (loadedSprite != null)
            {
                characterStandingImage.sprite = loadedSprite;
                characterStandingImage.enabled = true;
            }
            else
            {
                characterStandingImage.enabled = false;
            }
        }

        gameObject.SetActive(true);
    }
}
