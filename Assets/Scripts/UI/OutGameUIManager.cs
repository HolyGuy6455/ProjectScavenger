using System;
using System.Collections.Generic;
using UnityEngine;

namespace Scavenger.UI
{
    public enum OutGamePanelType
    {
        // 기존 씬과 프리팹에 저장된 패널 번호를 유지합니다.
        MainLobby = 1,
        ReadyToBattle,
        FriendParty,
        CharacterDeck,
        SelectOperation,
        SelectStage,
        TrainCharacter,
        StageInfo,
        Storage, // 계정 창고 패널 추가
        QuestBoard,
        Setting
    }

    public enum OutGamePopupType
    {
        CharacterSelect,
        CharacterLevelUpCelebration,
        SkillLevelUpCelebration,
        SkillLevelUp,
        SkillSelect,
        ItemInfo,
        DefaultConfirm,
        PartyPick
    }

    public class OutGameUIManager : MonoBehaviour
    {
        // =====================================================================
        // UI 전환 애니메이션 시간 상수 (빌드 시 이 상수 값들이 기본 사용됩니다)
        // =====================================================================
        public const float DEFAULT_PANEL_BG_FADE_IN_DURATION  = 0.20f;
        public const float DEFAULT_PANEL_UI_FADE_IN_DELAY     = 0.15f;
        public const float DEFAULT_PANEL_UI_FADE_IN_DURATION  = 0.20f;

        public const float DEFAULT_PANEL_UI_FADE_OUT_DURATION = 0.15f;
        public const float DEFAULT_PANEL_BG_FADE_OUT_DELAY    = 0.10f;
        public const float DEFAULT_PANEL_BG_FADE_OUT_DURATION = 0.20f;

        public const float DEFAULT_POPUP_FADE_IN_DURATION     = 0.15f;
        public const float DEFAULT_POPUP_FADE_OUT_DURATION    = 0.15f;

        public static OutGameUIManager Instance { get; private set; }

#if UNITY_EDITOR
        [Header("Transition Timing (Editor Only Override)")]
        [Tooltip("활성화하면 유니티 에디터 플레이 중 아래 인스펙터 수치로 상수 값을 실시간 오버라이드합니다.")]
        [SerializeField] private bool overrideTransitionTimingInEditor = true;
        [Min(0f)] [SerializeField] private float editorPanelBgFadeInDuration  = DEFAULT_PANEL_BG_FADE_IN_DURATION;
        [Min(0f)] [SerializeField] private float editorPanelUiFadeInDelay     = DEFAULT_PANEL_UI_FADE_IN_DELAY;
        [Min(0f)] [SerializeField] private float editorPanelUiFadeInDuration  = DEFAULT_PANEL_UI_FADE_IN_DURATION;
        [Min(0f)] [SerializeField] private float editorPanelUiFadeOutDuration = DEFAULT_PANEL_UI_FADE_OUT_DURATION;
        [Min(0f)] [SerializeField] private float editorPanelBgFadeOutDelay    = DEFAULT_PANEL_BG_FADE_OUT_DELAY;
        [Min(0f)] [SerializeField] private float editorPanelBgFadeOutDuration = DEFAULT_PANEL_BG_FADE_OUT_DURATION;
        [Min(0f)] [SerializeField] private float editorPopupFadeInDuration    = DEFAULT_POPUP_FADE_IN_DURATION;
        [Min(0f)] [SerializeField] private float editorPopupFadeOutDuration   = DEFAULT_POPUP_FADE_OUT_DURATION;
#endif

        public float PanelBgFadeInDuration
        {
            get
            {
#if UNITY_EDITOR
                if (overrideTransitionTimingInEditor) return Mathf.Max(0f, editorPanelBgFadeInDuration);
#endif
                return DEFAULT_PANEL_BG_FADE_IN_DURATION;
            }
        }

