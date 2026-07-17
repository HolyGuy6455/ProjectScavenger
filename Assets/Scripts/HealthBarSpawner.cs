using UnityEngine;

/// <summary>
/// HealthBarUI 프리팹을 런타임에 동적으로 생성하고 Health 컴포넌트와 연결해주는 스크립트입니다.
/// </summary>
public class HealthBarSpawner : MonoBehaviour
{
    [Header("UI Prefab")]
    [Tooltip("생성할 HealthBarUI가 포함된 프리팹입니다.")]
    [SerializeField] private GameObject healthBarPrefab;

    [Tooltip("생성된 UI가 소속될 Canvas의 Transform입니다. 비워두면 씬 내의 아무 Canvas나 찾아 소속시킵니다.")]
    [SerializeField] private Transform canvasTransform;

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

    private GameObject spawnedUI;

    private void Start()
    {
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

        // Canvas 찾기
        if (canvasTransform == null)
        {
            Canvas canvas = FindAnyObjectByType<Canvas>();
            if (canvas != null)
            {
                canvasTransform = canvas.transform;
            }
            else
            {
                Debug.LogWarning("[HealthBarSpawner] 씬에 Canvas가 존재하지 않아 부모 없이 생성합니다.");
            }
        }

        // UI 생성
        spawnedUI = Instantiate(healthBarPrefab, canvasTransform);

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
