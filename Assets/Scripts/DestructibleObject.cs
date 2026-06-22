using UnityEngine;

/// <summary>
/// 중립 설치물(드럼통, 상자 등)이나 피격 반응이 필요한 오브젝트에 부착하여 
/// Health 컴포넌트의 이벤트를 처리하는 컴포넌트입니다.
/// </summary>
[RequireComponent(typeof(Health))]
public class DestructibleObject : MonoBehaviour
{
    [Header("Visual Feedback")]
    [SerializeField] private GameObject hitEffectPrefab;     // 피격 시 생성할 이펙트
    [SerializeField] private GameObject destroyEffectPrefab; // 파괴 시 생성할 이펙트
    [SerializeField] private bool applyVisualPunch = true;    // 피격 시 크기가 일시적으로 찌그러지는 연출 여부
    [SerializeField] private float punchScaleAmount = 0.1f;  // 피격 연출 크기 변형 정도

    private Health _health;
    private Vector3 _originalScale;
    private float _punchTimer = 0f;
    private const float PunchDuration = 0.15f;

    private void Awake()
    {
        _health = GetComponent<Health>();
        _originalScale = transform.localScale;
    }

    private void OnEnable()
    {
        // Health 이벤트 구독
        _health.OnDamageTaken += HandleDamageTaken;
        _health.OnDeath += HandleDeath;
    }

    private void OnDisable()
    {
        // 이벤트 구독 해제 (메모리 누수 방지)
        _health.OnDamageTaken -= HandleDamageTaken;
        _health.OnDeath -= HandleDeath;
    }

    private void Update()
    {
        // 피격 찌그러짐 연출 복원 처리
        if (applyVisualPunch && _punchTimer > 0)
        {
            _punchTimer -= Time.deltaTime;
            if (_punchTimer <= 0)
            {
                transform.localScale = _originalScale;
            }
            else
            {
                // 간단하게 선형 보간으로 스케일을 되돌립니다.
                float t = _punchTimer / PunchDuration;
                transform.localScale = Vector3.Lerp(_originalScale, _originalScale + Vector3.one * punchScaleAmount, t);
            }
        }
    }

    /// <summary>
    /// 대미지를 받았을 때 호출되는 이벤트 핸들러입니다.
    /// </summary>
    private void HandleDamageTaken(DamageInfo info)
    {
        Debug.Log($"[{gameObject.name}] {info.amount} 대미지를 입었습니다. 공격자: {info.attacker?.name}");

        // 1. 피격 이펙트 생성
        if (hitEffectPrefab != null)
        {
            // hitPoint가 지정되어 있다면 그 위치에, 아니면 콜라이더 중심 근처에 생성
            Vector3 spawnPoint = info.hitPoint != Vector3.zero ? info.hitPoint : transform.position;
            Quaternion spawnRot = info.hitNormal != Vector3.zero ? Quaternion.LookRotation(info.hitNormal) : Quaternion.identity;
            
            Instantiate(hitEffectPrefab, spawnPoint, spawnRot);
        }

        // 2. 피격 흔들림/스케일 연출 시작
        if (applyVisualPunch)
        {
            _punchTimer = PunchDuration;
            transform.localScale = _originalScale + Vector3.one * punchScaleAmount;
        }
    }

    /// <summary>
    /// 사망(체력 0 이하) 시 호출되는 이벤트 핸들러입니다.
    /// </summary>
    private void HandleDeath(DamageInfo info)
    {
        Debug.Log($"[{gameObject.name}] 오브젝트가 파괴되었습니다! 마지막 일격 공격자: {info.attacker?.name}");

        // 1. 파괴 이펙트 생성
        if (destroyEffectPrefab != null)
        {
            Instantiate(destroyEffectPrefab, transform.position, Quaternion.identity);
        }

        // 2. 물리 파편 효과 구현 등을 위해 렌더러와 콜라이더를 끄고 이벤트를 마친 후 파괴되도록 처리
        Collider[] colliders = GetComponentsInChildren<Collider>();
        foreach (var col in colliders)
        {
            col.enabled = false;
        }

        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        foreach (var rend in renderers)
        {
            rend.enabled = false;
        }

        // 추가적인 파괴 처리 (예: 아이템 드랍 등)를 여기에 구현할 수 있습니다.
        
        // 씬에서 오브젝트 제거 (Health의 destroyOnDeath를 쓰지 않고 여기서 개별적으로 비주얼 제거 후 파괴 가능)
        Destroy(gameObject, 0.5f); // 효과음이나 이펙트 마무리를 위해 약간의 딜레이 후 파괴
    }
}
