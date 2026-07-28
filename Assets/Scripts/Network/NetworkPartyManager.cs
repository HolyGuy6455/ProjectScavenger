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
        public string connectionId { get; set; }
        public string username { get; set; }
        public string partyId { get; set; }
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
            if (success)
            {
                await ConnectToPartyHub();
            }
        }

        private async Task ConnectToPartyHub()
        {
            string url = "http://localhost:5000/partyHub";

            _hubConnection = new HubConnectionBuilder()
                .WithUrl(url, options =>
                {
                    options.AccessTokenProvider = () => Task.FromResult(NetworkAuthManager.Instance.AuthToken);
                })
                .Build();

            // 서버에서 "UpdateOnlineUsers" 브로드캐스트가 올 때 실행됨
            _hubConnection.On<List<UserInfo>>("UpdateOnlineUsers", (users) =>
            {
                // 유니티 메인 쓰레드에서 실행되도록 처리
                UnityMainThreadDispatcher.Enqueue(() =>
                {
                    OnlineUsers = users;
                    OnOnlineUsersUpdated?.Invoke();
                });
            });

            // 서버에서 "UpdatePartyMembers" 브로드캐스트가 올 때 실행됨
            _hubConnection.On<List<UserInfo>>("UpdatePartyMembers", (members) =>
            {
                UnityMainThreadDispatcher.Enqueue(() =>
                {
                    PartyMembers = members;
                    OnPartyMembersUpdated?.Invoke();
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

            try
            {
                await _hubConnection.StartAsync();
                Debug.Log("[Party] 파티 허브 접속 성공!");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Party] 파티 허브 접속 실패: {ex.Message}");
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

        // 파티원이 게임 시작을 누를 때 호출하는 함수
        public async void StartGame()
        {
            if (_hubConnection != null && _hubConnection.State == HubConnectionState.Connected)
            {
                try
                {
                    await _hubConnection.InvokeAsync("StartGame");
                    Debug.Log("[Party] 게임 시작 요청 완료");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[Party] 게임 시작 실패: {ex.Message}");
                }
            }
        }
    }
}
