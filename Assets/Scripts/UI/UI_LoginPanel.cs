using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Scavenger.Audio;
using Scavenger.Network;

namespace Scavenger.UI
{
    public class UI_LoginPanel : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private Button startButton;

        private bool loginSucceeded;
        private bool soundInitializationFinished;

        private async void Start()
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

            // 임시 초기화 진입점. 추후 앱 부트스트랩으로 옮깁니다.
            // 로그인 이벤트를 먼저 구독하여 사운드를 기다리는 동안의 로그인 완료도 받습니다.
            try
            {
                if (SoundManager.IsAudioEnabled)
                    await SoundManager.InitializeAsync();
                // 초기화 실패는 SoundManager가 기록하며, 로그인 자체는 무음으로 계속 진행합니다.
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception);
            }

            if (this == null)
                return;

            soundInitializationFinished = true;
            RefreshStartState();
        }

        private void OnDestroy()
        {
            if (startButton != null)
                startButton.onClick.RemoveListener(OnClickStart);
            if (NetworkAuthManager.Instance != null)
            {
                NetworkAuthManager.Instance.OnLoginComplete -= HandleLoginComplete;
            }
        }

        private void HandleLoginComplete(bool success)
        {
            loginSucceeded = success;
            if (!success)
                statusText.text = "Login Failed";

            RefreshStartState();
        }

        private void RefreshStartState()
        {
            startButton.interactable = loginSucceeded && soundInitializationFinished;
            if (loginSucceeded)
                statusText.text = soundInitializationFinished ? "Touch to Start" : "Loading...";
        }

        private void OnClickStart()
        {
            if (!loginSucceeded || !soundInitializationFinished)
                return;
            if (OutGameUIManager.Instance != null)
            {
                OutGameUIManager.Instance.ShowPanel(OutGamePanelType.MainLobby);
            }
        }
    }
}
