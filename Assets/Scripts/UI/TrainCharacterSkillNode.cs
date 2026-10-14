using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TrainCharacterSkillNode : MonoBehaviour
{
    [SerializeField] private Image skillIcon;
    [SerializeField] private TextMeshProUGUI skillNameText;
    
    private int _currentSkillId;
    private int _slotIndex;
    [SerializeField] private Button nodeButton;

    public event System.Action<int> OnNodeClicked;

    private void Awake()
    {
        if (nodeButton == null) nodeButton = GetComponentInChildren<Button>();

        if (nodeButton != null)
        {
            nodeButton.onClick.AddListener(() => OnNodeClicked?.Invoke(_slotIndex));
        }
    }

    public void Setup(SkillData skillData, int slotIndex)
    {
        _slotIndex = slotIndex;
        if (skillData != null)
        {
            _currentSkillId = skillData.id;

            if (skillIcon != null)
            {
                Sprite loadedSprite = SkillIconLoader.Load(skillData.resourceIcon);
                
                if (loadedSprite != null)
                {
                    skillIcon.sprite = loadedSprite;
                    skillIcon.enabled = true;
                }
                else
                {
                    skillIcon.enabled = false;
                }
            }

            if (skillNameText != null)
            {
                skillNameText.text = skillData.name;
            }
        }
        else
        {
            _currentSkillId = 0;
            if (skillIcon != null) skillIcon.enabled = false;
            if (skillNameText != null) skillNameText.text = "Empty";
        }
    }
}
