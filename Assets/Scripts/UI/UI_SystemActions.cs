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
            // 클리어 연출 대기 중인 경우도 매니저가 먼저 취소한 뒤 이탈하도록 넘깁니다.
            if (Scavenger.Manager.GameFlowManager.Instance != null && 
                (Scavenger.Manager.GameFlowManager.Instance.CurrentState == Scavenger.Manager.GameResultState.None ||
                 Scavenger.Manager.GameFlowManager.Instance.CurrentState == Scavenger.Manager.GameResultState.Clear))
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

            // 로그인을 안 한 상태라면(테스트 씬을 통해 진입) IngameTestScene으로 돌아감
            if (Scavenger.Network.NetworkAuthManager.Instance == null || 
                string.IsNullOrEmpty(Scavenger.Network.NetworkAuthManager.Instance.AuthToken))
            {
                Scavenger.Core.SceneTransitionManager.LoadSceneWithIntent(Scavenger.Core.LoadingIntent.ReturnToIngameTest);
            }
            else
            {
                Scavenger.Core.SceneTransitionManager.LoadSceneWithIntent(Scavenger.Core.LoadingIntent.ReturnToLobby);
            }
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