        public float PanelUiFadeInDelay
        {
            get
            {
#if UNITY_EDITOR
                if (overrideTransitionTimingInEditor) return Mathf.Max(0f, editorPanelUiFadeInDelay);
#endif
                return DEFAULT_PANEL_UI_FADE_IN_DELAY;
            }
        }

        public float PanelUiFadeInDuration
        {
            get
            {
#if UNITY_EDITOR
                if (overrideTransitionTimingInEditor) return Mathf.Max(0f, editorPanelUiFadeInDuration);
#endif
                return DEFAULT_PANEL_UI_FADE_IN_DURATION;
            }
        }

        public float PanelUiFadeOutDuration
        {
            get
            {
#if UNITY_EDITOR
                if (overrideTransitionTimingInEditor) return Mathf.Max(0f, editorPanelUiFadeOutDuration);
#endif
                return DEFAULT_PANEL_UI_FADE_OUT_DURATION;
            }
        }

        public float PanelBgFadeOutDelay
        {
            get
            {
#if UNITY_EDITOR
                if (overrideTransitionTimingInEditor) return Mathf.Max(0f, editorPanelBgFadeOutDelay);
#endif
                return DEFAULT_PANEL_BG_FADE_OUT_DELAY;
            }
        }

        public float PanelBgFadeOutDuration
        {
            get
            {
#if UNITY_EDITOR
                if (overrideTransitionTimingInEditor) return Mathf.Max(0f, editorPanelBgFadeOutDuration);
#endif
                return DEFAULT_PANEL_BG_FADE_OUT_DURATION;
            }
        }

        public float PopupFadeInDuration
        {
            get
            {
#if UNITY_EDITOR
                if (overrideTransitionTimingInEditor) return Mathf.Max(0f, editorPopupFadeInDuration);
#endif
                return DEFAULT_POPUP_FADE_IN_DURATION;
            }
        }

        public float PopupFadeOutDuration
        {
            get
            {
#if UNITY_EDITOR
                if (overrideTransitionTimingInEditor) return Mathf.Max(0f, editorPopupFadeOutDuration);
#endif
                return DEFAULT_POPUP_FADE_OUT_DURATION;
            }
        }

        [Header("Canvas Settings")]
        [SerializeField] private Transform canvasTransform;

        [Header("Panel Prefabs")]
        [SerializeField] private GameObject mainLobbyPanelPrefab;
        [SerializeField] private GameObject readyToBattlePanelPrefab;
        [SerializeField] private GameObject friendPartyPanelPrefab;
        [SerializeField] private GameObject characterDeckPanelPrefab;
        [SerializeField] private GameObject selectOperationPanelPrefab;
        [SerializeField] private GameObject selectStagePanelPrefab;
        [SerializeField] private GameObject trainCharacterPanelPrefab;
        [SerializeField] private GameObject stageInfoPanelPrefab;
        [SerializeField] private GameObject storagePanelPrefab; // 계정 창고 프리팹
        [SerializeField] private GameObject questBoardPanelPrefab;
        [SerializeField] private GameObject settingPanelPrefab;

        [Header("Popup Prefabs")]
        [SerializeField] private GameObject characterSelectPopupPrefab;
        [SerializeField] private GameObject partyPickPopupPrefab;
        [SerializeField] private GameObject characterLevelUpCelebrationPopupPrefab;
        [SerializeField] private GameObject skillLevelUpCelebrationPopupPrefab;
        [SerializeField] private GameObject skillLevelUpPopupPrefab;
        [SerializeField] private GameObject skillSelectPopupPrefab;
        [SerializeField] private GameObject itemInfoPopupPrefab;
        [SerializeField] private GameObject defaultConfirmPopupPrefab;

        // [Header("Persistent UI")]
        // [SerializeField] private GameObject persistentPartyListUI; // 파티 리스트 패널

        private struct PanelHistoryEntry
        {
            public OutGamePanelType panelType;
            public List<GameObject> openPopups; // 아래(먼저 열린 팝업) -> 위(나중에 열린 팝업) 순서
        }

