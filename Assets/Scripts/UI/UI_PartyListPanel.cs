using UnityEngine;
using Scavenger.Network;

namespace Scavenger.UI
{
    public class UI_PartyListPanel : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Transform partyListContent; // ScrollView의 Content
        [SerializeField] private GameObject partyListItemPrefab; // 파티원 한 명을 표시할 UI 프리팹
        [SerializeField] private UnityEngine.UI.Button leavePartyButton; // 파티 떠나기 버튼

        private void Start()
        {
            if (leavePartyButton != null)
            {
                leavePartyButton.onClick.AddListener(() => 
                {
                    if (NetworkPartyManager.Instance != null)
                    {
                        NetworkPartyManager.Instance.LeaveParty();
                    }
                });
            }
        }

        private void OnEnable()
        {
            // 패널이 켜질 때 이벤트 구독 및 현재 리스트 갱신
            if (NetworkPartyManager.Instance != null)
            {
                NetworkPartyManager.Instance.OnPartyMembersUpdated += RefreshPartyList;
                RefreshPartyList(); // 켜질 때 바로 최신화
            }
        }

        private void OnDisable()
        {
            if (NetworkPartyManager.Instance != null)
            {
                NetworkPartyManager.Instance.OnPartyMembersUpdated -= RefreshPartyList;
            }
        }

        private void RefreshPartyList()
        {
            if (NetworkPartyManager.Instance == null || partyListContent == null) return;

            // 기존에 생성된 아이템들 전부 삭제
            foreach (Transform child in partyListContent)
            {
                Destroy(child.gameObject);
            }

            // 최신 파티원 목록을 가져와서 프리팹 생성
            var members = NetworkPartyManager.Instance.PartyMembers;
            
            foreach (var member in members)
            {
                if (partyListItemPrefab != null)
                {
                    var itemGo = Instantiate(partyListItemPrefab, partyListContent);
                    var itemScript = itemGo.GetComponent<UI_PartyListItem>();
                    if (itemScript != null)
                    {
                        itemScript.Initialize(member);
                    }
                }
            }
        }
    }
}
