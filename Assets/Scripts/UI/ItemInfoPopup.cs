using System.Collections;
using System.Collections.Generic;
using System.IO;
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
            RefreshUI();
        }

        private void OnDisable()
        {
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
            if (itemNameText != null) itemNameText.text = _itemData.name ?? string.Empty;
            if (itemDescriptionText != null) itemDescriptionText.text = _itemData.Desc ?? string.Empty;

            string category = GetCategoryName(_itemData.Category);
            if (categoryText != null) categoryText.text = category;

            if (itemIconImage != null)
            {
                Sprite icon = LoadItemIcon(_itemData.uiResource);
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
            ownedAmountText.text = $"보유 수량  {amount:N0}개";
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

        private static Sprite LoadItemIcon(string resource)
        {
            if (string.IsNullOrWhiteSpace(resource)) return null;
            string path = Path.ChangeExtension(resource.Trim().Replace('\\', '/'), null);
            Sprite icon = Resources.Load<Sprite>($"Items/{path}");
            return icon != null ? icon : Resources.Load<Sprite>(path);
        }

        private static string GetCategoryName(ItemCategory category)
        {
            switch (category)
            {
                case ItemCategory.Important: return "중요";
                case ItemCategory.Food: return "음식";
                case ItemCategory.Tool: return "도구";
                case ItemCategory.Consumable: return "소모품";
                default: return "기타";
            }
        }
    }
}