        private Dictionary<OutGamePanelType, GameObject> _cachedPanels = new Dictionary<OutGamePanelType, GameObject>();
        private Stack<PanelHistoryEntry> _panelHistory = new Stack<PanelHistoryEntry>();
        private OutGamePanelType _currentPanelType = OutGamePanelType.MainLobby;
        private Dictionary<OutGamePopupType, GameObject> _cachedPopups = new Dictionary<OutGamePopupType, GameObject>();
        private Stack<GameObject> _activePopups = new Stack<GameObject>();
        private readonly Dictionary<GameObject, UI_Transition> _closingPopups = new Dictionary<GameObject, UI_Transition>();
        private Transform _inactiveUIRoot;
        private CanvasGroup _sceneLobbyBackgroundCanvasGroup;
        private Action _activePanelTransitionCleanup;

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

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            _activePanelTransitionCleanup = null;
            _cachedPanels.Clear();
            _cachedPopups.Clear();
            _activePopups.Clear();
            _closingPopups.Clear();
            _panelHistory.Clear();
        }

        private void Start()
        {
            ShowPanel(OutGamePanelType.MainLobby);
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
            ShowPanel(panelType, null);
        }

        private void ShowPanel(OutGamePanelType panelType, Action<GameObject> beforeEnable)
        {
            CompleteActivePanelTransitionImmediate();

            if (!IsPartyScope(panelType))
            {
                Scavenger.Network.NetworkPartyManager.Instance?.LeavePartyAndDestroy();
            }

            bool hasActiveOutgoing = _cachedPanels.TryGetValue(_currentPanelType, out GameObject outgoingPanel)
                                     && outgoingPanel != null
                                     && outgoingPanel.activeSelf;

            // 이미 해당 패널이 활성화된 상태에서 동일 패널이 재호출된 경우
            if (hasActiveOutgoing && _currentPanelType == panelType)
            {
                beforeEnable?.Invoke(outgoingPanel);
                outgoingPanel.SetActive(true);
                GetOrAddTransition(outgoingPanel, UITransitionType.Panel, panelType).ShowImmediate(true);
                return;
            }

            GameObject targetPanel = GetOrCreatePanel(panelType, beforeEnable);
            if (targetPanel == null)
            {
                return;
            }

            OutGamePanelType previousPanelType = _currentPanelType;
            _currentPanelType = panelType;

            // 1) 최초 진입 (예: 게임 시작 시 MainLobby 오픈): 페이드인으로 등장
            if (!hasActiveOutgoing)
            {
                if (panelType == OutGamePanelType.MainLobby)
                {
                    ClearPanelHistoryAndPopups();
                }

                targetPanel.SetActive(true);
                targetPanel.transform.SetAsLastSibling();

                UI_Transition targetTrans = GetOrAddTransition(targetPanel, UITransitionType.Panel, panelType);
                _activePanelTransitionCleanup = () =>
                {
                    _activePanelTransitionCleanup = null;
                    if (targetTrans != null) targetTrans.ShowImmediate(true);
                };
                targetTrans.PlayFadeIn(() => _activePanelTransitionCleanup?.Invoke());
                return;
            }

            // 2) 예외 케이스: 다른 패널에서 MainLobby로 이동할 때는 히스토리를 비우고 현재 패널이 투명으로 벗겨지는 연출
            if (panelType == OutGamePanelType.MainLobby)
            {
                ClearPanelHistoryAndPopups();

                // 현재 패널에 떠 있던 팝업들도 함께 닫아줍니다.
                List<GameObject> outgoingPopups = CaptureAndClearCurrentActivePopups();
                for (int i = 0; i < outgoingPopups.Count; i++)
                {
                    ClosePopup(outgoingPopups[i]);
                }

                targetPanel.SetActive(true);
                targetPanel.transform.SetAsLastSibling();
                outgoingPanel.transform.SetAsLastSibling();
                for (int i = 0; i < outgoingPopups.Count; i++)
                {
                    if (outgoingPopups[i] != null)
                    {
                        outgoingPopups[i].transform.SetAsLastSibling();
                    }
                }

                UI_Transition targetTrans = GetOrAddTransition(targetPanel, UITransitionType.Panel, panelType);
                UI_Transition outgoingTrans = GetOrAddTransition(outgoingPanel, UITransitionType.Panel, previousPanelType);

                targetTrans.ShowImmediate(false);

                _activePanelTransitionCleanup = () =>
                {
                    _activePanelTransitionCleanup = null;
                    if (outgoingTrans != null) outgoingTrans.StopTransition();
                    if (outgoingPanel != null) outgoingPanel.SetActive(false);
                    for (int i = 0; i < outgoingPopups.Count; i++)
                    {
                        if (outgoingPopups[i] != null)
                        {
                            if (_closingPopups.TryGetValue(outgoingPopups[i], out UI_Transition pTrans) && pTrans != null)
                            {
                                pTrans.StopTransition();
                            }
                            _closingPopups.Remove(outgoingPopups[i]);
                            outgoingPopups[i].SetActive(false);
                        }
                    }
                    if (targetTrans != null) targetTrans.ShowImmediate(true);
                };

                outgoingTrans.PlayFadeOut(() => _activePanelTransitionCleanup?.Invoke());
                return;
            }

            // 3) 일반 패널 진입:
            // 기존 패널과 그 위에 떠 있던 팝업들을 세트로 히스토리에 저장하고,
            // 새 패널이 맨 위에서 투명 -> 불투명으로 덮은 뒤 기존 패널+팝업을 비활성화합니다.
            List<GameObject> savedPopups = CaptureAndClearCurrentActivePopups();
            PushOrUpdatePanelHistory(previousPanelType, savedPopups);

            UI_Transition prevTrans = GetOrAddTransition(outgoingPanel, UITransitionType.Panel, previousPanelType);
            prevTrans.ShowImmediate(false);
            for (int i = 0; i < savedPopups.Count; i++)
            {
                if (savedPopups[i] != null)
                {
                    GetOrAddTransition(savedPopups[i], UITransitionType.Popup).ShowImmediate(false);
                }
            }

            targetPanel.SetActive(true);
            targetPanel.transform.SetAsLastSibling();

            UI_Transition newTrans = GetOrAddTransition(targetPanel, UITransitionType.Panel, panelType);
            _activePanelTransitionCleanup = () =>
            {
                _activePanelTransitionCleanup = null;
                if (newTrans != null) newTrans.ShowImmediate(true);
                if (outgoingPanel != null) outgoingPanel.SetActive(false);
                for (int i = 0; i < savedPopups.Count; i++)
                {
                    if (savedPopups[i] != null && !_activePopups.Contains(savedPopups[i]))
                    {
                        savedPopups[i].SetActive(false);
                    }
                }
            };

            newTrans.PlayFadeIn(() => _activePanelTransitionCleanup?.Invoke());
        }

