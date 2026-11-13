using System;
using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;
using Scavenger.Manager;
using Scavenger.Network.DTOs;

public struct KilledMonsterNetEntry : INetworkSerializable, IEquatable<KilledMonsterNetEntry>
{
    public int MonsterId;
    public int Amount;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref MonsterId);
        serializer.SerializeValue(ref Amount);
    }

    public bool Equals(KilledMonsterNetEntry other)
    {
        return MonsterId == other.MonsterId && Amount == other.Amount;
    }
}

public class MobManager : NetworkBehaviour
{
    public static MobManager Instance { get; private set; }

    private NetworkObject _mobContainerNetObj;
    private readonly NetworkList<KilledMonsterNetEntry> _syncedKilledMonsters = new NetworkList<KilledMonsterNetEntry>();
    private static readonly Dictionary<int, int> _fallbackKilledMonsters = new Dictionary<int, int>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        Instance = null;
        _fallbackKilledMonsters.Clear();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
        _fallbackKilledMonsters.Clear();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        _fallbackKilledMonsters.Clear();
        if (IsServer)
        {
            _syncedKilledMonsters.Clear();
        }
    }

    public override void OnNetworkDespawn()
    {
        _fallbackKilledMonsters.Clear();
        for (int i = 0; i < _syncedKilledMonsters.Count; i++)
        {
            var entry = _syncedKilledMonsters[i];
            if (entry.MonsterId > 0 && entry.Amount > 0)
            {
                _fallbackKilledMonsters[entry.MonsterId] = entry.Amount;
            }
        }
        base.OnNetworkDespawn();
    }

    public override void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
        base.OnDestroy();
    }

    /// <summary>
    /// 세션 내 적 처치 기록을 추가합니다. (온라인에서는 서버가 NetworkList로 파티원 전원에게 동기화)
    /// </summary>
    public static void RecordKill(int enemyId)
    {
        if (enemyId <= 0) return;

        if (GameFlowManager.Instance != null && GameFlowManager.Instance.CurrentState != GameResultState.None)
        {
            return;
        }

        if (Instance != null && NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && Instance.IsSpawned)
        {
            if (!Instance.IsServer) return;
            Instance.AddSyncedKill(enemyId);
            return;
        }

        if (_fallbackKilledMonsters.TryGetValue(enemyId, out int count))
        {
            _fallbackKilledMonsters[enemyId] = count + 1;
        }
        else
        {
            _fallbackKilledMonsters[enemyId] = 1;
        }
    }

    private void AddSyncedKill(int enemyId)
    {
        for (int i = 0; i < _syncedKilledMonsters.Count; i++)
        {
            if (_syncedKilledMonsters[i].MonsterId == enemyId)
            {
                var updated = _syncedKilledMonsters[i];
                updated.Amount += 1;
                _syncedKilledMonsters[i] = updated;
                return;
            }
        }

        _syncedKilledMonsters.Add(new KilledMonsterNetEntry
        {
            MonsterId = enemyId,
            Amount = 1
        });
    }

    /// <summary>
    /// 현재 세션에서 누적된 몬스터 처치 목록을 DTO 리스트로 반환합니다.
    /// </summary>
    public static List<KilledMonsterDto> GetKilledMonsters()
    {
        var result = new List<KilledMonsterDto>();

        if (Instance != null && NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && Instance.IsSpawned)
        {
            for (int i = 0; i < Instance._syncedKilledMonsters.Count; i++)
            {
                var entry = Instance._syncedKilledMonsters[i];
                if (entry.MonsterId > 0 && entry.Amount > 0)
                {
                    result.Add(new KilledMonsterDto
                    {
                        MonsterId = entry.MonsterId,
                        Amount = entry.Amount
                    });
                }
            }
            return result;
        }

        foreach (var kvp in _fallbackKilledMonsters)
        {
            if (kvp.Key > 0 && kvp.Value > 0)
            {
                result.Add(new KilledMonsterDto
                {
                    MonsterId = kvp.Key,
                    Amount = kvp.Value
                });
            }
        }

        return result;
    }

    /// <summary>
    /// 조건에 맞는 몹 그룹을 결정하고 개별 소환을 진행합니다.
    /// </summary>
    public List<GameObject> SpawnMobGroup(MobCategory allowedCategories, int maxDangerLevel, float spawnRadius, Vector3 centerPosition)
    {
        var spawnedMobs = new List<GameObject>();

        // NGO 멀티플레이 환경: 몹 소환은 서버에서만 권한을 가짐
        if (NetworkManager.Singleton != null && !NetworkManager.Singleton.IsServer)
        {
            Debug.LogWarning("[MobManager] 몹 스폰은 서버에서만 실행될 수 있습니다.");
            return spawnedMobs;
        }

        if (DataManager.Instance == null)
        {
            Debug.LogError("[MobManager] 씬에 DataManager가 존재하지 않습니다!");
            return spawnedMobs;
        }

        // 1. 내 조건에 맞는 랜덤 몹 그룹 데이터 가져오기
        MobGroupData groupData = DataManager.Instance.GetRandomMatchingGroup(allowedCategories, maxDangerLevel, spawnRadius);
        
        if (groupData == null)
        {
            return spawnedMobs;
        }

        // 2. 그룹 안의 몬스터 데이터(SpawnInfos)를 순회하며 개별 소환
        foreach (var spawnInfo in groupData.SpawnInfos)
        {
            var spawnedMob = SpawnSingleMob(spawnInfo, centerPosition);
            if (spawnedMob != null) spawnedMobs.Add(spawnedMob);
        }
        
        Debug.Log($"[MobManager] 성공적으로 '{groupData.GroupID}' 그룹을 소환했습니다.");
        return spawnedMobs;
    }

    private GameObject SpawnSingleMob(MobSpawnInfo info, Vector3 centerPosition)
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
            return null;
        }

        // 2. 프리팹 로드 
        // TODO: 현재 임시로 Resources/Prefabs/ 폴더 경로를 사용합니다. 
        // 실제 프로젝트의 어드레서블/로딩 경로에 맞게 이 부분을 수정하세요!
        GameObject prefab = Resources.Load<GameObject>($"Prefabs/Enemy/{enemyData.prefabName}");
        if (prefab == null)
        {
            Debug.LogError($"[MobManager] 프리팹을 찾을 수 없습니다: Resources/Prefabs/Enemy/{enemyData.prefabName}");
            return null;
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
        return spawnedMob;
    }
}
