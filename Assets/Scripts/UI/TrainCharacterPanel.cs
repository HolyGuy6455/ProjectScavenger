using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using Scavenger.Network;
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
    [SerializeField] private Image xpFill;
    [SerializeField] private TextMeshProUGUI xpRatioText;
    [SerializeField] private GameObject[] roleTags; // 0: Infiltrator, 1: Striker, 2: Anchor, 3: Suppressor
    
    [Header("Skills UI")]
    [SerializeField] private TrainCharacterSkillNode[] skillNodes; // 4 nodes

    [Header("Level Scroll")]
    [SerializeField] private LevelScrollController levelScrollController;
    [SerializeField] private Transform levelScrollContent;
    [SerializeField] private GameObject trainCharacterLevelScrollTextPrefab;

    [Header("Level Cost")]
    [SerializeField] private TrainCharacterLevelingCost[] levelingCostSlots;
    
    private List<TrainCharacterLevelScrollItem> _levelScrollItems = new List<TrainCharacterLevelScrollItem>();
    private List<GameObject> _spawnedPolaroids = new List<GameObject>();
    private readonly float[] _polaroidRotations = { 2f, -3f, 1.5f, -2f };
    private int _selectedCharId = -1;
    private int _characterIdOnNextOpen = -1;
    private int _currentTargetLevel = -1;
    
    [Header("Level Up UI")]
    [SerializeField] private Button levelUpButton;

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
        
        if (levelScrollController != null)
        {
            levelScrollController.OnLevelSelected += HandleLevelSelected;
        }
    }

    private void Start()
    {
        InitLevelScroll();

        // 첫 진입의 OnEnable/바로가기 선택은 Start보다 먼저 실행됩니다.
        // 스크롤 생성 후에도 그 캐릭터의 레벨과 비용을 표시합니다.
        if (_selectedCharId > 0)
        {
            SelectCharacter(_selectedCharId);
        }
    }

    private void OnDestroy()
    {
        if (levelScrollController != null)
        {
            levelScrollController.OnLevelSelected -= HandleLevelSelected;
        }
    }

    private void InitLevelScroll()
    {
        if (levelScrollContent == null || trainCharacterLevelScrollTextPrefab == null) return;
        
        foreach (Transform child in levelScrollContent)
        {
            Destroy(child.gameObject);
        }
        _levelScrollItems.Clear();

        var allLevelData = DataManager.Instance.GetAllData<LevelData>("Level");
        if (allLevelData != null)
        {
            foreach (var levelData in allLevelData)
            {
                GameObject obj = Instantiate(trainCharacterLevelScrollTextPrefab, levelScrollContent);
                TrainCharacterLevelScrollItem item = obj.GetComponent<TrainCharacterLevelScrollItem>();
                if (item != null)
                {
                    item.Setup(levelData);
                    _levelScrollItems.Add(item);
                }
            }

            if (levelScrollContent is RectTransform contentRect && levelScrollController != null)
            {
                var controllerRect = levelScrollController.transform as RectTransform;
                if (controllerRect != null)
                {
                    float viewWidth = controllerRect.rect.width;
                    var layoutGroup = levelScrollContent.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>();
                    
                    float contentWidth = allLevelData.Count * 100f; // 기본 아이템 가로 길이(예: 100)

                    if (layoutGroup != null)
                    {
                        int padding = Mathf.RoundToInt(viewWidth / 2f);
                        layoutGroup.padding.left = padding;
                        layoutGroup.padding.right = padding;

                        contentWidth += (allLevelData.Count - 1) * layoutGroup.spacing;
                        contentWidth += layoutGroup.padding.left + layoutGroup.padding.right;
                    }
                    else
                    {
                        contentWidth += viewWidth;
                    }
                    
                    contentRect.sizeDelta = new Vector2(contentWidth, contentRect.sizeDelta.y);
                }
            }
        }
    }

    private void OnEnable()
    {
        if (_characterIdOnNextOpen >= 0)
        {
            _selectedCharId = _characterIdOnNextOpen;
            _characterIdOnNextOpen = -1;
        }

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
        _viewVersion++;
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
            ApplyCharacterSelection(charToSelect, masterData);
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
        ApplyCharacterSelection(charDto, masterData);
    }

    public void SetCharacterForNextOpen(int charId)
    {
        _characterIdOnNextOpen = charId;
    }

    public void SelectCharacter(int charId)
    {
        // 보유 캐릭터 데이터가 늦게 도착해도 RefreshUI에서 이 선택을 사용합니다.
        _selectedCharId = charId;
        if (UserDataManager.Instance == null || DataManager.Instance == null) return;

        var charDto = UserDataManager.Instance.GetCharacter(charId);
        var masterData = charDto != null ? DataManager.Instance.GetData<CharData>("Char", charId) : null;
        if (charDto != null && masterData != null)
        {
            ApplyCharacterSelection(charDto, masterData);
        }
        else
        {
            // 0은 특정 캐릭터 지정 없음. 미보유 캐릭터도 기본 보유 캐릭터를 표시합니다.
            RefreshUI();
        }
    }

    private void ApplyCharacterSelection(UserCharacterDto charDto, CharData masterData)
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
        
        if (levelScrollController != null)
        {
            levelScrollController.Initialize(_levelScrollItems, charDto.level);
        }

        if (xpFill != null || xpRatioText != null)
        {
            var levelData = DataManager.Instance.GetData<LevelData>("Level", charDto.level);
            if (levelData != null && levelData.requiredExp > 0)
            {
                float ratio = (float)charDto.exp / levelData.requiredExp;
                if (xpFill != null) xpFill.fillAmount = ratio;
                if (xpRatioText != null) xpRatioText.text = $"{charDto.exp} / {levelData.requiredExp}";
            }
            else
            {
                if (xpFill != null) xpFill.fillAmount = 1f;
                if (xpRatioText != null) xpRatioText.text = "MAX";
            }
        }
    }

    private void HandleLevelSelected(int targetLevel)
    {
        if (_selectedCharId == -1 || levelingCostSlots == null) return;

        var userChar = UserDataManager.Instance.GetCharacter(_selectedCharId);
        if (userChar == null) return;

        _currentTargetLevel = targetLevel;

        Dictionary<int, int> totalCosts = new Dictionary<int, int>();
        int currentExp = userChar.exp;

        // Calculate costs from current level to targetLevel - 1
        for (int i = userChar.level; i < targetLevel; i++)
        {
            var levelData = DataManager.Instance.GetData<LevelData>("Level", i);
            if (levelData == null) continue;
            
            int expNeededForThisLevel = levelData.requiredExp - currentExp;
            if (expNeededForThisLevel > 0)
            {
                int reqItemId = levelData.requiredItemId;
                if (reqItemId == 0) continue;
                
                var itemData = DataManager.Instance.GetData<ItemData>("Item", reqItemId);
                if (itemData == null || itemData.serverFunc != 1 || itemData.serverValue <= 0) continue;
                
                int expPerItem = itemData.serverValue;
                int itemsNeeded = (expNeededForThisLevel + expPerItem - 1) / expPerItem;

                if (totalCosts.ContainsKey(reqItemId))
                    totalCosts[reqItemId] += itemsNeeded;
                else
                    totalCosts[reqItemId] = itemsNeeded;

                currentExp += itemsNeeded * expPerItem;
            }
            
            currentExp -= levelData.requiredExp;
        }

        int slotIndex = 0;
        foreach (var kvp in totalCosts)
        {
            if (slotIndex >= levelingCostSlots.Length) break;
            
            int itemId = kvp.Key;
            int requiredAmount = kvp.Value;
            
            var itemData = DataManager.Instance.GetData<ItemData>("Item", itemId);
            if (itemData != null)
            {
                int currentAmount = UserDataManager.Instance.GetGameItemAmount(itemId);
                Sprite icon = LoadSprite(itemData.uiResource);
                
                if (levelingCostSlots[slotIndex] != null)
                {
                    levelingCostSlots[slotIndex].Setup(icon, requiredAmount, currentAmount);
                }
                slotIndex++;
            }
        }

        // Hide unused slots
        for (int i = slotIndex; i < levelingCostSlots.Length; i++)
        {
            if (levelingCostSlots[i] != null)
            {
                levelingCostSlots[i].gameObject.SetActive(false);
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

    public void OnClickLevelUp()
    {
        if (_selectedCharId == -1 || _currentTargetLevel == -1) return;

        var userChar = UserDataManager.Instance.GetCharacter(_selectedCharId);
        if (userChar == null || userChar.level >= _currentTargetLevel) return;

        SendLevelUpRequest();
    }

    private int _viewVersion;

    private void SendLevelUpRequest()
    {
        var auth = NetworkAuthManager.Instance;
        var userData = UserDataManager.Instance;
        if (auth == null || userData == null) return;
        int charId = _selectedCharId;
        int targetLevel = _currentTargetLevel;
        int version = _viewVersion;
        string token = auth.AuthToken;
        var request = NetworkRequestManager.CreateJsonRequest(auth.ServerUrl + "/api/game/levelup-character", "POST",
            JsonUtility.ToJson(new LevelUpCharacterRequest { charId = charId, targetLevel = targetLevel }), token);
        NetworkRequestManager.Instance.Send<LevelUpCharacterResponse>(request, response =>
        {
            if (userData == null || auth == null || auth.AuthToken != token) return;
            var userChar = userData.GetCharacter(charId);
            if (userChar != null) userChar.level = response.level;
            userData.UpdateGameItems(response.updatedGameItems);
            if (this == null || !isActiveAndEnabled || version != _viewVersion || _selectedCharId != charId) return;
            RefreshUI();
            if (OutGameUIManager.Instance == null) return;
            var popup = OutGameUIManager.Instance.ShowPopup<CharacterLevelUpCelebrationPopup>(OutGamePopupType.CharacterLevelUpCelebration);
            if (popup != null) popup.Open(response.level, DataManager.Instance.GetData<CharData>("Char", charId));
        }, new NetworkRequestOptions
        {
            Name = "LevelUpCharacter", DuplicateKey = $"{auth.Username}:LevelUpCharacter:{charId}",
            DuplicatePolicy = DuplicateRequestPolicy.Reject
        }, onFailure: error =>
        {
            if (userData != null && auth != null && auth.AuthToken == token)
            {
                userData.FetchCharacterList(false, false);
                userData.FetchItemList(false, false);
            }
        }, validateResponse: response => response.level >= targetLevel);
    }
}