        private List<GameObject> CaptureAndClearCurrentActivePopups()
        {
            List<GameObject> list = new List<GameObject>(_activePopups.Count);
            if (_activePopups.Count > 0)
            {
                GameObject[] arr = _activePopups.ToArray();
                // Stack.ToArray()는 Top -> Bottom 순서이므로, 아래(먼저 열린 팝업) -> 위(나중에 열린 팝업) 순서로 뒤집어 저장합니다.
                for (int i = arr.Length - 1; i >= 0; i--)
                {
                    if (arr[i] != null && arr[i].activeSelf)
                    {
                        list.Add(arr[i]);
                    }
                }
                _activePopups.Clear();
            }
            return list;
        }

        private void PushOrUpdatePanelHistory(OutGamePanelType panelType, List<GameObject> openPopups)
        {
            if (_panelHistory.Count > 0 && _panelHistory.Peek().panelType == panelType)
            {
                _panelHistory.Pop();
            }

            _panelHistory.Push(new PanelHistoryEntry
            {
                panelType = panelType,
                openPopups = openPopups
            });
        }

        private void ClearPanelHistoryAndPopups()
        {
            while (_panelHistory.Count > 0)
            {
                PanelHistoryEntry entry = _panelHistory.Pop();
                if (entry.openPopups != null)
                {
                    for (int i = 0; i < entry.openPopups.Count; i++)
                    {
                        if (entry.openPopups[i] != null)
                        {
                            entry.openPopups[i].SetActive(false);
                        }
                    }
                }
            }
        }

