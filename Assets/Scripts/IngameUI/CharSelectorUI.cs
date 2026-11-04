using UnityEngine;
using System.Collections.Generic;

public class CharSelectorUI : MonoBehaviour
{


    [SerializeField] private List<CharSelectorSlot> slots = new List<CharSelectorSlot>();
    [SerializeField] private UnityEngine.UI.Image standingImage;

    private BuffController _buffController;
    
    private void Start()
    {
        InitializeUI();
    }

    private void OnEnable()
    {
        // Start보다 늦게 등록될 수 있으므로 예외처리
        if (CharacterStateManager.Instance != null)
            CharacterStateManager.Instance.OnCharacterChanged += UpdateUIOnCharacterChange;

        if (_buffController != null)
        {
            _buffController.OnBuffsChanged -= RefreshAllSlotBuffs;
            _buffController.OnBuffsChanged += RefreshAllSlotBuffs;
            RefreshAllSlotBuffs();
        }
    }

    private void OnDisable()
    {
        if (CharacterStateManager.Instance != null)
            CharacterStateManager.Instance.OnCharacterChanged -= UpdateUIOnCharacterChange;

        if (_buffController != null)
            _buffController.OnBuffsChanged -= RefreshAllSlotBuffs;
    }

    private void UpdateUIOnCharacterChange(CharData charData)
    {
        if (standingImage != null)
        {
            Sprite standSprite = SpriteManager.Instance.LoadSprite(charData.resourceStand);
            standingImage.sprite = standSprite;
            standingImage.enabled = standSprite != null;
        }
        RefreshAllSlotBuffs();
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
            Sprite iconSprite = SpriteManager.Instance.LoadSprite(data.resourceSlot);
            
            slots[i].Setup(data, iconSprite, i);
            slots[i].OnSlotClickedEvent -= HandleCharacterSelected;
            slots[i].OnSlotClickedEvent += HandleCharacterSelected;
        }

        BindBuffController(PlayerController.LocalInstance.GetComponent<BuffController>());

        // UI may load after a death/selection. Render the confirmed character.
        var selected = CharacterStateManager.Instance.CurrentCharData;
        if (selected != null) UpdateUIOnCharacterChange(selected);
    }

    public void BindBuffController(BuffController buffController)
    {
        if (_buffController != null)
        {
            _buffController.OnBuffsChanged -= RefreshAllSlotBuffs;
        }

        _buffController = buffController;

        if (_buffController != null && isActiveAndEnabled)
        {
            _buffController.OnBuffsChanged += RefreshAllSlotBuffs;
        }

        RefreshAllSlotBuffs();
    }

    public void RefreshAllSlotBuffs()
    {
        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i] != null)
            {
                slots[i].RefreshBuffIcons(_buffController);
            }
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
}
