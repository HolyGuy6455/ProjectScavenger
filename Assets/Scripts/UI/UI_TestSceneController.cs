using UnityEngine;
using UnityEngine.UI;
using Scavenger.Core;
using TMPro;

namespace Scavenger.UI
{
    public class UI_TestSceneController : MonoBehaviour
    {
        [SerializeField] private Button startButton;
        [SerializeField] private Button startDedicatedButton;
        [SerializeField] private TMP_Dropdown mapDropdown; // 나중을 위해 추가
        
        [Header("임시 캐릭터 테스트용")]
        [SerializeField] private TMP_InputField char1Input;
        [SerializeField] private TMP_InputField char2Input;
        [SerializeField] private TMP_InputField char3Input;
        [SerializeField] private TMP_InputField char4Input;

        private void Start()
        {
            if (startButton != null)
            {
                startButton.onClick.AddListener(OnStartClicked);
            }
            
            if (startDedicatedButton != null)
            {
                startDedicatedButton.onClick.AddListener(OnStartDedicatedClicked);
            }
            
            if (mapDropdown != null)
            {
                // 기본 맵 초기화
                mapDropdown.options.Clear();
                mapDropdown.options.Add(new TMP_Dropdown.OptionData("Stage001"));
                mapDropdown.options.Add(new TMP_Dropdown.OptionData("Stage002"));
                // 나중에 맵이 추가되면 여기에 추가할 수 있습니다.
            }
        }

        private void OnStartClicked()
        {
            string selectedMap = "Stage001";
            if (mapDropdown != null && mapDropdown.options.Count > 0)
            {
                selectedMap = mapDropdown.options[mapDropdown.value].text;
            }

            // 맵 이름을 PlayerPrefs에 저장하여 UI_LoadingScreen에서 사용할 수 있도록 함
            PlayerPrefs.SetString("SelectedMap", selectedMap);
            
            // 임시 테스트용 가짜 MatchToken 생성 로직
            int c1 = 1, c2 = 2, c3 = 3, c4 = 4; // 입력 안했을 때 기본 테스트용 번호 (10~13)
            if (char1Input != null && int.TryParse(char1Input.text, out int p1)) c1 = p1;
            if (char2Input != null && int.TryParse(char2Input.text, out int p2)) c2 = p2;
            if (char3Input != null && int.TryParse(char3Input.text, out int p3)) c3 = p3;
            if (char4Input != null && int.TryParse(char4Input.text, out int p4)) c4 = p4;

            string fakeMatchToken = $"{{\"selectedCharIds\":[{c1},{c2},{c3},{c4}]}}";
            PlayerPrefs.SetString("MatchToken", fakeMatchToken);
            
            PlayerPrefs.Save();

            Debug.Log($"[TestScene] 싱글플레이어 호스트로 시작합니다. 선택된 맵: {selectedMap}");

            // 인텐트를 StartSingleplayerHost로 지정하고 로딩 씬으로 이동
            SceneTransitionManager.LoadSceneWithIntent(LoadingIntent.StartSingleplayerHost);
        }

        private void OnStartDedicatedClicked()
        {
            if (startDedicatedButton != null)
            {
                startDedicatedButton.interactable = false;
            }

            string selectedMap = "Stage001";
            if (mapDropdown != null && mapDropdown.options.Count > 0)
            {
                selectedMap = mapDropdown.options[mapDropdown.value].text;
            }

            PlayerPrefs.SetString("SelectedMap", selectedMap);

            // 임시 테스트용 가짜 MatchToken 생성 로직
            int c1 = 10, c2 = 11, c3 = 12, c4 = 13;
            if (char1Input != null && int.TryParse(char1Input.text, out int p1)) c1 = p1;
            if (char2Input != null && int.TryParse(char2Input.text, out int p2)) c2 = p2;
            if (char3Input != null && int.TryParse(char3Input.text, out int p3)) c3 = p3;
            if (char4Input != null && int.TryParse(char4Input.text, out int p4)) c4 = p4;

            string fakeMatchToken = $"{{\"selectedCharIds\":[{c1},{c2},{c3},{c4}]}}";
            PlayerPrefs.SetString("MatchToken", fakeMatchToken);

            PlayerPrefs.Save();

            Debug.Log($"[TestScene] 싱글플레이어 데디케이티드 서버로 시작 요청...");
            StartCoroutine(SpawnDedicatedServerCoroutine());
        }

        private System.Collections.IEnumerator SpawnDedicatedServerCoroutine()
        {
            string serverUrl = "http://localhost:5000";
            if (Scavenger.Network.NetworkAuthManager.Instance != null)
            {
                serverUrl = Scavenger.Network.NetworkAuthManager.Instance.ServerUrl;
            }
            else
            {
                // NetworkAuthManager가 씬에 없다면 Resources에서 직접 설정값을 읽어옵니다.
                var textAsset = Resources.Load<TextAsset>("ServerConfig");
                if (textAsset != null)
                {
                    var config = JsonUtility.FromJson<Scavenger.Network.ServerConfigInfo>(textAsset.text);
                    if (config != null && !string.IsNullOrEmpty(config.ServerUrl))
                    {
                        serverUrl = config.ServerUrl;
                    }
                }
                Debug.Log($"[TestScene] NetworkAuthManager가 없습니다. ServerConfig.json에서 주소({serverUrl})를 로드합니다.");
            }

            string url = serverUrl + "/api/debug/spawn-dedicated";

            using (UnityEngine.Networking.UnityWebRequest request = new UnityEngine.Networking.UnityWebRequest(url, "POST"))
            {
                request.downloadHandler = new UnityEngine.Networking.DownloadHandlerBuffer();
                
                yield return request.SendWebRequest();

                if (request.result == UnityEngine.Networking.UnityWebRequest.Result.ConnectionError || 
                    request.result == UnityEngine.Networking.UnityWebRequest.Result.ProtocolError)
                {
                    Debug.LogError($"[TestScene] 데디케이티드 서버 스폰 실패: {request.error}\n응답: {request.downloadHandler.text}");
                    if (startDedicatedButton != null)
                    {
                        startDedicatedButton.interactable = true;
                    }
                }
                else
                {
                    Debug.Log($"[TestScene] 데디케이티드 서버 스폰 성공! 응답: {request.downloadHandler.text}");
                    
                    SpawnServerResponse response = JsonUtility.FromJson<SpawnServerResponse>(request.downloadHandler.text);
                    
                    PlayerPrefs.SetString("TargetServerIP", response.ip);
                    PlayerPrefs.SetInt("TargetServerPort", response.port);
                    PlayerPrefs.Save();

                    SceneTransitionManager.LoadSceneWithIntent(LoadingIntent.EnterInGame);
                }
            }
        }

        [System.Serializable]
        private class SpawnServerResponse
        {
            public string ip;
            public int port;
        }
    }
}
