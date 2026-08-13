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

        if (lootData.item != null && lootData.chance != null)
        {
            for (int i = 0; i < lootData.item.Length; i++)
            {
                if (lootData.item[i] == 0) continue;

                // 확률 계산 (데이터가 비어있으면 100%로 간주)
                int chance = i < lootData.chance.Length ? lootData.chance[i] : 100;
                
                // 0 ~ 99 의 난수가 chance보다 작으면 스폰 (예: chance가 50이면 50% 확률)
                if (Random.Range(0, 100) < chance)
                {
                    SpawnItem(lootData.item[i]);
                }
            }
        }
    }

    private void SpawnItem(int itemId)
    {
        Vector3 spawnPos = transform.position + Vector3.up * 0.5f;
        GameObject itemObj = ItemInstanceManager.Instance.SpawnFieldItem(itemId, 1, spawnPos);
        
        if (itemObj != null)
        {
            NetworkObject netObj = itemObj.GetComponent<NetworkObject>();
            if (netObj != null)
            {
                netObj.Spawn();
            }

            Rigidbody rb = itemObj.GetComponent<Rigidbody>();
            if (rb != null)
            {
                Vector3 force = new Vector3(Random.Range(-1f, 1f), 2f, Random.Range(-1f, 1f)).normalized * Random.Range(2f, 5f);
                rb.AddForce(force, ForceMode.Impulse);
            }
        }
    }
}
