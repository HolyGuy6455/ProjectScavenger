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
            PlayerController player = FindObjectOfType<PlayerController>();
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

    /// <summary>
    /// 플레이어를 스폰 포인트(PlayerSpawnPosition)로 강제 텔레포트 시킵니다.
    /// 디버그 UI 버튼의 OnClick()에 이 메서드를 연결하세요.
    /// </summary>
    public void TeleportToSpawnPoint()
    {
        if (playerHealth == null)
        {
            Debug.LogWarning("[Cheat] 씬에서 플레이어의 Health 컴포넌트를 찾지 못했습니다.");
            return;
        }

        GameObject spawnObj = GameObject.Find("PlayerSpawnPosition");
        if (spawnObj == null)
        {
            Debug.LogWarning("[Cheat] 씬에서 'PlayerSpawnPosition' 오브젝트를 찾지 못했습니다.");
            return;
        }

        var nt = playerHealth.GetComponent<Unity.Netcode.Components.NetworkTransform>();
        if (nt != null)
        {
            // 네트워크 환경이라면 NetworkTransform.Teleport를 사용합니다.
            nt.Teleport(spawnObj.transform.position, playerHealth.transform.rotation, playerHealth.transform.localScale);
        }
        else
        {
            // 싱글플레이나 권한 문제 등으로 NetworkTransform이 없으면 직접 이동
            playerHealth.transform.position = spawnObj.transform.position;
        }

        // NetworkTransform의 Teleport만 호출할 경우, 물리 엔진(Rigidbody)이 변경된 transform.position을
        // 즉각 반영하지 못해 FixedUpdate에서 과거 위치를 기준으로 연산하게 되고, 
        // 결과적으로 원래 자리로 되돌아가면서 바닥을 뚫고 떨어지는 현상이 발생할 수 있습니다.
        // 이를 방지하기 위해 Rigidbody의 position도 명시적으로 변경해줍니다.
        var rb = playerHealth.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.position = spawnObj.transform.position;
            rb.linearVelocity = Vector3.zero;
        }

        Debug.Log($"[Cheat] 플레이어를 스폰 포인트({spawnObj.transform.position})로 텔레포트했습니다.");
    }

    /// <summary>
    /// 수동으로 'Entering' 시작 연출 시네마틱을 테스트합니다.
    /// 디버그 UI 버튼의 OnClick()에 이 메서드를 연결하세요.
    /// </summary>
    public void TestEnteringCinematic()
    {
        if (PlayerController.LocalInstance != null)
        {
            PlayerController.LocalInstance.PlayCinematic("Entering", true, () =>
            {
                Debug.Log("[Cheat] 수동 테스트: 'Entering' 시네마틱 연출이 종료되었습니다.");
            });
            Debug.Log("[Cheat] 수동 테스트: 'Entering' 시네마틱 재생을 요청했습니다.");
        }
        else
        {
            Debug.LogWarning("[Cheat] PlayerController.LocalInstance를 찾을 수 없습니다.");
        }
    }
}
