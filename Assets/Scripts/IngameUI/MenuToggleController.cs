using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

public class MenuToggleController : MonoBehaviour
{
    [Header("UI Panels")]
    [SerializeField] private GameObject ingameUI;
    [SerializeField] private GameObject pauseMenuUI;

    [Header("Settings")]
    [Tooltip("UI에 있는 메뉴 버튼을 연결하면, 해당 버튼을 클릭할 때 메뉴가 토글됩니다.")]
    [SerializeField] private Button toggleButton;

    private bool isMenuOpen = false;

    private void Start()
    {
        // UI 버튼 클릭 이벤트 연결
        if (toggleButton != null)
        {
            toggleButton.onClick.AddListener(ToggleMenu);
        }

        // 초기 상태 설정: 게임 UI는 켜고 메뉴 UI는 끕니다.
        isMenuOpen = false;
        UpdateUIState();
    }

    private void Update()
    {
        // 새로운 Input System을 사용한 키보드 입력 처리 (예: ESC 키)
        // 만약 모바일 환경이라 키보드가 없다면 Keyboard.current는 null이 됩니다.
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            ToggleMenu();
        }
    }

    // UI 버튼의 OnClick 이벤트나 다른 스크립트(예: 가상 버튼)에서 직접 호출할 수 있는 public 함수
    public void ToggleMenu()
    {
        isMenuOpen = !isMenuOpen;
        UpdateUIState();
    }

    private void UpdateUIState()
    {
        if (ingameUI != null)
        {
            ingameUI.SetActive(!isMenuOpen);
        }
        
        if (pauseMenuUI != null)
        {
            pauseMenuUI.SetActive(isMenuOpen);
        }
    }
    
    private void OnDestroy()
    {
        if (toggleButton != null)
        {
            toggleButton.onClick.RemoveListener(ToggleMenu);
        }
    }
}
