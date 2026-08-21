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
        CharacterDeck
    }

    public enum OutGameWindowType
    {
        CharacterSelect
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

        [Header("Window Prefabs")]
        [SerializeField] private GameObject characterSelectWindowPrefab;

        [Header("Persistent UI")]
        [SerializeField] private GameObject persistentPartyListUI; // 파티 리스트 패널

        private Dictionary<OutGamePanelType, GameObject> _cachedPanels = new Dictionary<OutGamePanelType, GameObject>();
        private Stack<OutGamePanelType> _panelHistory = new Stack<OutGamePanelType>();
        private OutGamePanelType _currentPanelType;
        private Dictionary<OutGameWindowType, GameObject> _cachedWindows = new Dictionary<OutGameWindowType, GameObject>();
        private Stack<GameObject> _activeWindows = new Stack<GameObject>();

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

            if (persistentPartyListUI != null)
            {
                persistentPartyListUI.SetActive(panelType != OutGamePanelType.Login);
            }

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
                }
                else
                {
                    Debug.LogError($"[OutGameUIManager] 패널 프리팹 또는 캔버스 연결 누락! ({panelType})");
                    return;
                }
            }

            _currentPanelType = panelType;
        }

        public T ShowWindow<T>(OutGameWindowType windowType) where T : MonoBehaviour
        {
            GameObject windowObj = null;

            if (_cachedWindows.TryGetValue(windowType, out GameObject cachedWindow) && cachedWindow != null)
            {
                windowObj = cachedWindow;
                windowObj.SetActive(true);
            }
            else
            {
                GameObject prefabToLoad = GetWindowPrefabByType(windowType);
                if (prefabToLoad != null && canvasTransform != null)
                {
                    windowObj = Instantiate(prefabToLoad, canvasTransform, false);
                    RectTransform rect = windowObj.GetComponent<RectTransform>();
                    if (rect != null)
                    {
                        rect.anchoredPosition = Vector2.zero;
                        rect.localScale = Vector3.one;
                    }
                    _cachedWindows[windowType] = windowObj;
                }
                else
                {
                    Debug.LogError($"[OutGameUIManager] 윈도우 프리팹 또는 캔버스 연결 누락! ({windowType})");
                    return null;
                }
            }

            windowObj.transform.SetAsLastSibling();
            if (_activeWindows.Count == 0 || _activeWindows.Peek() != windowObj)
            {
                _activeWindows.Push(windowObj);
            }

            return windowObj.GetComponent<T>();
        }

        public void GoBack()
        {
            if (_activeWindows.Count > 0)
            {
                GameObject topWindow = _activeWindows.Pop();
                if (topWindow != null) topWindow.SetActive(false);
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
                    if (persistentPartyListUI != null)
                    {
                        persistentPartyListUI.SetActive(previousPanelType != OutGamePanelType.Login);
                    }
                    _currentPanelType = previousPanelType;
                }
            }
        }

        public void CloseWindow(GameObject window)
        {
            if (window == null) return;
            window.SetActive(false);
            if (_activeWindows.Count > 0 && _activeWindows.Peek() == window)
            {
                _activeWindows.Pop();
            }
            else
            {
                Stack<GameObject> temp = new Stack<GameObject>();
                while (_activeWindows.Count > 0)
                {
                    var w = _activeWindows.Pop();
                    if (w != window) temp.Push(w);
                }
                while (temp.Count > 0) _activeWindows.Push(temp.Pop());
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
            }
            return null;
        }

        private GameObject GetWindowPrefabByType(OutGameWindowType type)
        {
            switch (type)
            {
                case OutGameWindowType.CharacterSelect: return characterSelectWindowPrefab;
            }
            return null;
        }

        public void OnClickGoToLobby() => ShowPanel(OutGamePanelType.MainLobby);
        public void OnClickGoToReadyToBattle() => ShowPanel(OutGamePanelType.ReadyToBattle);
        public void OnClickGoToParty() => ShowPanel(OutGamePanelType.FriendParty);
        public void OnClickGoToCharacterDeck() => ShowPanel(OutGamePanelType.CharacterDeck);
    }
}
