using UnityEngine;

/// <summary>
/// 디버그/치트용 메서드들을 모아두는 스크립트입니다.
/// UI 버튼의 OnClick 이벤트에 메서드들을 연결하여 사용합니다.
/// </summary>
public class SimpleCheater : MonoBehaviour
{
    [Header("Target References")]
    [Tooltip("치트를 적용할 플레이어의 Health 컴포넌트입니다. 비워두면 씬에서 자동으로 찾습니다.")]
    [SerializeField] private Health playerHealth;

    private void Start()
    {
        // 인스펙터에서 플레이어를 수동으로 할당하지 않았다면 씬에서 자동으로 찾습니다.
        if (playerHealth == null)
        {
            PlayerMovement player = FindObjectOfType<PlayerMovement>();
            if (player != null)
            {
                playerHealth = player.GetComponent<Health>();
            }
        }
    }

    /// <summary>
    /// 플레이어의 체력을 20 깎습니다.
    /// 디버그 UI 버튼의 OnClick()에 이 메서드를 연결하세요.
    /// </summary>
    public void DamageSelf()
    {
        if (playerHealth != null)
        {
            // 공격자(attacker)를 이 스크립트가 붙은 오브젝트로 설정하고 20의 피해를 입힙니다.
            DamageInfo cheatDamage = new DamageInfo(20f, gameObject);
            playerHealth.TakeDamage(cheatDamage);
            
            Debug.Log($"[Cheat] 플레이어에게 20의 피해를 입혔습니다! 현재 체력: {playerHealth.CurrentHealth}");
        }
        else
        {
            Debug.LogWarning("[Cheat] 씬에서 플레이어의 Health 컴포넌트를 찾지 못했습니다.");
        }
    }
}
