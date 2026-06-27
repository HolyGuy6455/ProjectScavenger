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

    /// <summary>
    /// 발사 직후 총알의 진행 방향과 발사자 정보를 초기화합니다.
    /// </summary>
    /// <param name="direction">이동 방향</param>
    /// <param name="shooter">발사한 게임 오브젝트</param>
    public void Initialize(Vector3 direction, GameObject shooter)
    {
        moveDirection = direction.normalized;
        attacker = shooter;
        isInitialized = true;

        // 지정된 시간이 지나면 자동 파괴
        Destroy(gameObject, lifeTime);
    }

    private void Update()
    {
        if (!isInitialized) return;

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

    /// <summary>
    /// 대상과 충돌했을 때의 처리입니다.
    /// </summary>
    private void HitTarget(GameObject target, Vector3 hitPoint, Vector3 hitNormal)
    {
        // 대상이 데미지를 받을 수 있는 컴포넌트(Health 등)를 가지고 있는지 확인
        IDamageable damageable = target.GetComponent<IDamageable>();
        
        if (damageable != null)
        {
            // DamageInfo 생성 및 전달
            DamageInfo damageInfo = new DamageInfo(damage, attacker, hitPoint, hitNormal);
            damageable.TakeDamage(damageInfo);
        }

        // 관통 총알이 아니라면 충돌 후 총알 파괴
        if (!canPierce)
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
