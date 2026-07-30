using System;
using System.Linq;
using Scavenger.Core;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

namespace Scavenger.UI
{
    public class UI_LoadingScreen : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI statusText;

        private void Start()
        {
            ExecuteIntent();
        }

        private void ExecuteIntent()
        {
            var intent = SceneTransitionManager.CurrentIntent;
            Debug.Log($"[LoadingScreen] 인텐트 실행: {intent}");

            switch (intent)
            {
                case LoadingIntent.EnterInGame:
                    ConnectToDedicatedServer();
                    break;
                case LoadingIntent.ReturnToLobby:
                    ReturnToLobby();
                    break;
                case LoadingIntent.StartSingleplayerHost:
                    StartSingleplayerHost();
                    break;
                default:
                    Debug.LogWarning("정의되지 않거나 None인 인텐트입니다. 기본적으로 로비로 돌려보냅니다.");
                    ReturnToLobby();
                    break;
            }

            SceneTransitionManager.ConsumeIntent();
        }

        private void ConnectToDedicatedServer()
        {
            StartCoroutine(ConnectRoutine());
        }

        private System.Collections.IEnumerator ConnectRoutine()
        {
            string ip = PlayerPrefs.GetString("TargetServerIP", "127.0.0.1");
            int port = PlayerPrefs.GetInt("TargetServerPort", 7777);

            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            if (transport != null)
            {
                transport.SetConnectionData(ip, (ushort)port);
            }

            int attempts = 0;
            int maxAttempts = 20; // 20번 시도 (약 1분 대기)

            while (attempts < maxAttempts)
            {
                attempts++;
                if (statusText != null) statusText.text = $"Waiting for Server bootup... ({attempts}/{maxAttempts})\n{ip}:{port}";
                Debug.Log($"[NGO] 서버 접속 시도 ({attempts}/{maxAttempts}). 대상: {ip}:{port}");

                // 혹시 이전 시도로 켜져있다면 끄기
                if (NetworkManager.Singleton.IsClient)
                {
                    NetworkManager.Singleton.Shutdown();
                    yield return new WaitForSeconds(0.5f);
                }

                NetworkManager.Singleton.StartClient();

                // 접속 결과 대기
                float waitTime = 3f;
                while (waitTime > 0)
                {
                    if (NetworkManager.Singleton.IsConnectedClient)
                    {
                        if (statusText != null) statusText.text = "Connected! Waiting for Scene Sync...";
                        Debug.Log("[NGO] 서버 접속 성공! 씬 동기화 대기 중...");
                        yield break;
                    }
                    waitTime -= Time.deltaTime;
                    yield return null;
                }

                Debug.LogWarning("[NGO] 접속 실패. 서버가 아직 안 켜진 것 같으니 재시도합니다...");
            }

            if (statusText != null) statusText.text = "Connection Timeout!";
            NetworkManager.Singleton.Shutdown();
            Invoke(nameof(ReturnToLobby), 3f);
        }

        private void StartSingleplayerHost()
        {
            if (statusText != null) statusText.text = "Starting Singleplayer Host...";
            
            // 싱글플레이어는 자신이 호스트(서버+클라이언트)가 됩니다.
            if (NetworkManager.Singleton.StartHost())
            {
                Debug.Log("[NGO] 호스트 모드(싱글플레이어) 구동 성공. InGameScene으로 동기화합니다.");
                NetworkManager.Singleton.SceneManager.LoadScene("InGameScene", UnityEngine.SceneManagement.LoadSceneMode.Single);
            }
            else
            {
                Debug.LogError("[NGO] 호스트 모드 구동 실패.");
                if (statusText != null) statusText.text = "Failed to start Host.";
            }
        }

        private void ReturnToLobby()
        {
            if (statusText != null) statusText.text = "Returning to Lobby...";
            
            // 혹시라도 네트워크가 연결되어 있다면 종료
            if (NetworkManager.Singleton != null && (NetworkManager.Singleton.IsClient || NetworkManager.Singleton.IsServer))
            {
                NetworkManager.Singleton.Shutdown();
            }

            // 약간의 딜레이 후 아웃게임 씬으로 복귀
            Invoke(nameof(LoadOutgameScene), 1f);
        }

        private void LoadOutgameScene()
        {
            SceneManager.LoadScene("OutgameScene");
        }
    }
}
