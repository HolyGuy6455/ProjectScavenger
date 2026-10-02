using UnityEngine;
using System.Collections.Generic;
using Unity.Netcode;

public class ItemInstanceManager : NetworkBehaviour
{
    public static ItemInstanceManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
    }

    [Header("Common Field Item Prefab")]
    public GameObject fieldItemPrefab;

    /// <summary>
    /// 지정한 개수(count)만큼 아이템 인스턴스를 생성하여 반환합니다.
    /// 생성된 인스턴스들의 수량(Quantity) 설정은 호출부에서 직접 제어합니다.
    /// </summary>
    public List<InventoryItem> CreateItemInstances(int itemId, int count)
    {
        List<InventoryItem> result = new List<InventoryItem>();
        ItemData data = DataManager.Instance.GetData<ItemData>("Item", itemId);
        
        if (data == null)
        {
            Debug.LogWarning($"[ItemInstanceManager] Item '{itemId}' not found.");
            return result;
        }

        for (int i = 0; i < count; i++)
        {
            // 수량을 0으로 초기화하거나, 기본 1로 둘지 여부는 호출부에서 재설정합니다.
            // 여기서는 기본적으로 0으로 생성하여 호출부가 정확히 분배하도록 유도합니다.
            result.Add(new InventoryItem(data, 0));
        }

        return result;
    }

    /// <summary>
    /// 총 수량(totalQuantity)을 넣으면 최대 중첩 개수(maxQuantity)를 고려해 
    /// 자동으로 분할된 아이템 인스턴스 리스트를 생성하여 반환합니다.
    /// </summary>
    public List<InventoryItem> CreateItemInstancesByTotalQuantity(int itemId, int totalQuantity)
    {
        List<InventoryItem> result = new List<InventoryItem>();
        ItemData data = DataManager.Instance.GetData<ItemData>("Item", itemId);
        
        if (data == null)
        {
            Debug.LogWarning($"[ItemInstanceManager] Item '{itemId}' not found.");
            return result;
        }

        int remaining = totalQuantity;
        while (remaining > 0)
        {
            // 데이터 변환이 누락되어 maxQuantity가 0일 경우 무한루프 방지
            int safeMaxQuantity = data.maxQuantity > 0 ? data.maxQuantity : 1;
            int quantityToCreate = Mathf.Min(remaining, safeMaxQuantity);
            
            result.Add(new InventoryItem(data, quantityToCreate));
            remaining -= quantityToCreate;
        }

        return result;
    }

    /// <summary>
    /// 맵(월드) 상에 공용 FieldItem 프리팹을 생성합니다. 
    /// 클라이언트에서 호출 시 자동으로 서버에 생성을 요청(ServerRpc)합니다.
    /// 주의: 클라이언트가 호출했을 경우 반환값은 즉시 획득할 수 없으므로 null을 반환합니다.
    /// </summary>
    public GameObject SpawnFieldItem(int itemId, int quantity, Vector3 position, Vector3 throwVelocity = default, ulong parentNetworkId = 0)
    {
        if (fieldItemPrefab == null)
        {
            Debug.LogWarning("[ItemInstanceManager] 공용 fieldItemPrefab이 할당되지 않았습니다.");
            return null;
        }

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
        {
            // 서버(호스트)라면 즉시 스폰
            return SpawnFieldItemInternal(itemId, quantity, position, throwVelocity, parentNetworkId);
        }
        else if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsClient)
        {
            // 클라이언트라면 서버에 요청
            RequestSpawnFieldItemServerRpc(itemId, quantity, position, throwVelocity, parentNetworkId);
            return null;
        }

        return null;
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestSpawnFieldItemServerRpc(int itemId, int quantity, Vector3 position, Vector3 throwVelocity, ulong parentNetworkId)
    {
        SpawnFieldItemInternal(itemId, quantity, position, throwVelocity, parentNetworkId);
    }

    private GameObject SpawnFieldItemInternal(int itemId, int quantity, Vector3 position, Vector3 throwVelocity, ulong parentNetworkId)
    {
        GameObject spawned = Instantiate(fieldItemPrefab, position, Quaternion.identity);
        
        NetworkObject netObj = spawned.GetComponent<NetworkObject>();
        if (netObj != null)
        {
            // 스폰 전에 부모를 미리 지정합니다.
            if (parentNetworkId != 0 && NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(parentNetworkId, out var parentNetObj))
            {
                spawned.transform.SetParent(parentNetObj.transform, true);
            }
            else
            {
                // 별도의 부모가 없다면, 하이라키 정리를 위해 자신(ItemInstanceManager)을 부모로 설정합니다.
                spawned.transform.SetParent(this.transform, true);
            }

            netObj.Spawn();
        }

        FieldItem fieldItem = spawned.GetComponent<FieldItem>();
        if (fieldItem != null)
        {
            fieldItem.Setup(itemId, quantity);
        }
        else
        {
            Debug.LogWarning($"[ItemInstanceManager] 스폰된 프리팹 '{fieldItemPrefab.name}'에 FieldItem 컴포넌트가 없습니다.");
        }
        
        if (throwVelocity != default)
        {
            Rigidbody rb = spawned.GetComponent<Rigidbody>();
            if (rb != null) rb.AddForce(throwVelocity, ForceMode.Impulse);
        }
        
        return spawned;
    }
}
