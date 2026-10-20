using System.Collections.Generic;
using Scavenger.Audio;
using Unity.Netcode;
using UnityEngine;

public enum BulletTarget
{
    Player,
    Enemy
}

/// <summary>
/// 발사된 총알의 이동 및 충돌 처리를 담당하는 컴포넌트입니다.
/// 멀티플레이어 환경에서는 오직 서버만이 이동 및 충돌 판정을 수행합니다.
/// </summary>
public class Bullet : NetworkBehaviour
{
    [Header("Bullet Settings")]
    [Tooltip("총알의 이동 속도")]
    [SerializeField] private float speed = 20f;
    
    [Tooltip("총알이 입히는 데미지량")]
    [SerializeField] private float damage = 10f;
    
    [Tooltip("총알이 파괴되지 않고 존재할 수 있는 최대 시간 (초)")]
    [SerializeField] private float lifeTime = 5f;

    [Tooltip("관통 여부 (true면 충돌해도 파괴되지 않습니다)")]
    [SerializeField] private bool canPierce = false;

    [Header("Effects")]
    [Tooltip("총알 피격 시 생성할 파티클 프리팹")]
    [SerializeField] private GameObject hitEffectPrefab;
    
    [Tooltip("유탄 폭발 시 생성할 파티클 프리팹")]
    [SerializeField] private GameObject explosionEffectPrefab;

    private Vector3 moveDirection;
    private GameObject attacker;
    private BulletTarget targetType;
    private bool isInitialized = false;

    // 포물선용 변수
    private bool isParabola = false;
    private Vector3 startPosition;
    private Vector3 targetPosition;
    private float parabolaDuration;
    private float parabolaTime;
    private Vector3 previousPos;
    private float explosionRadius;
    private int specialBuffId = 0;
    private SoundKey impactSoundKey = SoundKey.None;

    public void Initialize(Vector3 direction, GameObject shooter, BulletTarget target, AmmoData ammoData = null)
    {
        impactSoundKey = ammoData != null ? ammoData.impactSoundKey : SoundKey.None;
        if (ammoData != null)
        {
            this.damage = ammoData.damage;
            this.specialBuffId = ammoData.specialBuff;
        }

        moveDirection = direction.normalized;
        attacker = shooter;
        targetType = target;
        startPosition = transform.position;
        isParabola = false;
        isInitialized = true;

        // 지정된 시간이 지나면 자동 파괴
        Destroy(gameObject, lifeTime);
    }

    /// <summary>
    /// 목표 지점을 향해 포물선으로 날아가도록 초기화합니다.
    /// </summary>
    public void InitializeParabola(Vector3 targetPos, GameObject shooter, float expRadius, BulletTarget target, AmmoData ammoData = null)
    {
        impactSoundKey = ammoData != null ? ammoData.impactSoundKey : SoundKey.None;
        if (ammoData != null)
        {
            this.damage = ammoData.damage;
            this.specialBuffId = ammoData.specialBuff;
        }

        targetPosition = targetPos;
        startPosition = transform.position;
        previousPos = startPosition;
        attacker = shooter;
        targetType = target;
        explosionRadius = expRadius;
        isParabola = true;
        isInitialized = true;

        // 거리 비례로 비행 시간을 계산 (일정한 수평 속도 유지)
        float distance = Vector3.Distance(new Vector3(startPosition.x, 0, startPosition.z), new Vector3(targetPosition.x, 0, targetPosition.z));
        parabolaDuration = distance / speed;
        if (parabolaDuration <= 0.001f) parabolaDuration = 0.001f;
        parabolaTime = 0f;

        Destroy(gameObject, lifeTime);
    }

