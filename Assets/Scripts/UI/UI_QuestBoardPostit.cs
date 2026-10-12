using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using TMPro;
using Scavenger.Network;
using Scavenger.Network.DTOs;
using UnityFigmaBridge.Runtime.UI;

namespace Scavenger.UI
{
    public class UI_QuestBoardPostit : MonoBehaviour
    {
        [Serializable]
        public struct PostitPalette
        {
            public Color postitFill;
            public Color stripeFill;
            public Color magnetFill;
            public Color magnetStroke;

            public PostitPalette(string p, string s, string mf, string ms)
            {
                ColorUtility.TryParseHtmlString("#" + p, out postitFill);
                ColorUtility.TryParseHtmlString("#" + s, out stripeFill);
                ColorUtility.TryParseHtmlString("#" + mf, out magnetFill);
                ColorUtility.TryParseHtmlString("#" + ms, out magnetStroke);
            }
        }

        private static readonly PostitPalette[] Palettes = new PostitPalette[]
        {
            new PostitPalette("FFFDE7", "F6C90E", "F6C90E", "D4A017"),
            new PostitPalette("FFF5F5", "FEB2B2", "FEB2B2", "E05252"),
            new PostitPalette("FAF5FF", "B794F4", "B794F4", "805AD5"),
            new PostitPalette("F0FFF4", "9AE6B4", "68D391", "38A169"),
            new PostitPalette("EBF8FF", "63B3ED", "63B3ED", "3182CE")
        };
        [Header("UI Texts")]
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI descText;
        [SerializeField] private TextMeshProUGUI progressText;
        [SerializeField] private Image progressFill;

        [Header("Rewards")]
        [SerializeField] private List<ItemSlotWrapper> rewardSlots;

        [Header("Figma Images for Styling")]
        [SerializeField] private FigmaImage bgFigmaImage;
        [SerializeField] private FigmaImage stripeFigmaImage;
        [SerializeField] private FigmaImage magnetFigmaImage;

        [Header("UI Elements")]
        [SerializeField] private GameObject goObject; 
        [SerializeField] private Button goButton;  

        [SerializeField] private GameObject claimObject; 
        [SerializeField] private Button claimButton;       
        
        [SerializeField] private GameObject stampReceived; 

        private UserQuestDto _currentQuest;
        private QuestData _masterData;
        private Action _onRewardClaimed;

        public void Setup(UserQuestDto quest, Action onRewardClaimed)
        {
            _currentQuest = quest;
            _onRewardClaimed = onRewardClaimed;

            _masterData = DataManager.Instance.GetData<QuestData>("Quest", _currentQuest.questId);
            if (_masterData == null) return;

            if (titleText != null) titleText.text = _masterData.Name;
            if (descText != null) descText.text = _masterData.Desc;
            
            if (progressText != null)
            {
                progressText.text = $"{_currentQuest.progressValue}/{_masterData.TargetCount}";
            }
            if (progressFill != null && _masterData.TargetCount > 0)
            {
                progressFill.fillAmount = (float)_currentQuest.progressValue / _masterData.TargetCount;
            }

            // 보상 슬롯 세팅 (에디터에서 연결한 ItemSlotWrapper 개수만큼 처리)
            if (rewardSlots != null)
            {
                for (int i = 0; i < rewardSlots.Count; i++)
                {
                    if (_masterData.Rewards != null && i < _masterData.Rewards.Length)
                    {
                        rewardSlots[i].gameObject.SetActive(true);
                        var rewardInfo = _masterData.Rewards[i];
                        rewardSlots[i].SetRewardData(LoadRewardIcon(rewardInfo), rewardInfo.Count);
                    }
                    else
                    {
                        rewardSlots[i].gameObject.SetActive(false);
                    }
                }
            }

            ApplyRandomStyle();
            UpdateState();
        }

