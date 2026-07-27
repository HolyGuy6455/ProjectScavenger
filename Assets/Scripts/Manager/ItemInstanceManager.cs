using UnityEngine;
using System.Collections.Generic;

public class ItemInstanceManager : MonoBehaviour
{
    public static ItemInstanceManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
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
    /// </summary>
    public GameObject SpawnFieldItem(int itemId, int quantity, Vector3 position)
    {
        if (fieldItemPrefab == null)
        {
            Debug.LogWarning("[ItemInstanceManager] 공용 fieldItemPrefab이 할당되지 않았습니다.");
            return null;
        }

        GameObject spawned = Instantiate(fieldItemPrefab, position, Quaternion.identity);
        FieldItem fieldItem = spawned.GetComponent<FieldItem>();
        if (fieldItem != null)
        {
            fieldItem.Setup(itemId, quantity);
        }
        else
        {
            Debug.LogWarning($"[ItemInstanceManager] 스폰된 프리팹 '{fieldItemPrefab.name}'에 FieldItem 컴포넌트가 없습니다.");
        }
        
        return spawned;
    }
}
