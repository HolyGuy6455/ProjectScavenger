using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using Scavenger.Network.DTOs;

public class TrainCharacterPolaroid : MonoBehaviour
{
    [SerializeField] private Image characterIcon;
    [SerializeField] private TextMeshProUGUI levelText;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private Button polaroidButton;

    private UserCharacterDto _characterData;
    private CharData _masterData;
    
    public event Action<UserCharacterDto, CharData> OnPolaroidClicked;

    public void Setup(UserCharacterDto charData, CharData masterData, Sprite iconSprite)
    {
        _characterData = charData;
        _masterData = masterData;

        if (characterIcon != null)
        {
            characterIcon.sprite = iconSprite;
            characterIcon.enabled = iconSprite != null;
        }

        if (levelText != null) 
        {
            levelText.text = $"Lv.{charData.level}";
        }

        if (nameText != null)
        {
            nameText.text = masterData.name;
        }

        if (polaroidButton != null)
        {
            polaroidButton.onClick.RemoveAllListeners();
            polaroidButton.onClick.AddListener(OnClicked);
        }
    }

    private void OnClicked()
    {
        if (_characterData != null && _masterData != null)
        {
            OnPolaroidClicked?.Invoke(_characterData, _masterData);
        }
    }
}
