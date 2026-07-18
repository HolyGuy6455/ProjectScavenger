using UnityEngine;
using UnityEngine.UI;
using System;

public class CharSelectorSlot : MonoBehaviour
{
    [SerializeField] private Image iconImage;
    [SerializeField] private Button slotButton;
    [SerializeField] private PartyCharacterHealthBar healthBar;

    private CharData _charData;
    public event Action<CharData> OnSlotClickedEvent;

    public void Setup(CharData data, Sprite iconSprite)
    {
        _charData = data;
        
        if (healthBar != null)
        {
            healthBar.SetupCharacter(data.id);
        }

        
        if (iconImage != null)
        {
            iconImage.sprite = iconSprite;
            iconImage.enabled = iconSprite != null;
        }

        if (slotButton != null)
        {
            slotButton.onClick.RemoveAllListeners();
            slotButton.onClick.AddListener(OnClicked);
        }
    }

    private void OnClicked()
    {
        if (_charData != null)
        {
            Debug.Log($"[CharSelectorSlot] Character Selected: {_charData.name} ({_charData.id})");
            OnSlotClickedEvent?.Invoke(_charData);
        }
    }
}
