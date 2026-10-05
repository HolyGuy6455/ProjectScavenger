using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

public class MobManager : NetworkBehaviour
{
    public static MobManager Instance { get; private set; }

    private NetworkObject _mobContainerNetObj;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
    }

    /// <summary>
    /// 조건에 맞는 몹 그룹을 결정하고 개별 소환을 진행합니다.
    /// </summary>
    public void SpawnMobGroup(MobCategory allowedCategories, int maxDangerLevel, float spawnRadius, Vector3 centerPosition)
    {
        // NGO 멀티플레이 환경: 몹 소환은 서버에서만 권한을 가짐
        if (NetworkManager.Singleton != null && !NetworkManager.Singleton.IsServer)
        {
            Debug.LogWarning("[MobManager] 몹 스폰은 서버에서만 실행될 수 있습니다.");
            return;
        }

        if (DataManager.Instance == null)
        {
            Debug.LogError("[MobManager] 씬에 DataManager가 존재하지 않습니다!");
            return;
        }

        // 1. 내 조건에 맞는 랜덤 몹 그룹 데이터 가져오기
        MobGroupData groupData = DataManager.Instance.GetRandomMatchingGroup(allowedCategories, maxDangerLevel, spawnRadius);
        
        if (groupData == null)
        {
            return;
        }

        // 2. 그룹 안의 몬스터 데이터(SpawnInfos)를 순회하며 개별 소환
        foreach (var spawnInfo in groupData.SpawnInfos)
        {
            SpawnSingleMob(spawnInfo, centerPosition);
        }
        
        Debug.Log($"[MobManager] 성공적으로 '{groupData.GroupID}' 그룹을 소환했습니다.");
    }

    private void SpawnSingleMob(MobSpawnInfo info, Vector3 centerPosition)
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
            Debug.LogError($"[MobManager] '{info.MobID}' 이름(prefabName)을 가진 EnemyData를 찾을 수 없습니다.");
            return;
        }

        // 2. 프리팹 로드 
        // TODO: 현재 임시로 Resources/Prefabs/ 폴더 경로를 사용합니다. 
        // 실제 프로젝트의 어드레서블/로딩 경로에 맞게 이 부분을 수정하세요!
        GameObject prefab = Resources.Load<GameObject>($"Prefabs/Enemy/{enemyData.prefabName}");
        if (prefab == null)
        {
            Debug.LogError($"[MobManager] 프리팹을 찾을 수 없습니다: Resources/Prefabs/Enemy/{enemyData.prefabName}");
            return;
        }

        // 3. 인스턴스화 (중심 위치 + 에디터 툴에서 저장한 상대 위치/회전값 적용)
        Vector3 spawnPos = centerPosition + info.LocalPosition;
        Quaternion spawnRot = Quaternion.Euler(info.LocalEulerAngles);
        
        GameObject spawnedMob = Instantiate(prefab, spawnPos, spawnRot);
        
        // 이름 기반 탐색의 불안정성을 해결하기 위해, 스포너가 명시적으로 고유 ID를 주입합니다.
        EnemyController enemyController = spawnedMob.GetComponent<EnemyController>();
        if (enemyController != null)
        {
            enemyController.SetEnemyId(enemyData.id);
        }

        // 4. NGO 네트워크 스폰 호출 및 컨테이너 하위로 부모 지정
        NetworkObject netObj = spawnedMob.GetComponent<NetworkObject>();
        if (netObj != null)
        {
            netObj.Spawn(true); // true = destroyWithScene

            if (_mobContainerNetObj == null)
            {
                GameObject mobContainer = GameObject.Find("MobContainer");
                if (mobContainer != null)
                {
                    _mobContainerNetObj = mobContainer.GetComponent<NetworkObject>();
                }
            }

            if (_mobContainerNetObj != null && _mobContainerNetObj.IsSpawned)
            {
                netObj.TrySetParent(_mobContainerNetObj.transform, true);
            }
        }
        else
        {
            Debug.LogWarning($"[MobManager] {enemyData.prefabName} 프리팹에 NetworkObject 컴포넌트가 없습니다!");
        }
    }
}
