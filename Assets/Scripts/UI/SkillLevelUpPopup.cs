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
    [SerializeField] private TextMeshProUGUI costText; 

    private int _charId;
    private int _targetSkillId;

    public void Open(int charId, SkillData targetSkill)
    {
        _charId = charId;
        _targetSkillId = targetSkill.id;

        if (skillIcon != null)
        {
            string iconPath = System.IO.Path.GetFileNameWithoutExtension(targetSkill.resourceIcon);
            Sprite loadedSprite = Resources.Load<Sprite>($"skill/{iconPath}");
            if (loadedSprite == null) loadedSprite = Resources.Load<Sprite>(iconPath);
            skillIcon.sprite = loadedSprite;
        }

        if (skillNameText != null) skillNameText.text = targetSkill.name;
        if (skillDescText != null) skillDescText.text = targetSkill.description;

        if (costText != null)
        {
            if (targetSkill.unlockItem != null && targetSkill.unlockItem.Length > 0)
            {
                costText.text = $"Need Item: {targetSkill.unlockItem[0]} x {targetSkill.unlockItemAmount[0]}";
            }
            else
            {
                costText.text = "Free";
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
                    var userChar = UserDataManager.Instance.GetCharacter(_charId);
                    if (userChar != null)
                    {
                        userChar.unlockedSkills = response.unlockedSkills;
                        UserDataManager.Instance.FetchCharacterList();
                    }
                    
                    Debug.Log("스킬 레벨업 성공!");
                    OutGameUIManager.Instance.ClosePopup(gameObject);
                }
            }
            else
            {
                Debug.LogError($"스킬 레벨업 실패: {request.error}\n{request.downloadHandler.text}");
            }
        }
    }
}
