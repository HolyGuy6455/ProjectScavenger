using System.Collections.Generic;
using Scavenger.Network;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Scavenger.UI
{
    public class InviteFriendPopup : MonoBehaviour
    {
        public const int MAX_INVITE_COUNT = 3;

        [Header("리스트 참조")]
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private Transform nodeContainer;
        [SerializeField] private InviteFriendNode nodePrefab;
        [SerializeField] private TextMeshProUGUI inviteCountText;

        [Header("하단 UI 및 버튼")]
        [SerializeField] private TextMeshProUGUI selectedFriendText;
        [SerializeField] private Button closeButton;
        [SerializeField] private Button cancelButton;
        [SerializeField] private Button requestButton;

        private readonly List<InviteFriendNode> _nodes = new List<InviteFriendNode>();
        private readonly List<InviteFriendNode> _selectedNodes = new List<InviteFriendNode>();

        private void Awake()
        {
            if (nodePrefab == null)
            {
                nodePrefab = Resources.Load<InviteFriendNode>("UIPrefabs/Node/InviteFriendNode");
            }

            if (cancelButton != null)
            {
                cancelButton.onClick.RemoveListener(OnClickCancel);
                cancelButton.onClick.AddListener(OnClickCancel);
            }

            if (requestButton != null)
            {
                requestButton.onClick.RemoveListener(OnClickRequest);
                requestButton.onClick.AddListener(OnClickRequest);
            }

            if (closeButton != null && closeButton.GetComponent<UI_NavigateBackButton>() == null)
            {
                closeButton.onClick.RemoveListener(OnClickCancel);
                closeButton.onClick.AddListener(OnClickCancel);
            }
        }

        private void OnEnable()
        {
            LocalizationManager.OnLanguageChanged += OnLanguageChanged;

            if (NetworkPartyManager.Instance != null)
            {
                NetworkPartyManager.Instance.OnOnlineUsersUpdated -= RefreshUserList;
                NetworkPartyManager.Instance.OnOnlineUsersUpdated += RefreshUserList;
            }

            _selectedNodes.Clear();
            RefreshUserList();
        }

        private void OnDisable()
        {
            LocalizationManager.OnLanguageChanged -= OnLanguageChanged;

            if (NetworkPartyManager.Instance != null)
            {
                NetworkPartyManager.Instance.OnOnlineUsersUpdated -= RefreshUserList;
            }
        }

        public void Open()
        {
            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }
            else
            {
                RefreshUserList();
            }
        }

        public void RefreshUI()
        {
            RefreshUserList();
        }

        public void RefreshUserList()
        {
            HashSet<string> prevSelectedConnIds = new HashSet<string>();
            for (int i = 0; i < _selectedNodes.Count; i++)
            {
                if (_selectedNodes[i] != null && !string.IsNullOrEmpty(_selectedNodes[i].ConnectionId))
                {
                    prevSelectedConnIds.Add(_selectedNodes[i].ConnectionId);
                }
            }

            _nodes.Clear();
            _selectedNodes.Clear();

            if (nodeContainer != null)
            {
                for (int i = nodeContainer.childCount - 1; i >= 0; i--)
                {
                    Destroy(nodeContainer.GetChild(i).gameObject);
                }
            }

            if (nodePrefab == null)
            {
                nodePrefab = Resources.Load<InviteFriendNode>("UIPrefabs/Node/InviteFriendNode");
            }

            if (NetworkPartyManager.Instance != null &&
                NetworkPartyManager.Instance.OnlineUsers != null &&
                nodeContainer != null &&
                nodePrefab != null)
            {
                string myUsername = NetworkAuthManager.Instance != null
                    ? NetworkAuthManager.Instance.Username
                    : string.Empty;

                var users = NetworkPartyManager.Instance.OnlineUsers;
                for (int i = 0; i < users.Count; i++)
                {
                    UserInfo user = users[i];
                    if (user == null) continue;

                    // 본인 제외
                    if (!string.IsNullOrEmpty(myUsername) && user.username == myUsername)
                    {
                        continue;
                    }

                    // 이미 파티에 속한 유저 제외
                    if (!string.IsNullOrEmpty(user.partyId))
                    {
                        continue;
                    }

                    InviteFriendNode node = Instantiate(nodePrefab, nodeContainer);
                    node.Setup(user, OnNodeSelected);

                    bool isPrevSelected = !string.IsNullOrEmpty(user.connectionId) &&
                                          prevSelectedConnIds.Contains(user.connectionId) &&
                                          _selectedNodes.Count < MAX_INVITE_COUNT;
                    node.SetSelected(isPrevSelected);
                    if (isPrevSelected)
                    {
                        _selectedNodes.Add(node);
                    }

                    _nodes.Add(node);
                }
            }

            UpdateSelectedFriendView();
        }

        private void OnLanguageChanged()
        {
            for (int i = 0; i < _nodes.Count; i++)
            {
                if (_nodes[i] != null)
                {
                    _nodes[i].RefreshLocalizedTexts();
                }
            }

            UpdateSelectedFriendView();
        }

        private void OnNodeSelected(InviteFriendNode node)
        {
            if (node == null) return;

            if (_selectedNodes.Contains(node))
            {
                // 이미 선택된 노드를 다시 클릭하면 선택 해제
                _selectedNodes.Remove(node);
                node.SetSelected(false);
            }
            else
            {
                // 최대 인원(3명)까지만 선택 가능
                if (_selectedNodes.Count >= MAX_INVITE_COUNT)
                {
                    return;
                }

                _selectedNodes.Add(node);
                node.SetSelected(true);
            }

            UpdateSelectedFriendView();
        }

        private void UpdateSelectedFriendView()
        {
            if (requestButton != null)
            {
                requestButton.interactable = _selectedNodes.Count > 0;
            }

            if (inviteCountText != null)
            {
                var loc = inviteCountText.GetComponent<LocalizedText>();
                if (loc != null)
                {
                    loc.SetKey("UI_INVITEFRIEND_INVITE_COUNT_FORMAT", _selectedNodes.Count, MAX_INVITE_COUNT);
                }
                else
                {
                    LocalizedText.ApplyFont(inviteCountText);
                    inviteCountText.text = LocalizationManager.Get(
                        "UI_INVITEFRIEND_INVITE_COUNT_FORMAT",
                        _selectedNodes.Count,
                        MAX_INVITE_COUNT);
                }
            }

            if (selectedFriendText != null)
            {
                var loc = selectedFriendText.GetComponent<LocalizedText>();
                if (_selectedNodes.Count > 0)
                {
                    List<string> names = new List<string>(_selectedNodes.Count);
                    for (int i = 0; i < _selectedNodes.Count; i++)
                    {
                        if (_selectedNodes[i] != null && !string.IsNullOrEmpty(_selectedNodes[i].FriendName))
                        {
                            names.Add(_selectedNodes[i].FriendName);
                        }
                    }

                    if (names.Count > 0)
                    {
                        string joined = string.Join(", ", names);
                        if (loc != null)
                        {
                            loc.SetKey("UI_INVITEFRIEND_SELECTED_FORMAT", joined);
                        }
                        else
                        {
                            LocalizedText.ApplyFont(selectedFriendText);
                            selectedFriendText.text = LocalizationManager.Get("UI_INVITEFRIEND_SELECTED_FORMAT", joined);
                        }
                    }
                    else if (loc != null)
                    {
                        loc.SetKey("UI_INVITEFRIEND_SELECTED_NONE");
                    }
                    else
                    {
                        LocalizedText.ApplyFont(selectedFriendText);
                        selectedFriendText.text = LocalizationManager.Get("UI_INVITEFRIEND_SELECTED_NONE");
                    }
                }
                else if (loc != null)
                {
                    loc.SetKey("UI_INVITEFRIEND_SELECTED_NONE");
                }
                else
                {
                    LocalizedText.ApplyFont(selectedFriendText);
                    selectedFriendText.text = LocalizationManager.Get("UI_INVITEFRIEND_SELECTED_NONE");
                }
            }
        }

        private void OnClickRequest()
        {
            if (_selectedNodes.Count > 0 && NetworkPartyManager.Instance != null)
            {
                for (int i = 0; i < _selectedNodes.Count; i++)
                {
                    InviteFriendNode node = _selectedNodes[i];
                    if (node != null && !string.IsNullOrEmpty(node.ConnectionId))
                    {
                        NetworkPartyManager.Instance.InviteToParty(node.ConnectionId);
                    }
                }
            }

            Close();
        }

        private void OnClickCancel()
        {
            Close();
        }

        public void Close()
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
