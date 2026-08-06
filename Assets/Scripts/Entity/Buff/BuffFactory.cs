public static class BuffFactory
{
    public static Buff CreateBuff(BuffData data)
    {
        if (data == null) return null;

        BuffType type = (BuffType)data.buffType;
        switch (type)
        {
            case BuffType.MoveSpeed:
                return new BuffMoveSpeed(data);
            case BuffType.HealOverTime:
                return new BuffHealOverTime(data);
            case BuffType.DamageOverTime:
                // TODO: DamageOverTime 구현 (현재는 기본 Buff 반환)
                return new Buff(data);
            default:
                return new Buff(data);
        }
    }
}
