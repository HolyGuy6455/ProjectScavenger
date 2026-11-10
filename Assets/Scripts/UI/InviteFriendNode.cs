using System;
using Scavenger.Network;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Scavenger.UI
{
    public class InviteFriendNode : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private GameObject selectedOverlay;
        [SerializeField] private Image profileImage;
        [SerializeField] private TextMeshProUGUI friendNameText;
        [SerializeField] private TextMeshProUGUI friendLevelText;
        [SerializeField] private Image[] deckMemberImages;
        [SerializeField] private Button selectButton;

        private Action<InviteFriendNode> _onSelectedCallback;
        private int _level;

        public UserInfo TargetUser { get; private set; }
        public string ConnectionId => TargetUser != null ? TargetUser.connectionId : string.Empty;
        public bool IsSelected { get; private set; }
        public string FriendName => TargetUser != null && !string.IsNullOrEmpty(TargetUser.username)
            ? TargetUser.username
            : (friendNameText != null ? friendNameText.text : string.Empty);
        public Image ProfileImage => profileImage;
        public TextMeshProUGUI FriendNameText => friendNameText;
        public TextMeshProUGUI FriendLevelText => friendLevelText;
        public Image[] DeckMemberImages => deckMemberImages;

        private void Awake()
        {
            if (selectButton == null)
            {
                selectButton = GetComponentInChildren<Button>(true);
            }

            if (selectButton != null)
            {
                selectButton.onClick.RemoveListener(OnClickNode);
                selectButton.onClick.AddListener(OnClickNode);
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

        public void BindSelectionCallback(Action<InviteFriendNode> onSelected)
        {
            _onSelectedCallback = onSelected;
        }

        public void Setup(UserInfo user, Action<InviteFriendNode> onSelected = null)
        {
            TargetUser = user;
            _level = user != null && user.level > 0 ? user.level : 1;
            if (onSelected != null)
            {
                _onSelectedCallback = onSelected;
            }

            if (friendNameText != null)
            {
                LocalizedText.ApplyFont(friendNameText);
                friendNameText.text = user != null && !string.IsNullOrEmpty(user.username)
                    ? user.username
                    : string.Empty;
            }

            Sprite firstValidSprite = null;
            if (deckMemberImages != null)
            {
                for (int i = 0; i < deckMemberImages.Length; i++)
                {
                    if (deckMemberImages[i] == null) continue;

                    int charId = (user != null && user.selectedCharIds != null && i < user.selectedCharIds.Count)
                        ? user.selectedCharIds[i]
                        : 0;

                    Sprite icon = null;
                    if (charId > 0 && DataManager.Instance != null)
                    {
                        var charData = DataManager.Instance.GetData<CharData>("Char", charId);
                        if (charData != null && !string.IsNullOrEmpty(charData.resourceSlot) && SpriteManager.Instance != null)
                        {
                            icon = SpriteManager.Instance.LoadSprite(charData.resourceSlot);
                        }
                    }

                    if (firstValidSprite == null && icon != null)
                    {
                        firstValidSprite = icon;
                    }

                    deckMemberImages[i].sprite = icon;
                    deckMemberImages[i].enabled = true;
                }
            }

            if (profileImage != null && firstValidSprite != null)
            {
                profileImage.sprite = firstValidSprite;
                profileImage.enabled = true;
            }

            RefreshLocalizedTexts();
        }

        public void Setup(string friendName, int level, Sprite profileSprite = null, Sprite[] deckSprites = null, Action<InviteFriendNode> onSelected = null)
        {
            TargetUser = null;
            _level = level;
            if (onSelected != null)
            {
                _onSelectedCallback = onSelected;
            }

            if (friendNameText != null)
            {
                LocalizedText.ApplyFont(friendNameText);
                friendNameText.text = friendName ?? string.Empty;
            }

            if (profileImage != null && profileSprite != null)
            {
                profileImage.sprite = profileSprite;
                profileImage.enabled = true;
            }

            if (deckMemberImages != null && deckSprites != null)
            {
                for (int i = 0; i < deckMemberImages.Length; i++)
                {
                    if (deckMemberImages[i] == null) continue;
                    Sprite sp = i < deckSprites.Length ? deckSprites[i] : null;
                    deckMemberImages[i].sprite = sp;
                    deckMemberImages[i].enabled = true;
                }
            }

            RefreshLocalizedTexts();
        }

        public void SetSelected(bool isSelected)
        {
            IsSelected = isSelected;
            if (selectedOverlay != null)
            {
                selectedOverlay.SetActive(isSelected);
            }
        }

        public void RefreshLocalizedTexts()
        {
            if (friendNameText != null)
            {
                LocalizedText.ApplyFont(friendNameText);
            }

            if (friendLevelText != null)
            {
                var loc = friendLevelText.GetComponent<LocalizedText>();
                if (_level > 0)
                {
                    if (loc != null)
                    {
                        loc.SetKey("UI_FORMAT_LEVEL_SPACED", _level);
                    }
                    else
                    {
                        LocalizedText.ApplyFont(friendLevelText);
                        friendLevelText.text = LocalizationManager.Get("UI_FORMAT_LEVEL_SPACED", _level);
                    }
                }
                else
                {
                    LocalizedText.ApplyFont(friendLevelText);
                }
            }
        }

        private void OnClickNode()
        {
            _onSelectedCallback?.Invoke(this);
        }
    }
}

