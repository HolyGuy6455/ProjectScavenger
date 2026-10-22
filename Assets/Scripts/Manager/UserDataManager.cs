using System;
using System.Collections.Generic;
using UnityEngine;
using Scavenger.Network;
using Scavenger.Network.DTOs;

public class UserDataManager : MonoBehaviour
{
    public static UserDataManager Instance { get; private set; }

    // Cached data
    public List<UserCharacterDto> OwnedCharacters { get; private set; } = new List<UserCharacterDto>();
    public int[] SelectedDeck { get; private set; } = new int[4];
    public Dictionary<int, int> OwnedUserCurrencys { get; private set; } = new Dictionary<int, int>();
    public Dictionary<int, int> OwnedGameItems { get; private set; } = new Dictionary<int, int>();
    public bool HasLoadedCharacterData { get; private set; }
    public bool HasLoadedItemData { get; private set; }
    public bool IsInitialDataLoaded => HasLoadedCharacterData && HasLoadedItemData;
    public string InitialDataError { get; private set; }
    private NetworkAuthManager authManager;

    public event Action OnCharacterDataUpdated;
    public event Action OnItemDataUpdated;
    public event Action OnCurrencyDataUpdated;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        // Start 실행 순서와 무관하게 이미 로그인된 경우에도 최초 데이터를 가져옵니다.
        authManager = NetworkAuthManager.Instance;
        if (authManager != null)
        {
            authManager.OnLoginComplete += HandleLoginComplete;
            if (!string.IsNullOrEmpty(authManager.AuthToken))
                HandleLoginComplete(true);
        }
    }

    private void OnDestroy()
    {
        if (authManager != null)
        {
            authManager.OnLoginComplete -= HandleLoginComplete;
        }
        if (Instance == this) Instance = null;
    }

    private void HandleLoginComplete(bool success)
    {
        if (success)
        {
            HasLoadedCharacterData = false;
            HasLoadedItemData = false;
            InitialDataError = null;
            FetchCharacterList();
            FetchItemList();
        }
    }

    private int _characterRequestVersion;
    private int _itemRequestVersion;

    public void FetchCharacterList() => FetchCharacterList(true);

    public void FetchCharacterList(bool showLoading, bool showErrorToast = true)
    {
        var auth = NetworkAuthManager.Instance;
        if (auth == null) return;
        string token = auth.AuthToken;
        int version = ++_characterRequestVersion;
        var request = NetworkRequestManager.CreateJsonRequest(auth.ServerUrl + "/api/game/character/list", "GET", authToken: token);
        NetworkRequestManager.Instance.Send<UserCharacterListResponse>(request, response =>
        {
            if (this == null || auth == null || auth.AuthToken != token || version != _characterRequestVersion) return;
            OwnedCharacters = response.characters;
            if (response.selectedDeck != null && response.selectedDeck.Length == 4) SelectedDeck = response.selectedDeck;
            HasLoadedCharacterData = true;
            if (IsInitialDataLoaded) InitialDataError = null;
            OnCharacterDataUpdated?.Invoke();
        }, new NetworkRequestOptions { Name = "CharacterList", ShowLoading = showLoading, ShowErrorToast = showErrorToast },
            onFailure: error =>
            {
                if (this != null && version == _characterRequestVersion) InitialDataError = error.DefaultMessage;
            }, validateResponse: response => response.characters != null);
    }

    public UserCharacterDto GetCharacter(int charId)
    {
        return OwnedCharacters.Find(c => c.charId == charId);
    }

    public void FetchItemList() => FetchItemList(true);

    public void FetchItemList(bool showLoading, bool showErrorToast = true)
    {
        var auth = NetworkAuthManager.Instance;
        if (auth == null) return;
        string token = auth.AuthToken;
        int version = ++_itemRequestVersion;
        var request = NetworkRequestManager.CreateJsonRequest(auth.ServerUrl + "/api/game/item/list", "GET", authToken: token);
        NetworkRequestManager.Instance.Send<UserCurrencyListResponse>(request, response =>
        {
            if (this == null || auth == null || auth.AuthToken != token || version != _itemRequestVersion) return;
            OwnedUserCurrencys.Clear();
            if (response.userCurrencys != null)
                foreach (var item in response.userCurrencys) OwnedUserCurrencys[item.itemId] = item.amount;
            OwnedGameItems.Clear();
            if (response.gameItems != null)
                foreach (var item in response.gameItems) OwnedGameItems[item.itemId] = item.amount;
            SyncStamina(response.stamina, response.maxStamina, response.nextStaminaRecoverySeconds);
            HasLoadedItemData = true;
            if (IsInitialDataLoaded) InitialDataError = null;
            OnItemDataUpdated?.Invoke();
            OnCurrencyDataUpdated?.Invoke();
        }, new NetworkRequestOptions { Name = "ItemList", ShowLoading = showLoading, ShowErrorToast = showErrorToast },
            onFailure: error =>
            {
                if (this != null && version == _itemRequestVersion) InitialDataError = error.DefaultMessage;
            }, validateResponse: response => response.userCurrencys != null || response.gameItems != null);
    }

    public int GetGameItemAmount(int itemId)
    {
        if (OwnedGameItems.TryGetValue(itemId, out int amount))
        {
            return amount;
        }
        return 0;
    }

    public int GetUserCurrencyAmount(int itemId)
    {
        if (OwnedUserCurrencys.TryGetValue(itemId, out int amount))
        {
            return amount;
        }
        return 0;
    }

    public void UpdateGameItems(List<UserCurrencyDto> updatedItems)
    {
        if (updatedItems == null || updatedItems.Count == 0) return;

        foreach (var item in updatedItems)
        {
            OwnedGameItems[item.itemId] = item.amount;
        }
        
        Debug.Log($"[UserDataManager] 아이템 수량 부분 갱신 완료 ({updatedItems.Count}종)");
        OnItemDataUpdated?.Invoke();
    }

    public void ApplyQuestReward(QuestRewardResponse response)
    {
        if (response == null) return;

        bool currencyUpdated = false;
        bool gameItemsUpdated = false;

        // 보상 응답의 amount는 지급량이 아니라 지급 후 보유 수량입니다.
        if (response.addedItems != null)
        {
            foreach (var item in response.addedItems)
            {
                if (item == null) continue;
                OwnedUserCurrencys[item.itemId] = item.amount;
                currencyUpdated = true;
            }
        }

        if (response.addedGameItems != null)
        {
            foreach (var item in response.addedGameItems)
            {
                if (item == null) continue;
                OwnedGameItems[item.itemId] = item.amount;
                gameItemsUpdated = true;
            }
        }

        // 이벤트 구독자는 두 종류의 잔액이 모두 반영된 상태를 조회합니다.
        if (gameItemsUpdated) OnItemDataUpdated?.Invoke();
        if (currencyUpdated) OnCurrencyDataUpdated?.Invoke();
    }

    public void SaveSelectedDeck(int[] newDeck, Action<bool> onComplete = null)
    {
        var auth = NetworkAuthManager.Instance;
        if (auth == null) { onComplete?.Invoke(false); return; }
        string token = auth.AuthToken;
        var request = NetworkRequestManager.CreateJsonRequest(auth.ServerUrl + "/api/game/character/deck", "POST",
            JsonUtility.ToJson(new SaveDeckRequest { selectedCharIds = (int[])newDeck.Clone() }), token);
        NetworkRequestManager.Instance.Send<SaveDeckResponse>(request, response =>
        {
            if (this != null && auth != null && auth.AuthToken == token) SelectedDeck = response.selectedDeck;
        }, new NetworkRequestOptions
        {
            Name = "SaveDeck", DuplicateKey = auth.Username + ":SaveDeck", DuplicatePolicy = DuplicateRequestPolicy.Reject
        }, validateResponse: response => response.selectedDeck != null && response.selectedDeck.Length == 4,
            onFinished: handle => onComplete?.Invoke(handle.Status == NetworkRequestStatus.Succeeded));
    }

    // --- 스테미너 동기화 및 로컬 계산 로직 ---
    private int _syncedStamina;
    private int _maxStamina = 300;
    private int _secondsUntilNextRecoveryAtSync;
    private System.DateTime _staminaSyncTime;

    public void SyncStamina(int current, int max, int nextRecoverySeconds)
    {
        _syncedStamina = current;
        _maxStamina = max;
        _secondsUntilNextRecoveryAtSync = nextRecoverySeconds;
        _staminaSyncTime = System.DateTime.Now;
    }

    public int GetCurrentStamina()
    {
        if (_syncedStamina >= _maxStamina) return _syncedStamina;

        double elapsedSeconds = (System.DateTime.Now - _staminaSyncTime).TotalSeconds;
        if (elapsedSeconds < _secondsUntilNextRecoveryAtSync)
        {
            return _syncedStamina;
        }

        // 첫 회복 이후 경과 시간
        double remainingElapsed = elapsedSeconds - _secondsUntilNextRecoveryAtSync;
        int recovered = 1 + (int)(remainingElapsed / 360.0); // 360초당 1회복
        int current = _syncedStamina + recovered;
        
        return Mathf.Min(current, _maxStamina);
    }

    public int GetMaxStamina() => _maxStamina;
}