        private GameObject GetOrCreatePanel(OutGamePanelType panelType, Action<GameObject> beforeEnable)
        {
            if (_cachedPanels.TryGetValue(panelType, out GameObject cachedPanel) && cachedPanel != null)
            {
                beforeEnable?.Invoke(cachedPanel);
                return cachedPanel;
            }

            GameObject prefabToLoad = GetPanelPrefabByType(panelType);
            if (prefabToLoad != null && canvasTransform != null)
            {
                GameObject newPanel = beforeEnable != null
                    ? InstantiateInactive(prefabToLoad)
                    : Instantiate(prefabToLoad, canvasTransform, false);
                RectTransform rect = newPanel.GetComponent<RectTransform>();
                if (rect != null)
                {
                    rect.anchoredPosition = Vector2.zero;
                    rect.localScale = Vector3.one;
                }
                _cachedPanels[panelType] = newPanel;
                beforeEnable?.Invoke(newPanel);
                return newPanel;
            }

            Debug.LogError($"[OutGameUIManager] 패널 프리팹 또는 캔버스 연결 누락! ({panelType})");
            return null;
        }

        private void CompleteActivePanelTransitionImmediate()
        {
            if (_activePanelTransitionCleanup != null)
            {
                Action cleanup = _activePanelTransitionCleanup;
                _activePanelTransitionCleanup = null;
                cleanup.Invoke();
            }
        }

        private UI_Transition GetOrAddTransition(GameObject obj, UITransitionType type, OutGamePanelType? panelType = null)
        {
            if (obj == null) return null;

            UI_Transition transition = obj.GetComponent<UI_Transition>();
            if (transition == null)
            {
                transition = obj.AddComponent<UI_Transition>();
            }

            transition.EnsureInitialized(type);

            if (type == UITransitionType.Panel && panelType == OutGamePanelType.MainLobby)
            {
                CanvasGroup lobbyBg = GetSceneLobbyBackgroundCanvasGroup();
                if (lobbyBg != null)
                {
                    transition.BindExternalBackground(lobbyBg);
                }
            }

            return transition;
        }

        private CanvasGroup GetSceneLobbyBackgroundCanvasGroup()
        {
            if (_sceneLobbyBackgroundCanvasGroup != null) return _sceneLobbyBackgroundCanvasGroup;
            if (canvasTransform == null) return null;

            Transform bgTransform = canvasTransform.Find("BackgroundImage");
            if (bgTransform != null)
            {
                _sceneLobbyBackgroundCanvasGroup = bgTransform.GetComponent<CanvasGroup>();
                if (_sceneLobbyBackgroundCanvasGroup == null)
                {
                    _sceneLobbyBackgroundCanvasGroup = bgTransform.gameObject.AddComponent<CanvasGroup>();
                }
            }

            return _sceneLobbyBackgroundCanvasGroup;
        }

        private GameObject InstantiateInactive(GameObject prefab)
        {
            if (_inactiveUIRoot == null)
            {
                var root = new GameObject("InactiveUIRoot");
                root.transform.SetParent(canvasTransform, false);
                root.SetActive(false);
                _inactiveUIRoot = root.transform;
            }

            // 활성 프리팹도 OnEnable이 실행되기 전에 초기값을 전달할 수 있게 합니다.
            GameObject instance = Instantiate(prefab, _inactiveUIRoot, false);
            instance.SetActive(false);
            instance.transform.SetParent(canvasTransform, false);
            return instance;
        }

