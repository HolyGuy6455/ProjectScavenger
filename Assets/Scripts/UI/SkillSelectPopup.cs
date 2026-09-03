using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Networking;
using Scavenger.Network;
using Scavenger.Network.DTOs;
using Scavenger.UI;

public class SkillSelectPopup : MonoBehaviour
{
    [SerializeField] private Transform rowContainer;
    [SerializeField] private GameObject rowPrefab;
    [SerializeField] private SkillSelectTabButton[] tabButtons; // 4 tabs

    private int _charId;
    private int _currentSlotIndex;
    private List<GameObject> _spawnedRows = new List<GameObject>();

    private void Awake()
    {
        for (int i = 0; i < tabButtons.Length; i++)
        {
            if (tabButtons[i] != null)
            {
                tabButtons[i].Init(i, OnTabClicked);
            }
        }

        // 에디터에 임시로 올려둔 더미 UI 비우기
        if (rowContainer != null)
        {
            foreach (Transform child in rowContainer)
            {
                Destroy(child.gameObject);
            }
        }
    }

    public void Open(int charId, int slotIndex)
    {
        _charId = charId;
        gameObject.SetActive(true);
        OnTabClicked(slotIndex);
    }

    private void OnTabClicked(int slotIndex)
    {
        _currentSlotIndex = slotIndex;
        RefreshUI();
    }

    public void RefreshUI()
    {
        foreach (var obj in _spawnedRows) Destroy(obj);
        _spawnedRows.Clear();

        var allSkills = DataManager.Instance.GetAllData<SkillData>("Skill")
            .Where(s => s.charId == _charId)
            .ToList();

        var filteredSkills = allSkills.Where(s => s.index != null && s.index.Contains(_currentSlotIndex)).ToList();
        var groupedSkills = filteredSkills.GroupBy(s => s.skillGroup);
        
        var userChar = UserDataManager.Instance.OwnedCharacters.FirstOrDefault(c => c.charId == _charId);
        if (userChar == null) return;

        // 탭 UI 갱신 (선택된 테두리 및 장착중인 스킬 정보 표기)
        for (int i = 0; i < tabButtons.Length; i++)
        {
            if (tabButtons[i] != null)
            {
                bool isSelected = (i == _currentSlotIndex);
                SkillData equippedSkill = null;
                if (userChar.equippedSkills != null && userChar.equippedSkills.Length > i)
                {
                    int skillId = userChar.equippedSkills[i];
                    equippedSkill = allSkills.FirstOrDefault(s => s.id == skillId);
                }
                tabButtons[i].Setup(equippedSkill, isSelected);
            }
        }

        foreach (var group in groupedSkills)
        {
            var skillsInGroup = group.OrderBy(s => s.lvl).ToList();
            SkillData displaySkill = null;
            bool isUnlocked = false;

            var unlockedInGroup = skillsInGroup.Where(s => userChar.unlockedSkills.Contains(s.id)).ToList();
            if (unlockedInGroup.Any())
            {
                displaySkill = unlockedInGroup.Last(); 
                isUnlocked = true;
            }
            else
            {
                displaySkill = skillsInGroup.First();
                isUnlocked = false;
            }

            if (displaySkill != null)
            {
                bool isEquipped = userChar.equippedSkills[_currentSlotIndex] == displaySkill.id;
                bool canLevelUp = isUnlocked && skillsInGroup.Any(s => s.lvl > displaySkill.lvl);

                SpawnRow(displaySkill, isUnlocked, isEquipped, canLevelUp);
            }
        }
    }

    private void SpawnRow(SkillData skillData, bool isUnlocked, bool isEquipped, bool canLevelUp)
    {
        GameObject obj = Instantiate(rowPrefab, rowContainer);
        _spawnedRows.Add(obj);

        SkillSelectRow row = obj.GetComponent<SkillSelectRow>();
        if (row != null)
        {
            row.Setup(skillData, isUnlocked, isEquipped, canLevelUp);
            row.OnSelectClicked += HandleSelectClicked;
            row.OnActionClicked += HandleActionClicked;
        }
    }

    private void HandleSelectClicked(SkillData skillData)
    {
        Debug.Log($"장착 요청: {skillData.name} -> 슬롯 {_currentSlotIndex}");
        StartCoroutine(EquipSkillRoutine(skillData.id));
    }

    private IEnumerator EquipSkillRoutine(int skillId)
    {
        string url = $"{NetworkAuthManager.Instance.ServerUrl}/api/game/equip-skill";
        
        var reqObj = new EquipSkillRequest
        {
            charId = _charId,
            skillId = skillId,
            slotIndex = _currentSlotIndex
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
                try
                {
                    var response = JsonUtility.FromJson<EquipSkillResponse>(request.downloadHandler.text);
                    if (response != null && response.equippedSkills != null)
                    {
                        var userChar = UserDataManager.Instance.OwnedCharacters.FirstOrDefault(c => c.charId == _charId);
                        if (userChar != null)
                        {
                            userChar.equippedSkills = response.equippedSkills;
                            UserDataManager.Instance.FetchCharacterList(); // UI 갱신을 위해 이벤트 트리거 겸 최신화
                        }
                        
                        RefreshUI(); // 현재 팝업 다시 그리기
                        Debug.Log("스킬 장착 성공!");
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[SkillSelectPopup] JSON Parsing Error: {e.Message}");
                }
            }
            else
            {
                Debug.LogError($"[SkillSelectPopup] 스킬 장착 실패: {request.error}\n{request.downloadHandler.text}");
            }
        }
    }

    private void HandleActionClicked(SkillData skillData)
    {
        var popup = OutGameUIManager.Instance.ShowPopup<SkillLevelUpPopup>(OutGamePopupType.SkillLevelUp);
        if (popup != null)
        {
            SkillData targetSkill = skillData;
            
            var userChar = UserDataManager.Instance.GetCharacter(_charId);
            bool isUnlocked = userChar != null && userChar.unlockedSkills.Contains(skillData.id);
            
            if (isUnlocked)
            {
                // 이미 해금된 스킬이라면, 다음 레벨 스킬을 찾아야 함
                var allSkills = DataManager.Instance.GetAllData<SkillData>("Skill");
                var nextSkill = allSkills.FirstOrDefault(s => s.charId == _charId && s.skillGroup == skillData.skillGroup && s.lvl == skillData.lvl + 1);
                if (nextSkill != null)
                {
                    targetSkill = nextSkill;
                }
            }
            
            popup.Open(_charId, targetSkill);
        }
    }
}
