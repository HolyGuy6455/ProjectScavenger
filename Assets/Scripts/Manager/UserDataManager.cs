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

    public event Action OnCharacterDataUpdated;

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
                        Debug.Log($"[UserDataManager] 캘릭터 목록 로드 성공. 보유 캐릭터 수: {OwnedCharacters.Count}");
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
}
