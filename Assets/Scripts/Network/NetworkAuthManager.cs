using System;
using UnityEngine;

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

            SendLoginRequest(deviceId);
        }

        // 유저가 원할 때 기기 키를 초기화하는 디버그/테스트용 함수
        public void ResetGuestAccount()
        {
            PlayerPrefs.DeleteKey(PREFS_DEVICE_ID);
            PlayerPrefs.Save();
            Debug.Log("[Auth] 기기 계정 키가 초기화되었습니다.");
            StartGuestLogin(); // 초기화 후 바로 새 계정 로그인 시도
        }

        private void SendLoginRequest(string deviceId)
        {
            var request = NetworkRequestManager.CreateJsonRequest(ServerUrl + "/api/auth/guest-login", "POST",
                JsonUtility.ToJson(new GuestLoginRequest { deviceId = deviceId }));
            NetworkRequestManager.Instance.Send<GuestLoginResponse>(request, response =>
            {
                if (this == null || PlayerPrefs.GetString(PREFS_DEVICE_ID) != deviceId) return;
                AuthToken = response.token;
                Username = response.username;
                OnLoginComplete?.Invoke(true);
            }, new NetworkRequestOptions
            {
                Name = "GuestLogin", DuplicateKey = "GuestLogin:" + deviceId,
                DuplicatePolicy = DuplicateRequestPolicy.Reject,
                // 로그인 화면 자체의 상태 안내와 시작 버튼으로 진행 상태를 관리합니다.
                ShowLoading = false
            }, onFailure: error =>
            {
                if (this != null && PlayerPrefs.GetString(PREFS_DEVICE_ID) == deviceId) OnLoginComplete?.Invoke(false);
            },
                validateResponse: response => !string.IsNullOrEmpty(response.token));
        }

        [Serializable]
        private class GuestLoginRequest { public string deviceId; }

        [Serializable]
        private class GuestLoginResponse
        {
            public string token;
            public string username;
            public bool isNewUser;
        }
    }
}
