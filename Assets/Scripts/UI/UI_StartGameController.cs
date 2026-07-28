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
            }
        }

        private void OnDisable()
        {
            if (NetworkPartyManager.Instance != null)
            {
                NetworkPartyManager.Instance.OnGameStartBroadcasted -= HandleGameStart;
            }
        }

        private void OnStartGameClicked()
        {
            if (NetworkPartyManager.Instance != null)
            {
                // 이 버튼을 누른 사람이 파티장이 아니어도 일단 무조건 시작되도록 (간이 버전)
                Debug.Log("[Match] 서버에 게임 시작을 요청합니다...");
                startGameButton.interactable = false;
                NetworkPartyManager.Instance.StartGame();
            }
        }

        private void HandleGameStart(string ip, int port)
        {
            Debug.Log($"[Match] 서버로부터 게임 시작 신호를 받았습니다! 접속 대상: {ip}:{port}");
            
            // TODO: ip와 port 정보를 저장해두고 인게임 씬에서 Netcode(NGO)로 접속 시도해야 함
            PlayerPrefs.SetString("TargetServerIP", ip);
            PlayerPrefs.SetInt("TargetServerPort", port);
            PlayerPrefs.Save();

            // 로딩 씬으로 넘어감
            SceneManager.LoadScene("LoadingScene");
        }
    }
}
