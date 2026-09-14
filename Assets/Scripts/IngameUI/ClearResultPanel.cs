using UnityEngine;
using UnityEngine.UI;

public class ClearResultPanel : MonoBehaviour
{
    [Header("UI Elements")]
    [Tooltip("로비로 돌아가기 버튼 (예: 확인 버튼)")]
    [SerializeField] private Button confirmButton;

    private void Start()
    {
        if (confirmButton != null)
        {
            confirmButton.onClick.AddListener(OnConfirmClicked);
        }
        else
        {
            Debug.LogWarning("[ClearResultPanel] confirmButton이 할당되지 않았습니다.");
        }
    }

    private void OnConfirmClicked()
    {
        // 클릭 방지 (중복 클릭 막기)
        if (confirmButton != null)
        {
            confirmButton.interactable = false;
        }

        // 씬에서 UI_SystemActions를 찾아서 메인 메뉴로 돌아가는 동작 실행
        var uiSystem = FindObjectOfType<Scavenger.UI.UI_SystemActions>();
        if (uiSystem != null)
        {
            Debug.Log("[ClearResultPanel] 로비(메인 메뉴)로 돌아갑니다.");
            uiSystem.DoReturnToMainMenu();
        }
        else
        {
            Debug.LogWarning("[ClearResultPanel] 씬에 UI_SystemActions가 없어서 로비로 돌아갈 수 없습니다!");
        }
    }

    private void OnDestroy()
    {
        if (confirmButton != null)
        {
            confirmButton.onClick.RemoveListener(OnConfirmClicked);
        }
    }
}
