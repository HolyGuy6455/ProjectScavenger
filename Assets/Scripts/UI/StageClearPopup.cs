using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System;
using TMPro;

namespace Scavenger.UI
{
    public struct LootInfo
    {
        public int itemId;
        public int count;
        public bool isBonus;
    }

    public struct CharExpResult
    {
        public CharData charData;
        public int oldLevel;
        public int newLevel;
        public int oldExp;
        public int newExp;
        public int maxExpForOldLevel;
        public int maxExpForNewLevel;
        public int addedExp;
    }
    
    public struct PlayerExpResult
    {
        public int oldLevel;
        public int newLevel;
        public int oldExp;
        public int newExp;
        public int maxExpForOldLevel;
        public int maxExpForNewLevel;
        public int addedExp;
    }

    public class StageClearPopup : MonoBehaviour
    {
        [Header("Loot Scroll View")]
        [SerializeField] private Transform scrollContent;
        [SerializeField] private StageClearItemRow rowPrefab;
        [SerializeField] private int itemsPerRow = 8;
        
        [Header("Character EXP View")]
        [Tooltip("프리팹에 미리 배치된 4개의 캐릭터 노드")]
        [SerializeField] private List<StageClearCharNode> charNodes;

        [Header("Commander EXP Banner")]
        [SerializeField] private TextMeshProUGUI commanderLevelText;
        [SerializeField] private Image commanderExpBarFill;
        [SerializeField] private TextMeshProUGUI commanderExpGainedText;
        [SerializeField] private TextMeshProUGUI commanderExpRatioText;

        [Header("Controls")]
        [SerializeField] private Button confirmButton;
        
        private List<StageClearItemRow> _spawnedRows = new List<StageClearItemRow>();
        private Action _onCloseCallback;

        public void Initialize(List<LootInfo> loots, List<CharExpResult> expResults, PlayerExpResult playerExpResult, Action onCloseCallback = null)
        {
            _onCloseCallback = onCloseCallback;
            
            if (confirmButton != null)
            {
                confirmButton.onClick.RemoveAllListeners();
                confirmButton.onClick.AddListener(() => {
                    _onCloseCallback?.Invoke();
                });
            }

            // 팝업 자체는 비활성화 상태로 둔 채 데이터 세팅만 함. PlayShowAnimation 시 켜짐.
            SetConfirmButtonInteractable(false);
            ClearLoot();

            // 0. 지휘관 경험치 초기 텍스트 세팅
            if (commanderLevelText != null) commanderLevelText.text = $"Lv.{playerExpResult.oldLevel}";
            if (commanderExpGainedText != null) commanderExpGainedText.text = "+0 EXP Gained";
            if (commanderExpRatioText != null) commanderExpRatioText.text = $"{playerExpResult.oldExp:N0} / {playerExpResult.maxExpForOldLevel:N0}";
            if (commanderExpBarFill != null) commanderExpBarFill.fillAmount = (float)playerExpResult.oldExp / playerExpResult.maxExpForOldLevel;

            // 지휘관 경험치 차오르는 애니메이션 코루틴은 PlayShowAnimation에서 팝업이 뜰 때 함께 실행합니다.
            // 여기서는 데이터를 어딘가 저장해둬야 합니다.
            _playerExpResultForAnim = playerExpResult;

            // 1. 전리품 세팅
            if (loots != null && loots.Count > 0 && rowPrefab != null && scrollContent != null)
            {
                StageClearItemRow currentRow = null;
                int currentCountInRow = 0;

                foreach (var loot in loots)
                {
                    if (currentRow == null || currentCountInRow >= itemsPerRow)
                    {
                        currentRow = Instantiate(rowPrefab, scrollContent);
                        _spawnedRows.Add(currentRow);
                        currentCountInRow = 0;
                    }

                    ItemData itemData = DataManager.Instance.GetData<ItemData>("Item", loot.itemId);
                    if (itemData != null)
                    {
                        currentRow.AddNode(itemData, loot.count, loot.isBonus);
                    }
                    
                    currentCountInRow++;
                }
            }

            // 2. 캐릭터 경험치 노드 세팅 (미리 배치된 노드 사용)
            if (charNodes != null)
            {
                for (int i = 0; i < charNodes.Count; i++)
                {
                    if (charNodes[i] == null) continue;

                    if (expResults != null && i < expResults.Count)
                    {
                        charNodes[i].gameObject.SetActive(true);
                        var expRes = expResults[i];
                        charNodes[i].Setup(
                            expRes.charData, 
                            expRes.oldLevel, 
                            expRes.newLevel, 
                            expRes.oldExp, 
                            expRes.newExp, 
                            expRes.maxExpForOldLevel, 
                            expRes.maxExpForNewLevel, 
                            expRes.addedExp
                        );
                    }
                    else
                    {
                        // 결과 데이터가 없으면 남는 노드는 숨김 처리
                        charNodes[i].gameObject.SetActive(false);
                    }
                }
            }
        }

        private PlayerExpResult _playerExpResultForAnim;

        public void PlayShowAnimation(Action onAnimationFinished)
        {
            gameObject.SetActive(true);
            
            // 캔버스 그룹을 가져오거나 추가해서 투명도를 조절합니다.
            CanvasGroup cg = GetComponent<CanvasGroup>();
            if (cg == null) cg = gameObject.AddComponent<CanvasGroup>();
            
            cg.alpha = 0f;
            StartCoroutine(FadeInRoutine(cg, 0.5f, () => 
            {
                // 팝업이 다 나타난 뒤에 경험치 바 차오르는 연출 시작
                StartCoroutine(CommanderExpBarAnimation(_playerExpResultForAnim));
                if (charNodes != null)
                {
                    foreach (var charNode in charNodes)
                    {
                        if (charNode != null) charNode.PlayExpBarAnimation();
                    }
                }
                onAnimationFinished?.Invoke();
            }));
        }

