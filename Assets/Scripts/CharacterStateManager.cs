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

    public event System.Action<int, float, float> OnCharacterHealthChanged;


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
            }
        }
    }
}
