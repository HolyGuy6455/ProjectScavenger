using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 체력 상태를 관리하고, 대미지 피격/치유 로직 및 관련 이벤트를 처리하는 범용 컴포넌트입니다.
/// </summary>
public class Health : NetworkBehaviour, IDamageable
{
    [Header("Health Settings")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private bool destroyOnDeath = false;
    [SerializeField] private float destroyDelay = 0f;

    // 네트워크 체력 동기화 변수 (서버만 쓰기 가능)
    public NetworkVariable<float> NetworkCurrentHealth = new NetworkVariable<float>(
        100f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // 기존 코드 호환을 위한 프로퍼티
    public float CurrentHealth
    {
        get => NetworkCurrentHealth.Value;
        private set 
        {
            if (IsServer) NetworkCurrentHealth.Value = value;
        }
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

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            ResetHealth();
        }

        // 체력 변경 시 UI 등 업데이트 트리거
        NetworkCurrentHealth.OnValueChanged += (oldValue, newValue) =>
        {
            OnHealthChanged?.Invoke(newValue, maxHealth);
            onHealthChangedUnityEvent?.Invoke(newValue, maxHealth);
        };
        
        // 스폰 시점에 현재 체력 UI 동기화
        OnHealthChanged?.Invoke(CurrentHealth, maxHealth);
        onHealthChangedUnityEvent?.Invoke(CurrentHealth, maxHealth);
    }

    /// <summary>
    /// 대미지를 입히는 메서드입니다. (서버에서만 실행)
    /// </summary>
    public void TakeDamage(DamageInfo damageInfo)
    {
        if (!IsServer) return; // 서버만 데미지 처리 가능
        if (IsDead) return;
        if (damageInfo.amount <= 0) return;

        CurrentHealth -= damageInfo.amount;
        
        // 서버측 이벤트 호출
        OnDamageTaken?.Invoke(damageInfo);
        onDamageTakenUnityEvent?.Invoke(damageInfo);

        // 클라이언트들에게 피격 연출(이벤트)을 재생하라고 명령
        TakeDamageClientRpc(damageInfo.amount, damageInfo.hitPoint, damageInfo.hitNormal);

        if (CurrentHealth <= 0)
        {
            CurrentHealth = 0;
            Die(damageInfo);
        }
    }

    [ClientRpc]
    private void TakeDamageClientRpc(float amount, Vector3 hitPoint, Vector3 hitNormal)
    {
        if (IsServer) return; // 서버는 위에서 이미 처리했음

        DamageInfo clientInfo = new DamageInfo(amount, null, hitPoint, hitNormal);
        OnDamageTaken?.Invoke(clientInfo);
        onDamageTakenUnityEvent?.Invoke(clientInfo);
    }

    /// <summary>
    /// 체력을 회복시키는 메서드입니다. (서버 전용)
    /// </summary>
    public void Heal(float amount)
    {
        if (!IsServer) return;
        if (IsDead) return;
        if (amount <= 0) return;

        CurrentHealth += amount;
        if (CurrentHealth > maxHealth)
        {
            CurrentHealth = maxHealth;
        }

        OnHealed?.Invoke(amount);
        onHealedUnityEvent?.Invoke(amount);
    }

    /// <summary>
    /// 체력을 최대치로 초기화하고 사망 플래그를 해제합니다. (서버 전용)
    /// </summary>
    public void ResetHealth()
    {
        if (!IsServer) return;
        IsDead = false;
        CurrentHealth = maxHealth;
    }

    /// <summary>
    /// 강제로 체력과 최대 체력을 설정합니다. (서버 전용)
    /// </summary>
    public void SetHealth(float hp, float maxHp)
    {
        if (!IsServer) return;
        maxHealth = maxHp;
        CurrentHealth = Mathf.Clamp(hp, 0f, maxHealth);
        IsDead = CurrentHealth <= 0;
    }

    /// <summary>
    /// 사망 처리를 수행합니다. (서버 내부용)
    /// </summary>
    private void Die(DamageInfo damageInfo)
    {
        IsDead = true;

        OnDeath?.Invoke(damageInfo);
        onDeathUnityEvent?.Invoke(damageInfo);

        DieClientRpc();

        if (destroyOnDeath && IsServer)
        {
            // NetworkObject는 파괴되면 클라이언트에서도 자동으로 제거됩니다.
            GetComponent<NetworkObject>()?.Despawn();
            Destroy(gameObject, destroyDelay);
        }
    }

    [ClientRpc]
    private void DieClientRpc()
    {
        if (IsServer) return;
        IsDead = true;
        DamageInfo clientInfo = new DamageInfo(0, null, transform.position, Vector3.up);
        OnDeath?.Invoke(clientInfo);
        onDeathUnityEvent?.Invoke(clientInfo);
    }

    public virtual void PlayDeathAnimation()
    {
        Animator animator = GetComponent<Animator>();
        if (animator != null)
        {
            animator.SetBool("IsDeath", true);
        }
    }

    public virtual void DeathAnimationDone()
    {
        if (IsServer)
        {
            GetComponent<NetworkObject>()?.Despawn();
        }
        Destroy(gameObject);
    }

    private void OnValidate()
    {
        if (maxHealth < 0) maxHealth = 0;

        // 플레이 모드에서 인스펙터로 체력을 직접 수정할 경우 이벤트를 발생시켜 UI 등을 동기화합니다.
        if (Application.isPlaying && IsServer)
        {
            // OnValidate에서는 NetworkVariable 할당이 문제를 일으킬 수 있으므로 주의
        }
    }
}
