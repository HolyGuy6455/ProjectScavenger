using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;
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
        _viewVersion++;
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

    private int _viewVersion;
    private void OnDisable() { _viewVersion++; }

    private void HandleSelectClicked(SkillData skillData)
    {
        var auth = NetworkAuthManager.Instance;
        var userData = UserDataManager.Instance;
        if (auth == null || userData == null) return;
        int charId = _charId;
        int slot = _currentSlotIndex;
        int version = _viewVersion;
        string token = auth.AuthToken;
        var request = NetworkRequestManager.CreateJsonRequest(auth.ServerUrl + "/api/game/equip-skill", "POST",
            JsonUtility.ToJson(new EquipSkillRequest { charId = charId, skillId = skillData.id, slotIndex = slot }), token);
        NetworkRequestManager.Instance.Send<EquipSkillResponse>(request, response =>
        {
            if (userData == null || auth == null || auth.AuthToken != token) return;
            var userChar = userData.GetCharacter(charId);
            if (userChar != null) userChar.equippedSkills = response.equippedSkills;
            userData.FetchCharacterList();
            if (this != null && isActiveAndEnabled && version == _viewVersion) RefreshUI();
        }, new NetworkRequestOptions
        {
            Name = "EquipSkill", DuplicateKey = $"{auth.Username}:EquipSkill:{charId}:{slot}",
            DuplicatePolicy = DuplicateRequestPolicy.Reject
        }, onFailure: error =>
        {
            if (userData != null && auth != null && auth.AuthToken == token) userData.FetchCharacterList(false, false);
        }, validateResponse: response => response.equippedSkills != null);
    }

    private void HandleActionClicked(SkillData skillData)
    {
        var popup = OutGameUIManager.Instance.ShowPopup<SkillLevelUpPopup>(OutGamePopupType.SkillLevelUp);
        if (popup != null)
        {
            var userChar = UserDataManager.Instance.GetCharacter(_charId);
            bool isUnlocked = userChar != null && userChar.unlockedSkills.Contains(skillData.id);
            
            SkillData currentSkill = isUnlocked ? skillData : null;
            SkillData nextSkill = null;
            
            if (isUnlocked)
            {
                var allSkills = DataManager.Instance.GetAllData<SkillData>("Skill");
                nextSkill = allSkills.FirstOrDefault(s => s.charId == _charId && s.skillGroup == skillData.skillGroup && s.lvl == skillData.lvl + 1);
            }
            else
            {
                nextSkill = skillData;
            }
            
            popup.Open(_charId, currentSkill, nextSkill);
        }
    }
}
