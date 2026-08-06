using UnityEngine;

public class BuffHealOverTime : Buff
{
    private float timeSinceLastTick = 0f;

    public BuffHealOverTime(BuffData data) : base(data)
    {
    }

    public override void OnTick(GameObject target, float dt)
    {
        if (tickInterval <= 0f) return;

        timeSinceLastTick += dt;

        if (timeSinceLastTick >= tickInterval)
        {
            timeSinceLastTick -= tickInterval;
            
            Health health = target.GetComponent<Health>();
            if (health != null && !health.IsDead)
            {
                // value만큼 틱당 회복시킵니다.
                // 힐은 서버 전용(NetworkBehaviour)이므로, 서버에서만 작동하게 됩니다.
                health.Heal(value);
            }
        }
    }
}
