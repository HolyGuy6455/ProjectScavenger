using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// 모바일 가상 조이스틱 스크립트
/// UI 드래그 입력을 감지하여 PlayerMovement 스크립트에 입력 값을 전달합니다.
/// 평상시엔 숨겨져 있다가 터치 시 해당 위치에 조이스틱이 나타나며, 
/// 화면 왼쪽은 이동용, 오른쪽은 조준/회전용으로 작동합니다.
/// </summary>
public class VirtualJoystick : MonoBehaviour, IDragHandler, IPointerDownHandler, IPointerUpHandler
{
    public static List<VirtualJoystick> AllJoysticks = new List<VirtualJoystick>();

    public enum JoystickType { Movement, Rotation }

    [Header("UI References")]
    [SerializeField] private RectTransform joystickBackground;
    [SerializeField] private RectTransform joystickHandle;

    [Header("Settings")]
    [SerializeField] private JoystickType joystickType = JoystickType.Movement;
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private float dragRange = 100f;

    private Vector2 _inputVector;
    private CanvasGroup _visualGroup;

    private void OnEnable()
    {
        if (!AllJoysticks.Contains(this)) AllJoysticks.Add(this);
        
        // UI가 늦게 켜졌을 경우, 이미 스폰된 내 캐릭터(로컬 플레이어)가 있다면 즉시 연결합니다.
        if (Unity.Netcode.NetworkManager.Singleton != null && Unity.Netcode.NetworkManager.Singleton.IsListening)
        {
            if (Unity.Netcode.NetworkManager.Singleton.LocalClient != null && Unity.Netcode.NetworkManager.Singleton.LocalClient.PlayerObject != null)
            {
                var pm = Unity.Netcode.NetworkManager.Singleton.LocalClient.PlayerObject.GetComponent<PlayerMovement>();
                if (pm != null)
                {
                    SetPlayer(pm);
                }
            }
        }
    }

    private void OnDisable()
    {
        AllJoysticks.Remove(this);
    }

    private void Start()
    {
        RectTransform myRect = GetComponent<RectTransform>();
        Image myImage = GetComponent<Image>();

        // 1. 조이스틱 배경 이미지가 자신일 경우, 시각적 요소를 분리하여 자식으로 만듭니다.
        // 터치 영역은 화면 절반을 차지하도록 하고, 시각적 요소는 터치한 곳으로 이동시키기 위함입니다.
        if (myImage != null && (joystickBackground == null || joystickBackground == myRect))
        {
            GameObject visBgObj = new GameObject("VisualBackground");
            RectTransform visRect = visBgObj.AddComponent<RectTransform>();
            visBgObj.transform.SetParent(transform, false);
            
            Image visImage = visBgObj.AddComponent<Image>();
            visImage.sprite = myImage.sprite;
            visImage.color = myImage.color;
            visImage.material = myImage.material;
            visImage.raycastTarget = false;

            visRect.sizeDelta = myRect.sizeDelta;
            visRect.anchoredPosition = Vector2.zero;

            // 원래의 Image는 터치 영역으로만 동작하도록 투명하게 만듭니다.
            myImage.color = new Color(0, 0, 0, 0);

            // 핸들이 있다면 새로 만든 시각적 배경의 자식으로 이동
            if (joystickHandle != null)
            {
                joystickHandle.SetParent(visRect, false);
            }

            joystickBackground = visRect;
        }

        if (joystickBackground == null)
        {
            joystickBackground = GetComponent<RectTransform>();
        }

        // 2. 터치 영역을 화면 절반으로 확장
        if (myRect != null)
        {
            if (joystickType == JoystickType.Movement)
            {
                // 왼쪽 절반 (이동용)
                myRect.anchorMin = new Vector2(0, 0);
                myRect.anchorMax = new Vector2(0.5f, 1);
            }
            else
            {
                // 오른쪽 절반 (조준/회전용)
                myRect.anchorMin = new Vector2(0.5f, 0);
                myRect.anchorMax = new Vector2(1, 1);
            }
            
            // 여백 0, 사이즈 0으로 두어 anchor 영역에 꽉 차게 설정
            myRect.offsetMin = Vector2.zero;
            myRect.offsetMax = Vector2.zero;
            myRect.anchoredPosition = Vector2.zero;
            myRect.sizeDelta = Vector2.zero;
        }

        // 3. 평상시 숨기기를 위한 CanvasGroup 추가
        if (joystickBackground != null)
        {
            _visualGroup = joystickBackground.gameObject.GetComponent<CanvasGroup>();
            if (_visualGroup == null)
            {
                _visualGroup = joystickBackground.gameObject.AddComponent<CanvasGroup>();
            }
            _visualGroup.alpha = 0f; // 초기 상태 숨김
            _visualGroup.blocksRaycasts = false;
        }

        // 씬에서 무조건 찾지 않고 외부에서 SetPlayer를 통해 연결해주기를 기다립니다.
        if (playerMovement != null)
        {
            SetPlayer(playerMovement);
        }

        // 핸들 위치 중앙 정렬
        if (joystickHandle != null)
        {
            joystickHandle.anchoredPosition = Vector2.zero;
        }
    }

    public void SetPlayer(PlayerMovement newPlayer)
    {
        playerMovement = newPlayer;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        // 터치한 위치로 조이스틱 배경 이동
        if (joystickBackground != null)
        {
            Vector2 localPos;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                GetComponent<RectTransform>(), 
                eventData.position, 
                eventData.pressEventCamera, 
                out localPos))
            {
                joystickBackground.localPosition = new Vector3(localPos.x, localPos.y, 0f);
            }
        }

        // 터치 시 조이스틱 표시
        if (_visualGroup != null)
        {
            _visualGroup.alpha = 1f;
        }

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
        // 터치 뗄 때 조이스틱 숨김
        if (_visualGroup != null)
        {
            _visualGroup.alpha = 0f;
        }

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
