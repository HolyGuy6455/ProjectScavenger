using UnityEngine;
using System.Collections.Generic;

public class CharSelectorUI : MonoBehaviour
{


    [SerializeField] private List<CharSelectorSlot> slots = new List<CharSelectorSlot>();
    [SerializeField] private UnityEngine.UI.Image standingImage;
    
    private void Start()
    {
        InitializeUI();
    }

    private void OnEnable()
    {
        // Start보다 늦게 등록될 수 있으므로 예외처리
        if (CharacterStateManager.Instance != null)
            CharacterStateManager.Instance.OnCharacterChanged += UpdateUIOnCharacterChange;
    }

    private void OnDisable()
    {
        if (CharacterStateManager.Instance != null)
            CharacterStateManager.Instance.OnCharacterChanged -= UpdateUIOnCharacterChange;
    }

    private void UpdateUIOnCharacterChange(CharData charData)
    {
        if (standingImage != null)
        {
            Sprite standSprite = LoadSprite(charData.resourceStand);
            standingImage.sprite = standSprite;
            standingImage.enabled = standSprite != null;
        }
    }

    private void InitializeUI()
    {
        StartCoroutine(WaitAndInitializeUIRoutine());
    }

    private System.Collections.IEnumerator WaitAndInitializeUIRoutine()
    {
        if (DataManager.Instance == null)
        {
            Debug.LogWarning("[CharSelectorUI] DataManager is not initialized.");
            yield break;
        }

        // 로컬 플레이어가 네트워크에 스폰될 때까지 대기
        while (PlayerController.LocalInstance == null)
        {
            yield return null;
        }

        var deck = PlayerController.LocalInstance.GetLocalDeck();
        List<CharData> charDatas = new List<CharData>();

        for (int i = 0; i < 4; i++)
        {
            int charId = deck.GetCharId(i);
            CharData data = DataManager.Instance.GetData<CharData>("Char", charId);
            if (data != null)
            {
                charDatas.Add(data);
            }
        }
        
        for (int i = 0; i < slots.Count && i < charDatas.Count; i++)
        {
            CharData data = charDatas[i];
            Sprite iconSprite = LoadSprite(data.resourceSlot);
            
            slots[i].Setup(data, iconSprite);
            slots[i].OnSlotClickedEvent += HandleCharacterSelected;
        }

        // 초기 선택 (첫 번째 캐릭터)
        if (charDatas.Count > 0)
        {
            HandleCharacterSelected(charDatas[0]);
        }
    }

    private void HandleCharacterSelected(CharData charData)
    {
        Debug.Log($"[CharSelectorUI] 캐릭터가 선택되었습니다: {charData.name}");
        CharacterStateManager.Instance.ChangeCharacter(charData.id);
    }

    private void OnDestroy()
    {
        foreach (var slot in slots)
        {
            if (slot != null)
            {
                slot.OnSlotClickedEvent -= HandleCharacterSelected;
            }
        }
    }

    private Sprite LoadSprite(string resourceName)
    {
        if (string.IsNullOrEmpty(resourceName)) return null;
        
        string nameWithoutExt = System.IO.Path.GetFileNameWithoutExtension(resourceName);
        
        // 새로 바뀐 폴더 구조 (Resources/char) 우선 탐색
        Sprite loadedSprite = Resources.Load<Sprite>($"char/{nameWithoutExt}");
        
        // 만약 없다면 기존 루트 폴더도 혹시 모르니 탐색
        if (loadedSprite == null)
        {
            loadedSprite = Resources.Load<Sprite>(nameWithoutExt);
        }
        
        if (loadedSprite == null)
        {
             Debug.LogWarning($"[CharSelectorUI] Icon not found: {nameWithoutExt}");
        }
        
        return loadedSprite;
    }
}
