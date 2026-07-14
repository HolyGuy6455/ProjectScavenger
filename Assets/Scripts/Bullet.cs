using UnityEngine;

/// <summary>
/// 발사된 총알의 이동 및 충돌 처리를 담당하는 컴포넌트입니다.
/// </summary>
public class Bullet : MonoBehaviour
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

    private Vector3 moveDirection;
    private GameObject attacker;
    private bool isInitialized = false;

    // 포물선용 변수
    private bool isParabola = false;
    private Vector3 startPosition;
    private Vector3 targetPosition;
    private float parabolaDuration;
    private float parabolaTime;
    private Vector3 previousPos;
    private float explosionRadius;

    public void Initialize(Vector3 direction, GameObject shooter)
    {
        moveDirection = direction.normalized;
        attacker = shooter;
        isParabola = false;
        isInitialized = true;

        // 지정된 시간이 지나면 자동 파괴
        Destroy(gameObject, lifeTime);
    }

    /// <summary>
    /// 목표 지점을 향해 포물선으로 날아가도록 초기화합니다.
    /// </summary>
    public void InitializeParabola(Vector3 targetPos, GameObject shooter, float expRadius)
    {
        targetPosition = targetPos;
        startPosition = transform.position;
        previousPos = startPosition;
        attacker = shooter;
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

            // 이동하기 전에 Raycast를 쏘아 충돌을 미리 감지 (총알이 얇은 벽을 뚫는 현상 방지)
            if (Physics.Raycast(transform.position, moveDirection, out RaycastHit hit, moveDistance))
            {
                HitTarget(hit.collider.gameObject, hit.point, hit.normal);
            }
            
            // 실제 이동 처리
            transform.position += moveDirection * moveDistance;
        }
    }

    private void HitTarget(GameObject target, Vector3 hitPoint, Vector3 hitNormal)
    {
        if (isParabola)
        {
            // 포물선(폭발)형 무기: OverlapSphere를 사용하여 광역 데미지 처리
            Collider[] colliders = Physics.OverlapSphere(hitPoint, explosionRadius);
            foreach (Collider col in colliders)
            {
                // 발사자는 데미지를 받지 않도록 예외 처리
                if (col.gameObject == attacker) continue;

                IDamageable damageable = col.GetComponent<IDamageable>();
                if (damageable != null)
                {
                    // 거리에 따른 데미지 감소 로직 등을 추후 추가할 수 있습니다.
                    DamageInfo damageInfo = new DamageInfo(damage, attacker, col.transform.position, hitNormal);
                    damageable.TakeDamage(damageInfo);
                }
            }
        }
        else
        {
            // 기존 직사형 무기: 단일 타겟 데미지 처리
            if (target != null)
            {
                IDamageable damageable = target.GetComponent<IDamageable>();
                
                if (damageable != null)
                {
                    DamageInfo damageInfo = new DamageInfo(damage, attacker, hitPoint, hitNormal);
                    damageable.TakeDamage(damageInfo);
                }
            }
        }

        // 관통 총알이 아니라면 충돌 후 총알 파괴
        // 포물선 폭발은 아직 구현 전이므로 도착 즉시 파괴
        if (!canPierce || isParabola)
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// (선택적) Rigidbody + Trigger Collider 환경을 위한 예비 충돌 처리입니다.
    /// Raycast 방식과 병행하거나, 필요에 따라 둘 중 하나를 선택할 수 있습니다.
    /// </summary>
    private void OnTriggerEnter(Collider other)
    {
        // 이미 발사자가 자신이 쏜 총알에 맞는 것 방지 (원한다면 레이어 설정으로도 가능)
        if (other.gameObject == attacker) return;

        HitTarget(other.gameObject, transform.position, -moveDirection);
    }
}
