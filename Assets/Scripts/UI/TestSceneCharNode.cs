using UnityEngine;
using UnityEngine.UI;
using Scavenger.Core;
using TMPro;
using System.Collections.Generic;
using System.Linq;

namespace Scavenger.UI
{
    public class TestSceneCharNode : MonoBehaviour
    {
        [SerializeField] private Image face;
        [SerializeField] private Image skill_1;
        [SerializeField] private Image skill_2;
        [SerializeField] private Image skill_3;
        [SerializeField] private Image skill_4;

        [SerializeField] private TMP_Dropdown charDropdown;
        [SerializeField] private TMP_Dropdown skillDropdown1;
        [SerializeField] private TMP_Dropdown skillDropdown2;
        [SerializeField] private TMP_Dropdown skillDropdown3;
        [SerializeField] private TMP_Dropdown skillDropdown4;

        public int CurrentCharId { get; private set; }
        public int[] CurrentSkills { get; private set; } = new int[4];

        private bool _isInitializing = false;
        private List<int> _charIds = new List<int>();
        private List<int> _skillIds = new List<int>();

        public void Initialize(int charId)
        {
            if (DataManager.Instance == null) return;
            
            _isInitializing = true;
            CurrentCharId = charId;
            
            var allChars = DataManager.Instance.GetAllData<CharData>("Char");
            var allSkills = DataManager.Instance.GetAllData<SkillData>("Skill");
            
            _charIds.Clear();
            _skillIds.Clear();

            if (charDropdown != null)
            {
                charDropdown.options.Clear();
                int selectedIndex = 0;
                for (int i = 0; i < allChars.Count; i++)
                {
                    var c = allChars[i];
                    _charIds.Add(c.id);
                    charDropdown.options.Add(new TMP_Dropdown.OptionData($"{c.id}: {c.name}"));
                    if (c.id == charId) selectedIndex = i;
                }
                charDropdown.value = selectedIndex;
                charDropdown.onValueChanged.RemoveAllListeners();
                charDropdown.onValueChanged.AddListener(OnCharChanged);
            }

            TMP_Dropdown[] skillDropdowns = new TMP_Dropdown[] { skillDropdown1, skillDropdown2, skillDropdown3, skillDropdown4 };
            for (int i = 0; i < 4; i++)
            {
                if (skillDropdowns[i] != null)
                {
                    skillDropdowns[i].options.Clear();
                    skillDropdowns[i].options.Add(new TMP_Dropdown.OptionData("0: None"));
                    
                    if (i == 0) // 첫 슬롯 세팅 시 ID 목록 한번만 채우기
                    {
                        for (int j = 0; j < allSkills.Count; j++)
                        {
                            _skillIds.Add(allSkills[j].id);
                        }
                    }

                    for (int j = 0; j < allSkills.Count; j++)
                    {
                        var s = allSkills[j];
                        skillDropdowns[i].options.Add(new TMP_Dropdown.OptionData($"{s.id}: {s.name}"));
                    }
                    
                    int slotIndex = i;
                    skillDropdowns[i].onValueChanged.RemoveAllListeners();
                    skillDropdowns[i].onValueChanged.AddListener((val) => OnSkillChanged(slotIndex, val));
                }
            }

            UpdateCharVisualsAndDefaultSkills(charId);
            
            _isInitializing = false;
        }

        private void OnCharChanged(int index)
        {
            if (_isInitializing || index < 0 || index >= _charIds.Count) return;
            UpdateCharVisualsAndDefaultSkills(_charIds[index]);
        }

        private void OnSkillChanged(int slotIndex, int dropdownValue)
        {
            if (_isInitializing) return;
            int skillId = 0;
            if (dropdownValue > 0 && dropdownValue - 1 < _skillIds.Count)
            {
                skillId = _skillIds[dropdownValue - 1];
            }
            CurrentSkills[slotIndex] = skillId;
            UpdateSkillVisual(slotIndex, skillId);
        }

        private void UpdateCharVisualsAndDefaultSkills(int charId)
        {
            CurrentCharId = charId;
            var charData = DataManager.Instance.GetData<CharData>("Char", charId);
            if (charData == null) return;

            if (face != null && !string.IsNullOrEmpty(charData.resourceSlot))
            {
                string path = charData.resourceSlot.Replace(".png", "");
                Sprite loadedSprite = Resources.Load<Sprite>($"char/{path}");
                if (loadedSprite == null) loadedSprite = Resources.Load<Sprite>(path);
                if (loadedSprite != null) face.sprite = loadedSprite;
            }

            _isInitializing = true;
            TMP_Dropdown[] skillDropdowns = new TMP_Dropdown[] { skillDropdown1, skillDropdown2, skillDropdown3, skillDropdown4 };
            for (int i = 0; i < 4; i++)
            {
                int skillId = (charData.defaultSkills != null && i < charData.defaultSkills.Length) ? charData.defaultSkills[i] : 0;
                CurrentSkills[i] = skillId;
                UpdateSkillVisual(i, skillId);
                
                if (skillDropdowns[i] != null)
                {
                    if (skillId == 0) skillDropdowns[i].value = 0;
                    else
                    {
                        int skillIndex = _skillIds.IndexOf(skillId);
                        if (skillIndex >= 0) skillDropdowns[i].value = skillIndex + 1;
                    }
                }
            }
            _isInitializing = false;
        }

        private void UpdateSkillVisual(int slotIndex, int skillId)
        {
            Image[] skillImages = new Image[] { skill_1, skill_2, skill_3, skill_4 };
            if (skillImages[slotIndex] == null) return;

            if (skillId <= 0)
            {
                skillImages[slotIndex].gameObject.SetActive(false);
                return;
            }

            var skillData = DataManager.Instance.GetData<SkillData>("Skill", skillId);
            if (skillData != null && !string.IsNullOrEmpty(skillData.resourceIcon))
            {
                string path = skillData.resourceIcon.Replace(".png", "");
                Sprite skillSprite = Resources.Load<Sprite>($"skill/{path}");
                if (skillSprite == null) skillSprite = Resources.Load<Sprite>(path);
                
                if (skillSprite != null)
                {
                    skillImages[slotIndex].sprite = skillSprite;
                    skillImages[slotIndex].gameObject.SetActive(true);
                }
                else
                {
                    skillImages[slotIndex].gameObject.SetActive(false);
                }
            }
            else
            {
                skillImages[slotIndex].gameObject.SetActive(false);
            }
        }
    }
}
