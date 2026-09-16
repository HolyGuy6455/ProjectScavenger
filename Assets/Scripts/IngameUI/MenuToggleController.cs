using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

public class MenuToggleController : MonoBehaviour
{
    public static MenuToggleController Instance { get; private set; }

    [Header("UI Panels")]
    [SerializeField] private GameObject ingameUI;
    [SerializeField] private GameObject pauseMenuUI;
    [SerializeField] private ClearResultPanel clearResultPanel;

    [Header("Buttons")]
    [Tooltip("일시정지 메뉴를 열거나 토글하는 버튼 (예: 인게임 햄버거 메뉴 버튼)")]
    [SerializeField] private Button openPauseButton;
    [Tooltip("일시정지 메뉴를 닫는 버튼 (예: 일시정지 창 내 Close 버튼)")]
    [SerializeField] private Button closePauseButton;

    private bool isMenuOpen = false;
    private bool isCinematicHidden = false; // 시네마틱에 의해 강제로 숨겨졌는지 여부
    
    // CanvasGroup 캐싱
    private CanvasGroup ingameCanvasGroup;
    private CanvasGroup pauseCanvasGroup;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        // UI 버튼 클릭 이벤트 연결
        if (openPauseButton != null)
        {
            openPauseButton.onClick.AddListener(OpenMenu);
        }

        if (closePauseButton != null)
        {
            closePauseButton.onClick.AddListener(CloseMenu);
        }

        // CanvasGroup 가져오기 (없으면 자동으로 추가)
        ingameCanvasGroup = GetOrAddCanvasGroup(ingameUI);
        pauseCanvasGroup = GetOrAddCanvasGroup(pauseMenuUI);

        // 초기 상태 설정: 게임 UI는 켜고 메뉴 UI와 결과창은 끕니다.
        isMenuOpen = false;
        
        if (clearResultPanel != null)
        {
            clearResultPanel.gameObject.SetActive(false);
        }
        
        UpdateUIState(true); // 처음엔 애니메이션 없이 즉시 세팅
    }

    private CanvasGroup GetOrAddCanvasGroup(GameObject obj)
    {
        if (obj == null) return null;
        CanvasGroup cg = obj.GetComponent<CanvasGroup>();
        if (cg == null) cg = obj.AddComponent<CanvasGroup>();
        return cg;
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

    public void OpenMenu()
    {
        if (isCinematicHidden) return;
        isMenuOpen = true;
        UpdateUIState(false);
    }

    public void CloseMenu()
    {
        if (isCinematicHidden) return;
        isMenuOpen = false;
        UpdateUIState(false);
    }

    // UI 버튼의 OnClick 이벤트나 다른 스크립트에서 직접 호출할 수 있는 public 함수
    public void ToggleMenu()
    {
        if (isCinematicHidden) return; // 시네마틱 연출 중에는 메뉴 토글 금지
        
        isMenuOpen = !isMenuOpen;
        UpdateUIState(false);
    }

    private void UpdateUIState(bool instant = false)
    {
        bool showIngame = !isMenuOpen && !isCinematicHidden;
        bool showPause = isMenuOpen && !isCinematicHidden;

        if (ingameCanvasGroup != null)
            FadeCanvasGroup(ingameCanvasGroup, showIngame ? 1f : 0f, instant ? 0f : 0.3f);
            
        if (pauseCanvasGroup != null)
            FadeCanvasGroup(pauseCanvasGroup, showPause ? 1f : 0f, instant ? 0f : 0.3f);
    }

    /// <summary>
    /// 시네마틱 연출 중 Ingame UI를 숨기거나 켭니다.
    /// </summary>
    public void SetUIVisibility(bool isVisible)
    {
        isCinematicHidden = !isVisible;
        
        // 시네마틱 때문에 UI가 가려질 때는 만약 메뉴가 열려있었다면 강제로 닫습니다.
        if (isCinematicHidden && isMenuOpen)
        {
            isMenuOpen = false;
        }
        
        UpdateUIState(false);
    }

    /// <summary>
    /// 게임 클리어 후 결산창을 띄웁니다.
    /// </summary>
    public void ShowClearResultUI()
    {
        // 모든 기존 UI 끄기
        isCinematicHidden = true;
        isMenuOpen = false;
        UpdateUIState(false);

        if (clearResultPanel != null)
        {
            clearResultPanel.gameObject.SetActive(true);
            CanvasGroup cg = GetOrAddCanvasGroup(clearResultPanel.gameObject);
            cg.alpha = 0f;
            FadeCanvasGroup(cg, 1f, 0.5f); // 0.5초 동안 서서히 등장
        }
        else
        {
            Debug.LogWarning("[MenuToggleController] ClearResultPanel이 할당되지 않았습니다!");
        }
    }

    private void FadeCanvasGroup(CanvasGroup cg, float targetAlpha, float duration)
    {
        if (cg == null) return;
        
        // 이미 진행 중인 코루틴이 있다면 멈출 수 있도록 구조를 짜도 되지만,
        // 단순함을 위해 DOTween 느낌으로 코루틴을 돌립니다. (빠른 전환 시 약간 튈 수 있음)
        StartCoroutine(FadeRoutine(cg, targetAlpha, duration));
    }

    private System.Collections.IEnumerator FadeRoutine(CanvasGroup cg, float targetAlpha, float duration)
    {
        // 목표가 0이 아니면 켜기 시작할 때 일단 렌더링 활성화
        if (targetAlpha > 0f && !cg.gameObject.activeSelf)
        {
            cg.gameObject.SetActive(true);
        }

        // 투명도 변화 중에는 클릭 방지
        cg.interactable = false;
        cg.blocksRaycasts = false;

        if (duration <= 0f)
        {
            cg.alpha = targetAlpha;
        }
        else
        {
            float startAlpha = cg.alpha;
            float time = 0f;
            while (time < duration)
            {
                time += Time.deltaTime;
                cg.alpha = Mathf.Lerp(startAlpha, targetAlpha, time / duration);
                yield return null;
            }
            cg.alpha = targetAlpha;
        }

        // 완전히 켜졌을 때만 상호작용 허용
        if (targetAlpha >= 1f)
        {
            cg.interactable = true;
            cg.blocksRaycasts = true;
        }
        else if (targetAlpha <= 0f) // 완전히 꺼지면 오브젝트 비활성화하여 렌더링 비용 절약
        {
            cg.gameObject.SetActive(false);
        }
    }
    
    private void OnDestroy()
    {
        if (openPauseButton != null)
        {
            openPauseButton.onClick.RemoveListener(OpenMenu);
        }
        if (closePauseButton != null)
        {
            closePauseButton.onClick.RemoveListener(CloseMenu);
        }
    }
}
