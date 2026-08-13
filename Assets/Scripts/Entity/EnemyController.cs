using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(Health))]
public class EnemyController : NetworkBehaviour, ILootTableProvider
{
    private EnemyData myData;

    /// <summary>
    /// MobSpawner 등에 의해 스폰될 때 호출되어 데이터를 주입받습니다.
    /// </summary>
    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        // 자신의 프리팹 이름을 기반으로 DataManager에서 데이터를 검색합니다.
        string cleanName = gameObject.name.Split(' ')[0];
        
        var allEnemies = DataManager.Instance.GetAllData<EnemyData>("Enemy");
        foreach (var enemy in allEnemies)
        {
            if (enemy.prefabName == cleanName)
            {
                myData = enemy;
                break;
            }
        }

        if (myData != null)
        {
            var health = GetComponent<Health>();
            if (health != null)
            {
                health.SetHealth(myData.hp, myData.hp);
            }
        }
        else
        {
            Debug.LogWarning($"[EnemyController] '{cleanName}'에 해당하는 EnemyData를 찾을 수 없습니다.");
        }
    }

    public int GetLootTableId()
    {
        if (myData != null)
        {
            return myData.lootTable;
        }
        return 0;
    }
}
