using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Health의 OnDeath 이벤트를 수신하여, 지정된 LootTable(LootData)에 맞춰 아이템을 스폰하는 컴포넌트입니다.
/// </summary>
[RequireComponent(typeof(Health))]
public class LootDropper : NetworkBehaviour
{
    private ILootTableProvider lootProvider;

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            var health = GetComponent<Health>();
            health.OnDeath -= HandleDeath;
            health.OnDeath += HandleDeath;

            lootProvider = GetComponent<ILootTableProvider>();
        }
    }

    private void HandleDeath(DamageInfo info)
    {
        if (!IsServer) return;

        int tableId = lootProvider != null ? lootProvider.GetLootTableId() : 0;
        if (tableId <= 0) return;

        LootData lootData = DataManager.Instance.GetData<LootData>("Loot", tableId);
        if (lootData == null) return;

        if (lootData.itemRef != null && lootData.chance != null)
        {
            for (int i = 0; i < lootData.itemRef.Length; i++)
            {
                string refValue = lootData.itemRef[i];
                if (string.IsNullOrEmpty(refValue) || refValue == "0") continue;

                // 확률 배열이 짧으면 100%(1000)로 간주합니다.
                int chance = i < lootData.chance.Length ? lootData.chance[i] : 1000;
                
                // 0 ~ 999의 난수와 비교합니다. (예: 500 = 50%, 1000 = 100%)
                if (Random.Range(0, 1000) < chance)
                {
                    if (int.TryParse(refValue, out int itemId))
                    {
                        SpawnItem(itemId);
                    }
                    else
                    {
                        SpawnItemByTag(refValue);
                    }
                }
            }
        }
    }

    private void SpawnItemByTag(string tag)
    {
        var tagItems = DataManager.Instance.GetItemsByTag(tag);
        if (tagItems.Count > 0)
        {
            int randomItemId = tagItems[Random.Range(0, tagItems.Count)].id;
            SpawnItem(randomItemId);
        }
        else
        {
            Debug.LogWarning($"[LootDropper] 기획 데이터 오류: '{tag}' 태그를 가진 아이템을 찾을 수 없습니다.");
        }
    }

    private void SpawnItem(int itemId)
    {
        Vector3 spawnPos = transform.position + Vector3.up * 0.5f;
        Vector3 force = new Vector3(Random.Range(-1f, 1f), 2f, Random.Range(-1f, 1f)).normalized * Random.Range(2f, 5f);
        
        ItemInstanceManager.Instance.SpawnFieldItem(itemId, 1, spawnPos, force);
    }
}
