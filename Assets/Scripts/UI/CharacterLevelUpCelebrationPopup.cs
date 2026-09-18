using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

public class CharacterLevelUpCelebrationPopup : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI levelAmount;
    [SerializeField] private Image characterStandingImage;
    [SerializeField] private Button closeButton;

    private Action _onCloseCallback;

    private void Awake()
    {
        if (closeButton != null)
        {
            closeButton.onClick.AddListener(OnCloseClicked);
        }
    }

    private void OnDestroy()
    {
        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(OnCloseClicked);
        }
    }

    public void Open(int newLevel, CharData charData, Action onCloseCallback = null)
    {
        if (charData == null) return;
        
        _onCloseCallback = onCloseCallback;

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

    private void OnCloseClicked()
    {
        if (Scavenger.UI.OutGameUIManager.Instance != null)
        {
            Scavenger.UI.OutGameUIManager.Instance.ClosePopup(gameObject);
        }
        else
        {
            gameObject.SetActive(false);
        }
        _onCloseCallback?.Invoke();
    }
}
