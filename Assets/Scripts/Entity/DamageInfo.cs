using UnityEngine;

/// <summary>
/// 대미지 전달 시 필요한 상세 정보를 담는 구조체입니다.
/// </summary>
public struct DamageInfo
{
    public float amount;
    public GameObject attacker;
    public Vector3 hitPoint;
    public Vector3 hitNormal;
    public int buffId;
    public Vector3? sourcePosition;
    public long hitSourceKey;
    public float hitCooldown;

    public DamageInfo(
        float amount,
        GameObject attacker = null,
        Vector3 hitPoint = default,
        Vector3 hitNormal = default,
        int buffId = 0,
        Vector3? sourcePosition = null,
        long hitSourceKey = 0,
        float hitCooldown = 0f)
    {
        this.amount = amount;
        this.attacker = attacker;
        this.hitPoint = hitPoint;
        this.hitNormal = hitNormal;
        this.buffId = buffId;
        this.sourcePosition = sourcePosition;
        this.hitSourceKey = hitSourceKey;
        this.hitCooldown = hitCooldown;
    }
}
