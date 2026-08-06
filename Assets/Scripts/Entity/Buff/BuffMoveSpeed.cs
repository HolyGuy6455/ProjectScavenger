public class BuffMoveSpeed : Buff
{
    public BuffMoveSpeed(BuffData data) : base(data)
    {
    }

    public override float GetAdditionalMoveSpeedMultiplier()
    {
        // Data.value가 0.3 이면 30% 증가를 의미
        return value;
    }
}
