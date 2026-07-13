using UnityEngine;
using System.Collections.Generic;

public class CharSelectorUI : MonoBehaviour
{
    public static event System.Action<CharData> OnCharacterChanged;

    [SerializeField] private List<CharSelectorSlot> slots = new List<CharSelectorSlot>();
    [SerializeField] private UnityEngine.UI.Image standingImage;
    
    private void Start()
    {
        InitializeUI();
    }

    private void InitializeUI()
    {
        if (DataManager.Instance == null)
        {
            Debug.LogWarning("[CharSelectorUI] DataManager is not initialized.");
            return;
        }

        // DataManager에서 모든 Char 데이터를 가져옴
        List<CharData> charDatas = DataManager.Instance.GetAllData<CharData>("Char");
        
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
        // TODO: 캐릭터 선택 시 실제 인게임 처리 로직 (애니메이션, 모델 교체 등은 아직 미구현)
        Debug.Log($"[CharSelectorUI] 캐릭터가 선택되었습니다: {charData.name}");

        OnCharacterChanged?.Invoke(charData);

        // 캐릭터에 지정된 총기 세팅
        if (DataManager.Instance != null)
        {
            GunData assignedGun = DataManager.Instance.GetData<GunData>("Gun", charData.gunData);
            if (assignedGun != null)
            {
                Shooter playerShooter = FindObjectOfType<Shooter>();
                if (playerShooter != null)
                {
                    playerShooter.EquipGun(assignedGun);
                    Debug.Log($"[CharSelectorUI] {charData.name}에게 {assignedGun.gunName}이(가) 장착되었습니다.");
                }
            }
        }

        if (standingImage != null)
        {
            Sprite standSprite = LoadSprite(charData.resourceStand);
            standingImage.sprite = standSprite;
            standingImage.enabled = standSprite != null;
        }
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
        // Resources 폴더에 해당 이미지가 있다고 가정하고 로드 (예: Resources/item_stagnag)
        Sprite loadedSprite = Resources.Load<Sprite>(nameWithoutExt);
        
        if (loadedSprite == null)
        {
             Debug.LogWarning($"[CharSelectorUI] Icon not found: {nameWithoutExt}");
        }
        
        return loadedSprite;
    }
}
