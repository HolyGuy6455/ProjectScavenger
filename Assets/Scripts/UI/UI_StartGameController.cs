using Scavenger.Network;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Scavenger.UI
{
    public class UI_StartGameController : MonoBehaviour
    {
        [SerializeField] private Button startGameButton;

        private void Awake()
        {
            if (startGameButton != null)
            {
                startGameButton.onClick.AddListener(OnStartGameClicked);
            }
        }

        private void OnEnable()
        {
            if (NetworkPartyManager.Instance != null)
            {
                NetworkPartyManager.Instance.OnGameStartBroadcasted += HandleGameStart;
                NetworkPartyManager.Instance.OnHostGameStartBroadcasted += HandleHostGameStart;
            }
        }

        private void OnDisable()
        {
            if (NetworkPartyManager.Instance != null)
            {
                NetworkPartyManager.Instance.OnGameStartBroadcasted -= HandleGameStart;
                NetworkPartyManager.Instance.OnHostGameStartBroadcasted -= HandleHostGameStart;
            }
        }

        private bool _isReady = false;

        private void OnStartGameClicked()
        {
            if (NetworkPartyManager.Instance != null)
            {
                _isReady = !_isReady;
                NetworkPartyManager.Instance.SendReadyState(_isReady);

                // Update button text if it has one
                var textComponent = startGameButton.GetComponentInChildren<TMPro.TextMeshProUGUI>();
                if (textComponent != null)
                {
                    textComponent.text = _isReady ? "Cancel Ready" : "Ready";
                }
                else
                {
                    var textMesh = startGameButton.GetComponentInChildren<UnityEngine.UI.Text>();
                    if (textMesh != null)
                    {
                        textMesh.text = _isReady ? "Cancel Ready" : "Ready";
                    }
                }
                
                Debug.Log($"[Match] 레디 상태 변경됨: {_isReady}");
            }
        }

        private void HandleGameStart(string ip, int port)
        {
            Debug.Log($"[Match] 서버로부터 게임 시작 신호를 받았습니다! 접속 대상: {ip}:{port}");
            
            // TODO: ip와 port 정보를 저장해두고 인게임 씬에서 Netcode(NGO)로 접속 시도해야 함
            PlayerPrefs.SetString("TargetServerIP", ip);
            PlayerPrefs.SetInt("TargetServerPort", port);
            PlayerPrefs.Save();

            // 로딩 씬으로 넘어감 (인텐트 지정)
            Scavenger.Core.SceneTransitionManager.LoadSceneWithIntent(Scavenger.Core.LoadingIntent.EnterInGame);
        }

        private void HandleHostGameStart()
        {
            Debug.Log($"[Match] 서버로부터 로컬 호스트 게임 시작 신호를 받았습니다!");
            
            // 로컬 호스트 모드로 로딩 씬 진입
            Scavenger.Core.SceneTransitionManager.LoadSceneWithIntent(Scavenger.Core.LoadingIntent.StartSingleplayerHost);
        }
    }
}
