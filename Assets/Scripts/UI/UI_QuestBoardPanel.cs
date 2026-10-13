using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Scavenger.Network;
using Scavenger.Network.DTOs;
using UnityEngine.Networking;

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

        private void FetchQuests()
        {
            ClearQuests();
            var auth = NetworkAuthManager.Instance;
            var userData = UserDataManager.Instance;
            if (auth == null || userData == null) return;

            _isLoading = true;
            UpdateClaimButtons();
            int version = ++_listRequestVersion;
            userData.StartCoroutine(FetchQuestsRoutine(auth.ServerUrl, auth.AuthToken, version));
        }

        private IEnumerator FetchQuestsRoutine(string serverUrl, string authToken, int version)
        {
            try
            {
                using (UnityWebRequest request = UnityWebRequest.Get($"{serverUrl}/api/quest/list"))
                {
                    request.timeout = 15;
                    request.SetRequestHeader("Authorization", $"Bearer {authToken}");
                    yield return request.SendWebRequest();

                    if (this == null || version != _listRequestVersion || !isActiveAndEnabled) yield break;

                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        var response = ParseResponse<QuestListResponse>(request.downloadHandler.text);
                        if (response?.quests != null) ApplyQuestList(response.quests);
                        else Debug.LogError("[QuestBoardPanel] 퀘스트 목록 응답에 quests가 없습니다.");
                    }
                    else
                    {
                        Debug.LogError($"[QuestBoardPanel] 퀘스트 목록 로드 실패: {request.error}\n{request.downloadHandler.text}");
                    }
                }
            }
            finally
            {
                if (this != null && version == _listRequestVersion)
                {
                    _isLoading = false;
                    if (isActiveAndEnabled) UpdateClaimButtons();
                }
            }
        }

        private static T ParseResponse<T>(string json) where T : class
        {
            try { return JsonUtility.FromJson<T>(json); }
            catch (Exception e)
            {
                Debug.LogError($"[QuestBoardPanel] JSON 응답 처리 실패: {e.Message}");
                return null;
            }
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

            _isClaimingReward = true;
            UpdateClaimButtons();
            // 화면을 닫거나 포스트잇을 재사용해도 서버 응답과 잔액 갱신은 완료합니다.
            userData.StartCoroutine(ClaimRewardsRoutine(userData, auth.ServerUrl + path, auth.AuthToken, json));
        }

        private IEnumerator ClaimRewardsRoutine(UserDataManager userData, string url, string authToken, string json)
        {
            bool applied = false;
            try
            {
                using (var request = new UnityWebRequest(url, "POST"))
                {
                    request.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json));
                    request.downloadHandler = new DownloadHandlerBuffer();
                    request.timeout = 15;
                    request.SetRequestHeader("Content-Type", "application/json");
                    request.SetRequestHeader("Authorization", $"Bearer {authToken}");
                    yield return request.SendWebRequest();

                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        var response = ParseResponse<QuestRewardResponse>(request.downloadHandler.text);
                        if (response?.quests != null)
                        {
                            if (userData != null) userData.ApplyQuestReward(response);
                            if (this != null) ApplyQuestList(response.quests);
                            applied = true;
                        }
                        else Debug.LogError("[QuestBoardPanel] 보상 응답에 갱신된 quests가 없습니다.");
                    }
                    else
                    {
                        Debug.LogError($"[QuestBoardPanel] 보상 수령 실패: {request.error}\n{request.downloadHandler.text}");
                    }
                }
            }
            finally
            {
                // 통신 실패여도 서버는 지급했을 수 있으므로 POST 재시도 대신 상태를 재조회합니다.
                if (!applied && userData != null && NetworkAuthManager.Instance != null)
                    userData.FetchItemList();

                if (this != null)
                {
                    _isClaimingReward = false;
                    if (isActiveAndEnabled)
                    {
                        if (!applied) FetchQuests();
                        else UpdateClaimButtons();
                    }
                }
            }
        }
    }
}
