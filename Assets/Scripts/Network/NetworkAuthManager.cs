using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace Scavenger.Network
{
    public class NetworkAuthManager : MonoBehaviour
    {
        public static NetworkAuthManager Instance { get; private set; }

        [Header("Server Settings")]
        public string ServerUrl { get; private set; }

        private void LoadServerConfig()
        {
            var textAsset = Resources.Load<TextAsset>("ServerConfig");
            if (textAsset != null)
            {
                var config = JsonUtility.FromJson<ServerConfigInfo>(textAsset.text);
                ServerUrl = config.ServerUrl;
                Debug.Log($"[Auth] 서버 주소를 불러왔습니다: {ServerUrl}");
            }
            else
            {
                ServerUrl = "http://localhost:5000"; // fallback
                Debug.LogWarning($"[Auth] ServerConfig.json 파일을 찾을 수 없어 기본값({ServerUrl})을 사용합니다.");
            }
        }

        // 발급받은 JWT 토큰
        public string AuthToken { get; private set; }
        public string Username { get; private set; }
        
        // 이벤트를 통해 로그인 성공 여부를 UI 쪽에 알려줄 수 있습니다.
        public Action<bool> OnLoginComplete;

        private const string PREFS_DEVICE_ID = "Guest_DeviceId";

        private void Awake()
        {
            LoadServerConfig();
            
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            // 게임 시작 시 자동으로 게스트 로그인 시도
            StartGuestLogin();
        }

        public void StartGuestLogin()
        {
            string deviceId = PlayerPrefs.GetString(PREFS_DEVICE_ID, string.Empty);
            
            if (string.IsNullOrEmpty(deviceId))
            {
                // 기기에 저장된 키가 없다면 새로 생성 (초기화)
                deviceId = Guid.NewGuid().ToString();
                PlayerPrefs.SetString(PREFS_DEVICE_ID, deviceId);
                PlayerPrefs.Save();
                Debug.Log($"[Auth] 새로운 DeviceId 발급: {deviceId}");
            }
            else
            {
                Debug.Log($"[Auth] 기존 DeviceId 발견: {deviceId}");
            }

            StartCoroutine(SendLoginRequest(deviceId));
        }

        // 유저가 원할 때 기기 키를 초기화하는 디버그/테스트용 함수
        public void ResetGuestAccount()
        {
            PlayerPrefs.DeleteKey(PREFS_DEVICE_ID);
            PlayerPrefs.Save();
            Debug.Log("[Auth] 기기 계정 키가 초기화되었습니다.");
            StartGuestLogin(); // 초기화 후 바로 새 계정 로그인 시도
        }

        private IEnumerator SendLoginRequest(string deviceId)
        {
            string url = $"{ServerUrl}/api/auth/guest-login";
            string jsonBody = $"{{\"deviceId\": \"{deviceId}\"}}";

            using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");

                Debug.Log("[Auth] 서버에 로그인 요청을 보냅니다...");
                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.ConnectionError || request.result == UnityWebRequest.Result.ProtocolError)
                {
                    Debug.LogError($"[Auth] 로그인 실패: {request.error}\n(서버가 켜져 있는지 확인하세요)");
                    OnLoginComplete?.Invoke(false);
                }
                else
                {
                    // JSON 응답 파싱 (간단한 파싱을 위해 JsonUtility 사용)
                    GuestLoginResponse response = JsonUtility.FromJson<GuestLoginResponse>(request.downloadHandler.text);
                    
                    if (response != null && !string.IsNullOrEmpty(response.token))
                    {
                        AuthToken = response.token;
                        Username = response.username;
                        Debug.Log($"[Auth] 로그인 성공! 환영합니다, {Username}");
                        OnLoginComplete?.Invoke(true);
                    }
                    else
                    {
                        Debug.LogError("[Auth] 로그인 응답 파싱 실패");
                        OnLoginComplete?.Invoke(false);
                    }
                }
            }
        }

        [Serializable]
        private class GuestLoginResponse
        {
            public string token;
            public string username;
            public bool isNewUser;
        }
    }
}
