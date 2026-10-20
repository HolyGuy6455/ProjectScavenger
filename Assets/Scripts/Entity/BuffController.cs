using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class BuffController : NetworkBehaviour
{
    // 스쿼드 4명 각각의 버프 리스트 (몬스터의 경우 0번 슬롯만 사용)
    private List<Buff>[] squadBuffs = new List<Buff>[4];
    // 캐릭터를 교체해도 현재 활동 중인 캐릭터에게 적용되는 효과
    private readonly List<Buff> sharedBuffs = new List<Buff>();

    private void Awake()
    {
        for (int i = 0; i < 4; i++)
        {
            squadBuffs[i] = new List<Buff>();
        }
    }

    private void Update()
    {
        if (!IsServer) return;

        float dt = Time.deltaTime;
        PlayerController player = GetComponent<PlayerController>();
        int activeSlot = player != null ? player.ActiveSlotIndex : 0;

        for (int i = 0; i < 4; i++)
        {
            UpdateBuffs(squadBuffs[i], dt, i == activeSlot);
        }
        UpdateBuffs(sharedBuffs, dt, true);
    }

    private void UpdateBuffs(List<Buff> buffs, float dt, bool tickActive)
    {
        for (int i = buffs.Count - 1; i >= 0; i--)
        {
            Buff buff = buffs[i];
            bool expires = buff.maxLifeTime > 0f;
            float activeTime = expires ? Mathf.Min(dt, Mathf.Max(0f, buff.lifeTime)) : dt;

            // 대기 캐릭터도 수명은 흐르지만 회복을 누적하지 않습니다.
            // 만료 시점의 마지막 틱까지 처리한 뒤 제거합니다.
            if (tickActive && activeTime > 0f) buff.OnTick(gameObject, activeTime);
            if (expires)
            {
                buff.lifeTime -= dt;
                if (buff.lifeTime <= 0f) buffs.RemoveAt(i);
            }
        }
    }

    /// <summary>플레이어 공용 버프를 부여합니다. 같은 효과는 중첩 없이 갱신합니다.</summary>
    public void AddSharedBuff(BuffData data)
    {
        if (!IsServer || data == null) return;

        Buff existing = sharedBuffs.Find(buff => buff.id == data.id);
        if (existing != null)
        {
            existing.Refresh(data);
            return;
        }

        Buff newBuff = BuffFactory.CreateBuff(data);
        if (newBuff != null) sharedBuffs.Add(newBuff);
    }

    /// <summary>
    /// 버프를 추가합니다. (서버에서만 호출 가능)
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    public void AddBuffServerRpc(int slotIndex, int buffId)
    {
        if (slotIndex < 0 || slotIndex >= 4) return;
        if (DataManager.Instance == null) return;

        BuffData data = DataManager.Instance.GetData<BuffData>("Buff", buffId);
        if (data != null)
        {
            Buff newBuff = BuffFactory.CreateBuff(data);
            if (newBuff != null)
            {
                squadBuffs[slotIndex].Add(newBuff);
                
                // TODO: 클라이언트에게 이펙트를 생성하라고 알리는 ClientRpc 호출 가능
                // RpcSpawnBuffEffectClientRpc(slotIndex, data.prefabName);
            }
        }
    }

    /// <summary>
    /// 이동속도 추가 배율을 계산하여 반환합니다.
    /// </summary>
    public float GetTotalMoveSpeedMultiplier(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= 4) return 1f;

        float total = 1f;
        foreach (var buff in squadBuffs[slotIndex])
        {
            total += buff.GetAdditionalMoveSpeedMultiplier();
        }
        foreach (var buff in sharedBuffs)
        {
            total += buff.GetAdditionalMoveSpeedMultiplier();
        }
        return total;
    }

    /// <summary>
    /// 힐량 추가 배율을 계산하여 반환합니다.
    /// </summary>
    public float GetTotalHealMultiplier(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= 4) return 1f;

        float total = 1f;
        foreach (var buff in squadBuffs[slotIndex])
        {
            total += buff.GetAdditionalHealMultiplier();
        }
        foreach (var buff in sharedBuffs)
        {
            total += buff.GetAdditionalHealMultiplier();
        }
        return total;
    }
}
