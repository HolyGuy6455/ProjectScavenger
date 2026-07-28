using System.Collections.Generic;
using Scavenger.Network;
using UnityEngine;
using UnityEngine.UI;

namespace Scavenger.UI
{
    public class UI_FriendPartyPanel : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Transform userListContent; // ScrollView의 Content
        [SerializeField] private GameObject userListItemPrefab; // 접속자 한 명을 표시할 UI 프리팹

        private void OnEnable()
        {
            // 패널이 켜질 때 이벤트 구독 및 현재 리스트 갱신
            if (NetworkPartyManager.Instance != null)
            {
                NetworkPartyManager.Instance.OnOnlineUsersUpdated += RefreshUserList;
                RefreshUserList(); // 켜질 때 바로 최신화
            }
        }

        private void OnDisable()
        {
            if (NetworkPartyManager.Instance != null)
            {
                NetworkPartyManager.Instance.OnOnlineUsersUpdated -= RefreshUserList;
            }
        }

        private void RefreshUserList()
        {
            if (NetworkPartyManager.Instance == null || userListContent == null) return;

            // 기존에 생성된 아이템들 전부 삭제
            foreach (Transform child in userListContent)
            {
                Destroy(child.gameObject);
            }

            // 최신 접속자 목록을 가져와서 프리팹 생성
            var users = NetworkPartyManager.Instance.OnlineUsers;
            
            foreach (var user in users)
            {
                // 내 이름은 목록에서 제외 (나 자신을 파티에 초대할 순 없으니까요)
                if (user.username == NetworkAuthManager.Instance.Username)
                    continue;

                if (userListItemPrefab != null)
                {
                    var itemGo = Instantiate(userListItemPrefab, userListContent);
                    var itemScript = itemGo.GetComponent<UI_UserListItem>();
                    if (itemScript != null)
                    {
                        itemScript.Initialize(user);
                    }
                }
            }
        }
    }
}
