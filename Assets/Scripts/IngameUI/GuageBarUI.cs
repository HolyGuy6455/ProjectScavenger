using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Screen Space - Overlay Canvas 하위의 UI 요소를 
/// 3D 월드 상의 타겟 위치에 맞추어 화면 좌표로 추적 배치하는 컴포넌트입니다.
/// </summary>
public class GuageBarUI : MonoBehaviour
{
    [Header("References")]
    [Tooltip("위치를 조정할 UI의 RectTransform입니다.")]
    [SerializeField] protected RectTransform positionContainer;
    
    [Tooltip("크기(게이지)를 조정할 UI Image 컴포넌트입니다.")]
    [SerializeField] protected Image hpBarImage;

    [Header("Screen Space Offset")]
    [Tooltip("추적할 3D 월드 오브젝트의 Transform입니다. 비워둘 경우 부모 오브젝트의 Transform을 사용합니다.")]
    [SerializeField] protected Transform targetTransform;

    [Tooltip("타겟의 3D 월드 좌표 기준 오프셋입니다 (예: 머리 위 높이 Y축 값).")]
    [SerializeField] protected Vector3 worldOffset = new Vector3(0f, 2f, 0f);

    [Header("Gauge Type Settings")]
    [Tooltip("True이면 Image의 Fill Amount를 사용하고, False이면 RectTransform의 가로 너비(Width)를 직접 조정합니다.")]
    [SerializeField] protected bool useFillAmount = true;

    [Tooltip("너비(Width)를 직접 조정할 때 기준이 되는 최대 너비입니다.")]
    [SerializeField] protected float maxBarWidth = 100f;

    protected Camera mainCamera;
    protected RectTransform hpBarRectTransform;
    protected bool isInitialized = false;

    public virtual void Setup(Transform newTarget, Vector3 newOffset)
    {
        targetTransform = newTarget;
        worldOffset = newOffset;

        if (positionContainer == null && hpBarImage != null)
        {
            positionContainer = hpBarImage.rectTransform;
        }
        if (hpBarImage != null && hpBarRectTransform == null)
        {
            hpBarRectTransform = hpBarImage.rectTransform;
        }
    }

    protected virtual void Start()
    {
        mainCamera = Camera.main;

        if (positionContainer == null && hpBarImage != null)
        {
            positionContainer = hpBarImage.rectTransform;
        }

        if (hpBarImage != null)
        {
            hpBarRectTransform = hpBarImage.rectTransform;
        }

        isInitialized = true;
    }

    protected virtual void LateUpdate()
    {
        // 3D 월드 좌표를 2D 화면 픽셀 좌표로 변환하여 UI 요소의 위치를 업데이트
        if (mainCamera != null && targetTransform != null && positionContainer != null)
        {
            Vector3 worldPosition = targetTransform.position + worldOffset;
            Vector3 screenPosition = mainCamera.WorldToScreenPoint(worldPosition);

            // 카메라 뒤쪽에 있는 경우 UI를 비활성화하거나 화면 밖으로 숨김
            if (screenPosition.z < 0)
            {
                if (positionContainer.gameObject.activeSelf)
                    positionContainer.gameObject.SetActive(false);
            }
            else
            {
                if (!positionContainer.gameObject.activeSelf)
                    positionContainer.gameObject.SetActive(true);

                // Overlay Canvas 모드에서는 스크린 좌표를 RectTransform의 position에 직접 대입하여 추적 가능
                positionContainer.position = screenPosition;
            }
        }
    }

    public virtual void UpdateGuage(float currentValue, float maxValue)
    {
        if (hpBarImage == null) return;

        float percentage = maxValue > 0 ? Mathf.Clamp01(currentValue / maxValue) : 0f;

        if (useFillAmount)
        {
            hpBarImage.fillAmount = percentage;
        }
        else
        {
            if (hpBarRectTransform != null)
            {
                hpBarRectTransform.sizeDelta = new Vector2(percentage * maxBarWidth, hpBarRectTransform.sizeDelta.y);
            }
        }
    }

    public void SetColor(Color color)
    {
        if (hpBarImage != null)
        {
            hpBarImage.color = color;
        }
    }
}
