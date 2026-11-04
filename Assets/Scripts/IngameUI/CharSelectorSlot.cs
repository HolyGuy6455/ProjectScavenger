using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections.Generic;

public class CharSelectorSlot : MonoBehaviour
{
    [SerializeField] private Image iconImage;
    [SerializeField] private Button slotButton;
    [SerializeField] private PartyCharacterHealthBar healthBar;
    [SerializeField] private List<BuffSlotIconUI> buffIcons = new List<BuffSlotIconUI>();

    private CharData _charData;
    private int _slotIndex = -1;
    private readonly List<Buff> _displayBuffBuffer = new List<Buff>(BuffController.MaxDisplayedBuffs);

    public int SlotIndex => _slotIndex;
    public event Action<CharData> OnSlotClickedEvent;

    public void Setup(CharData data, Sprite iconSprite, int slotIndex = -1)
    {
        _charData = data;
        _slotIndex = slotIndex;
        
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

        ClearBuffIcons();
    }

    public void RefreshBuffIcons(BuffController buffController)
    {
        if (buffIcons == null || buffIcons.Count == 0) return;

        if (buffController == null || _slotIndex < 0)
        {
            ClearBuffIcons();
            return;
        }

        buffController.GetDisplayBuffs(_slotIndex, _displayBuffBuffer, Mathf.Min(BuffController.MaxDisplayedBuffs, buffIcons.Count));

        for (int i = 0; i < buffIcons.Count; i++)
        {
            if (buffIcons[i] == null) continue;

            if (i < _displayBuffBuffer.Count)
            {
                buffIcons[i].Bind(_displayBuffBuffer[i]);
            }
            else
            {
                buffIcons[i].Clear();
            }
        }
    }

    public void ClearBuffIcons()
    {
        if (buffIcons == null) return;
        for (int i = 0; i < buffIcons.Count; i++)
        {
            if (buffIcons[i] != null)
            {
                buffIcons[i].Clear();
            }
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
