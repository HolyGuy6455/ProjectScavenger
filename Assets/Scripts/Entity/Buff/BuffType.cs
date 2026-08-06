public enum BuffType
{
    None = 0,
    MoveSpeed = 1,          // 이동속도 증가/감소 (value = 0.3 이면 30% 증가, -0.3 이면 30% 감소)
    HealOverTime = 2,       // 도트 힐 (tickInterval 마다 value 만큼 회복)
    DamageOverTime = 3      // 도트 데미지 (독, 화상 등)
}
