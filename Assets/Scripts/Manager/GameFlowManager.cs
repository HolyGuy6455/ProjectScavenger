using System;
using UnityEngine;
using System.Collections.Generic;
using Unity.Netcode;

namespace Scavenger.Manager
{
    public enum GameResultState
    {
        None,
        Clear,
        Fail,
        Exit
    }

    public class GameFlowManager : MonoBehaviour
    {
        public static GameFlowManager Instance { get; private set; }

        public GameResultState CurrentState { get; private set; } = GameResultState.None;

        public event Action<GameResultState> OnGameEnded;

        // 탈출 UI 연동을 위한 이벤트
        public event Action<Scavenger.Environment.ExtractionZone, bool> OnExtractionStateChanged;
        
        // 생존자 수 변경 이벤트 (탈출 구역 재평가용)
        public event Action<int> OnAlivePlayersChanged;

        private int _lastAlivePlayersCount = -1;
        private bool _gameEndedNotified;
        private bool _returnToMainMenuStarted;
        private bool _menuPresentationInterrupted;
        private bool _clearFlowActive;
        private int _clearGeneration;
        private PlayerController _clearPlayer;
        private BaseUltimateSkill _clearUltimate;
        private bool _clearHasPlayer;
        private bool _clearHadUltimate;
        private bool _clearWaitedForUltimate;
        private bool _clearExitStarted;
        private bool _clearInputLocked;
        private bool _clearPlayerWasSpawned;
        private bool _clearPlayerWasOwner;
        private int _clearPlayerPresentationVersion;
        private int _clearUltimatePresentationVersion;
        private NetworkManager _clearNetwork;
        private bool _clearNetworkWasListening;
        private ulong _clearLocalClientId;
        private Scavenger.Network.NetworkGameResultSender _clearResultSender;

        public void NotifyExtractionState(Scavenger.Environment.ExtractionZone zone, bool isExtracting)
        {
            OnExtractionStateChanged?.Invoke(zone, isExtracting);
        }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Update()
        {
            if (_clearFlowActive)
            {
                if (!IsClearContextValid(_clearGeneration)) CancelClearFlow();
                else if (_clearHasPlayer && !_clearExitStarted && !_gameEndedNotified) TryStartClearExit();
            }

            // 방장(서버) 혹은 오프라인 상태에서만 생존자 수 추적
            bool isOffline = NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening;
            if (isOffline || NetworkManager.Singleton.IsServer)
            {
                int currentAlive = GetAlivePlayersCount();
                if (currentAlive != _lastAlivePlayersCount)
                {
                    _lastAlivePlayersCount = currentAlive;
                    OnAlivePlayersChanged?.Invoke(currentAlive);

                    if (currentAlive == 0 && CurrentState == GameResultState.None)
                    {
                        OnGameFailed();
                    }
                }
            }
        }

        public int GetAlivePlayersCount()
        {
            bool isOffline = NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening;
            if (isOffline)
            {
                if (PlayerController.LocalInstance != null)
                {
                    var health = PlayerController.LocalInstance.GetComponent<Health>();
                    return (health != null && !health.IsDead) ? 1 : 0;
                }
                return 1;
            }

            int count = 0;
            foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
            {
                if (client.PlayerObject != null)
                {
                    var health = client.PlayerObject.GetComponent<Health>();
                    if (health != null && !health.IsDead) count++;
                }
            }
            return count;
        }

        public void OnGameCleared()
        {
            if (CurrentState != GameResultState.None || !isActiveAndEnabled) return;

            Debug.Log("[GameFlowManager] 게임 클리어 상태로 전환됩니다.");
            CurrentState = GameResultState.Clear;

            _clearGeneration++;
            _clearFlowActive = true;
            _clearPlayer = PlayerController.LocalInstance;
            _clearHasPlayer = _clearPlayer != null;
            _clearNetwork = NetworkManager.Singleton;
            _clearNetworkWasListening = _clearNetwork != null && _clearNetwork.IsListening;
            if (_clearNetworkWasListening)
            {
                _clearLocalClientId = _clearNetwork.LocalClientId;
                _clearNetwork.OnClientDisconnectCallback += HandleClearClientDisconnected;
            }

            // 처음부터 로컬 플레이어가 없었던 경우에는 기존 즉시 완료 흐름을 유지합니다.
            if (!_clearHasPlayer)
            {
                PrepareClearUI();
                EndGame(GameResultState.Clear);
                return;
            }

            _clearPlayerPresentationVersion = _clearPlayer.PresentationVersion;
            _clearPlayerWasSpawned = _clearPlayer.IsSpawned;
            _clearPlayerWasOwner = _clearPlayer.IsOwner;
            _clearUltimate = _clearPlayer.GetActiveUltimateSkill();
            _clearHadUltimate = _clearUltimate != null;
            _clearUltimatePresentationVersion = _clearHadUltimate ? _clearUltimate.PresentationVersion : 0;
            _clearWaitedForUltimate = _clearHadUltimate && _clearUltimate.IsPresentationPlaying;
            _clearPlayer.PresentationInterrupted += HandleClearPresentationInterrupted;
            _clearInputLocked = true;
            _clearPlayer.SetCinematicInputLocked(this, true);
            TryStartClearExit();
        }

