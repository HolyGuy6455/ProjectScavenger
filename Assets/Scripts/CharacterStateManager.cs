using UnityEngine;
using System.Collections.Generic;

public class CharacterStateManager : MonoBehaviour
{
    private static CharacterStateManager _instance;
    public static CharacterStateManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<CharacterStateManager>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("CharacterStateManager");
                    _instance = go.AddComponent<CharacterStateManager>();
                }
            }
            return _instance;
        }
    }

    private Dictionary<int, CharacterState> charStates = new Dictionary<int, CharacterState>();
    private Health playerHealth;
    private int currentCharId = -1;
    public int CurrentCharId => currentCharId;

    public event System.Action<int, float, float> OnCharacterHealthChanged;
    public event System.Action<int, float, float> OnCharacterHungerChanged;
    public event System.Action<int, float, float> OnCharacterThirstChanged;


    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        InitializeStates();
        ConnectPlayerHealth();
    }

    private void OnEnable()
    {
        CharSelectorUI.OnCharacterChanged += HandleCharacterChanged;
    }

    private void OnDisable()
    {
        CharSelectorUI.OnCharacterChanged -= HandleCharacterChanged;
    }

    private void HandleCharacterChanged(CharData charData)
    {
        if (currentCharId > 0 && playerHealth != null)
        {
            if (charStates.TryGetValue(currentCharId, out CharacterState oldState))
            {
                oldState.currentHealth = playerHealth.CurrentHealth;
                oldState.maxHealth = playerHealth.MaxHealth;
                OnCharacterHealthChanged?.Invoke(currentCharId, oldState.currentHealth, oldState.maxHealth);
            }
        }

        currentCharId = charData.id;

        if (playerHealth != null)
        {
            if (charStates.TryGetValue(currentCharId, out CharacterState newState))
            {
                playerHealth.SetHealth(newState.currentHealth, newState.maxHealth);
            }
        }
        
        if (charStates.TryGetValue(currentCharId, out CharacterState state))
        {
            OnCharacterHungerChanged?.Invoke(currentCharId, state.currentHunger, state.maxHunger);
            OnCharacterThirstChanged?.Invoke(currentCharId, state.currentThirst, state.maxThirst);
        }
    }

    private void ConnectPlayerHealth()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerHealth = player.GetComponent<Health>();
            if (playerHealth != null)
            {
                float defaultMax = playerHealth.MaxHealth;
                foreach (var state in charStates.Values)
                {
                    state.maxHealth = defaultMax;
                    state.currentHealth = defaultMax;
                }

                playerHealth.OnHealthChanged += HandleHealthChanged;

                if (currentCharId > 0 && charStates.TryGetValue(currentCharId, out CharacterState currentState))
                {
                    playerHealth.SetHealth(currentState.currentHealth, currentState.maxHealth);
                    OnCharacterHealthChanged?.Invoke(currentCharId, currentState.currentHealth, currentState.maxHealth);
                }
            }
        }
    }

    private void HandleHealthChanged(float currentHealth, float maxHealth)
    {
        if (currentCharId > 0 && charStates.TryGetValue(currentCharId, out CharacterState state))
        {
            state.currentHealth = currentHealth;
            state.maxHealth = maxHealth;
            OnCharacterHealthChanged?.Invoke(currentCharId, currentHealth, maxHealth);
        }
    }


    private void InitializeStates()
    {
        if (DataManager.Instance == null) return;

        var charDataList = DataManager.Instance.GetAllData<CharData>("Char");
        if (charDataList != null)
        {
            foreach (var charData in charDataList)
            {
                if (charData != null)
                {
                    charStates[charData.id] = new CharacterState(charData);
                }
            }
        }
    }

    public CharacterState GetState(int charId)
    {
        if (charStates.Count == 0) InitializeStates();
        if (charStates.TryGetValue(charId, out CharacterState state)) return state;
        return null;
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        foreach (var statePair in charStates)
        {
            CharacterState state = statePair.Value;
            if (state != null)
            {
                state.UpdateAllCooldowns(dt);
                
                // 생존 스탯(배고픔, 체력) 업데이트
                bool statsChanged = state.UpdateSurvivalStats(dt);
                if (statsChanged)
                {
                    // 해당 캐릭터가 현재 조종 중인 캐릭터라면 UI 및 플레이어 컴포넌트 동기화
                    if (statePair.Key == currentCharId)
                    {
                        if (playerHealth != null)
                        {
                            // Health(네트워크 컴포넌트)에 로컬 state 체력을 덮어씌웁니다. 
                            // 단, SetHealth는 기본적으로 서버에서만 동작합니다.
                            playerHealth.SetHealth(state.currentHealth, state.maxHealth);
                        }
                        
                        OnCharacterHungerChanged?.Invoke(currentCharId, state.currentHunger, state.maxHunger);
                        OnCharacterHealthChanged?.Invoke(currentCharId, state.currentHealth, state.maxHealth);
                    }
                }
            }
        }
    }

    public void ModifyHunger(int charId, float amount)
    {
        if (charStates.TryGetValue(charId, out CharacterState state))
        {
            state.currentHunger = Mathf.Clamp(state.currentHunger + amount, 0, state.maxHunger);
            OnCharacterHungerChanged?.Invoke(charId, state.currentHunger, state.maxHunger);
        }
    }

    public void ModifyThirst(int charId, float amount)
    {
        if (charStates.TryGetValue(charId, out CharacterState state))
        {
            state.currentThirst = Mathf.Clamp(state.currentThirst + amount, 0, state.maxThirst);
            OnCharacterThirstChanged?.Invoke(charId, state.currentThirst, state.maxThirst);
        }
    }

    public void ModifyCurrentCharacterHunger(float amount)
    {
        if (currentCharId > 0) ModifyHunger(currentCharId, amount);
    }

    public void ModifyCurrentCharacterThirst(float amount)
    {
        if (currentCharId > 0) ModifyThirst(currentCharId, amount);
    }
}
