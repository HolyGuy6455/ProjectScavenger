using UnityEngine;

/// <summary>
/// 마우스 클릭을 통해 IDamageable 컴포넌트를 가진 오브젝트를 원거리 공격하는 테스트 스크립트입니다.
/// </summary>
public class DummyAttackTester : MonoBehaviour
{
    [Header("Attack Settings")]
    [SerializeField] private float damageAmount = 25f;
    [SerializeField] private float maxRange = 100f;
    [SerializeField] private LayerMask hitLayers = ~0; // 기본값 모든 레이어

    private Camera _mainCamera;

    private void Start()
    {
        _mainCamera = Camera.main;
        if (_mainCamera == null)
        {
            Debug.LogWarning("DummyAttackTester: 씬에 MainCamera 태그를 가진 카메라가 없습니다.");
        }
    }

    private void Update()
    {
        // 마우스 좌클릭 시 공격 테스트
        if (Input.GetMouseButtonDown(0))
        {
            PerformAttack();
        }
    }

    private void PerformAttack()
    {
        if (_mainCamera == null) return;

        // 마우스 포인터 위치로부터 광선(Ray) 생성
        Ray ray = _mainCamera.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, maxRange, hitLayers))
        {
            // 피격 지점에서 IDamageable 인터페이스 탐색 (본체 혹은 상위 부모 컴포넌트 포함)
            IDamageable damageable = hit.collider.GetComponentInParent<IDamageable>();

            if (damageable != null)
            {
                // 피격 법선 벡터 계산 (반사 벡터나 이펙트 방향용)
                Vector3 hitNormal = hit.normal;

                // 대미지 정보 패키징
                DamageInfo damageInfo = new DamageInfo(
                    amount: damageAmount,
                    attacker: gameObject,
                    hitPoint: hit.point,
                    hitNormal: hitNormal
                );

                Debug.Log($"[DummyAttackTester] {hit.collider.gameObject.name}을(를) 조준하여 {damageAmount} 대미지를 가합니다.");
                
                // 대미지 전달
                damageable.TakeDamage(damageInfo);
            }
            else
            {
                Debug.Log($"[DummyAttackTester] {hit.collider.gameObject.name}에 맞았지만 IDamageable을 찾을 수 없습니다.");
            }
        }
    }
}
