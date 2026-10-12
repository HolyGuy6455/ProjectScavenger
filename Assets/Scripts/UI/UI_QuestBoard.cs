// using System.Collections;
// using System.Collections.Generic;
// using UnityEngine;
// using UnityEngine.UI;
// using Scavenger.Network;
// using Scavenger.Network.DTOs;
// using Scavenger.Data;
// using UnityEngine.Networking;
// using System.Linq;

// namespace Scavenger.UI
// {
//     public class UI_QuestBoard : MonoBehaviour
//     {
//         [Header("Tab Buttons")]
//         [SerializeField] private Button dailyTabButton;
//         [SerializeField] private Button weeklyTabButton;

//         [Header("Scroll View Elements")]
//         [SerializeField] private Transform questItemContainer;
//         [SerializeField] private GameObject questItemPrefab;

//         private List<UserQuestDto> _allQuests = new List<UserQuestDto>();
//         private QuestCycleType _currentTab = QuestCycleType.DAILY;

//         private void Start()
//         {
//             dailyTabButton.onClick.AddListener(() => OnTabClicked(QuestCycleType.DAILY));
//             weeklyTabButton.onClick.AddListener(() => OnTabClicked(QuestCycleType.WEEKLY));
//         }

//         private void OnEnable()
//         {
//             // Fetch fresh quest data whenever the panel is opened
//             FetchQuests();
//         }

//         private void OnTabClicked(QuestCycleType tabType)
//         {
//             _currentTab = tabType;
//             // Optionally update tab UI visuals (active/inactive sprites) here
//             RefreshQuestList();
//         }

//         private void FetchQuests()
//         {
//             StartCoroutine(FetchQuestsRoutine());
//         }

//         private IEnumerator FetchQuestsRoutine()
//         {
//             // Clear existing items while loading
//             foreach (Transform child in questItemContainer)
//             {
//                 Destroy(child.gameObject);
//             }

//             string url = $"{NetworkAuthManager.Instance.ServerUrl}/api/quest/list";

//             using (UnityWebRequest request = UnityWebRequest.Get(url))
//             {
//                 request.SetRequestHeader("Authorization", $"Bearer {NetworkAuthManager.Instance.AuthToken}");
                
//                 yield return request.SendWebRequest();

//                 if (request.result == UnityWebRequest.Result.Success)
//                 {
//                     var response = JsonUtility.FromJson<QuestListResponse>(request.downloadHandler.text);
//                     if (response != null && response.quests != null)
//                     {
//                         _allQuests = response.quests;
//                         RefreshQuestList();
//                     }
//                 }
//                 else
//                 {
//                     Debug.LogError($"[QuestBoard] 퀘스트 목록 로드 실패: {request.error}\n{request.downloadHandler.text}");
//                 }
//             }
//         }

//         private void RefreshQuestList()
//         {
//             // Clear existing
//             foreach (Transform child in questItemContainer)
//             {
//                 Destroy(child.gameObject);
//             }

//             if (_allQuests == null || _allQuests.Count == 0) return;

//             // Filter and sort quests (uncompleted first, then completed, then rewarded)
//             var filteredQuests = _allQuests.Where(q => {
//                 var masterData = DataManager.Instance.GetQuest(q.questId);
//                 return masterData != null && masterData.CycleType == _currentTab;
//             }).OrderBy(q => q.isRewarded ? 2 : (q.isCompleted ? 0 : 1)).ToList();

//             foreach (var quest in filteredQuests)
//             {
//                 GameObject obj = Instantiate(questItemPrefab, questItemContainer);
//                 UI_QuestItem questItem = obj.GetComponent<UI_QuestItem>();
//                 if (questItem != null)
//                 {
//                     questItem.Setup(quest, OnRewardClaimed);
//                 }
//             }
//         }

//         private void OnRewardClaimed()
//         {
//             // When a reward is successfully claimed, refresh the UI so it drops to the bottom or shows "Claimed"
//             RefreshQuestList();
//         }
//     }
// }
