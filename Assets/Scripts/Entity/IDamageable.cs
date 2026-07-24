/// <summary>
/// 대미지를 입을 수 있는 모든 오브젝트(플레이어, 적, 중립 설치물 등)가 구현해야 하는 인터페이스입니다.
/// </summary>
public interface IDamageable
{
    void TakeDamage(DamageInfo damageInfo);
}