        private void TryStartClearExit()
        {
            int generation = _clearGeneration;
            if (!IsClearContextValid(generation))
            {
                CancelClearFlow();
                return;
            }
            if (_clearExitStarted) return;
            if (_clearWaitedForUltimate)
            {
                if (_clearUltimate.IsPresentationPlaying) return;
                if (!_clearUltimate.LastPresentationCompleted)
                {
                    CancelClearFlow();
                    return;
                }
            }

            // 이 클라이언트의 진행 중이던 궁극기만 기다립니다. 파티원의 연출과는 동기화하지 않습니다.
            _clearExitStarted = true;
            PrepareClearUI();
            if (!IsClearContextValid(generation))
            {
                CancelClearFlow();
                return;
            }
            _clearPlayer.PlayCinematic("Exit", false,
                () => CompleteClearExit(generation), () => CancelClearFlow(generation));
        }

        private void CompleteClearExit(int generation)
        {
            if (!IsClearContextValid(generation))
            {
                CancelClearFlow(generation);
                return;
            }
            EndGame(GameResultState.Clear);
        }

        private bool IsClearContextValid(int generation)
        {
            if (!_clearFlowActive || generation != _clearGeneration
                || !isActiveAndEnabled || CurrentState != GameResultState.Clear) return false;
            if (HasClearConnectionChanged()) return false;
            if (!_clearHasPlayer) return true;
            if (_clearPlayer == null || !ReferenceEquals(PlayerController.LocalInstance, _clearPlayer)
                || !_clearPlayer.isActiveAndEnabled || _clearPlayer.IsDeathPresented
                || _clearPlayer.PresentationVersion != _clearPlayerPresentationVersion
                || (_clearPlayerWasSpawned && !_clearPlayer.IsSpawned)
                || (_clearPlayerWasOwner && !_clearPlayer.IsOwner)) return false;
            if (!ReferenceEquals(_clearPlayer.GetActiveUltimateSkill(), _clearUltimate)) return false;
            if (_clearHadUltimate && (_clearUltimate == null || !_clearUltimate.isActiveAndEnabled
                || _clearUltimate.PresentationVersion != _clearUltimatePresentationVersion)) return false;
            return true;
        }

        private bool HasClearConnectionChanged()
        {
            return _clearNetworkWasListening && (_clearNetwork == null
                || !ReferenceEquals(NetworkManager.Singleton, _clearNetwork) || !_clearNetwork.IsListening);
        }

        private static void PrepareClearUI()
        {
            var menuToggle = FindObjectOfType<MenuToggleController>();
            if (menuToggle != null) menuToggle.PrepareClearResultUI();
        }

        private void HandleClearPresentationInterrupted() => CancelClearFlow();

        private void HandleClearClientDisconnected(ulong clientId)
        {
            if (clientId == _clearLocalClientId) CancelClearFlow(interruptPresentation: true);
        }

        private void ReleaseClearInputLock()
        {
            if (!_clearInputLocked) return;
            _clearInputLocked = false;
            if (_clearPlayer != null) _clearPlayer.SetCinematicInputLocked(this, false);
        }

        private void UnsubscribeClearResult()
        {
            if (!ReferenceEquals(_clearResultSender, null))
                _clearResultSender.OnGameResultReceived -= HandleGameResultReceived;
            _clearResultSender = null;
        }

        private void CancelClearFlow(int generation)
        {
            if (generation == _clearGeneration) CancelClearFlow();
        }

