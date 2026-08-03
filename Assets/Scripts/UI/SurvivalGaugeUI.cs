using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 선택된 캐릭터의 배고픔/목마름 게이지를 UI(Image Fill Amount)에 표시해주는 컴포넌트입니다.
/// </summary>
public class SurvivalGaugeUI : MonoBehaviour
{
    [Header("Hunger UI")]
    [Tooltip("배고픔을 표시할 Image (Image Type: Filled)")]
    [SerializeField] private Image hungerFillImage;

    [Header("Thirst UI")]
    [Tooltip("목마름을 표시할 Image (Image Type: Filled)")]
    [SerializeField] private Image thirstFillImage;

    private void OnEnable()
    {
        if (CharacterStateManager.Instance != null)
        {
            CharacterStateManager.Instance.OnCharacterHungerChanged += UpdateHungerBar;
            CharacterStateManager.Instance.OnCharacterThirstChanged += UpdateThirstBar;
        }
    }

    private void Start()
    {
        // OnEnable에서 CharacterStateManager가 아직 초기화되지 않았을 경우를 대비해 Start에서도 구독
        if (CharacterStateManager.Instance != null)
        {
            CharacterStateManager.Instance.OnCharacterHungerChanged -= UpdateHungerBar;
            CharacterStateManager.Instance.OnCharacterThirstChanged -= UpdateThirstBar;
            
            CharacterStateManager.Instance.OnCharacterHungerChanged += UpdateHungerBar;
            CharacterStateManager.Instance.OnCharacterThirstChanged += UpdateThirstBar;
        }
    }

    private void OnDisable()
    {
        if (CharacterStateManager.Instance != null)
        {
            CharacterStateManager.Instance.OnCharacterHungerChanged -= UpdateHungerBar;
            CharacterStateManager.Instance.OnCharacterThirstChanged -= UpdateThirstBar;
        }
    }

    private void UpdateHungerBar(int charId, float currentHunger, float maxHunger)
    {
        if (hungerFillImage != null)
        {
            hungerFillImage.fillAmount = maxHunger > 0 ? Mathf.Clamp01(currentHunger / maxHunger) : 0f;
        }
    }

    private void UpdateThirstBar(int charId, float currentThirst, float maxThirst)
    {
        if (thirstFillImage != null)
        {
            thirstFillImage.fillAmount = maxThirst > 0 ? Mathf.Clamp01(currentThirst / maxThirst) : 0f;
        }
    }
}
