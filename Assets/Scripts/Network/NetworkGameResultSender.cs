using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
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
            StartCoroutine(SendResultRoutine(state));
        }

        public event System.Action<GameResultResponse> OnGameResultReceived;

        private IEnumerator SendResultRoutine(GameResultState state)
        {
            if (NetworkAuthManager.Instance == null || string.IsNullOrEmpty(NetworkAuthManager.Instance.AuthToken))
            {
                Debug.LogWarning("[NetworkGameResultSender] 서버 인증 토큰이 없습니다. 결과를 송신하지 않습니다.");
                yield break;
            }

            var requestDto = new GameResultRequest
            {
                ResultStatus = state.ToString(),
                StageId = 1, // TODO: Replace with actual StageId when available
                CollectedItems = new List<GameItemDto>(),
                KilledMonsters = new List<KilledMonsterDto>() // TODO: Replace with actual killed monsters list
            };

            // 인벤토리 아이템 취합
            if (PlayerInventory.Instance != null && PlayerInventory.Instance.Items != null)
            {
                foreach (var item in PlayerInventory.Instance.Items)
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

            using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
            {
                byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("Authorization", $"Bearer {NetworkAuthManager.Instance.AuthToken}");

                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.ConnectionError || request.result == UnityWebRequest.Result.ProtocolError)
                {
                    Debug.LogError($"[NetworkGameResultSender] 결과 송신 실패: {request.error} / {request.downloadHandler.text}");
                }
                else
                {
                    Debug.Log($"[NetworkGameResultSender] 결과 송신 성공: {request.downloadHandler.text}");
                    try
                    {
                        var response = JsonUtility.FromJson<GameResultResponse>(request.downloadHandler.text);
                        if (response != null)
                        {
                            Debug.Log($"[NetworkGameResultSender] 플레이어 경험치 변화: {response.UserPrevExp} -> {response.UserCurrentExp}, 레벨: {response.UserPrevLevel} -> {response.UserCurrentLevel}");
                            if (response.CharacterExpData != null)
                            {
                                foreach (var charExp in response.CharacterExpData)
                                {
                                    Debug.Log($"[NetworkGameResultSender] 캐릭터 {charExp.CharId} 경험치 변화: {charExp.PrevExp} -> {charExp.CurrentExp}, 레벨: {charExp.PrevLevel} -> {charExp.CurrentLevel}");
                                }
                            }
                            OnGameResultReceived?.Invoke(response);
                        }
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogError($"[NetworkGameResultSender] 응답 파싱 오류: {e.Message}");
                    }
                }
            }
        }
    }
}