        private void CancelClearFlow(bool interruptPresentation = false)
        {
            // 연결 종료나 매니저 해제는 플레이어 despawn을 기다리지 않고 진행 중인 연출도 중단합니다.
            // 이미 완료한 Clear 또는 다른 수명의 플레이어 연출에는 관여하지 않습니다.
            var playerToInterrupt = _clearFlowActive && !_gameEndedNotified
                && (interruptPresentation || HasClearConnectionChanged())
                && _clearPlayer != null && _clearPlayer.PresentationVersion == _clearPlayerPresentationVersion
                ? _clearPlayer : null;
            _clearGeneration++;
            _clearFlowActive = false;
            if (!ReferenceEquals(_clearPlayer, null))
                _clearPlayer.PresentationInterrupted -= HandleClearPresentationInterrupted;
            if (!ReferenceEquals(_clearNetwork, null))
                _clearNetwork.OnClientDisconnectCallback -= HandleClearClientDisconnected;
            ReleaseClearInputLock();
            UnsubscribeClearResult();
            _clearPlayer = null;
            _clearUltimate = null;
            _clearNetwork = null;
            // 취소 콜백이 재진입하기 전에 위에서 세대와 구독을 무효화합니다.
            if (playerToInterrupt != null) playerToInterrupt.InterruptPresentation();
        }

        private void OnDisable() => CancelClearFlow(interruptPresentation: true);

        private void OnDestroy()
        {
            CancelClearFlow(interruptPresentation: true);
            if (Instance == this) Instance = null;
        }

        public void OnGameFailed()
        {
            if (CurrentState != GameResultState.None) return;

            Debug.Log("[GameFlowManager] 게임 오버 상태로 전환됩니다.");
            CurrentState = GameResultState.Fail;
            EndGame(GameResultState.Fail);
        }

        public void OnGameExited()
        {
            if (_returnToMainMenuStarted || CurrentState == GameResultState.Exit
                || CurrentState == GameResultState.Fail) return;
            CancelClearFlow();

            Debug.Log("[GameFlowManager] 사용자가 메뉴로 이탈합니다.");
            CurrentState = GameResultState.Exit;
            InterruptForMenuExit();
            // Clear가 이미 보고됐더라도 메뉴 이탈은 즉시 처리하되 결과 이벤트는 중복 발행하지 않습니다.
            if (_gameEndedNotified) ReturnToMainMenu();
            else EndGame(GameResultState.Exit);
        }

        private void EndGame(GameResultState result)
        {
            if (_gameEndedNotified) return;
            int generation = _clearGeneration;
            if (result == GameResultState.Clear && !IsClearContextValid(generation))
            {
                CancelClearFlow();
                return;
            }
            _gameEndedNotified = true;
            ReleaseClearInputLock();
            // 여기서 이벤트를 발생시키면 서버로 결과를 전송하는 로직 등이 실행됩니다.
            OnGameEnded?.Invoke(result);
            if (!isActiveAndEnabled) return;

            // 이벤트 송신이 완료된 후 후속 처리
            // 1. 클리어가 아니면 즉시 로비로 복귀
            if (result != GameResultState.Clear)
            {
                ReturnToMainMenu();
                return;
            }

            // 이벤트 구독자가 화면을 떠나거나 플레이어를 교체한 경우 후속 UI를 열지 않습니다.
            if (!IsClearContextValid(generation))
            {
                CancelClearFlow(generation);
                return;
            }

            // 2. 서버 응답을 기다립니다. NetworkGameResultSender 이벤트 구독.
            _clearResultSender = Scavenger.Network.NetworkGameResultSender.Instance;
            if (_clearResultSender != null)
            {
                _clearResultSender.OnGameResultReceived += HandleGameResultReceived;
            }
            else
            {
                Debug.LogWarning("[GameFlowManager] NetworkGameResultSender 인스턴스가 없어 결과창 데이터를 받을 수 없습니다.");
            }

            // 3. 결산창 표시 시도 (시네마틱 종료 콜백을 위해)
            if (MenuToggleController.Instance == null)
            {
                Debug.LogWarning("[GameFlowManager] MenuToggleController 인스턴스가 없어 결과창을 띄우지 못했습니다. 바로 로비로 복귀합니다.");
                ReturnToMainMenu();
                return;
            }
            MenuToggleController.Instance.ShowClearResultUI();
        }

