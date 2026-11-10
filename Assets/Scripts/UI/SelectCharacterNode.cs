using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Scavenger.UI
{
    public class SelectCharacterNode : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Image portraitImage;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI levelText;
        [SerializeField] private GameObject selectedOverlay;
        [SerializeField] private GameObject organizedOverlay;
        [SerializeField] private Button nodeButton;
        [SerializeField] private CanvasGroup canvasGroup;

        private CharData _charData;
        public CharData CharacterData => _charData;

        private int _level = 1;
        private bool _isSelected;
        private bool _isOrganized;
        private Action<SelectCharacterNode, CharData> _onSelectedCallback;

        private void Awake()
        {
            if (nodeButton == null)
            {
                nodeButton = GetComponentInChildren<Button>(true);
            }

            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
            }
        }

        private void OnEnable()
        {
            LocalizationManager.OnLanguageChanged += RefreshLocalizedTexts;
            RefreshLocalizedTexts();
        }

        private void OnDisable()
        {
            LocalizationManager.OnLanguageChanged -= RefreshLocalizedTexts;
        }

        public void Initialize(CharData data, Action<SelectCharacterNode, CharData> onSelected, bool isOrganized = false)
        {
            int level = 1;
            if (data != null && UserDataManager.Instance != null)
            {
                var userChar = UserDataManager.Instance.GetCharacter(data.id);
                if (userChar != null && userChar.level > 0)
                {
                    level = userChar.level;
                }
            }

            Initialize(data, level, onSelected, isOrganized);
        }

        public void Initialize(CharData data, int level, Action<SelectCharacterNode, CharData> onSelected, bool isOrganized = false)
        {
            _charData = data;
            _level = level;
            _onSelectedCallback = onSelected;
            _isOrganized = isOrganized;
            _isSelected = false;

            if (data == null)
            {
                SetEmpty();
                return;
            }

            gameObject.SetActive(true);
            SetVisible(true);

            if (portraitImage != null)
            {
                Sprite faceSprite = !string.IsNullOrEmpty(data.resourceSlot) && SpriteManager.Instance != null
                    ? SpriteManager.Instance.LoadSprite(data.resourceSlot)
                    : null;

                portraitImage.sprite = faceSprite;
                portraitImage.enabled = faceSprite != null;
            }

            RefreshLocalizedTexts();

            if (nodeButton == null)
            {
                nodeButton = GetComponentInChildren<Button>(true);
            }

            if (nodeButton != null)
            {
                nodeButton.onClick.RemoveAllListeners();
                nodeButton.onClick.AddListener(OnClickNode);
            }

            UpdateOverlayState();
        }

        private void RefreshLocalizedTexts()
        {
            if (_charData == null) return;

            if (nameText != null)
            {
                LocalizedText.ApplyFont(nameText);
                nameText.text = _charData.GetLocalizedName();
            }

            if (levelText != null)
            {
                LocalizedText.ApplyFont(levelText);
                levelText.text = LocalizationManager.Get("UI_FORMAT_LEVEL_COMPACT", _level);
            }
        }

        public void SetEmpty()
        {
            _charData = null;
            _onSelectedCallback = null;
            _isSelected = false;
            _isOrganized = false;
            if (nameText != null) nameText.text = string.Empty;
            if (levelText != null) levelText.text = string.Empty;
            UpdateOverlayState();
            gameObject.SetActive(true);
            SetVisible(false);
        }

        private void SetVisible(bool isVisible)
        {
            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
            }

            if (canvasGroup != null)
            {
                canvasGroup.alpha = isVisible ? 1f : 0f;
                canvasGroup.interactable = isVisible;
                canvasGroup.blocksRaycasts = isVisible;
            }
        }

        public void SetSelected(bool isSelected)
        {
            _isSelected = isSelected;
            UpdateOverlayState();
        }

        public void SetOrganized(bool isOrganized)
        {
            _isOrganized = isOrganized;
            UpdateOverlayState();
        }

        private void UpdateOverlayState()
        {
            if (selectedOverlay != null)
            {
                selectedOverlay.SetActive(_isSelected);
            }

            if (organizedOverlay != null)
            {
                organizedOverlay.SetActive(!_isSelected && _isOrganized);
            }
        }

        private void OnClickNode()
        {
            if (_charData != null)
            {
                _onSelectedCallback?.Invoke(this, _charData);
            }
        }
    }
}

