using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class OffScreenAllyUI : MonoBehaviour
{
    [Header("References")]
    [Tooltip("전체 UI를 움직일 기준 RectTransform입니다. 비워두면 자신의 RectTransform을 사용합니다.")]
    [SerializeField] private RectTransform uiRoot;
    
    [Tooltip("화면 내에 있을 때 보여줄 Ellipse 오브젝트")]
    [SerializeField] private GameObject ellipseObj;
    
    [Tooltip("화면 밖에 있을 때 보여줄 Arrow(offscreen-ally-tag-nw) 오브젝트")]
    [SerializeField] private GameObject arrowObj;
    
    [Tooltip("색상을 변경할 Arrow의 Image 컴포넌트")]
    [SerializeField] private Image arrowImage;
    
    [Tooltip("이름을 표시할 텍스트 컴포넌트")]
    [SerializeField] private TextMeshProUGUI nameText;
    
    [Tooltip("거리를 표시할 텍스트 컴포넌트")]
    [SerializeField] private TextMeshProUGUI distanceText;

    [Header("Target Tracking")]
    [Tooltip("추적할 대상의 Transform")]
    public Transform target;
    [Tooltip("타겟 위치의 오프셋 (예: 캐릭터 머리 위)")]
    public Vector3 worldOffset = new Vector3(0, 2f, 0);

    [Header("Padding (Edge clamping)")]
    [Tooltip("좌/우 화면 여백")]
    [SerializeField] private float paddingX = 50f;
    [Tooltip("상/하 화면 여백")]
    [SerializeField] private float paddingY = 50f;

    private Camera mainCamera;
    
    // 생성된 순서에 따라 색상을 순환하기 위한 정적 변수
    private static int spawnCount = 0;
    
    // E5A93C, 52B788, 4A90E2, E63946
    private readonly Color[] playerColors = new Color[]
    {
        new Color(0.898f, 0.663f, 0.235f, 1f), // #E5A93C
        new Color(0.322f, 0.718f, 0.533f, 1f), // #52B788
        new Color(0.290f, 0.565f, 0.886f, 1f), // #4A90E2
        new Color(0.902f, 0.224f, 0.275f, 1f)  // #E63946
    };

    private void Start()
    {
        mainCamera = Camera.main;

        // 부모를 WorldUIContainer로 설정
        if (WorldUIContainer.Instance != null)
        {
            transform.SetParent(WorldUIContainer.Instance.transform, false);
        }

        if (uiRoot == null)
        {
            uiRoot = GetComponent<RectTransform>();
        }

        // Arrow 색상 설정 (생성 순서에 따라 반복)
        if (arrowImage != null)
        {
            arrowImage.color = playerColors[spawnCount % playerColors.Length];
        }
        
        spawnCount++;
    }

    private void LateUpdate()
    {
        if (target == null || mainCamera == null)
        {
            if (uiRoot.gameObject.activeSelf)
                uiRoot.gameObject.SetActive(false);
            return;
        }

        if (!uiRoot.gameObject.activeSelf)
            uiRoot.gameObject.SetActive(true);

        Vector3 worldPos = target.position + worldOffset;
        Vector3 screenPos = mainCamera.WorldToScreenPoint(worldPos);

        bool isBehindCamera = screenPos.z < 0;
        
        if (isBehindCamera)
        {
            // 카메라 뒤에 있으면 좌표를 뒤집어서 화면 중앙을 지나 뒤쪽 반대편으로 향하도록 계산
            screenPos *= -1;
        }

        // 화면 영역 판별
        bool isOffScreen = isBehindCamera || screenPos.x < 0 || screenPos.x > Screen.width || screenPos.y < 0 || screenPos.y > Screen.height;

        if (isOffScreen)
        {
            if (ellipseObj != null && ellipseObj.activeSelf) ellipseObj.SetActive(false);
            if (arrowObj != null && !arrowObj.activeSelf) arrowObj.SetActive(true);

            // 화면 중앙점
            Vector2 screenCenter = new Vector2(Screen.width / 2f, Screen.height / 2f);
            Vector2 currentPos = new Vector2(screenPos.x, screenPos.y);

            // 화면 중심에서 타겟(방향)을 향하는 벡터
            Vector2 dir = (currentPos - screenCenter).normalized;

            // 클램핑 영역의 절반 크기
            float halfWidth = Screen.width / 2f - paddingX;
            float halfHeight = Screen.height / 2f - paddingY;

            float x, y;

            if (Mathf.Abs(dir.x) < 0.0001f)
            {
                // 수직 방향
                x = 0;
                y = dir.y > 0 ? halfHeight : -halfHeight;
            }
            else
            {
                float slope = dir.y / dir.x;
                // 기울기 절대값이 화면 높이/너비 비율보다 작으면 좌/우 가장자리에 닿음
                if (Mathf.Abs(slope) < halfHeight / halfWidth)
                {
                    x = dir.x > 0 ? halfWidth : -halfWidth;
                    y = x * slope;
                }
                else // 상/하 가장자리에 닿음
                {
                    y = dir.y > 0 ? halfHeight : -halfHeight;
                    x = y / slope;
                }
            }

            Vector2 clampedPos = screenCenter + new Vector2(x, y);
            uiRoot.position = clampedPos;

            // 화살표 회전 처리
            if (arrowObj != null)
            {
                float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                // 리소스가 기본적으로 위쪽(90도 방향)을 가리킨다고 가정하므로 90도를 빼줌
                arrowObj.transform.rotation = Quaternion.Euler(0, 0, angle - 90f);
            }
        }
        else
        {
            if (ellipseObj != null && !ellipseObj.activeSelf) ellipseObj.SetActive(true);
            if (arrowObj != null && arrowObj.activeSelf) arrowObj.SetActive(false);

            uiRoot.position = screenPos;
            
            // 화면 안에 있을 때는 회전 초기화
            if (arrowObj != null)
            {
                arrowObj.transform.localRotation = Quaternion.identity;
            }
        }

        // 거리 텍스트 갱신
        if (distanceText != null)
        {
            float distance = Vector3.Distance(mainCamera.transform.position, target.position);
            distanceText.text = $"{Mathf.RoundToInt(distance)}m";
        }
    }

    /// <summary>
    /// 외부에서 타겟과 이름을 초기화할 때 호출하는 메서드입니다.
    /// </summary>
    public void Setup(Transform newTarget, string name)
    {
        target = newTarget;
        if (nameText != null)
        {
            nameText.text = name;
        }
    }
}
