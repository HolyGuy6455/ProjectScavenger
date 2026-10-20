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

        Health health = target.GetComponent<Health>();
        // 만료 경계의 부동소수점 오차로 마지막 회복이 빠지지 않게 합니다.
        while (timeSinceLastTick > 0f && timeSinceLastTick + 0.0001f >= tickInterval)
        {
            timeSinceLastTick = Mathf.Max(0f, timeSinceLastTick - tickInterval);
            if (health != null && !health.IsDead)
            {
                // value만큼 틱당 회복시킵니다.
                // 힐은 서버 전용(NetworkBehaviour)이므로, 서버에서만 작동하게 됩니다.
                health.Heal(value);
            }
        }
    }
}
