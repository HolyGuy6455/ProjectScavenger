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
            if (transform.root != null)
            {
                DontDestroyOnLoad(transform.root.gameObject);
            }

            SceneManager.sceneLoaded += OnSceneLoaded;
            StartCoroutine(StartupCleanupRoutine());
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // 서버가 Additive로 맵을 열어주면 클라이언트도 로드가 끝납니다.
            // 그때 로딩 화면을 파괴하여 빈 화면이 보이지 않도록 합니다.
            string expectedMap = PlayerPrefs.GetString("SelectedMap", "Stage001");
            if (scene.name == expectedMap)
            {
                Destroy(transform.root.gameObject);
            }
        }

        private System.Collections.IEnumerator StartupCleanupRoutine()
        {
            // 로딩 씬에 진입하자마자 이전 씬(메인화면이든 인게임이든)에서 남긴 쓰레기 메모리를 싹 비웁니다.
            System.GC.Collect();
            yield return Resources.UnloadUnusedAssets();

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
                case LoadingIntent.ReturnToIngameTest:
                    ReturnToTestScene();
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

                // 클라이언트 연결 대기 (NGO의 후발 접속자는 최초 씬 동기화 시 OnLoadEventCompleted를 호출하지 않으므로 직접 로드)
                if (NetworkManager.Singleton.SceneManager != null)
                {
                    NetworkManager.Singleton.SceneManager.VerifySceneBeforeLoading = (sceneIndex, sceneName, loadSceneMode) =>
                    {
                        string expectedMap = PlayerPrefs.GetString("SelectedMap", "Stage001");
                        if (sceneName.Contains(expectedMap)) return false;
                        return true;
                    };
                }
                else
                {
                    Debug.Log("[NGO] NetworkManager.Singleton.SceneManager is null!");
                }
                
                // 접속 결과 대기
                float waitTime = 15f; // 인터넷 환경에서는 씬 동기화에 수 초가 걸릴 수 있으므로 넉넉히 15초 대기
                while (waitTime > 0)
                {
                    if (NetworkManager.Singleton.IsConnectedClient)
                    {
                        if (statusText != null) statusText.text = "Connected! Loading Map via Addressables...";
                        string mapToLoad = PlayerPrefs.GetString("SelectedMap", "Stage001");
                        Debug.Log($"[NGO] 서버 접속 및 IngameScene 동기화 완료! Addressables로 {mapToLoad} 로드 시작...");
                        
                        // 접속이 완료되었다는 것은 IngameScene 로드가 끝났음을 의미하므로 바로 맵을 로드합니다.
                        UnityEngine.AddressableAssets.Addressables.LoadSceneAsync(mapToLoad, LoadSceneMode.Additive);
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
                
                string mapToLoad = PlayerPrefs.GetString("SelectedMap", "Stage001");
                NetworkManager.Singleton.SceneManager.VerifySceneBeforeLoading = (sceneIndex, sceneName, loadSceneMode) =>
                {
                    if (sceneName != null && sceneName.Contains(mapToLoad)) return false;
                    return true;
                };
                
                NetworkManager.Singleton.SceneManager.OnLoadEventCompleted += (sceneName, loadSceneMode, clientsCompleted, clientsTimedOut) =>
                {
                    if (sceneName != null && sceneName.Equals("InGameScene", System.StringComparison.OrdinalIgnoreCase) && NetworkManager.Singleton.IsServer)
                    {
                        Debug.Log($"[NGO-DEBUG] 싱글플레이어 내부 씬 로드 완료! Addressables를 통해 {mapToLoad}을(를) 얹습니다.");
                        UnityEngine.AddressableAssets.Addressables.LoadSceneAsync(mapToLoad, LoadSceneMode.Additive).Completed += handle =>
                        {
                            if (handle.Status == UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationStatus.Succeeded)
                            {
                                Debug.Log("[Bootstrapper] 싱글플레이어 맵 로드 성공! 스폰 시작!");
                                Scavenger.Core.MapSpawner.SpawnMapObjects();
                            }
                        };
                    }
                };

                NetworkManager.Singleton.SceneManager.LoadScene("InGameScene", LoadSceneMode.Single);
            }
            else
            {
                Debug.LogError("[NGO] 호스트 모드 구동 실패.");
                if (statusText != null) statusText.text = "Failed to start Host.";
            }
        }

        private void ReturnToLobby()
        {
            StartCoroutine(ReturnToLobbyRoutine());
        }

        private System.Collections.IEnumerator ReturnToLobbyRoutine()
        {
            if (statusText != null) statusText.text = "Returning to Lobby...\nCleaning up resources...";
            
            // 혹시라도 네트워크가 연결되어 있다면 종료
            if (NetworkManager.Singleton != null && (NetworkManager.Singleton.IsClient || NetworkManager.Singleton.IsServer))
            {
                NetworkManager.Singleton.Shutdown();
            }

            // 무거운 리소스 해제 및 가비지 컬렉터 강제 실행
            System.GC.Collect();
            yield return Resources.UnloadUnusedAssets();

            // 짧은 대기 후 아웃게임 씬으로 복귀
            yield return new WaitForSeconds(0.5f);
            SceneManager.LoadScene("OutgameScene");
        }

        private void ReturnToTestScene()
        {
            StartCoroutine(ReturnToTestSceneRoutine());
        }

        private System.Collections.IEnumerator ReturnToTestSceneRoutine()
        {
            if (statusText != null) statusText.text = "Returning to Test Scene...\nCleaning up resources...";
            
            if (NetworkManager.Singleton != null && (NetworkManager.Singleton.IsClient || NetworkManager.Singleton.IsServer))
            {
                NetworkManager.Singleton.Shutdown();
            }

            System.GC.Collect();
            yield return Resources.UnloadUnusedAssets();

            yield return new WaitForSeconds(0.5f);
            SceneManager.LoadScene("IngameTestScene");
        }
    }
}
