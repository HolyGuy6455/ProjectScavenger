using System;
using UnityEngine;

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

        public void OnGameCleared()
        {
            if (CurrentState != GameResultState.None) return;

            Debug.Log("[GameFlowManager] 게임 클리어 상태로 전환됩니다.");
            CurrentState = GameResultState.Clear;
            EndGame(GameResultState.Clear);
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
            OnGameEnded?.Invoke(result);

            // Phase 2에서 여기에 서버 송신 로직을 연결하거나, 
            // OnGameEnded 이벤트를 NetworkGameResultSender 가 구독하도록 할 예정입니다.
            
            // 임시로 ReturnToMainMenu 호출
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
