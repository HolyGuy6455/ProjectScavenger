using System;
using System.Collections.Generic;
using UnityEngine;

namespace Scavenger.UI
{
    public enum OutGamePanelType
    {
        Login,
        MainLobby,
        ReadyToBattle,
        FriendParty,
        CharacterDeck,
        SelectOperation,
        SelectStage,
        TrainCharacter,
        StageInfo
    }

    public enum OutGamePopupType
    {
        CharacterSelect,
        CharacterLevelUpCelebration,
        SkillLevelUpCelebration,
        SkillLevelUp,
        SkillSelect
    }

    public class OutGameUIManager : MonoBehaviour
    {
        public static OutGameUIManager Instance { get; private set; }

        [Header("Canvas Settings")]
        [SerializeField] private Transform canvasTransform;

        [Header("Panel Prefabs")]
        [SerializeField] private GameObject loginPanelPrefab;
        [SerializeField] private GameObject mainLobbyPanelPrefab;
        [SerializeField] private GameObject readyToBattlePanelPrefab;
        [SerializeField] private GameObject friendPartyPanelPrefab;
        [SerializeField] private GameObject characterDeckPanelPrefab;
        [SerializeField] private GameObject selectOperationPanelPrefab;
        [SerializeField] private GameObject selectStagePanelPrefab;
        [SerializeField] private GameObject trainCharacterPanelPrefab;
        [SerializeField] private GameObject stageInfoPanelPrefab;

        [Header("Popup Prefabs")]
        [SerializeField] private GameObject characterSelectPopupPrefab;
        [SerializeField] private GameObject characterLevelUpCelebrationPopupPrefab;
        [SerializeField] private GameObject skillLevelUpCelebrationPopupPrefab;
        [SerializeField] private GameObject skillLevelUpPopupPrefab;
        [SerializeField] private GameObject skillSelectPopupPrefab;

        // [Header("Persistent UI")]
        // [SerializeField] private GameObject persistentPartyListUI; // 파티 리스트 패널

        private Dictionary<OutGamePanelType, GameObject> _cachedPanels = new Dictionary<OutGamePanelType, GameObject>();
        private Stack<OutGamePanelType> _panelHistory = new Stack<OutGamePanelType>();
        private OutGamePanelType _currentPanelType;
        private Dictionary<OutGamePopupType, GameObject> _cachedPopups = new Dictionary<OutGamePopupType, GameObject>();
        private Stack<GameObject> _activePopups = new Stack<GameObject>();

        private bool IsPartyScope(OutGamePanelType type)
        {
            return type == OutGamePanelType.StageInfo || 
                   type == OutGamePanelType.CharacterDeck || 
                   type == OutGamePanelType.FriendParty;
        }

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        private void Start()
        {
            ShowPanel(OutGamePanelType.Login);
        }

        private void Update()
        {
            if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                GoBack();
            }
        }

        public void ShowPanel(OutGamePanelType panelType)
        {
            if (panelType == OutGamePanelType.MainLobby)
            {
                _panelHistory.Clear();
            }

            if (!IsPartyScope(panelType))
            {
                Scavenger.Network.NetworkPartyManager.Instance?.LeavePartyAndDestroy();
            }
            
            // 현재 떠있는 패널이 있다면 무조건 끕니다.
            if (_cachedPanels.ContainsKey(_currentPanelType) && _cachedPanels[_currentPanelType] != null && _cachedPanels[_currentPanelType].activeSelf)
            {
                _cachedPanels[_currentPanelType].SetActive(false);
                
                // 메인 로비로 진입하는 게 아닐 때만 히스토리에 저장
                if (panelType != OutGamePanelType.MainLobby)
                {
                    if (_panelHistory.Count == 0 || _panelHistory.Peek() != _currentPanelType)
                    {
                        _panelHistory.Push(_currentPanelType);
                    }
                }
            }

            // if (persistentPartyListUI != null)
            // {
            //     persistentPartyListUI.SetActive(panelType != OutGamePanelType.Login);
            // }

            if (_cachedPanels.TryGetValue(panelType, out GameObject cachedPanel) && cachedPanel != null)
            {
                cachedPanel.SetActive(true);
                cachedPanel.transform.SetAsLastSibling();
            }
            else
            {
                GameObject prefabToLoad = GetPanelPrefabByType(panelType);
                if (prefabToLoad != null && canvasTransform != null)
                {
                    GameObject newPanel = Instantiate(prefabToLoad, canvasTransform, false);
                    RectTransform rect = newPanel.GetComponent<RectTransform>();
                    if (rect != null)
                    {
                        rect.anchoredPosition = Vector2.zero;
                        rect.localScale = Vector3.one;
                    }
                    _cachedPanels[panelType] = newPanel;
                    newPanel.SetActive(true);
                    newPanel.transform.SetAsLastSibling();
                }
                else
                {
                    Debug.LogError($"[OutGameUIManager] 패널 프리팹 또는 캔버스 연결 누락! ({panelType})");
                    return;
                }
            }

            _currentPanelType = panelType;
        }

