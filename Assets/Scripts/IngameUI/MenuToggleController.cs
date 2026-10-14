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
    private readonly System.Collections.Generic.HashSet<Object> cinematicHideSources = new System.Collections.Generic.HashSet<Object>();
    private bool IsCinematicHidden => isCinematicHidden || cinematicHideSources.Count > 0;
    
    // CanvasGroup 캐싱
    private CanvasGroup ingameCanvasGroup;
    private CanvasGroup pauseCanvasGroup;
    private readonly System.Collections.Generic.Dictionary<CanvasGroup, Coroutine> fadeRoutines = new System.Collections.Generic.Dictionary<CanvasGroup, Coroutine>();

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
        if (IsCinematicHidden) return;
        isMenuOpen = true;
        UpdateUIState(false);
    }

    public void CloseMenu()
    {
        if (IsCinematicHidden) return;
        isMenuOpen = false;
        UpdateUIState(false);
    }

    // UI 버튼의 OnClick 이벤트나 다른 스크립트에서 직접 호출할 수 있는 public 함수
    public void ToggleMenu()
    {
        if (IsCinematicHidden) return; // 시네마틱 연출 중에는 메뉴 토글 금지
        
        isMenuOpen = !isMenuOpen;
        UpdateUIState(false);
    }

    private void UpdateUIState(bool instant = false)
    {
        bool showIngame = !isMenuOpen && !IsCinematicHidden;
        bool showPause = isMenuOpen && !IsCinematicHidden;

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

    /// <summary>다른 시네마틱이 끝나도 현재 보스 연출의 UI 숨김은 유지합니다.</summary>
    public void SetCinematicHidden(Object source, bool hidden)
    {
        if (ReferenceEquals(source, null)) return;
        bool changed = hidden ? cinematicHideSources.Add(source) : cinematicHideSources.Remove(source);
        if (!changed) return;
        if (IsCinematicHidden) isMenuOpen = false;
        UpdateUIState(false);
    }

    /// <summary>
    /// 게임 클리어 연출이 시작될 때 미리 결산창 패널을 투명하게 켭니다. (버튼 입력을 받기 위함)
    /// </summary>
    public void PrepareClearResultUI()
    {
        isCinematicHidden = true;
        isMenuOpen = false;
        UpdateUIState(false);

        if (clearResultPanel != null)
        {
            clearResultPanel.gameObject.SetActive(true);
            CanvasGroup cg = GetOrAddCanvasGroup(clearResultPanel.gameObject);
            cg.alpha = 1f; // 버튼 입력을 받아야 하므로 패널 자체는 1f
        }
    }

    /// <summary>
    /// 서버로부터 받은 게임 클리어 결과를 세팅합니다.
    /// </summary>
    public void SetupClearResultData(System.Collections.Generic.List<Scavenger.UI.LootInfo> loots, System.Collections.Generic.List<Scavenger.UI.CharExpResult> charExp, Scavenger.UI.PlayerExpResult playerExp)
    {
        if (clearResultPanel != null)
        {
            var panelScript = clearResultPanel.GetComponent<ClearResultPanel>();
            if (panelScript != null)
            {
                panelScript.SetupResultFlow(loots, charExp, playerExp);
            }
        }
    }

    /// <summary>
    /// 게임 클리어 시네마틱이 끝난 후 불립니다.
    /// </summary>
    public void ShowClearResultUI()
    {
        if (clearResultPanel != null)
        {
            // 이제 ClearResultPanel 내부에서 스르륵 연출 등을 처리합니다.
            var panelScript = clearResultPanel.GetComponent<ClearResultPanel>();
            if (panelScript != null)
            {
                panelScript.OnCinematicFinished();
            }
        }
        else
        {
            Debug.LogWarning("[MenuToggleController] ClearResultPanel이 할당되지 않았습니다!");
        }
    }

    private void FadeCanvasGroup(CanvasGroup cg, float targetAlpha, float duration)
    {
        if (cg == null) return;

        // 인트로가 바로 취소돼도 이전 숨김 페이드가 UI를 뒤늦게 꺼버리지 않게 합니다.
        if (fadeRoutines.TryGetValue(cg, out var previous) && previous != null)
            StopCoroutine(previous);
        fadeRoutines[cg] = StartCoroutine(FadeRoutine(cg, targetAlpha, duration));
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
