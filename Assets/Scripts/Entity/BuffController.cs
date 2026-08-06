using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class BuffController : NetworkBehaviour
{
    // 스쿼드 4명 각각의 버프 리스트 (몬스터의 경우 0번 슬롯만 사용)
    private List<Buff>[] squadBuffs = new List<Buff>[4];

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

        // 4개의 슬롯(비활성된 캐릭터 포함)을 매 프레임 순회하며 버프 연산
        for (int i = 0; i < 4; i++)
        {
            List<Buff> buffs = squadBuffs[i];
            for (int j = buffs.Count - 1; j >= 0; j--)
            {
                Buff buff = buffs[j];
                
                // 수명 깎기 (duration이 0 이하면 무한 지속 패시브로 취급)
                if (buff.maxLifeTime > 0f)
                {
                    buff.lifeTime -= dt;
                    if (buff.lifeTime <= 0f)
                    {
                        buffs.RemoveAt(j);
                        continue;
                    }
                }

                // 틱 연산 (예: 도트 힐, 독 데미지 등)
                // 서버에서만 틱을 실행하여 중복 연산 및 비동기화 방지
                if (IsServer)
                {
                    buff.OnTick(this.gameObject, dt);
                }
            }
        }
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
        return total;
    }
}
