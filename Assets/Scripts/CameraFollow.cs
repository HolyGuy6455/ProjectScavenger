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
    private PlayerMovement _playerMovement;

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
        
        _playerMovement = target.GetComponent<PlayerMovement>();
    }

    private void LateUpdate()
    {
        if (target == null) return;

        Vector3 desiredPos = target.position + _offset;
        
        if (_playerMovement != null)
        {
            Vector2 rotInput = _playerMovement.JoystickRotationInput;
            desiredPos += new Vector3(rotInput.x, 0f, rotInput.y) * lookOffsetMultiplier;
        }

        transform.position = Vector3.Lerp(transform.position, desiredPos, smoothSpeed * Time.deltaTime);
    }
}
