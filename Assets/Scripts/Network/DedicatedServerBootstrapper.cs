using System.Linq;
using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;

namespace Scavenger.Network
{
    public class DedicatedServerBootstrapper : MonoBehaviour
    {
        private float _serverStartupTime;
        private bool _hasAnyClientConnected = false;
        private const float EMPTY_SERVER_TIMEOUT = 60f; // 60초 대기

        private void Start()
        {
            if (IsDedicatedServer())
            {
                DontDestroyOnLoad(gameObject);
                StartAsServer();
            }
        }

        private bool IsDedicatedServer()
        {
            // 유니티 실행 인자에 -server 가 포함되어 있으면 서버로 간주
            string[] args = System.Environment.GetCommandLineArgs();
            return args.Contains("-server");
        }

        private void StartAsServer()
        {
            Debug.Log("[NGO] 초기화 중... 데디케이티드 서버 전용 씬 부팅");
            
            // 포트 파싱 (-port 7777)
            string[] args = System.Environment.GetCommandLineArgs();
            int port = 7777; // 기본값
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "-port" && args.Length > i + 1)
                {
                    int.TryParse(args[i + 1], out port);
                }
            }

            Debug.Log($"[NGO] 데디케이티드 서버 모드로 가동합니다. 포트: {port}");

            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            if (transport != null)
            {
                transport.SetConnectionData("0.0.0.0", (ushort)port);
            }

            // 서버 구동
            if (NetworkManager.Singleton.StartServer())
            {
                Debug.Log("[NGO] 전용 서버 구동 성공. InGameScene으로 동기화합니다.");
                
                // NGO가 Addressable 씬(Stage001)을 클라이언트에게 동기화하려다 해시를 찾지 못해 터지는 것을 방지합니다.
                NetworkManager.Singleton.SceneManager.VerifySceneBeforeLoading = (sceneIndex, sceneName, loadSceneMode) =>
                {
                    if (sceneName != null && sceneName.Contains("Stage001")) return false;
                    return true;
                };
                
                NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
                _serverStartupTime = Time.realtimeSinceStartup;

                NetworkManager.Singleton.SceneManager.OnLoadEventCompleted += (sceneName, loadSceneMode, clientsCompleted, clientsTimedOut) =>
                {
                    Debug.Log($"[NGO-DEBUG] OnLoadEventCompleted! sceneName: {sceneName}");
                    if (sceneName != null && sceneName.Equals("IngameScene", System.StringComparison.OrdinalIgnoreCase))
                    {
                        Debug.Log("[NGO-DEBUG] NGO 내부 씬 로드 완료 감지! Addressables를 통해 Stage001을 로드합니다.");
                        UnityEngine.AddressableAssets.Addressables.LoadSceneAsync("Stage001", UnityEngine.SceneManagement.LoadSceneMode.Additive).Completed += handle =>
                        {
                            if (handle.Status == UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationStatus.Succeeded)
                            {
                                Debug.Log("[Bootstrapper] Stage001 맵 로드 성공! 몬스터/엄폐물 동적 스폰을 시작합니다.");
                                Scavenger.Core.MapSpawner.SpawnMapObjects();
                            }
                            else
                            {
                                Debug.LogError("[Bootstrapper] Stage001 맵 로드 실패!");
                            }
                        };
                    }
                };

                NetworkManager.Singleton.SceneManager.LoadScene("IngameScene", UnityEngine.SceneManagement.LoadSceneMode.Single);
            }
            else
            {
                Debug.LogError("[NGO] 서버 구동 실패.");
            }
        }

        private void OnClientConnected(ulong clientId)
        {
            _hasAnyClientConnected = true;
            Debug.Log($"[NGO] 클라이언트 접속 완료. ClientID: {clientId}");
        }

        private void Update()
        {
            if (!IsDedicatedServer() || NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) 
                return;

            if (!_hasAnyClientConnected)
            {
                // 1. 처음 구동 후 60초 동안 아무도 접속하지 않으면 자동 종료
                if (Time.realtimeSinceStartup - _serverStartupTime > EMPTY_SERVER_TIMEOUT)
                {
                    Debug.Log("[NGO] 60초 내에 접속한 클라이언트가 없어 데디케이티드 서버를 자동 종료합니다.");
                    Application.Quit();
                }
            }
            else
            {
                // 2. 한 명이라도 접속했었는데, 현재 접속자가 0명이 되면 자동 종료
                if (NetworkManager.Singleton.ConnectedClientsIds.Count == 0)
                {
                    Debug.Log("[NGO] 모든 클라이언트가 퇴장하여 데디케이티드 서버를 자동 종료합니다.");
                    Application.Quit();
                }
            }
        }
    }
}