    private void Update()
    {
        if (!IsServer) return; // 오직 서버에서만 이동 및 충돌 연산
        if (!isInitialized) return;

        if (isParabola)
        {
            parabolaTime += Time.deltaTime;
            float t = parabolaTime / parabolaDuration;
            
            if (t >= 1f)
            {
                // 타겟(바닥)에 도달
                HitTarget(null, targetPosition, Vector3.up);
                return;
            }

            // 곡선 위치 계산 (Y축 높이 추가)
            Vector3 currentPos = Vector3.Lerp(startPosition, targetPosition, t);
            float arcHeight = 3f; // 포물선 최대 높이
            currentPos.y += 4f * arcHeight * t * (1f - t);

            transform.position = currentPos;
            
            // 날아가는 방향으로 회전
            Vector3 moveDir = currentPos - previousPos;
            if (moveDir.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.LookRotation(moveDir);
            }
            previousPos = currentPos;
        }
        else
        {
            // 이번 프레임에서 이동할 거리
            float moveDistance = speed * Time.deltaTime;

            // 아군 뒤에 있는 적/벽도 같은 프레임에 찾도록 유효한 충돌 중 가장 가까운 것을 고릅니다.
            var hits = Physics.RaycastAll(transform.position, moveDirection, moveDistance,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            RaycastHit nearest = default;
            float nearestDistance = float.PositiveInfinity;
            foreach (var hit in hits)
            {
                if (hit.distance >= nearestDistance || ShouldIgnore(hit.collider)) continue;
                nearest = hit;
                nearestDistance = hit.distance;
            }
            if (nearest.collider != null)
            {
                HitTarget(nearest.collider, nearest.point, nearest.normal);
                if (!isInitialized) return;
            }
            
            // 실제 이동 처리
            transform.position += moveDirection * moveDistance;
        }
    }

    private bool ShouldIgnore(Collider other)
    {
        if (other == null || other.isTrigger || other.transform.IsChildOf(transform)) return true;
        if (attacker != null && other.transform.IsChildOf(attacker.transform)) return true;

        return TryGetTarget(other.transform, out var actor) && actor != targetType;
    }

    // 감지와 피격에서 같은 분류를 사용합니다. 태그가 없는 레거시 모델은 레이어로 판별합니다.
    public static bool TryGetTarget(Transform target, out BulletTarget actor)
    {
        // 부모의 캐릭터 태그를 자식 메시 레이어보다 우선합니다.
        for (var current = target; current != null; current = current.parent)
        {
            if (current.CompareTag("Player")) { actor = BulletTarget.Player; return true; }
            if (current.CompareTag("Enemy")) { actor = BulletTarget.Enemy; return true; }
        }
        int playerLayer = LayerMask.NameToLayer("Player");
        int enemyLayer = LayerMask.NameToLayer("Enemy");
        for (var current = target; current != null; current = current.parent)
        {
            if (current.gameObject.layer == playerLayer) { actor = BulletTarget.Player; return true; }
            if (current.gameObject.layer == enemyLayer) { actor = BulletTarget.Enemy; return true; }
        }
        // 벽과 중립 파괴물은 기존처럼 탄환을 막고 피해를 받습니다.
        actor = default;
        return false;
    }

    private void HitTarget(Collider target, Vector3 hitPoint, Vector3 hitNormal)
    {
        if (!IsServer || !isInitialized || (target != null && ShouldIgnore(target))) return;
        bool despawnOnHit = !canPierce || isParabola;
        if (despawnOnHit) isInitialized = false;

        // 클라이언트들에게 피격/폭발 이펙트 재생 명령
        SpawnHitEffectClientRpc(hitPoint, hitNormal, isParabola, impactSoundKey);

        if (isParabola)
        {
            Collider[] colliders = Physics.OverlapSphere(hitPoint, explosionRadius);
            var damagedTargets = new HashSet<IDamageable>();
            foreach (Collider col in colliders)
            {
                if (ShouldIgnore(col)) continue;

                IDamageable damageable = col.GetComponentInParent<IDamageable>();
                if (damageable != null && damagedTargets.Add(damageable))
                {
                    // 거리에 따른 데미지 감소 로직 등을 추후 추가할 수 있습니다.
                    DamageInfo damageInfo = new DamageInfo(damage, attacker, col.transform.position, hitNormal, specialBuffId, startPosition);
                    damageable.TakeDamage(damageInfo);
                }
            }
        }
        else
        {
            // 기존 직사형 무기: 단일 타겟 데미지 처리
            if (target != null)
            {
                IDamageable damageable = target.GetComponentInParent<IDamageable>();
                
                if (damageable != null)
                {
                    DamageInfo damageInfo = new DamageInfo(damage, attacker, hitPoint, hitNormal, specialBuffId, startPosition);
                    damageable.TakeDamage(damageInfo);
                }
            }
        }

        // 관통 총알이 아니라면 충돌 후 총알 파괴
        if (despawnOnHit)
        {
            GetComponent<NetworkObject>()?.Despawn();
        }
    }

    [ClientRpc]
    private void SpawnHitEffectClientRpc(Vector3 position, Vector3 normal, bool explosion, SoundKey soundKey)
    {
        if (soundKey != SoundKey.None)
            SoundManager.PlayAt(soundKey, position);

        GameObject prefab = explosion ? explosionEffectPrefab : hitEffectPrefab;
        if (prefab != null)
        {
            GameObject effect = Instantiate(prefab, position, Quaternion.LookRotation(normal));
            Destroy(effect, 2f);
        }
    }

    /// <summary>
    /// (선택적) Rigidbody + Trigger Collider 환경을 위한 예비 충돌 처리입니다.
    /// Raycast 방식과 병행하거나, 필요에 따라 둘 중 하나를 선택할 수 있습니다.
    /// </summary>
    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer || !isInitialized || ShouldIgnore(other)) return;
        HitTarget(other, transform.position, -moveDirection);
    }
}
