using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

public class MobSpawner : MonoBehaviour
{
    [Header("Spawner Settings")]
    public MobCategory AllowedCategories = MobCategory.Animal | MobCategory.Robot;
    
    [Range(1, 10)]
    public int MaxDangerLevel = 3;
    
    public float SpawnRadius = 5f;

    [Header("Network Spawning Settings")]
    [Tooltip("If true, only the server will execute the spawn logic. Must be true for standard NGO spawning.")]
    public bool ServerOnlySpawn = true;

    private void OnDrawGizmos()
    {
        // Danger Level에 따라 기즈모 색상을 서서히 변경 (1=초록, 5=노랑, 10=빨강)
        float t = (MaxDangerLevel - 1) / 9f;
        Gizmos.color = Color.Lerp(new Color(0f, 1f, 0f, 0.5f), new Color(1f, 0f, 0f, 0.5f), t);
        
        // 스폰 가능 반경 표시
        Gizmos.DrawSphere(transform.position, SpawnRadius);
    }

    /// <summary>
    /// 외부 매니저(예: 웨이브 매니저, 트리거 영역)가 이 스포너를 작동시킬 때 호출하는 전용 함수입니다.
    /// </summary>
    public void SpawnMobs()
    {
        // NGO 멀티플레이 환경: 몹 소환은 서버에서만 권한을 가짐
        if (ServerOnlySpawn && NetworkManager.Singleton != null && !NetworkManager.Singleton.IsServer)
        {
            Debug.LogWarning("[MobSpawner] 몹 스폰은 서버에서만 실행될 수 있습니다.");
            return;
        }

        if (DataManager.Instance == null)
        {
            Debug.LogError("[MobSpawner] 씬에 DataManager가 존재하지 않습니다!");
            return;
        }

        // 1. 내 조건에 맞는 랜덤 몹 그룹 데이터 가져오기
        MobGroupData groupData = DataManager.Instance.GetRandomMatchingGroup(AllowedCategories, MaxDangerLevel, SpawnRadius);
        
        if (groupData == null)
        {
            return;
        }

        // 2. 그룹 안의 몬스터 데이터(SpawnInfos)를 순회하며 개별 소환
        foreach (var spawnInfo in groupData.SpawnInfos)
        {
            SpawnSingleMob(spawnInfo);
        }
        
        Debug.Log($"[MobSpawner] 성공적으로 '{groupData.GroupID}' 그룹을 소환했습니다.");
    }

    private void SpawnSingleMob(MobSpawnInfo info)
    {
        // 1. DataManager에서 EnemyData 가져오기 (문자열 prefabName으로 검색)
        var allEnemies = DataManager.Instance.GetAllData<EnemyData>("Enemy");
        EnemyData enemyData = null;
        
        foreach (var enemy in allEnemies)
        {
            if (enemy.prefabName == info.MobID)
            {
                enemyData = enemy;
                break;
            }
        }

        if (enemyData == null)
        {
            Debug.LogError($"[MobSpawner] '{info.MobID}' 이름(prefabName)을 가진 EnemyData를 찾을 수 없습니다.");
            return;
        }

        // 2. 프리팹 로드 
        // TODO: 현재 임시로 Resources/Prefabs/ 폴더 경로를 사용합니다. 
        // 실제 프로젝트의 어드레서블/로딩 경로에 맞게 이 부분을 수정하세요!
        GameObject prefab = Resources.Load<GameObject>($"Prefabs/Enemy/{enemyData.prefabName}");
        if (prefab == null)
        {
            Debug.LogError($"[MobSpawner] 프리팹을 찾을 수 없습니다: Resources/Prefabs/Enemy/{enemyData.prefabName}");
            return;
        }

        // 3. 인스턴스화 (스포너 위치 + 에디터 툴에서 저장한 상대 위치/회전값 적용)
        Vector3 spawnPos = transform.position + info.LocalPosition;
        Quaternion spawnRot = Quaternion.Euler(info.LocalEulerAngles);
        
        GameObject spawnedMob = Instantiate(prefab, spawnPos, spawnRot);

        // 4. NGO 네트워크 스폰 호출 (서버가 클라이언트들에게 동기화 지시)
        NetworkObject netObj = spawnedMob.GetComponent<NetworkObject>();
        if (netObj != null)
        {
            netObj.Spawn(true); // true = destroyWithScene
        }
        else
        {
            Debug.LogWarning($"[MobSpawner] {enemyData.prefabName} 프리팹에 NetworkObject 컴포넌트가 없습니다!");
        }
    }
}
