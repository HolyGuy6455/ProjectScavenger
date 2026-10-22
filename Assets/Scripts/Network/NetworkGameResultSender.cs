using System.Collections.Generic;
using UnityEngine;
using Scavenger.Network.DTOs;
using Scavenger.Manager;

namespace Scavenger.Network
{
    public class NetworkGameResultSender : MonoBehaviour
    {
        public static NetworkGameResultSender Instance { get; private set; }

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        private void Start()
        {
            if (GameFlowManager.Instance != null)
            {
                GameFlowManager.Instance.OnGameEnded += SendGameResult;
            }
        }

        private void OnDestroy()
        {
            if (GameFlowManager.Instance != null)
            {
                GameFlowManager.Instance.OnGameEnded -= SendGameResult;
            }
        }

        public void SendGameResult(GameResultState state)
        {
            SendResult(state);
        }

        public event System.Action<GameResultResponse> OnGameResultReceived;

        private void SendResult(GameResultState state)
        {
            if (NetworkAuthManager.Instance == null || string.IsNullOrEmpty(NetworkAuthManager.Instance.AuthToken))
            {
                Debug.LogError("[NetworkGameResultSender] 서버 인증 토큰이 없습니다. 결과를 송신하지 않습니다.", this);
                return;
            }

            var requestDto = new GameResultRequest
            {
                ResultStatus = state.ToString(),
                StageId = 1, // TODO: Replace with actual StageId when available
                CollectedItems = new List<GameItemDto>(),
                KilledMonsters = new List<KilledMonsterDto>() // TODO: Replace with actual killed monsters list
            };

            // 모든 캐릭터의 인벤토리 아이템 취합
            if (PlayerInventory.Instance != null)
            {
                foreach (var item in PlayerInventory.Instance.GetAllItems())
                {
                    if (item != null && !item.IsEmpty)
                    {
                        requestDto.CollectedItems.Add(new GameItemDto
                        {
                            ItemId = item.Data.id,
                            Amount = item.Quantity
                        });
                    }
                }
            }

            string json = JsonUtility.ToJson(requestDto);
            string url = $"{NetworkAuthManager.Instance.ServerUrl}/api/game/result";

            Debug.Log($"[NetworkGameResultSender] 게임 결과 전송 중... ({state}) / Items: {requestDto.CollectedItems.Count}");

            var auth = NetworkAuthManager.Instance;
            string token = auth.AuthToken;
            var request = NetworkRequestManager.CreateJsonRequest(url, "POST", json, token);
            NetworkRequestManager.Instance.Send<GameResultResponse>(request, response =>
            {
                if (auth == null || auth.AuthToken != token) return;
                var userData = UserDataManager.Instance;
                if (userData != null)
                {
                    if (response.CharacterExpData != null)
                        foreach (var exp in response.CharacterExpData)
                        {
                            var character = userData.GetCharacter(exp.CharId);
                            if (character == null) continue;
                            character.level = exp.CurrentLevel;
                            character.exp = exp.CurrentExp;
                        }
                    userData.FetchItemList(false, false);
                }
                if (this != null && isActiveAndEnabled) OnGameResultReceived?.Invoke(response);
            }, new NetworkRequestOptions
            {
                Name = "GameResult", DuplicateKey = auth.Username + ":GameResult", DuplicatePolicy = DuplicateRequestPolicy.Reject
            }, validateResponse: response => response.CharacterExpData != null);
        }
    }
}
