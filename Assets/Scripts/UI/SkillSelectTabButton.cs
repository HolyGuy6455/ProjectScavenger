using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SkillSelectTabButton : MonoBehaviour
{
    [SerializeField] private Image skillIcon;
    [SerializeField] private TextMeshProUGUI skillNameText;
    [SerializeField] private GameObject selectedBorder;
    [SerializeField] private Button button;

    public int slotIndex { get; private set; }
    
    private void Awake()
    {
        if (button == null) button = GetComponent<Button>();
    }

    public void Init(int index, System.Action<int> onClickAction)
    {
        slotIndex = index;
        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => onClickAction?.Invoke(slotIndex));
        }
    }

    public void Setup(SkillData equippedSkill, bool isSelected)
    {
        if (selectedBorder != null) selectedBorder.SetActive(isSelected);

        if (equippedSkill != null)
        {
            if (skillIcon != null)
            {
                string iconPath = System.IO.Path.GetFileNameWithoutExtension(equippedSkill.resourceIcon);
                Sprite loadedSprite = Resources.Load<Sprite>($"skill/{iconPath}");
                if (loadedSprite == null) loadedSprite = Resources.Load<Sprite>(iconPath);
                
                skillIcon.sprite = loadedSprite;
                skillIcon.color = Color.white;
            }
            if (skillNameText != null) skillNameText.text = equippedSkill.name;
        }
        else
        {
            if (skillIcon != null)
            {
                skillIcon.sprite = null;
                skillIcon.color = new Color(1, 1, 1, 0); // 투명하게
            }
            if (skillNameText != null) skillNameText.text = "Empty";
        }
    }
}
