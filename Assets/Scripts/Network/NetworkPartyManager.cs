using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;
using UnityEngine;

namespace Scavenger.Network
{
    [Serializable]
    public class UserInfo
    {
        public string connectionId;
        public string username;
        public string partyId;
        public bool isReady;
    }

    [System.Serializable]
    public class UserInfoListWrapper
    {
        public List<UserInfo> items;
    }

    public class NetworkPartyManager : MonoBehaviour
    {
        public static NetworkPartyManager Instance { get; private set; }

        private HubConnection _hubConnection;

        public List<UserInfo> OnlineUsers { get; private set; } = new List<UserInfo>();
        public List<UserInfo> PartyMembers { get; private set; } = new List<UserInfo>();

        public event Action OnOnlineUsersUpdated;
        public event Action OnPartyMembersUpdated;
        public event Action<string, int> OnGameStartBroadcasted;
        public event Action OnHostGameStartBroadcasted;

        private void Awake()
        {
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
            // 인증 매니저의 로그인이 완료되면 SignalR 파티 허브에 접속 시도
            NetworkAuthManager.Instance.OnLoginComplete += HandleLoginComplete;
        }

        private void OnDestroy()
        {
            if (NetworkAuthManager.Instance != null)
                NetworkAuthManager.Instance.OnLoginComplete -= HandleLoginComplete;

            if (_hubConnection != null)
            {
                _ = _hubConnection.StopAsync();
            }
        }

        private async void HandleLoginComplete(bool success)
        {
            Debug.Log($"[Party] 로그인 완료 이벤트 수신 (성공 여부: {success})");
            if (success)
            {
                await ConnectToPartyHub();
            }
        }

        private async Task ConnectToPartyHub()
        {
            string url = NetworkAuthManager.Instance.ServerUrl + "/partyHub";

            _hubConnection = new HubConnectionBuilder()
                .WithUrl(url, options =>
                {
                    options.AccessTokenProvider = () => Task.FromResult(NetworkAuthManager.Instance.AuthToken);
                })
                .Build();

            // 디버깅 및 안전한 파싱: 원시 데이터로 받아서 Unity JsonUtility로 직접 파싱
            _hubConnection.On<object>("UpdateOnlineUsers", (rawData) =>
            {
                UnityMainThreadDispatcher.Enqueue(() =>
                {
                    try
                    {
                        string rawString = rawData?.ToString();
                        // JsonUtility는 최상위 배열을 지원하지 않으므로 Wrapper 형태로 감싸서 파싱
                        string wrappedJson = $"{{\"items\":{rawString}}}";
                        var wrapper = JsonUtility.FromJson<UserInfoListWrapper>(wrappedJson);
                        
                        var users = wrapper?.items ?? new List<UserInfo>();

                        Debug.Log($"[Party] JsonUtility 파싱 완료! 총 {users.Count}명");
                        if (users.Count > 0)
                        {
                            Debug.Log($"[Party] 첫 번째 유저 정보 - Name: {users[0].username}, ConnID: {users[0].connectionId}");
                        }
                        
                        OnlineUsers = users;
                        OnOnlineUsersUpdated?.Invoke();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[Party] JSON 파싱 에러: {ex.Message}");
                    }
                });
            });

            // 서버에서 "UpdatePartyMembers" 브로드캐스트가 올 때 실행됨
            _hubConnection.On<object>("UpdatePartyMembers", (rawData) =>
            {
                UnityMainThreadDispatcher.Enqueue(() =>
                {
                    try
                    {
                        string rawString = rawData?.ToString();
                        string wrappedJson = $"{{\"items\":{rawString}}}";
                        var wrapper = JsonUtility.FromJson<UserInfoListWrapper>(wrappedJson);
                        PartyMembers = wrapper?.items ?? new List<UserInfo>();
                        OnPartyMembersUpdated?.Invoke();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[Party] 파티 멤버 파싱 에러: {ex.Message}");
                    }
                });
            });

            // 서버에서 "OnGameStart" 브로드캐스트가 올 때 실행됨
            _hubConnection.On<string, int>("OnGameStart", (ip, port) =>
            {
                UnityMainThreadDispatcher.Enqueue(() =>
                {
                    OnGameStartBroadcasted?.Invoke(ip, port);
                });
            });

            // 서버에서 "OnHostGameStart" 브로드캐스트가 올 때 실행됨
            _hubConnection.On("OnHostGameStart", () =>
            {
                UnityMainThreadDispatcher.Enqueue(() =>
                {
                    OnHostGameStartBroadcasted?.Invoke();
                });
            });

            try
            {
                Debug.Log($"[Party] 파티 허브({url})에 연결 시도 중...");
                await _hubConnection.StartAsync();
                Debug.Log("[Party] 파티 허브(서버)에 연결 성공!");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Party] 파티 허브 연결 실패!!! 상세 에러: {ex.ToString()}");
            }
        }

        // 친구(타겟)를 파티에 즉시 초대(가입) 시키는 함수
        public async void InviteToParty(string targetConnectionId)
        {
            if (_hubConnection != null && _hubConnection.State == HubConnectionState.Connected)
            {
                try
                {
                    await _hubConnection.InvokeAsync("InviteToParty", targetConnectionId);
                    Debug.Log($"[Party] 초대 요청 완료: {targetConnectionId}");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[Party] 초대 실패: {ex.Message}");
                }
            }
        }

        // 파티원이 레디 버튼을 누를 때 호출하는 함수
        public async void SendReadyState(bool isReady)
        {
            if (_hubConnection != null && _hubConnection.State == HubConnectionState.Connected)
            {
                try
                {
                    await _hubConnection.InvokeAsync("SetReady", isReady);
                    Debug.Log($"[Party] 레디 상태 전송 완료: {isReady}");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[Party] 레디 상태 전송 실패: {ex.Message}");
                }
            }
        }

        // 파티를 떠날 때 호출하는 함수
        public async void LeaveParty()
        {
            if (_hubConnection != null && _hubConnection.State == HubConnectionState.Connected)
            {
                try
                {
                    await _hubConnection.InvokeAsync("LeaveParty");
                    Debug.Log("[Party] 파티 떠나기 요청 완료");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[Party] 파티 떠나기 실패: {ex.Message}");
                }
            }
        }
    }
}
