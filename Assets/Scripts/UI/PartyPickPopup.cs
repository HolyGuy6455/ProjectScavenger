using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Scavenger.UI
{
    public class PartyPickPopup : MonoBehaviour
    {
        [Serializable]
        public struct RoleFilterTab
        {
            public CharRole role;
            public Button button;
            public GameObject inactiveObj;
            public GameObject selectedObj;

            public void SetSelected(bool isSelected)
            {
                if (inactiveObj != null) inactiveObj.SetActive(!isSelected);
                if (selectedObj != null) selectedObj.SetActive(isSelected);
            }
        }

        [Header("Grid References")]
        [SerializeField] private Transform contentParent;
        [SerializeField] private GameObject rowPrefab;
        [SerializeField] private int nodesPerRow = 5;

        [Header("Detail Panel References")]
        [SerializeField] private Image standingImage;
        [SerializeField] private TextMeshProUGUI detailNameText;
        [SerializeField] private GameObject[] roleTags; // 0: Infiltrator, 1: Striker, 2: Anchor, 3: Suppressor
        [SerializeField] private TextMeshProUGUI detailLevelText;
        [SerializeField] private TextMeshProUGUI detailDescriptionText;

        [Header("Footer Controls")]
        [SerializeField] private RoleFilterTab[] roleFilterTabs;
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button cancelButton;

        private readonly List<GameObject> _spawnedRows = new List<GameObject>();
        private readonly List<PartyPickCharNode> _nodes = new List<PartyPickCharNode>();
        private readonly HashSet<int> _organizedCharIds = new HashSet<int>();

        private CharRole _currentRoleFilter = CharRole.None;
        private CharData _selectedCharData;
        private int _selectedCharId;
        private Action<CharData> _onConfirmCallback;

        private void OnEnable()
        {
            if (UserDataManager.Instance != null)
            {
                UserDataManager.Instance.OnCharacterDataUpdated += PopulateNodes;
            }
            LocalizationManager.OnLanguageChanged += OnLanguageChanged;
        }

        private void OnDisable()
        {
            if (UserDataManager.Instance != null)
            {
                UserDataManager.Instance.OnCharacterDataUpdated -= PopulateNodes;
            }
            LocalizationManager.OnLanguageChanged -= OnLanguageChanged;
        }

        private void OnLanguageChanged()
        {
            UpdateDetailView(_selectedCharData);
        }

        public void Open(Action<CharData> onConfirm, int initialSelectedCharId = 0, IEnumerable<int> organizedCharIds = null)
        {
            _onConfirmCallback = onConfirm;
            _currentRoleFilter = CharRole.None;
            _selectedCharData = null;
            _selectedCharId = initialSelectedCharId;

            _organizedCharIds.Clear();
            if (organizedCharIds != null)
            {
                foreach (int id in organizedCharIds)
                {
                    if (id > 0 && id != initialSelectedCharId)
                    {
                        _organizedCharIds.Add(id);
                    }
                }
            }

            if (roleFilterTabs != null)
            {
                for (int i = 0; i < roleFilterTabs.Length; i++)
                {
                    CharRole targetRole = roleFilterTabs[i].role;
                    if (roleFilterTabs[i].button != null)
                    {
                        roleFilterTabs[i].button.onClick.RemoveAllListeners();
                        roleFilterTabs[i].button.onClick.AddListener(() => OnRoleFilterSelected(targetRole));
                    }
                }
            }

            if (confirmButton != null)
            {
                confirmButton.onClick.RemoveAllListeners();
                confirmButton.onClick.AddListener(OnConfirmButtonClicked);
            }

            if (cancelButton != null)
            {
                cancelButton.onClick.RemoveAllListeners();
                cancelButton.onClick.AddListener(OnCancelButtonClicked);
            }

            UpdateRoleFilterTabs();
            PopulateNodes();
        }

        private void OnRoleFilterSelected(CharRole role)
        {
            if (_currentRoleFilter == role) return;

            _currentRoleFilter = role;
            UpdateRoleFilterTabs();
            PopulateNodes();
        }

        private void UpdateRoleFilterTabs()
        {
            if (roleFilterTabs == null) return;

            for (int i = 0; i < roleFilterTabs.Length; i++)
            {
                roleFilterTabs[i].SetSelected(roleFilterTabs[i].role == _currentRoleFilter);
            }
        }

        private void PopulateNodes()
        {
            _spawnedRows.Clear();
            _nodes.Clear();

            if (contentParent != null)
            {
                foreach (Transform child in contentParent)
                {
                    Destroy(child.gameObject);
                }
            }

            if (DataManager.Instance == null || rowPrefab == null || contentParent == null)
            {
                UpdateDetailView(null);
                return;
            }

            var displayEntries = new List<(CharData data, int level)>();

            if (UserDataManager.Instance != null && UserDataManager.Instance.OwnedCharacters != null && UserDataManager.Instance.OwnedCharacters.Count > 0)
            {
                foreach (var userChar in UserDataManager.Instance.OwnedCharacters)
                {
                    CharData masterData = DataManager.Instance.GetData<CharData>("Char", userChar.charId);
                    if (masterData != null && IsRoleMatch(masterData.role))
                    {
                        displayEntries.Add((masterData, Mathf.Max(1, userChar.level)));
                    }
                }
            }
            else
            {
                List<CharData> charDatas = DataManager.Instance.GetAllData<CharData>("Char");
                if (charDatas != null)
                {
                    foreach (var data in charDatas)
                    {
                        if (data != null && IsRoleMatch(data.role))
                        {
                            displayEntries.Add((data, 1));
                        }
                    }
                }
            }

            if (displayEntries.Count == 0)
            {
                _selectedCharData = null;
                UpdateDetailView(null);
                return;
            }

            int slotsPerRow = Mathf.Max(1, nodesPerRow);
            int rowCount = Mathf.CeilToInt((float)displayEntries.Count / slotsPerRow);

            PartyPickCharNode nodeToSelect = null;

            for (int rowIndex = 0; rowIndex < rowCount; rowIndex++)
            {
                GameObject rowObj = Instantiate(rowPrefab, contentParent);
                rowObj.SetActive(true);
                _spawnedRows.Add(rowObj);

                PartyPickCharNode[] rowNodes = rowObj.GetComponentsInChildren<PartyPickCharNode>(true);
                for (int i = 0; i < rowNodes.Length; i++)
                {
                    int dataIndex = (rowIndex * slotsPerRow) + i;
                    if (dataIndex < displayEntries.Count)
                    {
                        var entry = displayEntries[dataIndex];
                        bool isOrganized = _organizedCharIds.Contains(entry.data.id);
                        rowNodes[i].Initialize(entry.data, entry.level, OnNodeSelected, isOrganized);
                        _nodes.Add(rowNodes[i]);

                        if (nodeToSelect == null || entry.data.id == _selectedCharId)
                        {
                            nodeToSelect = rowNodes[i];
                        }
                    }
                    else
                    {
                        rowNodes[i].SetEmpty();
                    }
                }
            }

            if (nodeToSelect != null && nodeToSelect.CharacterData != null)
            {
                OnNodeSelected(nodeToSelect, nodeToSelect.CharacterData);
            }
            else
            {
                _selectedCharData = null;
                UpdateDetailView(null);
            }
        }

        private bool IsRoleMatch(CharRole role)
        {
            return _currentRoleFilter == CharRole.None || role == _currentRoleFilter;
        }

        private void OnNodeSelected(PartyPickCharNode selectedNode, CharData charData)
        {
            _selectedCharData = charData;
            _selectedCharId = charData != null ? charData.id : 0;

            foreach (var node in _nodes)
            {
                if (node != null)
                {
                    node.SetSelected(node == selectedNode);
                }
            }

            UpdateDetailView(charData);
        }

        private void UpdateDetailView(CharData charData)
        {
            if (confirmButton != null)
            {
                confirmButton.interactable = charData != null;
            }

            if (charData == null)
            {
                if (standingImage != null)
                {
                    standingImage.sprite = null;
                    standingImage.enabled = false;
                }
                if (detailNameText != null) detailNameText.text = string.Empty;
                if (roleTags != null)
                {
                    foreach (var tag in roleTags)
                    {
                        if (tag != null) tag.SetActive(false);
                    }
                }
                if (detailLevelText != null) detailLevelText.text = string.Empty;
                return;
            }

            if (standingImage != null)
            {
                Sprite standSprite = !string.IsNullOrEmpty(charData.resourceStand) && SpriteManager.Instance != null
                    ? SpriteManager.Instance.LoadSprite(charData.resourceStand)
                    : null;

                standingImage.sprite = standSprite;
                standingImage.enabled = standSprite != null;
            }

            if (detailNameText != null)
            {
                detailNameText.text = charData.GetLocalizedName();
            }

            if (roleTags != null && roleTags.Length > 0)
            {
                for (int i = 0; i < roleTags.Length; i++)
                {
                    if (roleTags[i] != null)
                    {
                        roleTags[i].SetActive((int)charData.role == (i + 1));
                    }
                }
            }

            int level = 1;
            if (UserDataManager.Instance != null)
            {
                var userChar = UserDataManager.Instance.GetCharacter(charData.id);
                if (userChar != null && userChar.level > 0)
                {
                    level = userChar.level;
                }
            }

            if (detailLevelText != null)
            {
                detailLevelText.text = LocalizationManager.Get("UI_FORMAT_LEVEL_SPACED", level);
            }
        }

        private void OnConfirmButtonClicked()
        {
            if (_selectedCharData != null)
            {
                _onConfirmCallback?.Invoke(_selectedCharData);
            }

            Close();
        }

        private void OnCancelButtonClicked()
        {
            Close();
        }

        private void Close()
        {
            if (OutGameUIManager.Instance != null)
            {
                OutGameUIManager.Instance.ClosePopup(gameObject);
            }
            else
            {
                gameObject.SetActive(false);
            }
        }
    }
}
