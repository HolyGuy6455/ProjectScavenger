using UnityEngine;
using Unity.Netcode;
using Unity.Collections;
using System.Collections.Generic;

public class OffScreenAllySpawner : NetworkBehaviour
{
    [Header("References")]
    [Tooltip("생성할 OffScreenAllyTag 프리팹")]
    [SerializeField] private GameObject offScreenAllyPrefab;
    
    // 접속 승인 시 임시로 닉네임을 저장하는 딕셔너리
    public static Dictionary<ulong, string> PendingClientNames = new Dictionary<ulong, string>();

    // 모두에게 동기화될 플레이어 이름
    public NetworkVariable<FixedString32Bytes> NetPlayerName = new NetworkVariable<FixedString32Bytes>(
        "", 
        NetworkVariableReadPermission.Everyone, 
        NetworkVariableWritePermission.Server
    );

    private GameObject spawnedUI;
    private OffScreenAllyUI allyUI;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (IsServer)
        {
            // 서버 스폰 시 저장해둔 이름을 NetworkVariable에 주입
            if (PendingClientNames.TryGetValue(OwnerClientId, out var pName))
            {
                NetPlayerName.Value = pName;
                PendingClientNames.Remove(OwnerClientId);
            }
        }

        // 내 캐릭터가 아닐 때(다른 플레이어일 때)만 아군 태그 UI를 생성합니다.
        if (!IsOwner && offScreenAllyPrefab != null)
        {
            spawnedUI = Instantiate(offScreenAllyPrefab);
            allyUI = spawnedUI.GetComponentInChildren<OffScreenAllyUI>();
            
            if (allyUI != null)
            {
                // 타겟을 이 스포너가 붙어있는 캐릭터로 지정하고, 동기화된 이름을 세팅합니다.
                allyUI.Setup(this.transform, NetPlayerName.Value.ToString());
            }
            else
            {
                Debug.LogWarning($"[OffScreenAllySpawner] {offScreenAllyPrefab.name} 프리팹에서 OffScreenAllyUI 컴포넌트를 찾을 수 없습니다.");
            }
        }

        // 만약 런타임에 이름이 변경될 경우를 대비해 이벤트 구독
        NetPlayerName.OnValueChanged += OnNameChanged;
    }

    private void OnNameChanged(FixedString32Bytes previousValue, FixedString32Bytes newValue)
    {
        if (allyUI != null)
        {
            allyUI.Setup(this.transform, newValue.ToString());
        }
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();

        NetPlayerName.OnValueChanged -= OnNameChanged;

        // 캐릭터가 디스폰(접속 종료, 씬 전환 등)될 때 생성했던 UI도 함께 제거합니다.
        if (spawnedUI != null)
        {
            Destroy(spawnedUI);
        }
    }
}
