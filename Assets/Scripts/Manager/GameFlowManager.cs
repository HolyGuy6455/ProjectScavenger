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

            // 2. 결산창을 띄울 수 없는 예외 상황
            if (MenuToggleController.Instance == null)
            {
                Debug.LogWarning("[GameFlowManager] MenuToggleController 인스턴스가 없어 결과창을 띄우지 못했습니다. 바로 로비로 복귀합니다.");
                ReturnToMainMenu();
                return;
            }

            // 3. 클리어 시 결산창 표시 시도
            MenuToggleController.Instance.ShowClearResultUI();
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
