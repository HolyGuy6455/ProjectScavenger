using UnityEngine;

public class Buff
{
    public int id;
    public float lifeTime;
    public float maxLifeTime;
    public float value;
    public float tickInterval;
    
    protected float lastTickTime;

    public Buff(BuffData data)
    {
        Refresh(data);
        this.lastTickTime = 0f;
    }

    /// <summary>남은 시간과 수치를 갱신하며, 진행 중인 틱 주기는 유지합니다.</summary>
    public void Refresh(BuffData data)
    {
        this.id = data.id;
        this.maxLifeTime = data.duration;
        this.lifeTime = data.duration;
        this.value = data.value;
        this.tickInterval = data.tickInterval;
    }

    /// <summary>
    /// 추가 이동 속도 배율을 반환합니다. (기본 0)
    /// 예: 0.3을 리턴하면 기존 이속에 +30% 가산.
    /// </summary>
    public virtual float GetAdditionalMoveSpeedMultiplier() 
    { 
        return 0f; 
    }

    /// <summary>
    /// 추가 힐량 배율을 반환합니다. (기본 0)
    /// 예: 0.5를 리턴하면 받는 힐량이 50% 증폭됨.
    /// </summary>
    public virtual float GetAdditionalHealMultiplier() 
    { 
        return 0f; 
    }

    /// <summary>
    /// 매 프레임(또는 지정된 틱 주기마다) 호출되는 로직입니다.
    /// 도트 힐이나 독 데미지 등 지속성 효과를 구현할 때 오버라이드합니다.
    /// </summary>
    public virtual void OnTick(GameObject target, float dt)
    {
        // 기본적으로 아무것도 하지 않음
    }
}
