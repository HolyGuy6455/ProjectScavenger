using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using Scavenger.Network;
using Scavenger.Network.DTOs;
using Scavenger.UI;

public class SkillLevelUpPopup : MonoBehaviour
{
    [SerializeField] private Image skillIcon;
    [SerializeField] private TextMeshProUGUI skillNameText;
    [SerializeField] private TextMeshProUGUI skillDescText;
    [SerializeField] private Button levelUpButton;
    [SerializeField] private Transform itemsRow;
    [SerializeField] private GameObject itemSlotPrefab;
    [SerializeField] private TextMeshProUGUI currentLevelText;
    [SerializeField] private TextMeshProUGUI nextLevelText;

    private int _charId;
    private int _targetSkillId;

    public void Open(int charId, SkillData currentSkill, SkillData nextSkill)
    {
        _viewVersion++;
        _charId = charId;
        
        SkillData displaySkill = nextSkill != null ? nextSkill : currentSkill;
        if (displaySkill == null) return;
        
        _targetSkillId = displaySkill.id;

        if (currentLevelText != null) 
            currentLevelText.text = currentSkill != null ? $"Lv.{currentSkill.lvl}" : "Lv.0";
            
        if (nextLevelText != null) 
            nextLevelText.text = nextSkill != null ? $"Lv.{nextSkill.lvl}" : "MAX";

        if (skillIcon != null)
        {
            Sprite loadedSprite = SkillIconLoader.Load(displaySkill.resourceIcon);
            skillIcon.sprite = loadedSprite;
        }

        if (skillNameText != null) skillNameText.text = displaySkill.name;
        if (skillDescText != null) skillDescText.text = displaySkill.description;



        if (itemsRow != null)
        {
            foreach (Transform child in itemsRow)
            {
                Destroy(child.gameObject);
            }

            if (nextSkill == null)
            {
                if (levelUpButton != null) levelUpButton.gameObject.SetActive(false);
            }
            else
            {
                if (levelUpButton != null) levelUpButton.gameObject.SetActive(true);

                if (nextSkill.unlockItem != null && nextSkill.unlockItem.Length > 0 && itemSlotPrefab != null)
                {

                    for (int i = 0; i < nextSkill.unlockItem.Length; i++)
                    {
                        int itemId = nextSkill.unlockItem[i];
                    if (itemId == 0) continue;
                    
                    int requiredAmount = nextSkill.unlockItemAmount[i];
                    int currentAmount = UserDataManager.Instance != null ? UserDataManager.Instance.GetGameItemAmount(itemId) : 0; 
                    
                    string itemName = $"Item {itemId}"; 
                    Sprite itemIcon = null;

                    var itemData = DataManager.Instance?.GetData<ItemData>("Item", itemId);
                    if (itemData != null)
                    {
                        itemName = itemData.name;
                        if (!string.IsNullOrEmpty(itemData.uiResource))
                        {
                            string itemIconPath = System.IO.Path.GetFileNameWithoutExtension(itemData.uiResource);
                            itemIcon = Resources.Load<Sprite>($"Items/{itemIconPath}");
                            if (itemIcon == null) itemIcon = Resources.Load<Sprite>(itemIconPath);
                        }
                    }

                    var slotGo = Instantiate(itemSlotPrefab, itemsRow);
                    var slotWrapper = slotGo.GetComponent<ItemSlotWrapper>();
                    if (slotWrapper != null)
                    {
                        slotWrapper.SetData(itemIcon, itemName, requiredAmount, currentAmount, itemId);
                    }
                    }
                }
            }
        }

        if (levelUpButton != null)
        {
            levelUpButton.onClick.RemoveAllListeners();
            levelUpButton.onClick.AddListener(OnLevelUpClicked);
        }

        gameObject.SetActive(true);
    }

    private int _viewVersion;
    private void OnDisable() { _viewVersion++; }

    private void OnLevelUpClicked()
    {
        var auth = NetworkAuthManager.Instance;
        var userData = UserDataManager.Instance;
        if (auth == null || userData == null) return;
        int charId = _charId;
        int skillId = _targetSkillId;
        int version = _viewVersion;
        string token = auth.AuthToken;
        var request = NetworkRequestManager.CreateJsonRequest(auth.ServerUrl + "/api/game/levelup-skill", "POST",
            JsonUtility.ToJson(new LevelUpSkillRequest { charId = charId, targetSkillId = skillId }), token);
        NetworkRequestManager.Instance.Send<LevelUpSkillResponse>(request, response =>
        {
            if (userData == null || auth == null || auth.AuthToken != token) return;
            userData.UpdateGameItems(response.updatedGameItems);
            var userChar = userData.GetCharacter(charId);
            if (userChar != null)
            {
                userChar.unlockedSkills = response.unlockedSkills;
                if (response.equippedSkills != null) userChar.equippedSkills = response.equippedSkills;
            }
            userData.FetchCharacterList();
            if (this == null || !isActiveAndEnabled || version != _viewVersion || OutGameUIManager.Instance == null) return;
            OutGameUIManager.Instance.ClosePopup(gameObject);
            var celebration = OutGameUIManager.Instance.ShowPopup<SkillLevelUpCelebrationPopup>(OutGamePopupType.SkillLevelUpCelebration);
            if (celebration != null) celebration.Open(DataManager.Instance.GetData<SkillData>("Skill", skillId));
        }, new NetworkRequestOptions
        {
            Name = "LevelUpSkill", DuplicateKey = $"{auth.Username}:LevelUpSkill:{charId}:{skillId}",
            DuplicatePolicy = DuplicateRequestPolicy.Reject, ShowErrorToast = false
        }, onFailure: error =>
        {
            NetworkRequestManager.Instance.ShowToast(GetFailureToastMessage(error));
            if (userData != null && auth != null && auth.AuthToken == token)
            {
                userData.FetchCharacterList(false, false);
                userData.FetchItemList(false, false);
            }
        }, validateResponse: response => response.unlockedSkills != null);
    }

    private static string GetFailureToastMessage(NetworkError error)
    {
        if (error.Kind == NetworkErrorKind.Connection || error.Kind == NetworkErrorKind.Timeout
            || error.HttpStatusCode == 401 || error.HttpStatusCode == 403) return error.DefaultMessage;
        var errorCode = (LevelUpSkillErrorCode)(error.ServerCode ?? 0);

        switch (errorCode)
        {
            case LevelUpSkillErrorCode.InvalidToken:
                return "로그인 정보를 확인할 수 없습니다. 다시 로그인해주세요.";
            case LevelUpSkillErrorCode.CharacterNotFound:
                return "캐릭터 정보를 찾을 수 없습니다. 다시 확인해주세요.";
            case LevelUpSkillErrorCode.SkillNotFound:
                return "스킬 정보를 찾을 수 없습니다. 다시 시도해주세요.";
            case LevelUpSkillErrorCode.AlreadyUnlocked:
                return "이미 해금되었거나 레벨업한 스킬입니다.";
            case LevelUpSkillErrorCode.InsufficientItems:
                return "스킬 레벨업에 필요한 재료가 부족합니다.";
            default:
                return "스킬 레벨업에 실패했습니다. 잠시 후 다시 시도해주세요.";
        }
    }
}
