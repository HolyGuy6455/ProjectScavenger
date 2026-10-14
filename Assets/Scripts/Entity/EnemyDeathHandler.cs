using UnityEngine;

/// <summary>적의 행동을 중단하고, 사망 애니메이션이 끝나면 서버에서 제거합니다.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Health), typeof(EnemyAnimator))]
public class EnemyDeathHandler : MonoBehaviour
{
    [SerializeField] private string deathStateName = "Death";

    private Health _health;
    private EnemyAnimator _enemyAnimator;
    private bool _isDying;

    private void Awake()
    {
        _health = GetComponent<Health>();
        _enemyAnimator = GetComponent<EnemyAnimator>();
    }

    /// <summary>Health의 On Death 이벤트에 연결합니다. 서버와 클라이언트에서 각각 호출됩니다.</summary>
    public void BeginDeath()
    {
        if (_isDying || !_health.IsDead) return;
        _isDying = true;

        var brain = GetComponent<AIBrain>();
        if (brain != null) brain.enabled = false;

        var controller = GetComponent<EnemyController>();
        if (controller != null)
        {
            // 데이터 로딩을 기다리던 초기화가 사망 후 체력/탄약을 다시 채우지 않도록 합니다.
            controller.StopAllCoroutines();
            controller.enabled = false;
        }

        var shooter = GetComponent<Shooter>();
        if (shooter != null)
        {
            shooter.SetTrigger(false);
            shooter.CancelReload();
            shooter.enabled = false;
        }

        // OnDisable의 스킬 취소가 이동 권한을 복구하므로, 이동 비활성화보다 먼저 처리합니다.
        foreach (var skill in GetComponents<EnemySkill>())
        {
            skill.StopAllCoroutines();
            skill.enabled = false;
        }

        var body = GetComponent<Rigidbody>();
        var movement = GetComponent<EntityMovement>();
        if (movement != null)
        {
            // 원격 클라이언트의 kinematic Rigidbody에는 속도를 쓰지 않습니다.
            if (body != null && !body.isKinematic) movement.Stop();
            movement.enabled = false;
        }
        if (body != null && !body.isKinematic)
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.isKinematic = true;
        }

        if (!_enemyAnimator.PlayDeathAnimation(deathStateName))
        {
            Debug.LogWarning($"[EnemyDeathHandler] 사망 애니메이션 상태를 찾을 수 없습니다: {deathStateName}", this);
            // Health가 사망 RPC를 전송한 다음 제거합니다.
            Invoke(nameof(CompleteDeathAnimation), 0f);
        }
    }

    /// <summary>사망 클립 마지막의 ForwardEvent에서 호출합니다. 클라이언트는 서버의 제거를 기다립니다.</summary>
    public void CompleteDeathAnimation()
    {
        if (!_isDying || !_health.IsDead || !_health.IsSpawned || !_health.IsServer) return;

        _isDying = false;
        _health.DeathAnimationDone();
    }
}
