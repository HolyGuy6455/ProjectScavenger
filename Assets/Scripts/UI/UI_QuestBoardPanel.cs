using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Scavenger.Network;
using Scavenger.Network.DTOs;

namespace Scavenger.UI
{
    public class UI_QuestBoardPanel : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Button claimAllButton;

        [Header("Tab Buttons")]
        [SerializeField] private Button dailyTabButton;
        [SerializeField] private Button weeklyTabButton;
        [SerializeField] private Button growthTabButton;
        
        [Header("Postits (미리 배치된 6개)")]
        [SerializeField] private List<UI_QuestBoardPostit> postits; 
        
        [Header("Pagination (page-indicator)")]
        [SerializeField] private Button prevPageBtn;
        [SerializeField] private Button nextPageBtn;
        [SerializeField] private TextMeshProUGUI pageText;

        private List<UserQuestDto> _allQuests = new List<UserQuestDto>();
        private List<UserQuestDto> _filteredQuests = new List<UserQuestDto>();
        private QuestCycleType _currentTab = QuestCycleType.DAILY;
        
        private int _currentPage = 0;
        private int _maxPage = 0;
        private bool _isLoading;
        private bool _isClaimingReward;
        private int _listRequestVersion;
        private NetworkRequestHandle _listRequest;

        private bool CanClaim => !_isLoading && !_isClaimingReward;
        
        private int ItemsPerPage => (postits != null && postits.Count > 0) ? postits.Count : 6;

        private void OnEnable()
        {
            ClearQuests();
            UpdateTabSelection();
            if (!_isClaimingReward) FetchQuests();
        }

        private void OnDisable()
        {
            // 재진입 전에 시작한 목록 요청이 새 화면을 덮어쓰지 않게 합니다.
            _listRequestVersion++;
            _listRequest?.Cancel();
            _isLoading = false;
        }

        private void Start()
        {
            if (dailyTabButton != null)
                dailyTabButton.onClick.AddListener(() => OnTabClicked(QuestCycleType.DAILY));

            if (weeklyTabButton != null)
                weeklyTabButton.onClick.AddListener(() => OnTabClicked(QuestCycleType.WEEKLY));

            if (growthTabButton != null)
                growthTabButton.onClick.AddListener(() => OnTabClicked(QuestCycleType.MAIN));

            if (claimAllButton != null)
                claimAllButton.onClick.AddListener(OnClickClaimAll);
                
            if (prevPageBtn != null)
                prevPageBtn.onClick.AddListener(OnClickPrevPage);
                
            if (nextPageBtn != null)
                nextPageBtn.onClick.AddListener(OnClickNextPage);
        }

        private void OnTabClicked(QuestCycleType tabType)
        {
            _currentTab = tabType;
            _currentPage = 0;
            UpdateTabSelection();
            FilterAndSortQuests();
            RefreshPage();
        }

        private void UpdateTabSelection()
        {
            if (dailyTabButton != null)
                dailyTabButton.GetComponentInParent<UI_QuestBoardTab>(true)?.SetSelected(_currentTab == QuestCycleType.DAILY);

            if (weeklyTabButton != null)
                weeklyTabButton.GetComponentInParent<UI_QuestBoardTab>(true)?.SetSelected(_currentTab == QuestCycleType.WEEKLY);

            if (growthTabButton != null)
                growthTabButton.GetComponentInParent<UI_QuestBoardTab>(true)?.SetSelected(_currentTab == QuestCycleType.MAIN);
        }

        private void FetchQuests(bool showErrorToast = true)
        {
            ClearQuests();
            var auth = NetworkAuthManager.Instance;
            if (auth == null) return;
            int version = ++_listRequestVersion;
            _listRequest?.Cancel();
            _isLoading = true;
            UpdateClaimButtons();
            var request = NetworkRequestManager.CreateJsonRequest(auth.ServerUrl + "/api/quest/list", "GET", authToken: auth.AuthToken);
            _listRequest = NetworkRequestManager.Instance.Send<QuestListResponse>(request, response =>
            {
                if (this != null && version == _listRequestVersion && isActiveAndEnabled) ApplyQuestList(response.quests);
            }, new NetworkRequestOptions
            {
                Name = "QuestList", Owner = this, CancelWhenOwnerUnavailable = true, ShowErrorToast = showErrorToast
            }, validateResponse: response => response.quests != null, onFinished: handle =>
            {
                if (this == null || version != _listRequestVersion) return;
                _isLoading = false;
                if (isActiveAndEnabled) UpdateClaimButtons();
            });
        }

        private void ApplyQuestList(List<UserQuestDto> quests)
        {
            _allQuests = quests;
            _currentPage = 0;
            FilterAndSortQuests();
            if (isActiveAndEnabled) RefreshPage();
        }

        private void FilterAndSortQuests()
        {
            if (_allQuests == null)
            {
                _filteredQuests.Clear();
                _maxPage = 0;
                return;
            }

            // 조건에 맞는 퀘스트 필터링 및 정렬 (수령 가능 -> 진행 중 -> 수령 완료 순)
            _filteredQuests = _allQuests.Where(q => {
                if (q == null) return false;
                var masterData = DataManager.Instance.GetData<QuestData>("Quest", q.questId);
                return masterData != null && masterData.CycleType == _currentTab;
            }).OrderBy(q => q.isRewarded ? 2 : (q.isCompleted ? 0 : 1)).ToList();
            
            _maxPage = Mathf.Max(0, (_filteredQuests.Count - 1) / ItemsPerPage);
        }

