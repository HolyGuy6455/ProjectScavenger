using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 체력 상태를 관리하고, 대미지 피격/치유 로직 및 관련 이벤트를 처리하는 범용 컴포넌트입니다.
/// </summary>
public class Health : MonoBehaviour, IDamageable
{
    [Header("Health Settings")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float currentHealth;
    [SerializeField] private bool destroyOnDeath = false;
    [SerializeField] private float destroyDelay = 0f;

    // 현재 체력 프로퍼티
    public float CurrentHealth
    {
        get => currentHealth;
        private set => currentHealth = value;
    }
    public float MaxHealth => maxHealth;
    public bool IsDead { get; private set; }

    #region C# Actions (스크립트용 이벤트)
    public event System.Action<float, float> OnHealthChanged; // (현재 체력, 최대 체력)
    public event System.Action<DamageInfo> OnDamageTaken;     // (피격 정보)
    public event System.Action<float> OnHealed;               // (치유량)
    public event System.Action<DamageInfo> OnDeath;           // (사망 피격 정보)
    #endregion

    #region Unity Events (인스펙터 바인딩용 이벤트)
    [System.Serializable] public class HealthChangedEvent : UnityEvent<float, float> { }
    [System.Serializable] public class DamageTakenEvent : UnityEvent<DamageInfo> { }
    [System.Serializable] public class HealedEvent : UnityEvent<float> { }
    [System.Serializable] public class DeathEvent : UnityEvent<DamageInfo> { }

    [Header("Events")]
    [SerializeField] private HealthChangedEvent onHealthChangedUnityEvent;
    [SerializeField] private DamageTakenEvent onDamageTakenUnityEvent;
    [SerializeField] private HealedEvent onHealedUnityEvent;
    [SerializeField] private DeathEvent onDeathUnityEvent;
    #endregion

    private void Awake()
    {
        ResetHealth();
    }

    /// <summary>
    /// 대미지를 입히는 메서드입니다. IDamageable 인터페이스 구현체입니다.
    /// </summary>
    public void TakeDamage(DamageInfo damageInfo)
    {
        if (IsDead) return;
        if (damageInfo.amount <= 0) return;

        CurrentHealth -= damageInfo.amount;
        
        // 이벤트 호출: 피격
        OnDamageTaken?.Invoke(damageInfo);
        onDamageTakenUnityEvent?.Invoke(damageInfo);

        // 이벤트 호출: 체력 변동
        OnHealthChanged?.Invoke(CurrentHealth, maxHealth);
        onHealthChangedUnityEvent?.Invoke(CurrentHealth, maxHealth);

        if (CurrentHealth <= 0)
        {
            CurrentHealth = 0;
            Die(damageInfo);
        }
    }

    /// <summary>
    /// 체력을 회복시키는 메서드입니다.
    /// </summary>
    public void Heal(float amount)
    {
        if (IsDead) return;
        if (amount <= 0) return;

        CurrentHealth += amount;
        if (CurrentHealth > maxHealth)
        {
            CurrentHealth = maxHealth;
        }

        // 이벤트 호출: 치유
        OnHealed?.Invoke(amount);
        onHealedUnityEvent?.Invoke(amount);

        // 이벤트 호출: 체력 변동
        OnHealthChanged?.Invoke(CurrentHealth, maxHealth);
        onHealthChangedUnityEvent?.Invoke(CurrentHealth, maxHealth);
    }

    /// <summary>
    /// 체력을 최대치로 초기화하고 사망 플래그를 해제합니다.
    /// </summary>
    public void ResetHealth()
    {
        IsDead = false;
        CurrentHealth = maxHealth;
        
        OnHealthChanged?.Invoke(CurrentHealth, maxHealth);
        onHealthChangedUnityEvent?.Invoke(CurrentHealth, maxHealth);
    }

    /// <summary>
    /// 강제로 체력과 최대 체력을 설정합니다. (캐릭터 스위칭 등에서 사용)
    /// </summary>
    public void SetHealth(float hp, float maxHp)
    {
        maxHealth = maxHp;
        CurrentHealth = Mathf.Clamp(hp, 0f, maxHealth);
        IsDead = CurrentHealth <= 0;

        OnHealthChanged?.Invoke(CurrentHealth, maxHealth);
        onHealthChangedUnityEvent?.Invoke(CurrentHealth, maxHealth);
    }

    /// <summary>
    /// 사망 처리를 수행합니다.
    /// </summary>
    private void Die(DamageInfo damageInfo)
    {
        IsDead = true;

        // 이벤트 호출: 사망
        OnDeath?.Invoke(damageInfo);
        onDeathUnityEvent?.Invoke(damageInfo);

        if (destroyOnDeath)
        {
            Destroy(gameObject, destroyDelay);
        }
    }

    private void OnValidate()
    {
        if (maxHealth < 0) maxHealth = 0;
        currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);

        // 플레이 모드에서 인스펙터로 체력을 직접 수정할 경우 이벤트를 발생시켜 UI 등을 동기화합니다.
        if (Application.isPlaying)
        {
            OnHealthChanged?.Invoke(currentHealth, maxHealth);
            onHealthChangedUnityEvent?.Invoke(currentHealth, maxHealth);
        }
    }
}
