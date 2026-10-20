using System;
using System.Linq;
using Scavenger.Audio;
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
            // 로딩 씬을 직접 실행한 경우에도 월드 리스너를 차단합니다.
            SoundManager.BeginSceneTransition();
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
            string expectedMap = PlayerPrefs.GetString("SelectedMap", "Stage002");
            if (scene.name == expectedMap)
            {
                SoundManager.CompleteSceneTransition();
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

                string matchToken = PlayerPrefs.GetString("MatchToken", "{\"selectedCharIds\":[1,2,3,4]}");
                
                // 멀티플레이어 배열 형태의 matchToken인 경우 내 정보만 추출
                if (matchToken.Trim().StartsWith("["))
                {
                    string wrapped = $"{{\"items\":{matchToken.Replace("\"SelectedCharIds\"", "\"selectedCharIds\"").Replace("\"Username\"", "\"username\"")}}}";
                    var wrapper = JsonUtility.FromJson<Scavenger.Network.UserInfoListWrapper>(wrapped);
                    if (wrapper != null && wrapper.items != null)
                    {
                        var myUsername = Scavenger.Network.NetworkAuthManager.Instance?.Username;
                        var myInfo = wrapper.items.Find(x => x.username == myUsername);
                        if (myInfo == null && wrapper.items.Count > 0) myInfo = wrapper.items[0]; // fallback
                        if (myInfo != null) matchToken = JsonUtility.ToJson(myInfo);
                    }
                }
                
                Debug.Log($"[DEBUG-TRACE] [UI_LoadingScreen Client] 서버로 전송할 내 MatchToken: {matchToken}");
                
                NetworkManager.Singleton.NetworkConfig.ConnectionApproval = true;
                NetworkManager.Singleton.NetworkConfig.ConnectionApproval = true;
                NetworkManager.Singleton.NetworkConfig.ConnectionData = System.Text.Encoding.UTF8.GetBytes(matchToken);
                Debug.Log($"[NGO] ConnectionData 세팅 완료: {matchToken}");

                NetworkManager.Singleton.StartClient();

                // 클라이언트 연결 대기 (NGO의 후발 접속자는 최초 씬 동기화 시 OnLoadEventCompleted를 호출하지 않으므로 직접 로드)
                if (NetworkManager.Singleton.SceneManager != null)
                {
                    NetworkManager.Singleton.SceneManager.VerifySceneBeforeLoading = (sceneIndex, sceneName, loadSceneMode) =>
                    {
                        string expectedMap = PlayerPrefs.GetString("SelectedMap", "Stage002");
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
                        string mapToLoad = PlayerPrefs.GetString("SelectedMap", "Stage002");
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
            
            // 싱글플레이어는 외부 접속을 허용할 필요가 없으므로 로컬 루프백(127.0.0.1)으로 포트를 바인딩합니다.
            // 안드로이드 등에서 불명확한 IP로 Bind()가 실패하는 현상(Address unreachable)을 방지합니다.
            // 포트 0으로 OS가 빈 포트를 배정하게 하여 다른 세션과의 포트 충돌을 피합니다.
            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            if (transport != null)
            {
                transport.SetConnectionData("127.0.0.1", 0, "127.0.0.1");
            }

            // 싱글플레이어는 자신이 호스트(서버+클라이언트)가 됩니다.
            string matchToken = PlayerPrefs.GetString("MatchToken", "{\"selectedCharIds\":[1,2,3,4]}");
            Debug.Log($"[DEBUG-TRACE] [UI_LoadingScreen Host] PlayerPrefs에서 읽어온 MatchToken: {matchToken}");
            
            NetworkManager.Singleton.NetworkConfig.ConnectionApproval = true;
                NetworkManager.Singleton.NetworkConfig.ConnectionData = System.Text.Encoding.UTF8.GetBytes(matchToken);
            
            // Host는 ConnectionApproval 로직을 타지 않으므로 직접 서버용 딕셔너리에 꽂아줍니다.
            try
            {
                Scavenger.Network.UserInfo myInfo = null;
                
                // 백엔드 파스칼케이스(PascalCase) 이슈 우회
                matchToken = matchToken.Replace("\"SelectedCharIds\"", "\"selectedCharIds\"").Replace("\"Username\"", "\"username\"");
                
                if (matchToken.Trim().StartsWith("["))
                {
                    string wrapped = $"{{\"items\":{matchToken}}}";
                    var wrapper = JsonUtility.FromJson<Scavenger.Network.UserInfoListWrapper>(wrapped);
                    if (wrapper != null && wrapper.items != null)
                    {
                        var myUsername = Scavenger.Network.NetworkAuthManager.Instance?.Username;
                        myInfo = wrapper.items.Find(x => x.username == myUsername);
                        if (myInfo == null && wrapper.items.Count > 0) myInfo = wrapper.items[0];
                    }
                }
                else
                {
                    myInfo = JsonUtility.FromJson<Scavenger.Network.UserInfo>(matchToken);
                }

                if (myInfo != null && myInfo.selectedCharIds != null && myInfo.selectedCharIds.Count >= 4)
                {
                    var deck = new PlayerController.CharacterDeckData
                    {
                        char1 = myInfo.selectedCharIds[0],
                        char2 = myInfo.selectedCharIds[1],
                        char3 = myInfo.selectedCharIds[2],
                        char4 = myInfo.selectedCharIds[3]
                    };
                    PlayerController.PendingClientDecks[NetworkManager.ServerClientId] = deck;
                    Debug.Log($"[NGO] Host 덱 데이터를 서버용 딕셔너리에 직접 등록했습니다: {deck.char1},{deck.char2},{deck.char3},{deck.char4}");
                }
                else
                {
                    Debug.LogWarning($"[NGO] 파싱된 Host MatchToken에 캐릭터 데이터가 부족합니다: {matchToken}");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[NGO] Host MatchToken 파싱 중 에러: {ex.Message} \nToken: {matchToken}");
            }

            Debug.Log($"[NGO] Host ConnectionData 세팅 완료: {matchToken}");
            
            if (NetworkManager.Singleton.StartHost())
            {
                Debug.Log("[NGO] 호스트 모드(싱글플레이어) 구동 성공. InGameScene으로 동기화합니다.");
                
                string mapToLoad = PlayerPrefs.GetString("SelectedMap", "Stage002");
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
            yield return StartCoroutine(CleanupAndLoadScene("OutgameScene"));
        }

        private void ReturnToTestScene()
        {
            StartCoroutine(ReturnToTestSceneRoutine());
        }

        private System.Collections.IEnumerator ReturnToTestSceneRoutine()
        {
            yield return StartCoroutine(CleanupAndLoadScene("IngameTestScene"));
        }

        private System.Collections.IEnumerator CleanupAndLoadScene(string targetSceneName)
        {
            if (statusText != null) statusText.text = $"Returning to {targetSceneName}...\nCleaning up resources...";
            
            // 네트워크 셧다운
            if (NetworkManager.Singleton != null && (NetworkManager.Singleton.IsClient || NetworkManager.Singleton.IsServer))
            {
                NetworkManager.Singleton.Shutdown();
            }

            // 셧다운이 처리될 약간의 시간을 줌
            yield return new WaitForSeconds(0.1f);

            // DontDestroyOnLoad로 남아서 다음 게임에 영향을 주거나 중복을 유발하는 매니저들을 강제 파괴
            if (NetworkManager.Singleton != null)
            {
                Destroy(NetworkManager.Singleton.gameObject);
            }
            
            if (CharacterStateManager.Instance != null)
            {
                Destroy(CharacterStateManager.Instance.gameObject);
            }

            if (Scavenger.Manager.GameFlowManager.Instance != null)
            {
                Destroy(Scavenger.Manager.GameFlowManager.Instance.gameObject);
            }

            // 기타 불필요한 메모리 해제
            System.GC.Collect();
            yield return Resources.UnloadUnusedAssets();

            yield return new WaitForSeconds(0.4f);
            
            // 목표 씬 로드
            SoundManager.CompleteSceneTransition();
            SceneManager.LoadScene(targetSceneName);

            // 로딩 화면 자기 자신도 파괴 (Test 씬 등에 이미 배치되어 있을 경우 중복 방지)
            Destroy(transform.root.gameObject);
        }
    }
}
