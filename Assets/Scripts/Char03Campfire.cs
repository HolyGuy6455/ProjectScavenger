using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

public class Char03Campfire : NetworkBehaviour
{
    [Header("Campfire Settings")]
    [Tooltip("버프를 제공할 반경")]
    [SerializeField] private float effectRadius = 5f;
    [Tooltip("몇 초마다 버프를 갱신/부여할 것인가?")]
    [SerializeField] private float tickRate = 1f;
    
    // 이 캠프파이어가 발동시킬 버프의 ID (Data/Scavenger - Buff.tsv 의 id)
    // 예: 2번이 도트 힐이라면 2로 설정
    [SerializeField] private int buffIdToApply = 2;

    private float lifeTime = 0f;
    private float maxLifeTime = 10f; // 궁극기 데이터에서 받아올 지속시간
    private float nextTickTime = 0f;

    /// <summary>
    /// 궁극기를 시전할 때 Char03_Ultimate에서 호출하여 초기 데이터를 세팅합니다.
    /// </summary>
    public void Initialize(UltimateSkillData ultData)
    {
        if (ultData != null)
        {
            maxLifeTime = ultData.duration;
            // ultData.value_1 같은 수치를 버프 시스템 외에 자체적으로 쓸 일이 있다면 여기서 세팅합니다.
        }
        lifeTime = maxLifeTime;
    }

    private void Update()
    {
        // 캠프파이어의 수명 타이머 및 버프 뿌리기 로직은 서버에서만 처리합니다.
        if (!IsServer) return;

        // 1. 수명 깎기
        lifeTime -= Time.deltaTime;
        if (lifeTime <= 0f)
        {
            // 수명이 다하면 스스로 파괴 (NetworkObject Despawn)
            GetComponent<NetworkObject>()?.Despawn();
            return;
        }

        // 2. 주기적으로 주변에 버프 뿌리기
        if (Time.time >= nextTickTime)
        {
            nextTickTime = Time.time + tickRate;
            ApplyBuffToNearbyAllies();
        }
    }

    private void ApplyBuffToNearbyAllies()
    {
        // 내 주변 effectRadius 반경 안의 모든 콜라이더를 찾습니다.
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, effectRadius);
        
        foreach (var col in hitColliders)
        {
            // 타겟에게 BuffController가 있는지 확인합니다. (플레이어나 용병 등)
            BuffController targetBuffController = col.GetComponent<BuffController>();
            
            if (targetBuffController != null)
            {
                // 플레이어라면 현재 활성화된 슬롯을 알아내서 해당 슬롯에 버프를 줍니다.
                int activeSlot = 0;
                PlayerController pc = col.GetComponent<PlayerController>();
                if (pc != null)
                {
                    activeSlot = pc.ActiveSlotIndex;
                }

                // 타겟에게 버프 부여!
                // 이미 동일한 버프가 있다면 BuffController 내부에서 갱신되거나 중첩되도록 나중에 손볼 수도 있습니다.
                targetBuffController.AddBuffServerRpc(activeSlot, buffIdToApply);
            }
        }
    }

#if UNITY_EDITOR
    // 에디터에서 반경을 시각적으로 확인하기 위한 기즈모
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0, 1, 0, 0.3f);
        Gizmos.DrawSphere(transform.position, effectRadius);
    }
#endif
}
