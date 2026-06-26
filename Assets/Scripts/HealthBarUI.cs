using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Screen Space - Overlay Canvas 하위의 UI 요소를 
/// 3D 월드 상의 타겟 위치(몬스터 머리 위 등)에 맞추어 화면 좌표로 추적 배치하는 컴포넌트입니다.
/// </summary>
public class HealthBarUI : MonoBehaviour
{
    [Header("References")]
    [Tooltip("체력 정보를 읽어올 Health 컴포넌트입니다.")]
    [SerializeField] private Health targetHealth;

    [Tooltip("위치를 조정할 UI의 RectTransform입니다.")]
    [SerializeField] private RectTransform positionContainer;
    
    [Tooltip("크기(게이지)를 조정할 UI Image 컴포넌트입니다.")]
    [SerializeField] private Image hpBarImage;

    [Header("Screen Space Offset")]
    [Tooltip("추적할 3D 월드 오브젝트의 Transform입니다. 비워둘 경우 부모 오브젝트의 Transform을 사용합니다.")]
    [SerializeField] private Transform targetTransform;

    [Tooltip("타겟의 3D 월드 좌표 기준 오프셋입니다 (예: 머리 위 높이 Y축 값).")]
    [SerializeField] private Vector3 worldOffset = new Vector3(0f, 2f, 0f);

    [Header("Gauge Type Settings")]
    [Tooltip("True이면 Image의 Fill Amount를 사용하고, False이면 RectTransform의 가로 너비(Width)를 직접 조정합니다.")]
    [SerializeField] private bool useFillAmount = true;

    [Tooltip("너비(Width)를 직접 조정할 때 기준이 되는 최대 너비입니다.")]
    [SerializeField] private float maxBarWidth = 100f;

    private Camera mainCamera;
    private RectTransform hpBarRectTransform;

    private void Start()
    {
        mainCamera = Camera.main;

        // 타겟 설정 자동화
        if (targetHealth == null)
        {
            targetHealth = GetComponentInParent<Health>();
        }

        if (targetTransform == null && targetHealth != null)
        {
            targetTransform = targetHealth.transform;
        }

        if (positionContainer == null && hpBarImage != null)
        {
            positionContainer = hpBarImage.rectTransform;
        }

        if (hpBarImage != null)
        {
            hpBarRectTransform = hpBarImage.rectTransform;
        }

        if (targetHealth != null)
        {
            UpdateHealthBar(targetHealth.CurrentHealth, targetHealth.MaxHealth);
            targetHealth.OnHealthChanged += UpdateHealthBar;
        }
        else
        {
            Debug.LogWarning($"[HealthBarUI] {gameObject.name}의 부모 오브젝트 등에서 Health 컴포넌트를 찾을 수 없습니다.");
        }
    }

    private void OnDestroy()
    {
        if (targetHealth != null)
        {
            targetHealth.OnHealthChanged -= UpdateHealthBar;
        }
    }

    private void LateUpdate()
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

    private void UpdateHealthBar(float currentHealth, float maxHealth)
    {
        if (hpBarImage == null) return;

        float percentage = maxHealth > 0 ? Mathf.Clamp01(currentHealth / maxHealth) : 0f;

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
}