        public void GoToQuestShortcut(string shortcut, string shortcutValue)
        {
            switch (shortcut)
            {
                case "STAGE_SELECT":
                    // 스테이지 선택 화면이 준비되기 전까지는 스테이지 정보로 이동합니다.
                    ShowPanel(OutGamePanelType.StageInfo);
                    break;

                case "TRAIN_CHARACTOR":
                    if (!int.TryParse(shortcutValue, out int charId) || charId < 0)
                    {
                        Debug.LogWarning($"[OutGameUIManager] 잘못된 캐릭터 바로가기 값: {shortcutValue}");
                        return;
                    }

                    ShowPanel(OutGamePanelType.TrainCharacter, panel =>
                    {
                        var trainPanel = panel.GetComponent<TrainCharacterPanel>();
                        if (trainPanel != null)
                        {
                            trainPanel.SetCharacterForNextOpen(charId);
                        }
                    });
                    break;

                default:
                    Debug.LogWarning($"[OutGameUIManager] 지원하지 않는 퀘스트 바로가기: {shortcut}");
                    break;
            }
        }

        public T ShowPopup<T>(OutGamePopupType popupType) where T : MonoBehaviour
        {
            return ShowPopup<T>(popupType, null);
        }

        // beforeEnable에서는 팝업을 직접 열지 않고 초기값만 설정합니다.
        public T ShowPopup<T>(OutGamePopupType popupType, Action<T> beforeEnable) where T : MonoBehaviour
        {
            GameObject popupObj = null;

            if (_cachedPopups.TryGetValue(popupType, out GameObject cachedPopup) && cachedPopup != null)
            {
                popupObj = cachedPopup;
            }
            else
            {
                GameObject prefabToLoad = GetPopupPrefabByType(popupType);
                if (prefabToLoad != null && canvasTransform != null)
                {
                    popupObj = beforeEnable != null
                        ? InstantiateInactive(prefabToLoad)
                        : Instantiate(prefabToLoad, canvasTransform, false);
                    RectTransform rect = popupObj.GetComponent<RectTransform>();
                    if (rect != null)
                    {
                        rect.anchoredPosition = Vector2.zero;
                        rect.localScale = Vector3.one;
                    }
                    _cachedPopups[popupType] = popupObj;
                }
                else
                {
                    Debug.LogError($"[OutGameUIManager] 윈도우 프리팹 또는 캔버스 연결 누락! ({popupType})");
                    return null;
                }
            }

            if (_closingPopups.TryGetValue(popupObj, out UI_Transition closingTrans))
            {
                _closingPopups.Remove(popupObj);
                if (closingTrans != null) closingTrans.StopTransition();
            }

            T popup = popupObj.GetComponent<T>();
            if (beforeEnable != null)
            {
                if (popup == null)
                {
                    Debug.LogError($"[OutGameUIManager] 팝업에 {typeof(T).Name} 컴포넌트가 없습니다. ({popupType})");
                    return null;
                }

                // 이미 열린 팝업을 재사용해도 새 인자로 OnEnable이 실행되게 합니다.
                popupObj.SetActive(false);
                beforeEnable(popup);
            }

            popupObj.SetActive(true);
            popupObj.transform.SetAsLastSibling();
            if (_activePopups.Count == 0 || _activePopups.Peek() != popupObj)
            {
                _activePopups.Push(popupObj);
            }

            UI_Transition popupTransition = GetOrAddTransition(popupObj, UITransitionType.Popup);
            popupTransition.PlayFadeIn();

            return popup;
        }

        public ItemInfoPopup ShowItemInfoPopup(int itemId)
        {
            if (itemId <= 0) return null;

            return ShowPopup<ItemInfoPopup>(OutGamePopupType.ItemInfo,
                popup => popup.SetItemForNextOpen(itemId));
        }

