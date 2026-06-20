using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 모바일 가상 조이스틱 스크립트
/// UI 드래그 입력을 감지하여 PlayerMovement 스크립트에 입력 값을 전달합니다.
/// </summary>
public class VirtualJoystick : MonoBehaviour, IDragHandler, IPointerDownHandler, IPointerUpHandler
{
    public enum JoystickType { Movement, Rotation }

    [Header("UI References")]
    [SerializeField] private RectTransform joystickBackground;
    [SerializeField] private RectTransform joystickHandle;

    [Header("Settings")]
    [SerializeField] private JoystickType joystickType = JoystickType.Movement;
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private float dragRange = 100f;

    private Vector2 _inputVector;

    private void Start()
    {
        // 배경 이미지 RectTransform 자동 할당 시도
        if (joystickBackground == null)
        {
            joystickBackground = GetComponent<RectTransform>();
        }

        // 씬 내 PlayerMovement 자동 검색 및 할당 시도
        if (playerMovement == null)
        {
            playerMovement = Object.FindAnyObjectByType<PlayerMovement>();
        }

        // 핸들 위치 중앙 정렬
        if (joystickHandle != null)
        {
            joystickHandle.anchoredPosition = Vector2.zero;
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        OnDrag(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (joystickBackground == null || joystickHandle == null) return;

        Vector2 pos;
        // 터치/마우스 위치를 조이스틱 배경 로컬 좌표계로 변환
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            joystickBackground, 
            eventData.position, 
            eventData.pressEventCamera, 
            out pos))
        {
            // 중앙(0,0) 기준 오프셋
            float bgHalfWidth = joystickBackground.rect.width / 2;

            // 드래그 범위를 배경 가로 크기 비율 혹은 직접 지정한 dragRange로 설정
            float maxDrag = dragRange > 0 ? dragRange : bgHalfWidth;

            // 크기를 제한하고 핸들 이동
            if (pos.magnitude > maxDrag)
            {
                pos = pos.normalized * maxDrag;
            }

            joystickHandle.anchoredPosition = pos;

            // 입력 벡터 구하기 (-1.0f ~ 1.0f 범위로 정규화)
            _inputVector = pos / maxDrag;

            // 캐릭터에 입력 값 주입
            if (playerMovement != null)
            {
                if (joystickType == JoystickType.Movement)
                {
                    playerMovement.SetMoveInput(_inputVector);
                }
                else
                {
                    playerMovement.SetRotationInput(_inputVector);
                }
            }
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        // 핸들 위치 중앙 원위치
        if (joystickHandle != null)
        {
            joystickHandle.anchoredPosition = Vector2.zero;
        }

        _inputVector = Vector2.zero;

        // 입력 해제
        if (playerMovement != null)
        {
            if (joystickType == JoystickType.Movement)
            {
                playerMovement.SetMoveInput(Vector2.zero);
            }
            else
            {
                playerMovement.SetRotationInput(Vector2.zero);
            }
        }
    }
}
