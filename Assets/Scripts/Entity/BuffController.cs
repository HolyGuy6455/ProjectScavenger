using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class BuffController : NetworkBehaviour
{
    public const int MaxDisplayedBuffs = 4;

    // 스쿼드 4명 각각의 버프 리스트 (몬스터의 경우 0번 슬롯만 사용)
    private List<Buff>[] squadBuffs = new List<Buff>[4];
    // 캐릭터를 교체해도 현재 활동 중인 캐릭터에게 적용되는 효과
    private readonly List<Buff> sharedBuffs = new List<Buff>();

    public event Action OnBuffsChanged;

    private void Awake()
    {
        for (int i = 0; i < 4; i++)
        {
            squadBuffs[i] = new List<Buff>();
        }
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        if (!IsServer)
        {
            for (int i = 0; i < 4; i++)
            {
                UpdateBuffs(squadBuffs[i], dt, false);
            }
            UpdateBuffs(sharedBuffs, dt, false);
            return;
        }

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
        bool removedAny = false;
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
                if (buff.lifeTime <= 0f)
                {
                    buffs.RemoveAt(i);
                    removedAny = true;
                }
            }
        }

        if (removedAny)
        {
            OnBuffsChanged?.Invoke();
        }
    }

    /// <summary>플레이어 공용 버프를 부여합니다. 같은 효과는 중첩 없이 갱신합니다.</summary>
    public void AddSharedBuff(BuffData data)
    {
        if (!IsServer || data == null) return;

        ApplySharedBuffInternal(data);
        if (IsSpawned)
        {
            SyncSharedBuffClientRpc(data.id, data.duration, data.name ?? string.Empty, data.resourceIcon ?? string.Empty);
        }
    }

    private void ApplySharedBuffInternal(BuffData data)
    {
        Buff existing = sharedBuffs.Find(buff => buff.id == data.id);
        if (existing != null)
        {
            existing.Refresh(data);
            OnBuffsChanged?.Invoke();
            return;
        }

        Buff newBuff = BuffFactory.CreateBuff(data);
        if (newBuff != null)
        {
            sharedBuffs.Add(newBuff);
            OnBuffsChanged?.Invoke();
        }
    }

    [ClientRpc]
    private void SyncSharedBuffClientRpc(int buffId, float duration, string buffName, string resourceIcon)
    {
        if (IsServer) return;

        BuffData data = DataManager.Instance != null && buffId > 0
            ? DataManager.Instance.GetData<BuffData>("Buff", buffId)
            : null;

        BuffData syncData = new BuffData
        {
            id = buffId,
            name = !string.IsNullOrEmpty(buffName) ? buffName : (data != null ? data.name : string.Empty),
            resourceIcon = !string.IsNullOrEmpty(resourceIcon) ? resourceIcon : (data != null ? data.resourceIcon : string.Empty),
            buffType = data != null ? data.buffType : 0,
            duration = duration,
            value = data != null ? data.value : 0f,
            tickInterval = data != null ? data.tickInterval : 0f
        };

        ApplySharedBuffInternal(syncData);
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
                OnBuffsChanged?.Invoke();

                if (IsSpawned)
                {
                    SyncSlotBuffClientRpc(slotIndex, data.id, data.duration, data.name ?? string.Empty, data.resourceIcon ?? string.Empty);
                }
            }
        }
    }

    [ClientRpc]
    private void SyncSlotBuffClientRpc(int slotIndex, int buffId, float duration, string buffName, string resourceIcon)
    {
        if (IsServer || slotIndex < 0 || slotIndex >= 4) return;

        BuffData data = DataManager.Instance != null && buffId > 0
            ? DataManager.Instance.GetData<BuffData>("Buff", buffId)
            : null;

        BuffData syncData = new BuffData
        {
            id = buffId,
            name = !string.IsNullOrEmpty(buffName) ? buffName : (data != null ? data.name : string.Empty),
            resourceIcon = !string.IsNullOrEmpty(resourceIcon) ? resourceIcon : (data != null ? data.resourceIcon : string.Empty),
            buffType = data != null ? data.buffType : 0,
            duration = duration,
            value = data != null ? data.value : 0f,
            tickInterval = data != null ? data.tickInterval : 0f
        };

        Buff newBuff = BuffFactory.CreateBuff(syncData);
        if (newBuff != null)
        {
            squadBuffs[slotIndex].Add(newBuff);
            OnBuffsChanged?.Invoke();
        }
    }

    /// <summary>
    /// 특정 슬롯 캐릭터에게 표시할 활성 버프 목록(공용 버프 + 개별 버프)을 최대 maxCount개까지 반환합니다.
    /// </summary>
    public void GetDisplayBuffs(int slotIndex, List<Buff> results, int maxCount = MaxDisplayedBuffs)
    {
        if (results == null) return;
        results.Clear();

        AppendUniqueBuffs(sharedBuffs, results, maxCount);
        if (slotIndex >= 0 && slotIndex < squadBuffs.Length && squadBuffs[slotIndex] != null)
        {
            AppendUniqueBuffs(squadBuffs[slotIndex], results, maxCount);
        }
    }

    private static void AppendUniqueBuffs(List<Buff> source, List<Buff> results, int maxCount)
    {
        if (source == null) return;
        for (int i = 0; i < source.Count; i++)
        {
            Buff buff = source[i];
            if (buff == null) continue;
            if (buff.maxLifeTime > 0f && buff.lifeTime <= 0f) continue;

            int existingIndex = -1;
            for (int j = 0; j < results.Count; j++)
            {
                if (results[j].id == buff.id)
                {
                    existingIndex = j;
                    break;
                }
            }

            if (existingIndex >= 0)
            {
                if (buff.lifeTime > results[existingIndex].lifeTime)
                {
                    results[existingIndex] = buff;
                }
            }
            else if (results.Count < maxCount)
            {
                results.Add(buff);
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
