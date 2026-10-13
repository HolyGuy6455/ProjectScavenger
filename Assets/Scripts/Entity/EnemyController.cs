using System.Collections;
using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(Health))]
public class EnemyController : NetworkBehaviour, ILootTableProvider
{
    [SerializeField]
    [Tooltip("수동 배치 시 EnemyData의 고유 ID를 지정합니다. 0이면 게임오브젝트 이름(프리팹 이름)으로 자동 탐색합니다.")]
    private int enemyId = 0;

    private EnemyData myData;
    private Coroutine dataInitialization;

    public void SetEnemyId(int id)
    {
        enemyId = id;
    }

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        if (DataManager.Instance != null && DataManager.Instance.IsDataLoaded)
        {
            InitializeEnemyData();
        }
        else
        {
            dataInitialization = StartCoroutine(WaitForEnemyData());
        }
    }

    public override void OnNetworkDespawn()
    {
        if (dataInitialization != null)
        {
            StopCoroutine(dataInitialization);
            dataInitialization = null;
        }
        myData = null;
    }

    private IEnumerator WaitForEnemyData()
    {
        while (DataManager.Instance == null || !DataManager.Instance.IsDataLoaded)
        {
            yield return null;
        }

        dataInitialization = null;
        if (IsSpawned && IsServer)
        {
            InitializeEnemyData();
        }
    }

    private void InitializeEnemyData()
    {
        myData = null;
        if (enemyId != 0)
        {
            myData = DataManager.Instance.GetData<EnemyData>("Enemy", enemyId);
        }
        else
        {
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

        if (myData == null)
        {
            Debug.LogWarning($"[EnemyController] 매칭되는 EnemyData를 찾을 수 없습니다. (검색된 ID: {enemyId}, GameObject: {gameObject.name})");
            return;
        }

        GetComponent<Health>().SetHealth(myData.hp, myData.hp);
        InitializeWeapon();
    }

    private void InitializeWeapon()
    {
        if (myData.ammoId <= 0) return;

        var shooter = GetComponent<Shooter>();
        if (shooter == null)
        {
            Debug.LogWarning($"[EnemyController] ammoId가 지정된 적에 Shooter가 없습니다. (Enemy: {myData.id}, Ammo: {myData.ammoId})", this);
            return;
        }

        var ammo = DataManager.Instance.GetData<AmmoData>("Ammo", myData.ammoId);
        if (ammo == null)
        {
            Debug.LogWarning($"[EnemyController] 적의 AmmoData를 찾을 수 없습니다. (Enemy: {myData.id}, Ammo: {myData.ammoId})", this);
            return;
        }

        shooter.EquipGun(ammo);
    }

    public int GetLootTableId()
    {
        return myData != null ? myData.lootTable : 0;
    }
}
