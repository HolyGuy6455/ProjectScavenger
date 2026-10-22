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

            string responseBody = request.downloadHandler.text;
            var response = ParseLevelUpResponse(responseBody);
            if (request.result == UnityWebRequest.Result.Success
                && response != null
                && response.errorCode == LevelUpSkillErrorCode.None
                && response.unlockedSkills != null)
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
            else
            {
                Debug.LogError($"스킬 레벨업 실패: {request.error}\n{responseBody}");
                if (OutGameUIManager.Instance != null)
                {
                    var errorCode = response != null ? response.errorCode : LevelUpSkillErrorCode.None;
                    OutGameUIManager.Instance.ShowToast(GetFailureToastMessage(errorCode, request.result, request.responseCode));
                }
            }
        }
    }

    private static LevelUpSkillResponse ParseLevelUpResponse(string responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody)) return null;
        try
        {
            return JsonUtility.FromJson<LevelUpSkillResponse>(responseBody);
        }
        catch (System.ArgumentException)
        {
            // 프록시의 HTML 오류 페이지나 잘못된 JSON도 기본 실패 토스트로 처리합니다.
            return null;
        }
    }

    private static string GetFailureToastMessage(LevelUpSkillErrorCode errorCode,
        UnityWebRequest.Result requestResult, long responseCode)
    {
        if (requestResult == UnityWebRequest.Result.ConnectionError)
            return "서버에 연결할 수 없습니다. 네트워크 상태를 확인해주세요.";

        // 인증 미들웨어가 본문 없이 반환하는 401/403도 처리합니다.
        if (responseCode == 401 || responseCode == 403)
            errorCode = LevelUpSkillErrorCode.InvalidToken;

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