        public DefaultConfirmPopup ShowDefaultConfirmPopup(string message, Action onConfirm,
            Action onCancel = null, string description = null,
            string confirmLabel = "확인", string cancelLabel = "취소")
        {
            return ShowPopup<DefaultConfirmPopup>(OutGamePopupType.DefaultConfirm,
                popup => popup.Configure(message, onConfirm, onCancel, description, confirmLabel, cancelLabel));
        }

        // 공용 오버레이의 토스트 대기열을 사용해 로딩 중에도 메시지가 가려지지 않게 합니다.
        public ToastPopup ShowToast(string message, float? duration = null)
        {
            var manager = Scavenger.Network.NetworkRequestManager.Instance;
            return manager != null ? manager.ShowToast(message, duration) : null;
        }

        public void GoBack()
        {
            if (Scavenger.Network.NetworkRequestManager.IsInputBlocked) return;

            // 이미 꺼져 있거나 파괴된 팝업 참조를 먼저 정리합니다.
            while (_activePopups.Count > 0 && (_activePopups.Peek() == null || !_activePopups.Peek().activeSelf))
            {
                _activePopups.Pop();
            }

            // 현재 패널에 떠 있는 최상단 팝업 1개만 닫습니다.
            if (_activePopups.Count > 0)
            {
                GameObject topPopup = _activePopups.Peek();
                if (topPopup.TryGetComponent<DefaultConfirmPopup>(out var confirmPopup)
                    && confirmPopup.isActiveAndEnabled)
                {
                    confirmPopup.Cancel();
                }
                else
                {
                    ClosePopup(topPopup);
                }
                return;
            }

            if (_panelHistory.Count > 0)
            {
                CompleteActivePanelTransitionImmediate();

                OutGamePanelType outgoingPanelType = _currentPanelType;
                PanelHistoryEntry prevEntry = _panelHistory.Pop();
                OutGamePanelType previousPanelType = prevEntry.panelType;
                List<GameObject> restoredPopups = prevEntry.openPopups;

                _cachedPanels.TryGetValue(outgoingPanelType, out GameObject currentPanel);

                if (_cachedPanels.TryGetValue(previousPanelType, out GameObject prevPanel) && prevPanel != null)
                {
                    _currentPanelType = previousPanelType;

                    if (!IsPartyScope(previousPanelType))
                    {
                        Scavenger.Network.NetworkPartyManager.Instance?.LeavePartyAndDestroy();
                    }

                    // 1) 직전 패널을 밑에 즉시 불투명 상태로 활성화
                    prevPanel.SetActive(true);
                    prevPanel.transform.SetAsLastSibling();
                    UI_Transition prevTrans = GetOrAddTransition(prevPanel, UITransitionType.Panel, previousPanelType);
                    prevTrans.ShowImmediate(false);

                    // 2) 직전 패널 위에 떠 있던 팝업들도 순서대로 즉시 불투명 상태로 복원
                    _activePopups.Clear();
                    if (restoredPopups != null)
                    {
                        for (int i = 0; i < restoredPopups.Count; i++)
                        {
                            GameObject popup = restoredPopups[i];
                            if (popup == null) continue;

                            if (_closingPopups.TryGetValue(popup, out UI_Transition closingTrans))
                            {
                                _closingPopups.Remove(popup);
                                if (closingTrans != null) closingTrans.StopTransition();
                            }

                            popup.SetActive(true);
                            popup.transform.SetAsLastSibling();
                            GetOrAddTransition(popup, UITransitionType.Popup).ShowImmediate(false);
                            _activePopups.Push(popup);
                        }
                    }

                    // 3) 현재 닫힐 패널을 가장 위에 배치한 뒤 투명으로 벗겨냅니다.
                    if (currentPanel != null)
                    {
                        currentPanel.transform.SetAsLastSibling();
                    }

                    if (currentPanel != null && currentPanel.activeSelf && currentPanel != prevPanel)
                    {
                        UI_Transition currentTrans = GetOrAddTransition(currentPanel, UITransitionType.Panel, outgoingPanelType);
                        _activePanelTransitionCleanup = () =>
                        {
                            _activePanelTransitionCleanup = null;
                            if (currentTrans != null) currentTrans.StopTransition();
                            if (currentPanel != null) currentPanel.SetActive(false);
                            if (prevTrans != null) prevTrans.ShowImmediate(true);
                            if (restoredPopups != null)
                            {
                                for (int i = 0; i < restoredPopups.Count; i++)
                                {
                                    if (restoredPopups[i] != null && restoredPopups[i].activeSelf)
                                    {
                                        GetOrAddTransition(restoredPopups[i], UITransitionType.Popup).ShowImmediate(true);
                                    }
                                }
                            }
                        };
                        currentTrans.PlayFadeOut(() => _activePanelTransitionCleanup?.Invoke());
                    }
                    else
                    {
                        if (currentPanel != null && currentPanel != prevPanel)
                        {
                            currentPanel.SetActive(false);
                        }
                        prevTrans.ShowImmediate(true);
                        if (restoredPopups != null)
                        {
                            for (int i = 0; i < restoredPopups.Count; i++)
                            {
                                if (restoredPopups[i] != null && restoredPopups[i].activeSelf)
                                {
                                    GetOrAddTransition(restoredPopups[i], UITransitionType.Popup).ShowImmediate(true);
                                }
                            }
                        }
                    }
                }
                else if (currentPanel != null)
                {
                    currentPanel.SetActive(false);
                }
            }
        }

