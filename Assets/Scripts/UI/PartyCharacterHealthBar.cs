using UnityEngine;

/// <summary>
/// HealthBarUI를 사용하여 특정 캐릭터(Party)의 체력을 표시하는 어댑터 컴포넌트입니다.
/// HealthBarUI와 함께 사용하거나 부모/자식 오브젝트에 부착하여 사용합니다.
/// </summary>
[RequireComponent(typeof(HealthBarUI))]
public class PartyCharacterHealthBar : MonoBehaviour
{
    [Tooltip("추적할 캐릭터의 ID입니다.")]
    public int charId;

    private HealthBarUI healthBarUI;
    private bool isInitialized = false;

    private void Awake()
    {
        healthBarUI = GetComponent<HealthBarUI>();
    }

    private void Start()
    {
        if (charId > 0)
        {
            SetupCharacter(charId);
        }
    }

    public void SetupCharacter(int newCharId)
    {
        if (charId > 0 && isInitialized && CharacterStateManager.Instance != null)
        {
            CharacterStateManager.Instance.OnCharacterHealthChanged -= HandleHealthChanged;
        }

        charId = newCharId;

        if (charId > 0 && CharacterStateManager.Instance != null)
        {
            var state = CharacterStateManager.Instance.GetState(charId);
            if (state != null && healthBarUI != null)
            {
                healthBarUI.UpdateHealthBar(state.currentHealth, state.maxHealth);
            }
            
            CharacterStateManager.Instance.OnCharacterHealthChanged += HandleHealthChanged;
            isInitialized = true;
        }
    }

    private void OnDestroy()
    {
        if (isInitialized && CharacterStateManager.Instance != null)
        {
            CharacterStateManager.Instance.OnCharacterHealthChanged -= HandleHealthChanged;
        }
    }

    private void HandleHealthChanged(int changedCharId, float currentHealth, float maxHealth)
    {
        if (changedCharId == this.charId && healthBarUI != null)
        {
            healthBarUI.UpdateHealthBar(currentHealth, maxHealth);
        }
    }
}
