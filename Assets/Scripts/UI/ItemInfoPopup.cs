using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Scavenger.UI
{
    public class ItemInfoPopup : MonoBehaviour
    {
        [Header("아이템 정보")]
        [SerializeField] private GameObject itemInfoRoot;
        [SerializeField] private Image itemIconImage;
        [SerializeField] private TextMeshProUGUI itemNameText;
        [SerializeField] private TextMeshProUGUI itemDescriptionText;
        [SerializeField] private TextMeshProUGUI ownedAmountText;
        [SerializeField] private TextMeshProUGUI categoryText;

        [Header("획득 경로")]
        [SerializeField] private GameObject shortcutRoot;
        [SerializeField] private ItemInfoShortcutNode shortcutNodeTemplate;

        private readonly List<ItemInfoShortcutNode> _shortcutNodes = new List<ItemInfoShortcutNode>();
        private int _itemId;
        private ItemData _itemData;
        private UserDataManager _subscribedUserData;
        private Coroutine _loadingRoutine;

        public int CurrentItemId => _itemId;

        private void OnEnable()
        {
            LocalizationManager.OnLanguageChanged += RefreshUI;
            RefreshUI();
        }

        private void OnDisable()
        {
            LocalizationManager.OnLanguageChanged -= RefreshUI;
            StopWaitingForData();
            UnsubscribeFromUserData();
        }

        public void Open(int itemId)
        {
            SetItemForNextOpen(itemId);
            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }
            else
            {
                RefreshUI();
            }
        }

        public void SetItemForNextOpen(int itemId)
        {
            _itemId = itemId;
        }

        public void RefreshUI()
        {
            SubscribeToUserData();

            if (_itemId <= 0)
            {
                StopWaitingForData();
                ClearDisplay();
                return;
            }

            if (DataManager.Instance == null || !DataManager.Instance.IsDataLoaded)
            {
                ClearDisplay();
                if (isActiveAndEnabled && _loadingRoutine == null)
                {
                    _loadingRoutine = StartCoroutine(WaitForData());
                }
                return;
            }

            StopWaitingForData();
            _itemData = DataManager.Instance.GetData<ItemData>("Item", _itemId);
            if (_itemData == null)
            {
                ClearDisplay();
                return;
            }

            if (itemInfoRoot != null) itemInfoRoot.SetActive(true);
            if (itemNameText != null)
            {
                LocalizedText.ApplyFont(itemNameText);
                itemNameText.text = _itemData.GetLocalizedName() ?? string.Empty;
            }
            if (itemDescriptionText != null)
            {
                LocalizedText.ApplyFont(itemDescriptionText);
                itemDescriptionText.text = _itemData.GetLocalizedDesc() ?? string.Empty;
            }

            string category = GetCategoryName(_itemData.Category);
            if (categoryText != null)
            {
                LocalizedText.ApplyFont(categoryText);
                categoryText.text = category;
            }

            if (itemIconImage != null)
            {
                Sprite icon = SpriteManager.Instance.LoadSprite(_itemData.uiResource);
                itemIconImage.sprite = icon;
                itemIconImage.enabled = icon != null;
            }

            RefreshOwnedAmount();
            RefreshShortcuts();
        }

        private void RefreshShortcuts()
        {
            if (shortcutRoot == null || shortcutNodeTemplate == null) return;
            if (_shortcutNodes.Count == 0) _shortcutNodes.Add(shortcutNodeTemplate);

            int visibleCount = 0;
            if (_itemData?.shortcut != null)
            {
                for (int i = 0; i < _itemData.shortcut.Length; i++)
                {
                    string shortcut = _itemData.shortcut[i];
                    if (string.IsNullOrWhiteSpace(shortcut)) continue;

                    if (visibleCount == _shortcutNodes.Count)
                    {
                        _shortcutNodes.Add(Instantiate(shortcutNodeTemplate, shortcutNodeTemplate.transform.parent));
                    }

                    string value = _itemData.shortcut_value != null && i < _itemData.shortcut_value.Length
                        ? _itemData.shortcut_value[i]
                        : string.Empty;
                    ItemInfoShortcutNode node = _shortcutNodes[visibleCount++];
                    node.Setup(shortcut, value);
                    node.gameObject.SetActive(true);
                }
            }

            for (int i = visibleCount; i < _shortcutNodes.Count; i++)
            {
                _shortcutNodes[i].gameObject.SetActive(false);
            }
            shortcutRoot.SetActive(visibleCount > 0);
        }

        private IEnumerator WaitForData()
        {
            while (DataManager.Instance == null || !DataManager.Instance.IsDataLoaded)
            {
                yield return null;
            }

            _loadingRoutine = null;
            RefreshUI();
        }

        private void StopWaitingForData()
        {
            if (_loadingRoutine == null) return;
            StopCoroutine(_loadingRoutine);
            _loadingRoutine = null;
        }

        private void SubscribeToUserData()
        {
            if (!isActiveAndEnabled || _subscribedUserData == UserDataManager.Instance) return;
            UnsubscribeFromUserData();
            _subscribedUserData = UserDataManager.Instance;
            if (_subscribedUserData != null)
            {
                _subscribedUserData.OnItemDataUpdated += RefreshOwnedAmount;
            }
        }

        private void UnsubscribeFromUserData()
        {
            if (_subscribedUserData != null)
            {
                _subscribedUserData.OnItemDataUpdated -= RefreshOwnedAmount;
            }
            _subscribedUserData = null;
        }

        private void RefreshOwnedAmount()
        {
            if (_itemData == null || ownedAmountText == null) return;
            int amount = UserDataManager.Instance != null
                ? UserDataManager.Instance.GetGameItemAmount(_itemId)
                : 0;
            LocalizedText.ApplyFont(ownedAmountText);
            ownedAmountText.text = LocalizationManager.Get("UI_ITEMINFO_OWNED_AMOUNT", amount.ToString("N0"));
        }

        private void ClearDisplay()
        {
            _itemData = null;
            RefreshShortcuts();
            if (itemNameText != null) itemNameText.text = string.Empty;
            if (itemDescriptionText != null) itemDescriptionText.text = string.Empty;
            if (ownedAmountText != null) ownedAmountText.text = string.Empty;
            if (categoryText != null) categoryText.text = string.Empty;
            if (itemIconImage != null)
            {
                itemIconImage.sprite = null;
                itemIconImage.enabled = false;
            }
            if (itemInfoRoot != null) itemInfoRoot.SetActive(false);
        }

        private static string GetCategoryName(ItemCategory category)
        {
            return ItemData.GetLocalizedCategoryName(category);
        }
    }
}
