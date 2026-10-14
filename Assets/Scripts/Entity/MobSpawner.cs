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

    private readonly List<GameObject> _spawnedMobs = new List<GameObject>();
    public IReadOnlyList<GameObject> SpawnedMobs => _spawnedMobs;

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
        if (NetworkManager.Singleton != null && !NetworkManager.Singleton.IsServer)
        {
            Debug.LogWarning("[MobSpawner] 몹 스폰은 서버에서만 실행될 수 있습니다.");
            return;
        }

        if (MobManager.Instance == null)
        {
            Debug.LogError("[MobSpawner] 씬에 MobManager가 존재하지 않습니다!");
            return;
        }

        // 💡 실제 몬스터 스폰 로직은 중앙 집중화된 MobManager에게 위임합니다.
        _spawnedMobs.RemoveAll(mob => mob == null);
        _spawnedMobs.AddRange(MobManager.Instance.SpawnMobGroup(AllowedCategories, MaxDangerLevel, SpawnRadius, transform.position));
    }
}