        public T ShowPopup<T>(OutGamePopupType popupType) where T : MonoBehaviour
        {
            GameObject popupObj = null;

            if (_cachedPopups.TryGetValue(popupType, out GameObject cachedPopup) && cachedPopup != null)
            {
                popupObj = cachedPopup;
                popupObj.SetActive(true);
            }
            else
            {
                GameObject prefabToLoad = GetPopupPrefabByType(popupType);
                if (prefabToLoad != null && canvasTransform != null)
                {
                    popupObj = Instantiate(prefabToLoad, canvasTransform, false);
                    RectTransform rect = popupObj.GetComponent<RectTransform>();
                    if (rect != null)
                    {
                        rect.anchoredPosition = Vector2.zero;
                        rect.localScale = Vector3.one;
                    }
                    _cachedPopups[popupType] = popupObj;
                    popupObj.SetActive(true);
                }
                else
                {
                    Debug.LogError($"[OutGameUIManager] 윈도우 프리팹 또는 캔버스 연결 누락! ({popupType})");
                    return null;
                }
            }

            popupObj.transform.SetAsLastSibling();
            if (_activePopups.Count == 0 || _activePopups.Peek() != popupObj)
            {
                _activePopups.Push(popupObj);
            }

            return popupObj.GetComponent<T>();
        }

        public void GoBack()
        {
            if (_activePopups.Count > 0)
            {
                GameObject topPopup = _activePopups.Pop();
                if (topPopup != null) topPopup.SetActive(false);
                return;
            }

            if (_panelHistory.Count > 0)
            {
                OutGamePanelType previousPanelType = _panelHistory.Pop();
                if (_cachedPanels.TryGetValue(_currentPanelType, out GameObject currentPanel) && currentPanel != null)
                {
                    currentPanel.SetActive(false);
                }
                if (_cachedPanels.TryGetValue(previousPanelType, out GameObject prevPanel) && prevPanel != null)
                {
                    prevPanel.SetActive(true);
                    prevPanel.transform.SetAsLastSibling();
                    // if (persistentPartyListUI != null)
                    // {
                    //     persistentPartyListUI.SetActive(previousPanelType != OutGamePanelType.Login);
                    // }
                    _currentPanelType = previousPanelType;

                    if (!IsPartyScope(previousPanelType))
                    {
                        Scavenger.Network.NetworkPartyManager.Instance?.LeavePartyAndDestroy();
                    }
                }
            }
        }

        public void ClosePopup(GameObject popup)
        {
            if (popup == null) return;
            popup.SetActive(false);
            if (_activePopups.Count > 0 && _activePopups.Peek() == popup)
            {
                _activePopups.Pop();
            }
            else
            {
                Stack<GameObject> temp = new Stack<GameObject>();
                while (_activePopups.Count > 0)
                {
                    var w = _activePopups.Pop();
                    if (w != popup) temp.Push(w);
                }
                while (temp.Count > 0) _activePopups.Push(temp.Pop());
            }
        }

        private GameObject GetPanelPrefabByType(OutGamePanelType type)
        {
            switch (type)
            {
                case OutGamePanelType.Login: return loginPanelPrefab;
                case OutGamePanelType.MainLobby: return mainLobbyPanelPrefab;
                case OutGamePanelType.ReadyToBattle: return readyToBattlePanelPrefab;
                case OutGamePanelType.FriendParty: return friendPartyPanelPrefab;
                case OutGamePanelType.CharacterDeck: return characterDeckPanelPrefab;
                case OutGamePanelType.SelectOperation: return selectOperationPanelPrefab;
                case OutGamePanelType.SelectStage: return selectStagePanelPrefab;
                case OutGamePanelType.TrainCharacter: return trainCharacterPanelPrefab;
                case OutGamePanelType.StageInfo: return stageInfoPanelPrefab;
            }
            return null;
        }

        private GameObject GetPopupPrefabByType(OutGamePopupType type)
        {
            switch (type)
            {
                case OutGamePopupType.CharacterSelect: return characterSelectPopupPrefab;
                case OutGamePopupType.CharacterLevelUpCelebration: return characterLevelUpCelebrationPopupPrefab;
                case OutGamePopupType.SkillLevelUpCelebration: return skillLevelUpCelebrationPopupPrefab;
                case OutGamePopupType.SkillLevelUp: return skillLevelUpPopupPrefab;
                case OutGamePopupType.SkillSelect: return skillSelectPopupPrefab;
            }
            return null;
        }

        public void OnClickGoToLobby() => ShowPanel(OutGamePanelType.MainLobby);
        public void OnClickGoToReadyToBattle() => ShowPanel(OutGamePanelType.ReadyToBattle);
        public void OnClickGoToParty() => ShowPanel(OutGamePanelType.FriendParty);
        public void OnClickGoToCharacterDeck() => ShowPanel(OutGamePanelType.CharacterDeck);
        public void OnClickGoToSelectOperation() => ShowPanel(OutGamePanelType.SelectOperation);
        public void OnClickGoToSelectStage() => ShowPanel(OutGamePanelType.SelectStage);
        public void OnClickGoToTrainCharacter() => ShowPanel(OutGamePanelType.TrainCharacter);
        public void OnClickGoToStageInfo() => ShowPanel(OutGamePanelType.StageInfo);
    }
}