        private Sprite LoadRewardIcon(QuestReward reward)
        {
            if (reward.Type == "USER_ITEM")
            {
                var currency = DataManager.Instance.GetData<UserCurrencyData>("UserCurrency", reward.Id);
                return LoadRewardSprite(currency?.ui_resources, "UserCurrency");
            }

            if (reward.Type == "ITEM")
            {
                var item = DataManager.Instance.GetData<ItemData>("Item", reward.Id);
                return LoadRewardSprite(item?.uiResource, "Items");
            }

            return null;
        }

        private Sprite LoadRewardSprite(string resourceName, string folder)
        {
            if (string.IsNullOrWhiteSpace(resourceName) || resourceName == "_") return null;

            string iconName = System.IO.Path.GetFileNameWithoutExtension(resourceName);
            return Resources.Load<Sprite>($"{folder}/{iconName}") ?? Resources.Load<Sprite>(iconName);
        }

        private void ApplyRandomStyle()
        {
            var palette = Palettes[UnityEngine.Random.Range(0, Palettes.Length)];
            
            if (bgFigmaImage != null)
                bgFigmaImage.FillColor = palette.postitFill;
                
            if (stripeFigmaImage != null)
                stripeFigmaImage.FillColor = palette.stripeFill;
                
            if (magnetFigmaImage != null)
            {
                magnetFigmaImage.FillColor = palette.magnetFill;
                magnetFigmaImage.StrokeColor = palette.magnetStroke;
            }

            float randomZ = UnityEngine.Random.Range(-3f, 3f);
            transform.localRotation = Quaternion.Euler(0, 0, randomZ);
        }

        private void UpdateState()
        {
            if (goObject != null) goObject.SetActive(false); 
            if (claimObject != null) claimObject.SetActive(false);
            if (stampReceived != null) stampReceived.SetActive(false);

            if (_currentQuest.isRewarded)
            {
                // 보상 수령 완료
                if (stampReceived != null) stampReceived.SetActive(true);
            }
            else if (_currentQuest.isCompleted || _currentQuest.progressValue >= _masterData.TargetCount)
            {
                // 목표 달성, 보상 미수령
                if (claimObject != null)
                {
                    claimObject.SetActive(true);
                    claimButton.interactable = true;
                    claimButton.onClick.RemoveAllListeners();
                    claimButton.onClick.AddListener(OnClickClaim);
                }
            }
            else
            {
                // 목표 미달성
                if (goObject != null)
                {
                    goObject.SetActive(true);
                    goButton.onClick.RemoveAllListeners();  
                    goButton.onClick.AddListener(OnClickGo);
                }
            }
        }

        private void OnClickGo()
        {
            Debug.Log($"[{_currentQuest.questId}] 바로가기 버튼 클릭됨");
            // TODO: 해당 던전이나 UI로 보내는 로직
        }

        private void OnClickClaim()
        {
            if (claimButton != null) claimButton.interactable = false;
            StartCoroutine(ClaimRewardRoutine());
        }

        private IEnumerator ClaimRewardRoutine()
        {
            string url = $"{NetworkAuthManager.Instance.ServerUrl}/api/quest/reward";
            var requestDto = new QuestRewardRequest { questId = _currentQuest.questId };
            string jsonData = JsonUtility.ToJson(requestDto);

            using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
            {
                byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonData);
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("Authorization", $"Bearer {NetworkAuthManager.Instance.AuthToken}");

                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    _currentQuest.isRewarded = true;
                    
                    // 수령 직후 즉시 로컬 UI 상태를 갱신해 도장을 찍어줌 (서버 새로고침 대기 없이)
                    UpdateState();
                    
                    UserDataManager.Instance.FetchItemList(); 
                    
                    // 패널 쪽 리스트 전체 새로고침 트리거
                    _onRewardClaimed?.Invoke();
                }
                else
                {
                    Debug.LogError($"[UI_QuestBoardPostit] 보상 수령 실패: {request.error}\n{request.downloadHandler.text}");
                    if (claimButton != null) claimButton.interactable = true;
                }
            }
        }
    }
}
