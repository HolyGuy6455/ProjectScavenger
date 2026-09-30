using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(Health))]
public class EnemyController : NetworkBehaviour, ILootTableProvider
{
    [SerializeField] 
    [Tooltip("수동 배치 시 EnemyData의 고유 ID를 지정합니다. 0이면 게임오브젝트 이름(프리팹 이름)으로 자동 탐색합니다.")]
    private int enemyId = 0;

    private EnemyData myData;

    public void SetEnemyId(int id)
    {
        enemyId = id;
    }

    /// <summary>
    /// MobSpawner 등에 의해 스폰될 때 호출되어 데이터를 주입받습니다.
    /// </summary>
    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        // 자신의 프리팹 이름을 기반으로 DataManager에서 데이터를 검색합니다.
        // 스포너에서 고유 ID를 직접 주입받았다면, O(1) 해시 검색으로 즉시 데이터를 가져옵니다.
        if (enemyId != 0)
        {
            myData = DataManager.Instance.GetData<EnemyData>("Enemy", enemyId);
        }
        else
        {
            // 수동 배치 등 ID가 없다면 기존처럼 오브젝트 이름으로 Fallback 탐색을 수행합니다.
            string cleanName = gameObject.name.Replace("(Clone)", "").Trim().Split(' ')[0];
            var allEnemies = DataManager.Instance.GetAllData<EnemyData>("Enemy");
            foreach (var enemy in allEnemies)
            {
                if (enemy.prefabName == cleanName)
                {
                    myData = enemy;
                    break;
                }
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
            Debug.LogWarning($"[EnemyController] 매칭되는 EnemyData를 찾을 수 없습니다. (검색된 ID: {enemyId}, GameObject: {gameObject.name})");
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
