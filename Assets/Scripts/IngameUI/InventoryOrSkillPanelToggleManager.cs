using UnityEngine;
using UnityEngine.UI;

public class InventoryOrSkillPanelToggleManager : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject skillPanel;
    [SerializeField] private GameObject inventoryPanel;

    [Header("Button Settings")]
    [SerializeField] private Image buttonImage;
    [SerializeField] private Sprite backpackIcon; // Shown when skill panel is active
    [SerializeField] private Sprite gunIcon;      // Shown when inventory panel is active

    private Button button;

    public static InventoryOrSkillPanelToggleManager Instance { get; private set; }

    public bool IsInventoryActive => inventoryPanel != null && inventoryPanel.activeInHierarchy;
    public bool IsSkillActive => skillPanel != null && skillPanel.activeInHierarchy;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        button = GetComponent<Button>();
        if (button != null)
        {
            button.onClick.AddListener(OnToggleClicked);
        }
    }

    private void Start()
    {
        // Initial state sync
        UpdateButtonIcon();
    }

    private void OnToggleClicked()
    {
        // Check current active states
        bool isSkillActive = skillPanel != null && skillPanel.activeSelf;
        
        // Toggle logic: If skill is active, switch to inventory. Otherwise, switch to skill.
        if (isSkillActive)
        {
            if (skillPanel != null) skillPanel.SetActive(false);
            if (inventoryPanel != null) inventoryPanel.SetActive(true);
        }
        else
        {
            if (skillPanel != null) skillPanel.SetActive(true);
            if (inventoryPanel != null) inventoryPanel.SetActive(false);
        }

        UpdateButtonIcon();
    }

    private void UpdateButtonIcon()
    {
        if (buttonImage == null) return;

        bool isInventoryActive = inventoryPanel != null && inventoryPanel.activeSelf;

        if (isInventoryActive)
        {
            if (gunIcon != null) buttonImage.sprite = gunIcon;
        }
        else 
        {
            // If inventory is not active, we assume skill is active (or should show backpack)
            if (backpackIcon != null) buttonImage.sprite = backpackIcon;
        }
    }
}