        private System.Collections.IEnumerator CommanderExpBarAnimation(PlayerExpResult expRes)
        {
            if (commanderExpBarFill == null) yield break;

            float duration = 1.0f; // 애니메이션 지속 시간
            float time = 0f;
            
            bool isLevelUp = expRes.newLevel > expRes.oldLevel;

            if (isLevelUp)
            {
                float startFill = (float)expRes.oldExp / expRes.maxExpForOldLevel;
                float halfDuration = duration / 2f;
                
                while (time < halfDuration)
                {
                    time += Time.deltaTime;
                    float progress = time / halfDuration;
                    commanderExpBarFill.fillAmount = Mathf.Lerp(startFill, 1f, progress);
                    
                    int currentAdded = Mathf.RoundToInt(Mathf.Lerp(0, expRes.addedExp / 2f, progress));
                    int currentExp = expRes.oldExp + currentAdded;
                    if (commanderExpGainedText != null) commanderExpGainedText.text = $"+{currentAdded:N0} EXP Gained";
                    if (commanderExpRatioText != null) commanderExpRatioText.text = $"{currentExp:N0} / {expRes.maxExpForOldLevel:N0}";
                    yield return null;
                }

                // 레벨업 순간 텍스트 업데이트
                if (commanderLevelText != null) commanderLevelText.text = $"Lv.{expRes.newLevel}";

                time = 0f;
                float targetFill = (float)expRes.newExp / expRes.maxExpForNewLevel;

                while (time < halfDuration)
                {
                    time += Time.deltaTime;
                    float progress = time / halfDuration;
                    commanderExpBarFill.fillAmount = Mathf.Lerp(0f, targetFill, progress);
                    
                    int currentAdded = Mathf.RoundToInt(Mathf.Lerp(expRes.addedExp / 2f, expRes.addedExp, progress));
                    int currentExp = Mathf.RoundToInt(Mathf.Lerp(0, expRes.newExp, progress));
                    if (commanderExpGainedText != null) commanderExpGainedText.text = $"+{currentAdded:N0} EXP Gained";
                    if (commanderExpRatioText != null) commanderExpRatioText.text = $"{currentExp:N0} / {expRes.maxExpForNewLevel:N0}";
                    yield return null;
                }
                
                commanderExpBarFill.fillAmount = targetFill;
                if (commanderExpGainedText != null) commanderExpGainedText.text = $"+{expRes.addedExp:N0} EXP Gained";
                if (commanderExpRatioText != null) commanderExpRatioText.text = $"{expRes.newExp:N0} / {expRes.maxExpForNewLevel:N0}";
            }
            else
            {
                float startFill = (float)expRes.oldExp / expRes.maxExpForOldLevel;
                float targetFill = (float)expRes.newExp / expRes.maxExpForNewLevel;

                while (time < duration)
                {
                    time += Time.deltaTime;
                    float progress = time / duration;
                    commanderExpBarFill.fillAmount = Mathf.Lerp(startFill, targetFill, progress);
                    
                    int currentAdded = Mathf.RoundToInt(Mathf.Lerp(0, expRes.addedExp, progress));
                    int currentExp = expRes.oldExp + currentAdded;
                    if (commanderExpGainedText != null) commanderExpGainedText.text = $"+{currentAdded:N0} EXP Gained";
                    if (commanderExpRatioText != null) commanderExpRatioText.text = $"{currentExp:N0} / {expRes.maxExpForOldLevel:N0}";
                    yield return null;
                }
                
                commanderExpBarFill.fillAmount = targetFill;
                if (commanderExpGainedText != null) commanderExpGainedText.text = $"+{expRes.addedExp:N0} EXP Gained";
                if (commanderExpRatioText != null) commanderExpRatioText.text = $"{expRes.newExp:N0} / {expRes.maxExpForNewLevel:N0}";
            }
        }

        private System.Collections.IEnumerator FadeInRoutine(CanvasGroup cg, float duration, Action onComplete)
        {
            float time = 0f;
            while (time < duration)
            {
                time += Time.deltaTime;
                cg.alpha = Mathf.Clamp01(time / duration);
                yield return null;
            }
            cg.alpha = 1f;
            onComplete?.Invoke();
        }

        public void SetConfirmButtonInteractable(bool isInteractable)
        {
            if (confirmButton != null)
            {
                confirmButton.interactable = isInteractable;
            }
        }

        private void ClearLoot()
        {
            foreach (var row in _spawnedRows)
            {
                if (row != null) Destroy(row.gameObject);
            }
            _spawnedRows.Clear();
            
            // Editor dummy object clear
            foreach (Transform child in scrollContent)
            {
                Destroy(child.gameObject);
            }
        }

        // UI 버튼 이벤트에 연결
        public void OnClickClose()
        {
            // 이 버튼을 눌렀을 때 팝업이 바로 꺼질지 여부는 매니저가 결정하거나 여기서 바로 꺼짐.
            // 일단 바로 꺼지는 대신 콜백만 날리고 매니저가 통제하게 할 수도 있음.
            // 기획 상 레벨업 팝업이 끝나야 이 버튼이 켜지고, 이 버튼을 누르면 로비로 감.
            gameObject.SetActive(false);
            _onCloseCallback?.Invoke();
        }
    }
}