        private void HandleGameResultReceived(Scavenger.Network.DTOs.GameResultResponse response)
        {
            if (!_gameEndedNotified || !IsClearContextValid(_clearGeneration))
            {
                CancelClearFlow();
                return;
            }
            UnsubscribeClearResult();

            // LootInfo 파싱
            List<Scavenger.UI.LootInfo> loots = new List<Scavenger.UI.LootInfo>();
            if (response.AddedItems != null)
            {
                foreach (var item in response.AddedItems)
                {
                    loots.Add(new Scavenger.UI.LootInfo { itemId = item.ItemId, count = item.Amount, isBonus = false });
                }
            }

            // PlayerExpResult 파싱
            Scavenger.UI.PlayerExpResult playerExp = new Scavenger.UI.PlayerExpResult();
            playerExp.oldLevel = response.UserPrevLevel;
            playerExp.newLevel = response.UserCurrentLevel;
            playerExp.oldExp = response.UserPrevExp;
            playerExp.newExp = response.UserCurrentExp;

            var oldLevelData = DataManager.Instance.GetData<LevelData>("Level", playerExp.oldLevel);
            var newLevelData = DataManager.Instance.GetData<LevelData>("Level", playerExp.newLevel);
            playerExp.maxExpForOldLevel = oldLevelData != null ? oldLevelData.requiredExp : 1;
            playerExp.maxExpForNewLevel = newLevelData != null ? newLevelData.requiredExp : 1;

            if (playerExp.newLevel > playerExp.oldLevel)
            {
                playerExp.addedExp = (playerExp.maxExpForOldLevel - playerExp.oldExp) + playerExp.newExp;
            }
            else
            {
                playerExp.addedExp = playerExp.newExp - playerExp.oldExp;
            }

            // CharExpResult 파싱
            List<Scavenger.UI.CharExpResult> charExpList = new List<Scavenger.UI.CharExpResult>();
            if (response.CharacterExpData != null)
            {
                foreach (var ch in response.CharacterExpData)
                {
                    var charData = DataManager.Instance.GetData<CharData>("Char", ch.CharId);
                    if (charData == null) continue;

                    Scavenger.UI.CharExpResult charExp = new Scavenger.UI.CharExpResult();
                    charExp.charData = charData;
                    charExp.oldLevel = ch.PrevLevel;
                    charExp.newLevel = ch.CurrentLevel;
                    charExp.oldExp = ch.PrevExp;
                    charExp.newExp = ch.CurrentExp;

                    var cOldLevelData = DataManager.Instance.GetData<LevelData>("Level", charExp.oldLevel);
                    var cNewLevelData = DataManager.Instance.GetData<LevelData>("Level", charExp.newLevel);
                    charExp.maxExpForOldLevel = cOldLevelData != null ? cOldLevelData.requiredExp : 1;
                    charExp.maxExpForNewLevel = cNewLevelData != null ? cNewLevelData.requiredExp : 1;

                    if (charExp.newLevel > charExp.oldLevel)
                    {
                        charExp.addedExp = (charExp.maxExpForOldLevel - charExp.oldExp) + charExp.newExp;
                    }
                    else
                    {
                        charExp.addedExp = charExp.newExp - charExp.oldExp;
                    }

                    charExpList.Add(charExp);
                }
            }

            if (MenuToggleController.Instance != null)
            {
                MenuToggleController.Instance.SetupClearResultData(loots, charExpList, playerExp);
            }
            // 응답까지 처리한 뒤에는 플레이어/네트워크 수명 감시도 해제합니다.
            CancelClearFlow();
        }

        private void ReturnToMainMenu()
        {
            if (_returnToMainMenuStarted) return;
            _returnToMainMenuStarted = true;
            CancelClearFlow();
            InterruptForMenuExit();
            var uiSystem = FindObjectOfType<Scavenger.UI.UI_SystemActions>();
            if (uiSystem != null)
            {
                uiSystem.DoReturnToMainMenu();
            }
            else
            {
                Debug.LogWarning("[GameFlowManager] UI_SystemActions를 찾을 수 없어 로비로 돌아갈 수 없습니다.");
            }
        }

        private void InterruptForMenuExit()
        {
            if (_menuPresentationInterrupted) return;
            _menuPresentationInterrupted = true;
            var player = PlayerController.LocalInstance;
            if (player != null) player.InterruptPresentation();
        }
    }
}