        private void ClearQuests()
        {
            _allQuests.Clear();
            _filteredQuests.Clear();
            _currentPage = 0;
            _maxPage = 0;
            UpdatePaginationUI();
            UpdateClaimButtons();
            if (postits == null || postits.Count == 0) return;

            for (int i = 0; i < postits.Count; i++)
            {
                if (postits[i] != null) postits[i].gameObject.SetActive(false);
            }
        }

        private void RefreshPage()
        {
            if (postits == null || postits.Count == 0)
            {
                UpdatePaginationUI();
                UpdateClaimButtons();
                return;
            }

            int startIndex = _currentPage * ItemsPerPage;
            
            for (int i = 0; i < postits.Count; i++)
            {
                if (postits[i] == null) continue;
                int dataIndex = startIndex + i;
                
                if (dataIndex < _filteredQuests.Count)
                {
                    // 데이터가 있으면 켜고 내용 세팅
                    postits[i].gameObject.SetActive(true);
                    postits[i].Setup(_filteredQuests[dataIndex], OnClickClaim);
                }
                else
                {
                    // 데이터가 모자라면 꺼둠
                    postits[i].gameObject.SetActive(false);
                }
            }

            UpdatePaginationUI();
            UpdateClaimButtons();
        }

        private void UpdatePaginationUI()
        {
            if (pageText != null)
            {
                // 예: 1 / 3
                int displayCurrent = _filteredQuests.Count > 0 ? _currentPage + 1 : 0;
                int displayMax = _filteredQuests.Count > 0 ? _maxPage + 1 : 0;
                pageText.text = $"{displayCurrent} / {displayMax}";
            }

            if (prevPageBtn != null)
                prevPageBtn.interactable = _currentPage > 0;
                
            if (nextPageBtn != null)
                nextPageBtn.interactable = _currentPage < _maxPage;
        }

        private void OnClickPrevPage()
        {
            if (_currentPage > 0)
            {
                _currentPage--;
                RefreshPage();
            }
        }

        private void OnClickNextPage()
        {
            if (_currentPage < _maxPage)
            {
                _currentPage++;
                RefreshPage();
            }
        }

        private void UpdateClaimButtons()
        {
            if (claimAllButton != null)
                claimAllButton.interactable = CanClaim && _filteredQuests.Any(q => q.isCompleted && !q.isRewarded);

            if (postits == null) return;
            foreach (var postit in postits)
                if (postit != null) postit.SetClaimInteractable(CanClaim);
        }

        private void OnClickClaim(int questId)
        {
            if (!CanClaim || !_filteredQuests.Any(q => q.questId == questId && q.isCompleted && !q.isRewarded)) return;

            BeginClaim("/api/quest/reward", JsonUtility.ToJson(new QuestRewardRequest { questId = questId }));
        }

        private void OnClickClaimAll()
        {
            if (!CanClaim) return;

            var questIds = GetClaimableQuestIds();
            if (questIds.Count == 0) return;

            BeginClaim("/api/quest/reward/batch", JsonUtility.ToJson(new QuestBatchRewardRequest { questIds = questIds }));
        }

        private List<int> GetClaimableQuestIds()
        {
            // 클릭 시점에 현재 탭의 모든 페이지에서 보이는 수령 가능 퀘스트만 고정합니다.
            return _filteredQuests.Where(q => q.isCompleted && !q.isRewarded)
                .Select(q => q.questId).Distinct().ToList();
        }

        private void BeginClaim(string path, string json)
        {
            var auth = NetworkAuthManager.Instance;
            var userData = UserDataManager.Instance;
            if (auth == null || userData == null) return;
            string token = auth.AuthToken;
            int version = _listRequestVersion;
            _isClaimingReward = true;
            UpdateClaimButtons();
            var request = NetworkRequestManager.CreateJsonRequest(auth.ServerUrl + path, "POST", json, token);
            NetworkRequestManager.Instance.Send<QuestRewardResponse>(request, response =>
            {
                // 지급 결과는 화면 수명과 무관하게 반영합니다.
                if (auth == null || auth.AuthToken != token) return;
                if (userData != null) userData.ApplyQuestReward(response);
                if (this != null && isActiveAndEnabled && version == _listRequestVersion) ApplyQuestList(response.quests);
            }, new NetworkRequestOptions
            {
                Name = "ClaimQuestRewards", DuplicateKey = auth.Username + ":ClaimQuestRewards",
                DuplicatePolicy = DuplicateRequestPolicy.Reject
            }, onFailure: error =>
            {
                // 응답이 유실되어도 서버에서 지급했을 수 있으므로 재전송 없이 상태를 조회합니다.
                if (userData != null && auth != null && auth.AuthToken == token) userData.FetchItemList(false, false);
            }, validateResponse: response => response.quests != null, onFinished: handle =>
            {
                if (this == null) return;
                _isClaimingReward = false;
                if (!isActiveAndEnabled) return;
                if (handle.Status == NetworkRequestStatus.Cancelled) { UpdateClaimButtons(); return; }
                if (handle.Status == NetworkRequestStatus.Failed || version != _listRequestVersion) FetchQuests(false);
                else UpdateClaimButtons();
            });
        }
    }
}
