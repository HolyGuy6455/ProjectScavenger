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
                StartCoroutine(StartAsServerRoutine());
            }
        }

        private bool IsDedicatedServer()
        {
            // 유니티 실행 인자에 -server 가 포함되어 있으면 서버로 간주
            string[] args = System.Environment.GetCommandLineArgs();
            return args.Contains("-server");
        }

        private System.Collections.IEnumerator StartAsServerRoutine()
        {
            Debug.Log("[NGO] 초기화 중... 데디케이티드 서버 전용 씬 부팅");
            
            // DataManager가 모두 로드될 때까지 대기 (서버는 로딩씬을 거치지 않으므로 여기서 대기 필수)
            yield return new WaitUntil(() => DataManager.Instance != null && DataManager.Instance.IsDataLoaded);
            Debug.Log("[NGO] DataManager 데이터 비동기 로드 완료 확인.");

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
            NetworkManager.Singleton.NetworkConfig.ConnectionApproval = true;
            NetworkManager.Singleton.ConnectionApprovalCallback = OnConnectionApproval;

            if (NetworkManager.Singleton.StartServer())
            {
                Debug.Log("[NGO] 전용 서버 구동 성공. InGameScene으로 동기화합니다.");
                
                // NGO가 Addressable 씬(Stage002)을 클라이언트에게 동기화하려다 해시를 찾지 못해 터지는 것을 방지합니다.
                NetworkManager.Singleton.SceneManager.VerifySceneBeforeLoading = (sceneIndex, sceneName, loadSceneMode) =>
                {
                    if (sceneName != null && sceneName.Contains("Stage002")) return false;
                    return true;
                };
                
                NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
                _serverStartupTime = Time.realtimeSinceStartup;

                NetworkManager.Singleton.SceneManager.OnLoadEventCompleted += (sceneName, loadSceneMode, clientsCompleted, clientsTimedOut) =>
                {
                    Debug.Log($"[NGO-DEBUG] OnLoadEventCompleted! sceneName: {sceneName}");
                    if (sceneName != null && sceneName.Equals("IngameScene", System.StringComparison.OrdinalIgnoreCase))
                    {
                        Debug.Log("[NGO-DEBUG] NGO 내부 씬 로드 완료 감지! Addressables를 통해 Stage002을 로드합니다.");
                        UnityEngine.AddressableAssets.Addressables.LoadSceneAsync("Stage002", UnityEngine.SceneManagement.LoadSceneMode.Additive).Completed += handle =>
                        {
                            if (handle.Status == UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationStatus.Succeeded)
                            {
                                Debug.Log("[Bootstrapper] Stage002 맵 로드 성공! 몬스터/엄폐물 동적 스폰을 시작합니다.");
                                Scavenger.Core.MapSpawner.SpawnMapObjects();
                            }
                            else
                            {
                                Debug.LogError("[Bootstrapper] Stage002 맵 로드 실패!");
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


        private void OnConnectionApproval(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            if (request.Payload != null && request.Payload.Length > 0)
            {
                try
                {
                    string payloadStr = System.Text.Encoding.UTF8.GetString(request.Payload);
                    Scavenger.Network.UserInfo userInfo = null;
                    
                    // 백엔드 파스칼케이스(PascalCase) 이슈 우회
                    payloadStr = payloadStr.Replace("\"SelectedCharIds\"", "\"selectedCharIds\"");
                    
                    if (payloadStr.Trim().StartsWith("["))
                    {
                        string wrapped = $"{{\"items\":{payloadStr}}}";
                        var wrapper = JsonUtility.FromJson<Scavenger.Network.UserInfoListWrapper>(wrapped);
                        if (wrapper != null && wrapper.items != null && wrapper.items.Count > 0)
                            userInfo = wrapper.items[0];
                    }
                    else
                    {
                        userInfo = JsonUtility.FromJson<Scavenger.Network.UserInfo>(payloadStr);
                    }
                    
                    if (userInfo != null && userInfo.selectedCharIds != null && userInfo.selectedCharIds.Count >= 4)
                    {
                        var deck = new PlayerController.CharacterDeckData
                        {
                            char1 = userInfo.selectedCharIds[0],
                            char2 = userInfo.selectedCharIds[1],
                            char3 = userInfo.selectedCharIds[2],
                            char4 = userInfo.selectedCharIds[3]
                        };
                        PlayerController.PendingClientDecks[request.ClientNetworkId] = deck;
                        
                        // 닉네임 저장 로직 (OffScreenAllySpawner 연동)
                        string playerName = !string.IsNullOrEmpty(userInfo.username) ? userInfo.username : $"Player_{request.ClientNetworkId}";
                        OffScreenAllySpawner.PendingClientNames[request.ClientNetworkId] = playerName;

                        Debug.Log($"[NGO] Connection Approval 성공! ClientID: {request.ClientNetworkId}, Name: {playerName}, Deck: {deck.char1}, {deck.char2}, {deck.char3}, {deck.char4}");
                    }
                    else
                    {
                        Debug.LogWarning($"[NGO] Connection Payload 파싱 실패 (데이터 불충분): {payloadStr}");
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[NGO] Connection Payload 파싱 중 에러: {ex.Message}");
                }
            }
            else
            {
                Debug.LogWarning($"[NGO] 클라이언트가 Connection Payload를 보내지 않았습니다. ClientID: {request.ClientNetworkId}");
            }

            // 테스트 환경이므로 일단 무조건 승인 및 플레이어 오브젝트 생성 허용
            response.Approved = true;
            response.CreatePlayerObject = true;
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
