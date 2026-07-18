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
