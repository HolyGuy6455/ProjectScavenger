using UnityEngine;
using System.Collections.Generic;

namespace Scavenger.UI
{
    public class StageInfoPanel : MonoBehaviour
    {
        [Header("Member Cards")]
        [SerializeField] private MemberCard myMemberCard;
        [SerializeField] private List<MemberCard> partyMemberCards = new List<MemberCard>();

        [Header("UI Elements")]
        [SerializeField] private GameObject supportButtonObject;

        private void OnEnable()
        {
            if (Scavenger.Network.NetworkPartyManager.Instance == null)
            {
                var partySessionObj = new GameObject("PartySession");
                var partyManager = partySessionObj.AddComponent<Scavenger.Network.NetworkPartyManager>();
                partyManager.Connect();
            }

            if (Scavenger.Network.NetworkPartyManager.Instance != null)
            {
                Scavenger.Network.NetworkPartyManager.Instance.OnPartyMembersUpdated += RefreshUI;
            }

            RefreshUI();
        }

        private void OnDisable()
        {
            if (Scavenger.Network.NetworkPartyManager.Instance != null)
            {
                Scavenger.Network.NetworkPartyManager.Instance.OnPartyMembersUpdated -= RefreshUI;
            }
        }

        public void RefreshUI()
        {
            bool isMultiplay = Scavenger.Network.NetworkPartyManager.Instance != null && Scavenger.Network.NetworkPartyManager.Instance.IsMultiplay;

            // 1. 로컬 유저 본인의 덱 정보 갱신
            if (myMemberCard != null && UserDataManager.Instance != null)
            {
                // UserDataManager에서 저장된 4인 덱 정보를 가져와 카드에 세팅
                myMemberCard.SetDeck(UserDataManager.Instance.SelectedDeck);

                // 로컬 유저의 레디 상태 갱신
                bool isMyReady = false;
                if (isMultiplay && Scavenger.Network.NetworkPartyManager.Instance.PartyMembers != null)
                {
                    var myInfo = Scavenger.Network.NetworkPartyManager.Instance.PartyMembers.Find(m => m.username == Scavenger.Network.NetworkAuthManager.Instance.Username);
                    if (myInfo != null) isMyReady = myInfo.isReady;
                }
                myMemberCard.SetReady(isMyReady);
            }

            if (supportButtonObject != null)
            {
                supportButtonObject.SetActive(!isMultiplay);
            }

            // 2. 파티 멤버들의 덱 정보 갱신
            if (partyMemberCards != null)
            {
                for (int i = 0; i < partyMemberCards.Count; i++)
                {
                    if (partyMemberCards[i] == null) continue;

                    // 멀티플레이가 아니면 파티원 카드를 모두 끕니다.
                    if (!isMultiplay)
                    {
                        partyMemberCards[i].gameObject.SetActive(false);
                        continue;
                    }

                    var partyMembers = Scavenger.Network.NetworkPartyManager.Instance.PartyMembers;
                    if (partyMembers != null)
                    {
                        // 본인을 제외한 파티원 리스트 생성
                        var otherMembers = new List<Scavenger.Network.UserInfo>();
                        foreach (var m in partyMembers)
                        {
                            if (m.username != Scavenger.Network.NetworkAuthManager.Instance.Username)
                            {
                                otherMembers.Add(m);
                            }
                        }

                        if (i < otherMembers.Count)
                        {
                            var member = otherMembers[i];
                            partyMemberCards[i].gameObject.SetActive(true);
                            partyMemberCards[i].SetReady(member.isReady);
                            
                            if (member.selectedCharIds != null)
                            {
                                partyMemberCards[i].SetDeck(member.selectedCharIds.ToArray());
                            }
                        }
                        else
                        {
                            partyMemberCards[i].gameObject.SetActive(false);
                        }
                    }
                    else
                    {
                        partyMemberCards[i].gameObject.SetActive(false);
                    }
                }
            }
        }
    }
}
