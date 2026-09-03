using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using Scavenger.Network.DTOs;
using Scavenger.UI;

public class TrainCharacterPanel : MonoBehaviour
{
    [SerializeField] private Transform leftCharacterRack;
    [SerializeField] private GameObject polaroidPrefab;
    
    [Header("Character Detail UI")]
    [SerializeField] private TextMeshProUGUI charNameText;
    [SerializeField] private Image characterImage;
    [SerializeField] private TextMeshProUGUI levelAmountText;
    [SerializeField] private GameObject[] roleTags; // 0: Infiltrator, 1: Striker, 2: Anchor, 3: Suppressor
    
    [Header("Skills UI")]
    [SerializeField] private TrainCharacterSkillNode[] skillNodes; // 4 nodes

    
    private List<GameObject> _spawnedPolaroids = new List<GameObject>();
    private readonly float[] _polaroidRotations = { 2f, -3f, 1.5f, -2f };
    private int _selectedCharId = -1;

    private void Awake()
    {
        if (skillNodes != null)
        {
            foreach (var node in skillNodes)
            {
                if (node != null)
                {
                    node.OnNodeClicked += HandleSkillNodeClicked;
                }
            }
        }
    }

    private void OnEnable()
    {
        if (UserDataManager.Instance != null)
        {
            UserDataManager.Instance.OnCharacterDataUpdated += RefreshUI;
            // 만약 이미 데이터가 로드되어 있다면 바로 UI 갱신
            if (UserDataManager.Instance.OwnedCharacters.Count > 0)
            {
                RefreshUI();
            }
        }
    }

    private void OnDisable()
    {
        _selectedCharId = -1;
        if (UserDataManager.Instance != null)
        {
            UserDataManager.Instance.OnCharacterDataUpdated -= RefreshUI;
        }
    }

    private void RefreshUI()
    {
        ClearRack();

        if (UserDataManager.Instance == null || DataManager.Instance == null) return;

        var ownedCharacters = UserDataManager.Instance.OwnedCharacters;
        
        UserCharacterDto charToSelect = null;
        
        foreach (var charDto in ownedCharacters)
        {
            CharData masterData = DataManager.Instance.GetData<CharData>("Char", charDto.charId);
            if (masterData != null)
            {
                SpawnPolaroid(charDto, masterData);
                
                if (charToSelect == null)
                {
                    charToSelect = charDto;
                }
                
                if (charDto.charId == _selectedCharId)
                {
                    charToSelect = charDto;
                }
            }
        }
        
        if (charToSelect != null)
        {
            CharData masterData = DataManager.Instance.GetData<CharData>("Char", charToSelect.charId);
            HandlePolaroidClicked(charToSelect, masterData);
        }
    }

    private void ClearRack()
    {
        foreach (var obj in _spawnedPolaroids)
        {
            if (obj != null) Destroy(obj);
        }
        _spawnedPolaroids.Clear();

        // 에디터에서 미리 만들어둔 더미 오브젝트가 있다면 삭제
        foreach (Transform child in leftCharacterRack)
        {
            Destroy(child.gameObject);
        }
    }

    private void SpawnPolaroid(UserCharacterDto charDto, CharData masterData)
    {
        if (polaroidPrefab == null || leftCharacterRack == null) return;

        GameObject obj = Instantiate(polaroidPrefab, leftCharacterRack);
        
        float zRot = _polaroidRotations[_spawnedPolaroids.Count % _polaroidRotations.Length];
        obj.transform.localRotation = Quaternion.Euler(0, 0, zRot);

        _spawnedPolaroids.Add(obj);

        TrainCharacterPolaroid polaroid = obj.GetComponent<TrainCharacterPolaroid>();
        if (polaroid != null)
        {
            Sprite icon = LoadSprite(masterData.resourceSlot);
            polaroid.Setup(charDto, masterData, icon);
            polaroid.OnPolaroidClicked += HandlePolaroidClicked;
        }
    }

    private void HandlePolaroidClicked(UserCharacterDto charDto, CharData masterData)
    {
        _selectedCharId = charDto.charId;
        Debug.Log($"[TrainCharacterPanel] 캐릭터 선택됨: {masterData.name} (Lv.{charDto.level})");
        
        if (charNameText != null) charNameText.text = masterData.name;
        if (levelAmountText != null) levelAmountText.text = charDto.level.ToString();
        
        if (characterImage != null)
        {
            Sprite standSprite = LoadSprite(masterData.resourceStand);
            if (standSprite != null)
            {
                characterImage.sprite = standSprite;
                characterImage.enabled = true;
            }
            else
            {
                characterImage.enabled = false;
            }
        }
        
        if (roleTags != null && roleTags.Length > 0)
        {
            for (int i = 0; i < roleTags.Length; i++)
            {
                if (roleTags[i] != null)
                {
                    // CharRole enum: Infiltrator=1, Striker=2, Anchor=3, Suppressor=4
                    roleTags[i].SetActive((int)masterData.role == (i + 1));
                }
            }
        }
        
        if (skillNodes != null && charDto.equippedSkills != null)
        {
            for (int i = 0; i < skillNodes.Length; i++)
            {
                if (skillNodes[i] != null)
                {
                    if (i < charDto.equippedSkills.Length && charDto.equippedSkills[i] != 0)
                    {
                        int skillId = charDto.equippedSkills[i];
                        SkillData skillData = DataManager.Instance.GetData<SkillData>("Skill", skillId);
                        skillNodes[i].Setup(skillData, i);
                    }
                    else
                    {
                        skillNodes[i].Setup(null, i);
                    }
                }
            }
        }
    }

    private void HandleSkillNodeClicked(int slotIndex)
    {
        if (_selectedCharId != -1)
        {
            var popup = OutGameUIManager.Instance.ShowPopup<SkillSelectPopup>(OutGamePopupType.SkillSelect);
            if (popup != null)
            {
                popup.Open(_selectedCharId, slotIndex);
            }
        }
    }

    private Sprite LoadSprite(string resourceName)
    {
        if (string.IsNullOrEmpty(resourceName)) return null;
        string nameWithoutExt = System.IO.Path.GetFileNameWithoutExtension(resourceName);
        
        Sprite loadedSprite = Resources.Load<Sprite>($"char/{nameWithoutExt}");
        if (loadedSprite == null)
        {
            loadedSprite = Resources.Load<Sprite>(nameWithoutExt);
        }
        return loadedSprite;
    }
}
