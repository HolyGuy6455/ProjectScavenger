using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(Health))]
public class ObstacleController : NetworkBehaviour, ILootTableProvider
{
    private ObstacleData myData;

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        // 자신의 프리팹 이름을 기반으로 DataManager에서 데이터를 검색합니다.
        // 유니티 인스턴스화 시 붙는 " (Clone)" 등을 제거합니다.
        string cleanName = gameObject.name.Split(' ')[0];
        
        var allObstacles = DataManager.Instance.GetAllData<ObstacleData>("Obstacle");
        foreach (var obs in allObstacles)
        {
            if (obs.prefabName == cleanName)
            {
                myData = obs;
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
            Debug.LogWarning($"[ObstacleController] '{cleanName}'에 해당하는 ObstacleData를 찾을 수 없습니다.");
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
