using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
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
        // Subscribe to login success to fetch characters
        if (NetworkAuthManager.Instance != null)
        {
            NetworkAuthManager.Instance.OnLoginComplete += HandleLoginComplete;
        }
    }

    private void OnDestroy()
    {
        if (NetworkAuthManager.Instance != null)
        {
            NetworkAuthManager.Instance.OnLoginComplete -= HandleLoginComplete;
        }
    }

    private void HandleLoginComplete(bool success)
    {
        if (success)
        {
            FetchCharacterList();
            FetchItemList();
        }
    }

    public void FetchCharacterList()
    {
        StartCoroutine(FetchCharacterListRoutine());
    }

    private IEnumerator FetchCharacterListRoutine()
    {
        string url = $"{NetworkAuthManager.Instance.ServerUrl}/api/game/character/list";
        
        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.SetRequestHeader("Authorization", $"Bearer {NetworkAuthManager.Instance.AuthToken}");
            
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    var response = JsonUtility.FromJson<UserCharacterListResponse>(request.downloadHandler.text);
                    if (response != null && response.characters != null)
                    {
                        OwnedCharacters = response.characters;
                        if (response.selectedDeck != null && response.selectedDeck.Length == 4)
                        {
                            SelectedDeck = response.selectedDeck;
                        }
                        Debug.Log($"[UserDataManager] 캐릭터 목록 로드 성공. 보유 캐릭터 수: {OwnedCharacters.Count}");
                        OnCharacterDataUpdated?.Invoke();
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"[UserDataManager] JSON Parsing Error: {e.Message}");
                }
            }
            else
            {
                Debug.LogError($"[UserDataManager] 캐릭터 목록 로드 실패: {request.error}\n{request.downloadHandler.text}");
            }
        }
    }
    
    public UserCharacterDto GetCharacter(int charId)
    {
        return OwnedCharacters.Find(c => c.charId == charId);
    }

    public void FetchItemList()
    {
        StartCoroutine(FetchItemListRoutine());
    }

    private IEnumerator FetchItemListRoutine()
    {
        string url = $"{NetworkAuthManager.Instance.ServerUrl}/api/game/item/list";
        
        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.SetRequestHeader("Authorization", $"Bearer {NetworkAuthManager.Instance.AuthToken}");
            
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    var response = JsonUtility.FromJson<UserCurrencyListResponse>(request.downloadHandler.text);
                    if (response != null)
                    {
                        OwnedUserCurrencys.Clear();
                        if (response.userCurrencys != null)
                        {
                            foreach (var item in response.userCurrencys)
                            {
                                OwnedUserCurrencys[item.itemId] = item.amount;
                            }
                        }

                        OwnedGameItems.Clear();
                        if (response.gameItems != null)
                        {
                            foreach (var item in response.gameItems)
                            {
                                OwnedGameItems[item.itemId] = item.amount;
                            }
                        }
                        
                        SyncStamina(response.stamina, response.maxStamina, response.nextStaminaRecoverySeconds);

                        Debug.Log($"[UserDataManager] 아이템 목록 로드 성공. (UserCurrencys: {OwnedUserCurrencys.Count}, GameItems: {OwnedGameItems.Count})");
                        OnItemDataUpdated?.Invoke();
                        OnCurrencyDataUpdated?.Invoke();
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"[UserDataManager] 아이템 JSON Parsing Error: {e.Message}");
                }
            }
            else
            {
                Debug.LogError($"[UserDataManager] 아이템 목록 로드 실패: {request.error}\n{request.downloadHandler.text}");
            }
        }
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

    public void SaveSelectedDeck(int[] newDeck, Action<bool> onComplete = null)
    {
        StartCoroutine(SaveSelectedDeckRoutine(newDeck, onComplete));
    }

    private IEnumerator SaveSelectedDeckRoutine(int[] newDeck, Action<bool> onComplete)
    {
        string url = $"{NetworkAuthManager.Instance.ServerUrl}/api/game/character/deck";
        
        var requestDto = new SaveDeckRequest { selectedCharIds = newDeck };
        string jsonData = JsonUtility.ToJson(requestDto);
        
        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonData);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Authorization", $"Bearer {NetworkAuthManager.Instance.AuthToken}");
            
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                var response = JsonUtility.FromJson<SaveDeckResponse>(request.downloadHandler.text);
                if (response != null && response.selectedDeck != null)
                {
                    SelectedDeck = response.selectedDeck;
                }
                Debug.Log("[UserDataManager] 덱 저장 성공");
                onComplete?.Invoke(true);
            }
            else
            {
                Debug.LogError($"[UserDataManager] 덱 저장 실패: {request.error}\n{request.downloadHandler.text}");
                onComplete?.Invoke(false);
            }
        }
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
