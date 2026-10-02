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
            if (CurrentState != GameResultState.None) return;

            Debug.Log("[GameFlowManager] 게임 클리어 상태로 전환됩니다.");
            CurrentState = GameResultState.Clear;
            
            // 시네마틱 시작 전, ClearResultPanel을 투명하게 미리 켭니다.
            var menuToggle = FindObjectOfType<MenuToggleController>();
            if (menuToggle != null)
            {
                menuToggle.PrepareClearResultUI();
            }

            // 로컬 플레이어가 존재하면 클리어 연출(exit) 재생 후 EndGame 호출, 없으면 바로 호출
            if (PlayerController.LocalInstance != null)
            {
                PlayerController.LocalInstance.PlayCinematic("Exit", false, () => 
                {
                    EndGame(GameResultState.Clear);
                });
            }
            else
            {
                EndGame(GameResultState.Clear);
            }
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
            if (CurrentState != GameResultState.None) return;

            Debug.Log("[GameFlowManager] 사용자가 메뉴로 이탈합니다.");
            CurrentState = GameResultState.Exit;
            EndGame(GameResultState.Exit);
        }

        private void EndGame(GameResultState result)
        {
            // 여기서 이벤트를 발생시키면 서버로 결과를 전송하는 로직 등이 실행됩니다.
            OnGameEnded?.Invoke(result);

            // 이벤트 송신이 완료된 후 후속 처리
            // 1. 클리어가 아니면 즉시 로비로 복귀
            if (result != GameResultState.Clear)
            {
                ReturnToMainMenu();
                return;
            }

            // 2. 서버 응답을 기다립니다. NetworkGameResultSender 이벤트 구독.
            if (Scavenger.Network.NetworkGameResultSender.Instance != null)
            {
                Scavenger.Network.NetworkGameResultSender.Instance.OnGameResultReceived += HandleGameResultReceived;
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
            if (Scavenger.Network.NetworkGameResultSender.Instance != null)
            {
                Scavenger.Network.NetworkGameResultSender.Instance.OnGameResultReceived -= HandleGameResultReceived;
            }

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
        }

        private void ReturnToMainMenu()
        {
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
    }
}
