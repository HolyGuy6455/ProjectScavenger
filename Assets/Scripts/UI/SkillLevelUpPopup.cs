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
        {
            LocalizedText.ApplyFont(currentLevelText);
            currentLevelText.text = currentSkill != null ? $"Lv.{currentSkill.lvl}" : "Lv.0";
        }
            
        if (nextLevelText != null)
        {
            LocalizedText.ApplyFont(nextLevelText);
            nextLevelText.text = nextSkill != null ? $"Lv.{nextSkill.lvl}" : "MAX";
        }

        if (skillIcon != null)
        {
            Sprite loadedSprite = SpriteManager.Instance.LoadSprite(displaySkill.resourceIcon);
            skillIcon.sprite = loadedSprite;
        }

        if (skillNameText != null)
        {
            LocalizedText.ApplyFont(skillNameText);
            skillNameText.text = displaySkill.GetLocalizedName();
        }
        if (skillDescText != null)
        {
            LocalizedText.ApplyFont(skillDescText);
            skillDescText.text = displaySkill.GetLocalizedDescription();
        }



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
                        itemName = itemData.GetLocalizedName();
                        if (!string.IsNullOrEmpty(itemData.uiResource))
                        {
                            itemIcon = SpriteManager.Instance.LoadSprite(itemData.uiResource);
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
                return LocalizationManager.Get("UI_NET_ERR_AUTH");
            case LevelUpSkillErrorCode.CharacterNotFound:
                return LocalizationManager.Get("UI_TOAST_SKILL_ERR_CHAR_NOT_FOUND");
            case LevelUpSkillErrorCode.SkillNotFound:
                return LocalizationManager.Get("UI_TOAST_SKILL_ERR_SKILL_NOT_FOUND");
            case LevelUpSkillErrorCode.AlreadyUnlocked:
                return LocalizationManager.Get("UI_TOAST_SKILL_ERR_ALREADY_UNLOCKED");
            case LevelUpSkillErrorCode.InsufficientItems:
                return LocalizationManager.Get("UI_TOAST_SKILL_ERR_INSUFFICIENT_ITEMS");
            default:
                return LocalizationManager.Get("UI_TOAST_SKILL_ERR_DEFAULT");
        }
    }
}
