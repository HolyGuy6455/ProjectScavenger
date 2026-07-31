using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Scavenger.Network;

namespace Scavenger.UI
{
    public class UI_LoginPanel : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private Button startButton;

        private void Start()
        {
            statusText.text = "Login...";
            startButton.interactable = false;
            
            // 버튼 클릭 시 메인 로비로 넘어가기
            startButton.onClick.AddListener(OnClickStart);

            if (NetworkAuthManager.Instance != null)
            {
                // 이미 로그인이 완료된 상태인지 확인 (예: 게임을 끝내고 다시 로비 씬으로 돌아왔을 때)
                if (!string.IsNullOrEmpty(NetworkAuthManager.Instance.AuthToken))
                {
                    HandleLoginComplete(true);
                }
                else
                {
                    // 아직 로그인 중이라면 이벤트 구독
                    NetworkAuthManager.Instance.OnLoginComplete += HandleLoginComplete;
                }
            }
        }

        private void OnDestroy()
        {
            if (NetworkAuthManager.Instance != null)
            {
                NetworkAuthManager.Instance.OnLoginComplete -= HandleLoginComplete;
            }
        }

        private void HandleLoginComplete(bool success)
        {
            if (success)
            {
                statusText.text = "Touch to Start";
                startButton.interactable = true;
            }
            else
            {
                statusText.text = "Login Failed";
            }
        }

        private void OnClickStart()
        {
            if (OutGameUIManager.Instance != null)
            {
                OutGameUIManager.Instance.ShowPanel(OutGamePanelType.MainLobby);
            }
        }
    }
}
