using System.Collections.Generic;

[System.Serializable]
public class BuffData : IData
{
    public int id;
    public int buffType; // BuffType enum 값과 매핑 (JSON 특성상 int로 저장하는 경우가 많음)
    public float duration; // 유지 시간
    public float value;    // 힐량, 이속 배율 등 핵심 수치
    public float tickInterval; // 틱 주기 (도트 힐/독 등에서 사용, 0이면 단발성 또는 지속 패시브)
    public string prefabName;  // 이펙트 프리팹 이름

    public int GetId()
    {
        return id;
    }
}

[System.Serializable]
public class BuffDataList : IDataList<BuffData>
{
    public List<BuffData> buffs = new List<BuffData>();

    public List<BuffData> GetList()
    {
        return buffs;
    }
}
