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
        
        private int ItemsPerPage => (postits != null && postits.Count > 0) ? postits.Count : 6;

        private void OnEnable()
        {
            UpdateTabSelection();
            FetchQuests();
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
            StartCoroutine(FetchQuestsRoutine());
        }

        private IEnumerator FetchQuestsRoutine()
        {
            string url = $"{NetworkAuthManager.Instance.ServerUrl}/api/quest/list";

            using (UnityWebRequest request = UnityWebRequest.Get(url))
            {
                request.SetRequestHeader("Authorization", $"Bearer {NetworkAuthManager.Instance.AuthToken}");
                
                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    var response = JsonUtility.FromJson<QuestListResponse>(request.downloadHandler.text);
                    if (response != null && response.quests != null)
                    {
                        _allQuests = response.quests;
                        FilterAndSortQuests();
                        _currentPage = 0; // 데이터 갱신 시 첫 페이지로 리셋
                        RefreshPage();
                    }
                }
                else
                {
                    Debug.LogError($"[QuestBoardPanel] 퀘스트 목록 로드 실패: {request.error}\n{request.downloadHandler.text}");
                }
            }
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
                var masterData = DataManager.Instance.GetData<QuestData>("Quest", q.questId);
                return masterData != null && masterData.CycleType == _currentTab;
            }).OrderBy(q => q.isRewarded ? 2 : (q.isCompleted ? 0 : 1)).ToList();
            
            _maxPage = Mathf.Max(0, (_filteredQuests.Count - 1) / ItemsPerPage);
        }

        private void RefreshPage()
        {
            if (postits == null || postits.Count == 0) return;

            int startIndex = _currentPage * ItemsPerPage;
            
            for (int i = 0; i < postits.Count; i++)
            {
                int dataIndex = startIndex + i;
                
                if (dataIndex < _filteredQuests.Count)
                {
                    // 데이터가 있으면 켜고 내용 세팅
                    postits[i].gameObject.SetActive(true);
                    postits[i].Setup(_filteredQuests[dataIndex], OnRewardClaimed);
                }
                else
                {
                    // 데이터가 모자라면 꺼둠
                    postits[i].gameObject.SetActive(false);
                }
            }

            UpdatePaginationUI();
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

        private void OnRewardClaimed()
        {
            FetchQuests(); // 수령 후 상태 새로고침
        }

        private void OnClickClaimAll()
        {
            Debug.Log("일괄 수령 버튼 클릭됨!");
            // TODO: 일괄 수령 API 연결
        }
    }
}
