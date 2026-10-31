using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Scavenger.UI
{
    public class PartyPickCharNode : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Image portraitImage;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI levelText;
        [SerializeField] private GameObject selectedOverlay;
        [SerializeField] private GameObject organizedOverlay;
        [SerializeField] private Button nodeButton;

        private CharData _charData;
        public CharData CharacterData => _charData;

        private bool _isSelected;
        private bool _isOrganized;
        private Action<PartyPickCharNode, CharData> _onSelectedCallback;

        private void Awake()
        {
            if (nodeButton == null)
            {
                nodeButton = GetComponentInChildren<Button>(true);
            }
        }

        public void Initialize(CharData data, Action<PartyPickCharNode, CharData> onSelected, bool isOrganized = false)
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

        public void Initialize(CharData data, int level, Action<PartyPickCharNode, CharData> onSelected, bool isOrganized = false)
        {
            _charData = data;
            _onSelectedCallback = onSelected;
            _isOrganized = isOrganized;
            _isSelected = false;

            if (data == null)
            {
                SetEmpty();
                return;
            }

            gameObject.SetActive(true);

            if (portraitImage != null)
            {
                Sprite faceSprite = !string.IsNullOrEmpty(data.resourceSlot) && SpriteManager.Instance != null
                    ? SpriteManager.Instance.LoadSprite(data.resourceSlot)
                    : null;

                portraitImage.sprite = faceSprite;
                portraitImage.enabled = faceSprite != null;
            }

            if (nameText != null)
            {
                nameText.text = data.name;
            }

            if (levelText != null)
            {
                levelText.text = $"Lv.{level}";
            }

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

        public void SetEmpty()
        {
            _charData = null;
            _onSelectedCallback = null;
            _isSelected = false;
            _isOrganized = false;
            UpdateOverlayState();
            gameObject.SetActive(false);
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

