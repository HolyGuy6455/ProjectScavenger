using UnityEngine;

/// <summary>
/// GuageBarUI를 상속받아 체력(Health) 컴포넌트의 변경을 구독하고 UI를 자동 업데이트하는 클래스입니다.
/// </summary>
public class HealthBarUI : GuageBarUI
{
    [Header("Health References")]
    [Tooltip("체력 정보를 읽어올 Health 컴포넌트입니다.")]
    [SerializeField] private Health targetHealth;
    
    private bool isHealthInitialized = false;

    public void Setup(Health newHealth, Transform newTarget, Vector3 newOffset)
    {
        if (targetHealth != null && isHealthInitialized)
        {
            targetHealth.OnHealthChanged -= OnHealthChanged;
        }

        targetHealth = newHealth;
        base.Setup(newTarget, newOffset);

        if (targetHealth != null)
        {
            OnHealthChanged(targetHealth.CurrentHealth, targetHealth.MaxHealth);
            targetHealth.OnHealthChanged += OnHealthChanged;
            isHealthInitialized = true;
        }
    }

    protected override void Start()
    {
        base.Start();

        // 타겟 설정 자동화
        if (targetHealth == null)
        {
            targetHealth = GetComponentInParent<Health>();
        }

        if (targetHealth != null)
        {
            if (targetTransform == null)
            {
                targetTransform = targetHealth.transform;
            }

            OnHealthChanged(targetHealth.CurrentHealth, targetHealth.MaxHealth);
            targetHealth.OnHealthChanged += OnHealthChanged;
            isHealthInitialized = true;
        }
        else
        {
            Debug.LogWarning($"[HealthBarUI] {gameObject.name}의 부모 오브젝트 등에서 Health 컴포넌트를 찾을 수 없습니다.");
        }
    }

    protected virtual void OnDestroy()
    {
        if (targetHealth != null && isHealthInitialized)
        {
            targetHealth.OnHealthChanged -= OnHealthChanged;
        }
    }

    private void OnHealthChanged(float currentHealth, float maxHealth)
    {
        UpdateGuage(currentHealth, maxHealth);
    }
}
