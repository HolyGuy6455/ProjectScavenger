using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;

namespace Scavenger.UI
{
    public class UI_SystemActions : MonoBehaviour
    {
        /// <summary>
        /// 메인 메뉴(OutgameScene)로 복귀하는 버튼 클릭 시 호출
        /// </summary>
        public void ReturnToMainMenu()
        {
            // GameFlowManager가 존재하고 게임이 아직 끝나지 않은 상태라면 게임 이탈 처리로 넘김
            if (Scavenger.Manager.GameFlowManager.Instance != null && 
                Scavenger.Manager.GameFlowManager.Instance.CurrentState == Scavenger.Manager.GameResultState.None)
            {
                Scavenger.Manager.GameFlowManager.Instance.OnGameExited();
                return;
            }

            DoReturnToMainMenu();
        }

        public void DoReturnToMainMenu()
        {
            // 현재 게임 서버(Netcode)와 연결되어 있다면 연결을 안전하게 해제
            if (NetworkManager.Singleton != null && (NetworkManager.Singleton.IsClient || NetworkManager.Singleton.IsServer))
            {
                NetworkManager.Singleton.Shutdown();
            }

            // 아웃게임 씬으로 강제 전환 대신, 인텐트를 담아 로딩 씬으로 이동
            // 추후 이 로딩 씬에서 무거운 리소스 해제 등의 작업을 수행할 수 있습니다.
            Scavenger.Core.SceneTransitionManager.LoadSceneWithIntent(Scavenger.Core.LoadingIntent.ReturnToLobby);
        }

        /// <summary>
        /// 게임 종료 버튼 클릭 시 호출
        /// </summary>
        public void QuitGame()
        {
            Debug.Log("[System] 게임을 종료합니다.");
            Application.Quit();

#if UNITY_EDITOR
            // 유니티 에디터 환경에서는 플레이 모드 종료
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }
    }
}
