using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Networking;
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

    private void OnLevelUpClicked()
    {
        StartCoroutine(LevelUpSkillRoutine());
    }

    private IEnumerator LevelUpSkillRoutine()
    {
        string url = $"{NetworkAuthManager.Instance.ServerUrl}/api/game/levelup-skill";
        
        var reqObj = new LevelUpSkillRequest
        {
            charId = _charId,
            targetSkillId = _targetSkillId
        };
        
        string json = JsonUtility.ToJson(reqObj);
        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Authorization", $"Bearer {NetworkAuthManager.Instance.AuthToken}");
            
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                var response = JsonUtility.FromJson<LevelUpSkillResponse>(request.downloadHandler.text);
                if (response != null && response.unlockedSkills != null)
                {
                    // 서버로부터 받은 갱신된 아이템 수량 반영
                    if (response.updatedGameItems != null)
                    {
                        UserDataManager.Instance.UpdateGameItems(response.updatedGameItems);
                    }

                    var userChar = UserDataManager.Instance.GetCharacter(_charId);
                    if (userChar != null)
                    {
                        userChar.unlockedSkills = response.unlockedSkills;
                        if (response.equippedSkills != null && response.equippedSkills.Length > 0)
                        {
                            userChar.equippedSkills = response.equippedSkills;
                        }
                        UserDataManager.Instance.FetchCharacterList();
                    }
                    
                    Debug.Log("스킬 레벨업 성공!");
                    OutGameUIManager.Instance.ClosePopup(gameObject);
                    
                    var celebrationPopup = OutGameUIManager.Instance.ShowPopup<SkillLevelUpCelebrationPopup>(OutGamePopupType.SkillLevelUpCelebration);
                    if (celebrationPopup != null)
                    {
                        var newSkillData = DataManager.Instance.GetData<SkillData>("Skill", _targetSkillId);
                        celebrationPopup.Open(newSkillData);
                    }
                }
            }
            else
            {
                Debug.LogError($"스킬 레벨업 실패: {request.error}\n{request.downloadHandler.text}");
            }
        }
    }
}
