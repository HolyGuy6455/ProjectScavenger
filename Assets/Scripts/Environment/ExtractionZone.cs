using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Scavenger.Environment
{
    public class ExtractionZone : NetworkBehaviour
    {
        [Header("Extraction Settings")]
        public float extractionTime = 5.0f;
        
        // 서버에서만 관리: 현재 진입한 플레이어 목록
        private HashSet<ulong> playersInZone = new HashSet<ulong>();
        
        // 모든 클라이언트가 공유하는 탈출 타이머 및 진행 상태
        public NetworkVariable<float> currentExtractionTimer = new NetworkVariable<float>(0f);
        public NetworkVariable<bool> isExtracting = new NetworkVariable<bool>(false);

        private void OnTriggerEnter(Collider other)
        {
            if (IsOnlineAndNotServer()) return;

            var networkObject = other.GetComponentInParent<NetworkObject>();
            
            // 오프라인이거나, 온라인일 경우 PlayerObject인지 확인
            bool isPlayer = networkObject != null ? networkObject.IsPlayerObject : other.CompareTag("Player");

            if (isPlayer)
            {
                ulong clientId = networkObject != null ? networkObject.OwnerClientId : 0;
                playersInZone.Add(clientId);
                CheckExtractionState();
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (IsOnlineAndNotServer()) return;

            var networkObject = other.GetComponentInParent<NetworkObject>();
            bool isPlayer = networkObject != null ? networkObject.IsPlayerObject : other.CompareTag("Player");

            if (isPlayer)
            {
                ulong clientId = networkObject != null ? networkObject.OwnerClientId : 0;
                playersInZone.Remove(clientId);
                CheckExtractionState();
            }
        }

        private bool IsOnlineAndNotServer()
        {
            // 네트워크 매니저가 없거나 실행 중이 아니면 오프라인
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening) return false;
            
            // 네트워크는 실행 중이지만 이 오브젝트가 스폰되지 않았다면 오프라인(로컬)처럼 동작 허용
            if (!IsSpawned) return false;

            // 스폰된 온라인 오브젝트라면 서버(호스트)가 아닐 때 차단
            return !IsServer;
        }

        private void CheckExtractionState()
        {
            bool isOffline = NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening || !IsSpawned;
            
            // 요구 플레이어 수를 생존자 기준으로 가져옵니다. 0명 이하일 경우 1로 고정하여 예외 처리
            int requiredPlayers = 1;
            if (Scavenger.Manager.GameFlowManager.Instance != null)
            {
                requiredPlayers = Mathf.Max(1, Scavenger.Manager.GameFlowManager.Instance.GetAlivePlayersCount());
            }
            
            if (playersInZone.Count > 0 && playersInZone.Count >= requiredPlayers)
            {
                if (!isExtracting.Value && isOffline) 
                {
                    Debug.Log("[ExtractionZone] 플레이어 진입. 탈출 카운트다운을 시작합니다. (오프라인 모드)");
                }
                else if (!isExtracting.Value)
                {
                    Debug.Log("[ExtractionZone] 모든 플레이어가 모였습니다. 탈출 카운트다운을 시작합니다.");
                }
                
                // 오프라인일 때는 NetworkVariable 갱신 에러 방지를 위해 변수 직접 변경(에러 무시되거나 예외발생하므로 안전처리)
                if (IsSpawned || !isOffline)
                {
                    isExtracting.Value = true;
                }
                else
                {
                    if (!isExtractingLocal)
                    {
                        isExtractingLocal = true;
                        if (Scavenger.Manager.GameFlowManager.Instance != null)
                            Scavenger.Manager.GameFlowManager.Instance.NotifyExtractionState(this, true);
                    }
                }
            }
            else
            {
                if (isExtracting.Value || isExtractingLocal)
                {
                    Debug.Log("[ExtractionZone] 플레이어가 구역을 이탈하여 탈출이 취소되었습니다.");
                }
                
                if (IsSpawned || !isOffline)
                {
                    isExtracting.Value = false;
                    currentExtractionTimer.Value = 0f;
                }
                else
                {
                    if (isExtractingLocal)
                    {
                        isExtractingLocal = false;
                        if (Scavenger.Manager.GameFlowManager.Instance != null)
                            Scavenger.Manager.GameFlowManager.Instance.NotifyExtractionState(this, false);
                    }
                    localExtractionTimer = 0f;
                }
            }
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            isExtracting.OnValueChanged += HandleExtractionStateChanged;
            if (isExtracting.Value && Scavenger.Manager.GameFlowManager.Instance != null)
            {
                Scavenger.Manager.GameFlowManager.Instance.NotifyExtractionState(this, true);
            }
            
            if (Scavenger.Manager.GameFlowManager.Instance != null)
            {
                Scavenger.Manager.GameFlowManager.Instance.OnAlivePlayersChanged += HandleAlivePlayersChanged;
            }
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();
            isExtracting.OnValueChanged -= HandleExtractionStateChanged;
            if (Scavenger.Manager.GameFlowManager.Instance != null)
            {
                Scavenger.Manager.GameFlowManager.Instance.NotifyExtractionState(this, false);
                Scavenger.Manager.GameFlowManager.Instance.OnAlivePlayersChanged -= HandleAlivePlayersChanged;
            }
        }

        private void HandleAlivePlayersChanged(int aliveCount)
        {
            // 누군가 죽거나 나가서 생존자 수가 변하면 탈출 조건을 다시 평가합니다.
            if (IsServer || (!IsSpawned && (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)))
            {
                CheckExtractionState();
            }
        }

        private void HandleExtractionStateChanged(bool previousValue, bool newValue)
        {
            if (Scavenger.Manager.GameFlowManager.Instance != null)
            {
                Scavenger.Manager.GameFlowManager.Instance.NotifyExtractionState(this, newValue);
            }
        }

        private bool isExtractingLocal = false;
        private float localExtractionTimer = 0f;

        public bool IsExtracting => (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && IsSpawned) ? isExtracting.Value : isExtractingLocal;
        public float CurrentExtractionTimer => (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && IsSpawned) ? currentExtractionTimer.Value : localExtractionTimer;

        private void Update()
        {
            bool isOffline = NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening || !IsSpawned;
            if (!isOffline && !IsServer) return;

            if (isOffline)
            {
                if (isExtractingLocal)
                {
                    localExtractionTimer += Time.deltaTime;
                    if (localExtractionTimer >= extractionTime)
                    {
                        isExtractingLocal = false;
                        DoExtract();
                    }
                }
            }
            else
            {
                if (isExtracting.Value)
                {
                    currentExtractionTimer.Value += Time.deltaTime;
                    if (currentExtractionTimer.Value >= extractionTime)
                    {
                        isExtracting.Value = false;
                        ExtractAllPlayersClientRpc();
                    }
                }
            }
        }

        [ClientRpc]
        private void ExtractAllPlayersClientRpc()
        {
            DoExtract();
        }

        private void DoExtract()
        {
            Debug.Log("[ExtractionZone] 탈출 성공!");
            if (Scavenger.Manager.GameFlowManager.Instance != null)
            {
                Scavenger.Manager.GameFlowManager.Instance.OnGameCleared();
            }
        }
    }
}