        public void ClosePopup(GameObject popup)
        {
            if (popup == null) return;

            if (_activePopups.Count > 0 && _activePopups.Peek() == popup)
            {
                _activePopups.Pop();
            }
            else if (_activePopups.Count > 0)
            {
                Stack<GameObject> temp = new Stack<GameObject>();
                while (_activePopups.Count > 0)
                {
                    var w = _activePopups.Pop();
                    if (w != popup) temp.Push(w);
                }
                while (temp.Count > 0) _activePopups.Push(temp.Pop());
            }

            if (!popup.activeInHierarchy)
            {
                popup.SetActive(false);
                return;
            }

            if (_closingPopups.ContainsKey(popup))
            {
                return;
            }

            UI_Transition popupTransition = GetOrAddTransition(popup, UITransitionType.Popup);
            _closingPopups[popup] = popupTransition;
            popupTransition.PlayFadeOut(() =>
            {
                _closingPopups.Remove(popup);
                if (popup != null)
                {
                    popup.SetActive(false);
                }
            });
        }

        private GameObject GetPanelPrefabByType(OutGamePanelType type)
        {
            switch (type)
            {
                case OutGamePanelType.MainLobby: return mainLobbyPanelPrefab;
                case OutGamePanelType.ReadyToBattle: return readyToBattlePanelPrefab;
                case OutGamePanelType.FriendParty: return friendPartyPanelPrefab;
                case OutGamePanelType.CharacterDeck: return characterDeckPanelPrefab;
                case OutGamePanelType.SelectOperation: return selectOperationPanelPrefab;
                case OutGamePanelType.SelectStage: return selectStagePanelPrefab;
                case OutGamePanelType.TrainCharacter: return trainCharacterPanelPrefab;
                case OutGamePanelType.StageInfo: return stageInfoPanelPrefab;
                case OutGamePanelType.Storage: return storagePanelPrefab;
                case OutGamePanelType.QuestBoard: return questBoardPanelPrefab;
                case OutGamePanelType.Setting: return settingPanelPrefab;
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
                case OutGamePopupType.ItemInfo: return itemInfoPopupPrefab;
                case OutGamePopupType.DefaultConfirm: return defaultConfirmPopupPrefab;
                case OutGamePopupType.PartyPick: return partyPickPopupPrefab;
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
        public void OnClickGoToStorage() => ShowPanel(OutGamePanelType.Storage);
        public void OnClickGoToQuestBoard() => ShowPanel(OutGamePanelType.QuestBoard);
        public void OnClickGoToSetting() => ShowPanel(OutGamePanelType.Setting);
    }
}
