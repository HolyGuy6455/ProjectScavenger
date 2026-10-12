// using System;
// using UnityEngine;
// using UnityEngine.UI;
// using Scavenger.Network;
// using Scavenger.Network.DTOs;
// using Scavenger.Data;
// using System.Collections;
// using UnityEngine.Networking;
// using TMPro; // Assuming TextMeshPro is used for UI text

// namespace Scavenger.UI
// {
//     public class UI_QuestItem : MonoBehaviour
//     {
//         [Header("UI Elements")]
//         [SerializeField] private TextMeshProUGUI titleText;
//         [SerializeField] private TextMeshProUGUI descText;
//         [SerializeField] private TextMeshProUGUI progressText;
//         [SerializeField] private Image progressBarFill;
//         [SerializeField] private Button claimButton;
//         [SerializeField] private TextMeshProUGUI claimButtonText;
//         [SerializeField] private GameObject rewardIconPrefab; // Optional if you instantiate icons
//         [SerializeField] private Transform rewardContainer; // Where to put reward icons/text

//         private UserQuestDto _currentQuest;
//         private Action _onRewardClaimed;

//         public void Setup(UserQuestDto quest, Action onRewardClaimed)
//         {
//             _currentQuest = quest;
//             _onRewardClaimed = onRewardClaimed;

//             // Fetch master data for this quest to display info
//             var masterData = DataManager.Instance.GetQuest(_currentQuest.questId);
//             if (masterData == null)
//             {
//                 Debug.LogError($"Quest master data missing for ID: {_currentQuest.questId}");
//                 return;
//             }

//             titleText.text = masterData.Name;
//             descText.text = masterData.Desc;
            
//             progressText.text = $"{_currentQuest.progressValue}/{masterData.TargetCount}";
//             progressBarFill.fillAmount = (float)_currentQuest.progressValue / masterData.TargetCount;

//             UpdateClaimButtonState(masterData);
            
//             // TODO: Populate rewardContainer with masterData.Rewards if necessary
//             // For now, clear it or just leave as is
//         }

//         private void UpdateClaimButtonState(QuestData masterData)
//         {
//             claimButton.onClick.RemoveAllListeners();

//             if (_currentQuest.isRewarded)
//             {
//                 claimButton.interactable = false;
//                 if (claimButtonText != null) claimButtonText.text = "수령 완료";
//             }
//             else if (_currentQuest.isCompleted || _currentQuest.progressValue >= masterData.TargetCount)
//             {
//                 claimButton.interactable = true;
//                 if (claimButtonText != null) claimButtonText.text = "보상 받기";
//                 claimButton.onClick.AddListener(OnClickClaim);
//             }
//             else
//             {
//                 claimButton.interactable = false;
//                 if (claimButtonText != null) claimButtonText.text = "진행 중";
//             }
//         }

//         private void OnClickClaim()
//         {
//             claimButton.interactable = false;
//             StartCoroutine(ClaimRewardRoutine());
//         }

//         private IEnumerator ClaimRewardRoutine()
//         {
//             string url = $"{NetworkAuthManager.Instance.ServerUrl}/api/quest/reward";
//             var requestDto = new QuestRewardRequest { questId = _currentQuest.questId };
//             string jsonData = JsonUtility.ToJson(requestDto);

//             using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
//             {
//                 byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonData);
//                 request.uploadHandler = new UploadHandlerRaw(bodyRaw);
//                 request.downloadHandler = new DownloadHandlerBuffer();
//                 request.SetRequestHeader("Content-Type", "application/json");
//                 request.SetRequestHeader("Authorization", $"Bearer {NetworkAuthManager.Instance.AuthToken}");

//                 yield return request.SendWebRequest();

//                 if (request.result == UnityWebRequest.Result.Success)
//                 {
//                     Debug.Log($"[QuestItem] 보상 수령 성공! QuestID: {_currentQuest.questId}");
//                     var response = JsonUtility.FromJson<QuestRewardResponse>(request.downloadHandler.text);
                    
//                     _currentQuest.isRewarded = true;
                    
//                     // Update locally cached item amounts if necessary
//                     if (response.addedItems != null || response.addedGameItems != null)
//                     {
//                         UserDataManager.Instance.FetchItemList(); // Refresh items
//                     }

//                     _onRewardClaimed?.Invoke();
//                 }
//                 else
//                 {
//                     Debug.LogError($"[QuestItem] 보상 수령 실패: {request.error}\n{request.downloadHandler.text}");
//                     claimButton.interactable = true; // allow retry
//                 }
//             }
//         }
//     }
// }
