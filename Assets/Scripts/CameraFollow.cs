using UnityEngine;

/// <summary>
/// 쿼터뷰 카메라 추적 스크립트
/// 시작 시 플레이어와의 오프셋을 기억하고, 매 프레임 그 오프셋을 유지하며 따라갑니다.
/// </summary>
public class CameraFollow : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private float smoothSpeed = 8f;
    [SerializeField] private float lookOffsetMultiplier = 3f; // 조이스틱 입력에 따라 카메라가 이동하는 최대 거리
    [SerializeField] private Transform playerOffsetPosition; // 인스펙터에서 직접 할당할 수 있는 오프셋 기준 오브젝트

    private Vector3 _offset;
    private PlayerController _playerController;
    private System.Collections.Generic.HashSet<Scavenger.Environment.ObscurableCeiling> obscuredCeilings = new System.Collections.Generic.HashSet<Scavenger.Environment.ObscurableCeiling>();

    private void Start()
    {
        // 씬에서 무조건 태그로 찾지 않고, 외부에서 SetTarget()을 호출해주길 기다립니다.
        if (target != null)
        {
            SetTarget(target);
        }
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
        
        if (playerOffsetPosition != null)
        {
            _offset = transform.position - playerOffsetPosition.position;
        }
        else
        {
            _offset = transform.position - target.position;
        }
        
        _playerController = target.GetComponent<PlayerController>();
    }

    private void LateUpdate()
    {
        if (target == null) return;

        Vector3 desiredPos = target.position + _offset;
        
        if (_playerController != null)
        {
            Vector2 rotInput = _playerController.JoystickRotationInput;
            desiredPos += new Vector3(rotInput.x, 0f, rotInput.y) * lookOffsetMultiplier;
        }

        transform.position = Vector3.Lerp(transform.position, desiredPos, smoothSpeed * Time.deltaTime);
        
        HandleOcclusion();
    }

    private void HandleOcclusion()
    {
        if (target == null) return;
        
        Vector3 camPos = transform.position;
        Vector3 playerPos = target.position + Vector3.up * 1.5f; // Aim at chest/head
        Vector3 dir = playerPos - camPos;
        float dist = dir.magnitude;
        
        RaycastHit[] hits = Physics.RaycastAll(camPos, dir.normalized, dist);
        System.Collections.Generic.HashSet<Scavenger.Environment.ObscurableCeiling> currentHits = new System.Collections.Generic.HashSet<Scavenger.Environment.ObscurableCeiling>();
        
        foreach (var hit in hits)
        {
            var oc = hit.collider.GetComponent<Scavenger.Environment.ObscurableCeiling>();
            if (oc != null)
            {
                currentHits.Add(oc);
                oc.SetObscured(true);
            }
        }
        
        foreach (var oc in obscuredCeilings)
        {
            if (!currentHits.Contains(oc) && oc != null)
            {
                oc.SetObscured(false);
            }
        }
        
        obscuredCeilings = currentHits;
    }
}
