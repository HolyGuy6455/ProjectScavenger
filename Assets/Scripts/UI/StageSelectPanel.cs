using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Scavenger.UI
{
    public class StageSelectPanel : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Transform cardContainer;
        [SerializeField] private StageSelectCard cardPrefab;
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private TextMeshProUGUI stageCountText;

        private readonly List<StageSelectCard> _spawnedCards = new List<StageSelectCard>();
        private StageType _currentStageType = StageType.Normal;

        public void SetStageTypeForNextOpen(StageType stageType)
        {
            _currentStageType = stageType == StageType.None ? StageType.Normal : stageType;
        }

        private void OnEnable()
        {
            if (OutGameUIManager.Instance != null && OutGameUIManager.Instance.SelectedStageType != StageType.None)
            {
                _currentStageType = OutGameUIManager.Instance.SelectedStageType;
            }

            LocalizationManager.OnLanguageChanged += RefreshUI;
            RefreshUI();
        }

        private void OnDisable()
        {
            LocalizationManager.OnLanguageChanged -= RefreshUI;
        }

        public void RefreshUI()
        {
            List<StageData> stages = new List<StageData>();
            if (DataManager.Instance != null)
            {
                var allStages = DataManager.Instance.GetAllData<StageData>("Stage");
                if (allStages != null && allStages.Count > 0)
                {
                    stages = allStages
                        .Where(s => s != null && s.stageType == _currentStageType)
                        .OrderBy(s => s.id)
                        .ToList();

                    if (stages.Count == 0)
                    {
                        stages = allStages
                            .Where(s => s != null)
                            .OrderBy(s => s.id)
                            .ToList();
                    }
                }
            }

            if (stageCountText != null)
            {
                LocalizedText.ApplyFont(stageCountText);
                stageCountText.text = $"기록된 지역  {stages.Count:D2}";
            }

            EnsureCardPool(stages.Count);

            for (int i = 0; i < _spawnedCards.Count; i++)
            {
                StageSelectCard card = _spawnedCards[i];
                if (card == null) continue;

                if (i < stages.Count)
                {
                    card.Setup(stages[i], i + 1, OnStageCardSelected);
                }
                else
                {
                    card.gameObject.SetActive(false);
                }
            }

            if (cardContainer is RectTransform contentRt)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(contentRt);
            }

            if (scrollRect != null)
            {
                scrollRect.horizontalNormalizedPosition = 0f;
            }
        }

        private void EnsureCardPool(int requiredCount)
        {
            if (cardContainer == null) return;

            // Collect existing children on first run (e.g. preview cards in prefab)
            if (_spawnedCards.Count == 0)
            {
                foreach (Transform child in cardContainer)
                {
                    var existingCard = child.GetComponent<StageSelectCard>();
                    if (existingCard != null)
                    {
                        _spawnedCards.Add(existingCard);
                    }
                }
            }

            if (cardPrefab == null)
            {
                cardPrefab = Resources.Load<StageSelectCard>("UIPrefabs/Node/StageSelectCard");
            }

            while (_spawnedCards.Count < requiredCount && cardPrefab != null)
            {
                StageSelectCard newCard = Instantiate(cardPrefab, cardContainer, false);
                _spawnedCards.Add(newCard);
            }
        }

        private void OnStageCardSelected(StageData stageData)
        {
            if (stageData == null) return;

            if (OutGameUIManager.Instance != null)
            {
                OutGameUIManager.Instance.OpenStageInfo(stageData.id);
            }
        }
    }
}
