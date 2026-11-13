using System.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// HealthBarUI 프리팹을 런타임에 동적으로 생성하고 Health 컴포넌트와 연결해주는 스크립트입니다.
/// </summary>
public class HealthBarSpawner : MonoBehaviour
{
    [Header("UI Prefab")]
    [Tooltip("생성할 HealthBarUI가 포함된 프리팹입니다.")]
    [SerializeField] private GameObject healthBarPrefab;

    [Tooltip("생성된 UI가 소속될 부모 Transform입니다. 비워두면 씬 내의 WorldUIContainer를 찾아 소속시킵니다.")]
    [UnityEngine.Serialization.FormerlySerializedAs("canvasTransform")]
    [SerializeField] private Transform parentTransform;

    [Header("Target Settings")]
    [Tooltip("HealthBarUI에 연결할 Health 컴포넌트입니다. 비워두면 자신의 GameObject에서 찾습니다.")]
    [SerializeField] private Health targetHealth;
    
    [Tooltip("HealthBar가 추적할 3D 월드의 Transform입니다. 비워두면 자신의 Transform을 사용합니다.")]
    [SerializeField] private Transform targetTransform;

    [Tooltip("추적할 위치의 추가 오프셋입니다.")]
    [SerializeField] private Vector3 worldOffset = new Vector3(0f, 2f, 0f);

    [Header("Color Settings")]
    [Tooltip("HealthBar의 색상을 지정합니다. 체크하면 아래 색상으로 변경됩니다.")]
    [SerializeField] private bool useCustomColor = false;
    [SerializeField] private Color barColor = Color.red;

    [Header("Visibility Settings")]
    [Tooltip("체크하면 멀티플레이 원격 아군(내 소유가 아닌 네트워크 오브젝트)일 때만 머리 위 체력바를 생성합니다. 내 캐릭터(싱글/멀티)에서는 숨깁니다.")]
    [SerializeField] private bool onlyShowForRemotePlayer = false;

    private GameObject spawnedUI;

    private void Start()
    {
        if (Application.isBatchMode || Scavenger.Network.DedicatedServerBootstrapper.IsDedicatedServer())
        {
            return;
        }

        var netManager = NetworkManager.Singleton;
        if (netManager != null && netManager.IsListening && !netManager.IsClient)
        {
            return;
        }

        if (onlyShowForRemotePlayer)
        {
            // 싱글플레이/오프라인 상태이거나 NetworkObject가 없으면 원격 팀원이 아니므로 숨김
            if (netManager == null || !netManager.IsListening)
            {
                return;
            }

            if (!TryGetComponent<NetworkObject>(out var netObj))
            {
                return;
            }

            if (!netObj.IsSpawned)
            {
                StartCoroutine(WaitForNetworkSpawnRoutine(netObj));
                return;
            }

            if (!netManager.IsClient || netObj.IsOwner)
            {
                return;
            }
        }

        SpawnHealthBar();
    }

    private IEnumerator WaitForNetworkSpawnRoutine(NetworkObject netObj)
    {
        while (netObj != null && !netObj.IsSpawned)
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsListening || !nm.IsClient)
            {
                yield break;
            }
            yield return null;
        }

        var netManager = NetworkManager.Singleton;
        if (netObj == null || !netObj.IsSpawned || netManager == null || !netManager.IsListening || !netManager.IsClient || netObj.IsOwner)
        {
            yield break;
        }

        SpawnHealthBar();
    }

    private void SpawnHealthBar()
    {
        if (spawnedUI != null)
        {
            return;
        }

        if (healthBarPrefab == null)
        {
            Debug.LogWarning($"[HealthBarSpawner] {gameObject.name}에 HealthBar 프리팹이 할당되지 않았습니다.");
            return;
        }

        if (targetHealth == null)
        {
            targetHealth = GetComponent<Health>();
            if (targetHealth == null)
            {
                Debug.LogWarning($"[HealthBarSpawner] {gameObject.name}에서 Health 컴포넌트를 찾을 수 없습니다.");
                return;
            }
        }

        if (targetTransform == null)
        {
            targetTransform = transform;
        }

        // 부모(WorldUIContainer) 찾기
        if (parentTransform == null)
        {
            if (WorldUIContainer.Instance != null)
            {
                parentTransform = WorldUIContainer.Instance;
            }
            else
            {
                Debug.LogWarning("[HealthBarSpawner] 씬에 WorldUIContainer가 존재하지 않아 부모 없이 생성합니다.");
            }
        }

        // UI 생성
        spawnedUI = parentTransform != null
            ? Instantiate(healthBarPrefab, parentTransform, false)
            : Instantiate(healthBarPrefab);

        // HealthBarUI 컴포넌트 찾아서 초기화
        HealthBarUI healthBarUI = spawnedUI.GetComponentInChildren<HealthBarUI>();
        if (healthBarUI != null)
        {
            healthBarUI.Setup(targetHealth, targetTransform, worldOffset);
            if (useCustomColor)
            {
                healthBarUI.SetColor(barColor);
            }
        }
        else
        {
            Debug.LogWarning("[HealthBarSpawner] 생성된 프리팹에서 HealthBarUI 컴포넌트를 찾을 수 없습니다.");
        }
    }

    private void OnDestroy()
    {
        if (spawnedUI != null)
        {
            Destroy(spawnedUI);
        }
    }
}
