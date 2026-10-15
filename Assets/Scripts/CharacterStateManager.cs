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

    private CharacterState[] charStates = new CharacterState[4];
    private bool isInitialized = false;
    private Health playerHealth;
    private int currentCharId = -1;
    public int CurrentCharId => currentCharId;
    
    public CharData CurrentCharData { get; private set; }
    public event System.Action<CharData> OnCharacterChanged;

    public event System.Action<int, float, float> OnCharacterHealthChanged;
    public event System.Action<int, float, float> OnCharacterHungerChanged;
    public event System.Action<int, float, float> OnCharacterThirstChanged;


    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(this);
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

    public void ChangeCharacter(int id)
    {
        if (PlayerController.LocalInstance != null)
        {
            // Selection, HP and UI change only after the server accepts the slot.
            PlayerController.LocalInstance.TryChangeCharacter(id);
            return;
        }
        var state = GetState(id);
        if (state == null || state.currentHealth <= 0f) return;
        if (DataManager.Instance == null) return;
        CharData data = DataManager.Instance.GetData<CharData>("Char", id);
        if (data == null) return;

        CurrentCharData = data;
        HandleCharacterChanged(data);
        OnCharacterChanged?.Invoke(data);
    }

    public void ApplyPartyHealth(PlayerController player, PlayerController.PartyHealthState party)
    {
        if (player == null || !player.IsOwner) return;
        if (playerHealth != null) playerHealth.OnHealthChanged -= HandleHealthChanged;
        playerHealth = player.GetComponent<Health>();
        var deck = player.GetLocalDeck();
        for (int slot = 0; slot < 4; slot++)
        {
            var state = GetState(deck.GetCharId(slot));
            if (state == null) continue;
            bool changed = state.currentHealth != party.Health[slot] || state.maxHealth != party.MaxHealth[slot];
            bool hungerChanged = state.currentHunger != party.Hunger[slot];
            bool thirstChanged = state.currentThirst != party.Thirst[slot];
            state.currentHealth = party.Health[slot];
            state.maxHealth = party.MaxHealth[slot];
            state.currentHunger = party.Hunger[slot];
            state.currentThirst = party.Thirst[slot];
            if (changed) OnCharacterHealthChanged?.Invoke(state.charId, state.currentHealth, state.maxHealth);
            if (hungerChanged) OnCharacterHungerChanged?.Invoke(state.charId, state.currentHunger, state.maxHunger);
            if (thirstChanged) OnCharacterThirstChanged?.Invoke(state.charId, state.currentThirst, state.maxThirst);
        }

        int charId = deck.GetCharId(party.ActiveSlot);
        if (charId <= 0 || DataManager.Instance == null) return;
        var data = DataManager.Instance.GetData<CharData>("Char", charId);
        if (data == null || (currentCharId == charId && CurrentCharData != null)) return;
        currentCharId = charId;
        CurrentCharData = data;
        var activeState = GetState(charId);
        if (activeState != null)
        {
            OnCharacterHungerChanged?.Invoke(charId, activeState.currentHunger, activeState.maxHunger);
            OnCharacterThirstChanged?.Invoke(charId, activeState.currentThirst, activeState.maxThirst);
        }
        OnCharacterChanged?.Invoke(data);
    }

    private void HandleCharacterChanged(CharData charData)
    {
        if (currentCharId > 0 && playerHealth != null)
        {
            CharacterState oldState = GetState(currentCharId);
            if (oldState != null)
            {
                oldState.currentHealth = playerHealth.CurrentHealth;
                oldState.maxHealth = playerHealth.MaxHealth;
                OnCharacterHealthChanged?.Invoke(currentCharId, oldState.currentHealth, oldState.maxHealth);
            }
        }

        currentCharId = charData.id;

        if (playerHealth != null)
        {
            CharacterState newState = GetState(currentCharId);
            if (newState != null)
            {
                playerHealth.SetHealth(newState.currentHealth, newState.maxHealth);
            }
        }
        
        CharacterState state = GetState(currentCharId);
        if (state != null)
        {
            OnCharacterHungerChanged?.Invoke(currentCharId, state.currentHunger, state.maxHunger);
            OnCharacterThirstChanged?.Invoke(currentCharId, state.currentThirst, state.maxThirst);
        }
    }

    private void ConnectPlayerHealth()
    {
        // Network party snapshots identify the correct owner and slot together.
        // A raw Health callback can arrive before the new slot's network update.
        if (PlayerController.LocalInstance != null)
        {
            PlayerController.LocalInstance.RefreshLocalParty();
            return;
        }
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerHealth = player.GetComponent<Health>();
            if (playerHealth != null)
            {
                float defaultMax = playerHealth.MaxHealth;
                foreach (var state in charStates)
                {
                    if (state == null) continue;
                    state.maxHealth = defaultMax;
                    state.currentHealth = defaultMax;
                }

                playerHealth.OnHealthChanged += HandleHealthChanged;

                CharacterState currentState = GetState(currentCharId);
                if (currentCharId > 0 && currentState != null)
                {
                    playerHealth.SetHealth(currentState.currentHealth, currentState.maxHealth);
                    OnCharacterHealthChanged?.Invoke(currentCharId, currentState.currentHealth, currentState.maxHealth);
                }
            }
        }
    }

    private void HandleHealthChanged(float currentHealth, float maxHealth)
    {
        CharacterState state = GetState(currentCharId);
        if (currentCharId > 0 && state != null)
        {
            state.currentHealth = currentHealth;
            state.maxHealth = maxHealth;
            OnCharacterHealthChanged?.Invoke(currentCharId, currentHealth, maxHealth);
        }
    }


    [System.Serializable]
    private class TokenData
    {
        public int[] selectedCharIds;
    }

    private void InitializeStates()
    {
        if (DataManager.Instance == null) return;
        if (isInitialized) return;

        int[] defaultDeck = new int[] { 1, 2, 3, 4 };
        bool isTestMode = PlayerPrefs.GetInt("IsTestMode", 0) == 1;

        if (isTestMode)
        {
            string matchToken = PlayerPrefs.GetString("MatchToken", "");
            if (!string.IsNullOrEmpty(matchToken))
            {
                try
                {
                    TokenData tokenData = JsonUtility.FromJson<TokenData>(matchToken);
                    if (tokenData != null && tokenData.selectedCharIds != null && tokenData.selectedCharIds.Length > 0)
                    {
                        defaultDeck = tokenData.selectedCharIds;
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"Failed to parse Test MatchToken: {e.Message}");
                }
            }
        }
        else if (UserDataManager.Instance != null && UserDataManager.Instance.SelectedDeck != null && UserDataManager.Instance.SelectedDeck[0] > 0)
        {
            defaultDeck = UserDataManager.Instance.SelectedDeck;
        }

        InitializeDeckStates(defaultDeck);
    }

    public void InitializeDeckStates(int[] deckCharIds)
    {
        if (DataManager.Instance == null) return;
        
        bool isTestMode = PlayerPrefs.GetInt("IsTestMode", 0) == 1;

        for (int i = 0; i < 4; i++)
        {
            if (i < deckCharIds.Length && deckCharIds[i] > 0)
            {
                var charData = DataManager.Instance.GetData<CharData>("Char", deckCharIds[i]);
                if (charData != null)
                {
                    charStates[i] = new CharacterState(charData);
                    
                    if (isTestMode)
                    {
                        string testSkillsStr = PlayerPrefs.GetString($"TestSkills_{i}", "");
                        if (!string.IsNullOrEmpty(testSkillsStr))
                        {
                            string[] split = testSkillsStr.Split(',');
                            int[] parsedSkills = new int[split.Length];
                            for (int s = 0; s < split.Length; s++)
                            {
                                int.TryParse(split[s], out parsedSkills[s]);
                            }
                            charStates[i].SetEquippedSkills(parsedSkills);
                        }
                        else
                        {
                            charStates[i].SetEquippedSkills(new int[] { 1, 2, 3, 4 });
                        }
                    }
                    else if (UserDataManager.Instance != null)
                    {
                        var userChar = UserDataManager.Instance.GetCharacter(deckCharIds[i]);
                        if (userChar != null && userChar.equippedSkills != null)
                        {
                            charStates[i].SetEquippedSkills(userChar.equippedSkills);
                        }
                    }
                }
            }
            else
            {
                charStates[i] = null;
            }
        }
        isInitialized = true;
    }

    public CharacterState GetState(int charId)
    {
        if (!isInitialized) InitializeStates();
        foreach (var state in charStates)
        {
            if (state != null && state.charId == charId) return state;
        }
        return null;
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        foreach (var state in charStates)
        {
            if (state != null)
            {
                state.UpdateAllCooldowns(dt);
                // The server advances every player's party, including reserves.
                // Clients use the replicated snapshot for HP and survival UI.
                if (PlayerController.LocalInstance != null) continue;
                // 클리어 연출 중에는 허기 피해도 체력을 낮추지 않습니다.
                if (playerHealth != null && playerHealth.IsClearProtected) continue;
                
                // 생존 스탯(배고픔, 체력) 업데이트
                bool statsChanged = state.UpdateSurvivalStats(dt);
                if (statsChanged)
                {
                    // 해당 캐릭터가 현재 조종 중인 캐릭터라면 UI 및 플레이어 컴포넌트 동기화
                    if (state.charId == currentCharId)
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
                    else OnCharacterHealthChanged?.Invoke(state.charId, state.currentHealth, state.maxHealth);
                }
            }
        }
    }

    public void ModifyHunger(int charId, float amount)
    {
        if (PlayerController.LocalInstance != null)
        {
            PlayerController.LocalInstance.ModifyCharacterSurvivalStats(charId, amount, 0f);
            return;
        }
        CharacterState state = GetState(charId);
        if (state != null)
        {
            state.currentHunger = Mathf.Clamp(state.currentHunger + amount, 0, state.maxHunger);
            OnCharacterHungerChanged?.Invoke(charId, state.currentHunger, state.maxHunger);
        }
    }

    public void ModifyThirst(int charId, float amount)
    {
        if (PlayerController.LocalInstance != null)
        {
            PlayerController.LocalInstance.ModifyCharacterSurvivalStats(charId, 0f, amount);
            return;
        }
        CharacterState state = GetState(charId);
        if (state != null)
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
