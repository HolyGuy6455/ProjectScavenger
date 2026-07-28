using Scavenger.Network;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Scavenger.UI
{
    public class UI_UserListItem : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI usernameText;
        [SerializeField] private Button inviteButton;

        private UserInfo _targetUser;

        private void Awake()
        {
            if (inviteButton != null)
            {
                inviteButton.onClick.AddListener(OnInviteButtonClicked);
            }
        }

        public void Initialize(UserInfo user)
        {
            _targetUser = user;
            
            if (usernameText != null)
            {
                usernameText.text = _targetUser.username;
            }
        }

        private void OnInviteButtonClicked()
        {
            if (_targetUser != null && NetworkPartyManager.Instance != null)
            {
                // 이 버튼을 누르면 NetworkPartyManager를 통해 서버에 "이 사람 파티로 납치해줘" 라고 요청함
                NetworkPartyManager.Instance.InviteToParty(_targetUser.connectionId);
                
                // 연속 클릭 방지용으로 잠시 비활성화 (선택 사항)
                inviteButton.interactable = false;
            }
        }
    }
}
