using UnityEngine;
using System.Collections;
using Scavenger.Manager;

public abstract class BaseUltimateSkill : MonoBehaviour
{
    [Header("Cinematic Settings")]

    [Tooltip("시네마틱 연출 시간 (0 이하로 설정 시 애니메이션 길이를 자동 계산합니다)")]
    [SerializeField] protected float cinematicDuration = -1f;

    [Tooltip("궁극기 사용 중 메인 UI를 잠시 숨길지 여부입니다.")]
    [SerializeField] protected bool hideUIDuringUltimate = true;

    [Tooltip("궁극기 발동 시 재생할 필수 애니메이션 상태 이름")]
    [SerializeField] protected string ultimateAnimStateName = "Ultimate";

    protected SkillData currentData;
    private Coroutine presentationRoutine;
    private bool presentationPlaying;
    private bool presentationIsOwner;
    private CharacterAnimationPresenter animationPresenter;
    private CharacterCinematicController cinematicController;
    private int animationToken;
    private int cinematicToken;

    public bool IsPresentationPlaying => presentationPlaying;
    public bool LastPresentationCompleted { get; private set; }
    public int PresentationVersion { get; private set; }

    protected virtual void Awake()
    {
        // 시네마틱 카메라는 CharacterCinematicController에서 관리합니다.
    }

    /// <summary>
    /// 애니메이션 이벤트에서 호출할 트리거 함수입니다.
    /// 이 함수가 호출되면 클라이언트는 서버로 실제 로직 실행을 요청(RPC)합니다.
    /// 여러 개의 타이밍이 필요하다면 eventId로 구분합니다.
    /// </summary>
    public void TriggerAnimationEvent(int eventId = 0)
    {
        // PlayerSkillController에게 서버로의 호출(RPC)을 위임합니다.
        if (PlayerSkillController.Instance != null)
        {
            PlayerSkillController.Instance.OnUltimateAnimationEvent(eventId);
        }
    }

    /// <summary>
    /// 클라이언트(모든 플레이어 화면)에서 실행되는 연출용 로직입니다.
    /// (파티클 재생, 시네마틱 카메라 켜기 등)
    /// </summary>
    public virtual void OnClientExecute(SkillData data, bool isOwner)
    {
        // A request already in transit must not start a new ultimate after the game ends.
        if (GameFlowManager.Instance != null
            && GameFlowManager.Instance.CurrentState != GameResultState.None) return;
        if (presentationPlaying)
        {
            Debug.LogError($"[BaseUltimateSkill] {name}: 궁극기 실행 요청이 중복되었습니다. 현재 연출을 유지하고 새 요청을 무시합니다.", this);
            return;
        }
        LastPresentationCompleted = false;
        if (!isActiveAndEnabled)
        {
            Debug.LogError($"[BaseUltimateSkill] {name}: 비활성화된 캐릭터에 궁극기 실행을 요청했습니다.", this);
            return;
        }
        animationPresenter = CharacterAnimationPresenter.GetOrCreate(gameObject);
        if (animationPresenter == null || string.IsNullOrWhiteSpace(ultimateAnimStateName))
        {
            Debug.LogError($"[BaseUltimateSkill] {name}: 궁극기 연출을 시작할 수 없습니다. 캐릭터의 Animator와 궁극기 상태 이름('{ultimateAnimStateName}') 설정을 확인하세요.", this);
            return;
        }

        unchecked { PresentationVersion++; }
        int version = PresentationVersion;
        currentData = data;
        presentationPlaying = true;
        presentationIsOwner = isOwner;
        animationPresenter.PresentationDisabled += OnPresenterDisabled;
        animationToken = animationPresenter.PlayFullBody(ultimateAnimStateName);
        if (animationToken == 0)
        {
            FailPresentation($"궁극기 상태 '{ultimateAnimStateName}'를 재생하지 못했습니다. Animator Controller의 Base Layer 상태와 캐릭터 활성/사망 상태를 확인하세요.");
            return;
        }
        cinematicController = GetComponent<CharacterCinematicController>();
        cinematicToken = cinematicController != null
            ? cinematicController.BeginCinematic(ultimateAnimStateName, isOwner)
            : 0;

        Coroutine routine = StartCoroutine(UltimateSequenceRoutine(isOwner));
        if (presentationPlaying && PresentationVersion == version) presentationRoutine = routine;
        else if (routine != null) StopCoroutine(routine);
    }

    protected virtual IEnumerator UltimateSequenceRoutine(bool isOwner)
    {
        int version = PresentationVersion;
        CharacterAnimationPresenter presenter = animationPresenter;
        int playbackToken = animationToken;

        // 궁극기 연출 중 UI 숨김
        if (isOwner && hideUIDuringUltimate && MenuToggleController.Instance != null)
        {
            MenuToggleController.Instance.SetUIVisibility(false);
        }

        float waitTime = cinematicDuration;

        // cinematicDuration이 0 이하로 설정되어 있다면 애니메이션 길이 자동 계산
        if (waitTime <= 0f)
        {
            if (presenter != null && playbackToken != 0)
            {
                yield return presenter.WaitForDuration(playbackToken, duration => waitTime = duration);
            }
            else
            {
                yield return null;
            }
            if (!IsCurrentPresentation(version)) yield break;
            if (!IsPlaybackAvailable())
            {
                FailPresentation("길이를 확인하는 동안 궁극기 애니메이션 재생이 중단되었습니다.");
                yield break;
            }
            if (waitTime > 0f)
            {
                Debug.Log($"[BaseUltimateSkill] 궁극기 애니메이션({ultimateAnimStateName}) 자동 길이 계산: {waitTime}초");
            }
            else
            {
                waitTime = 2.0f; // 상태를 찾지 못했을 때의 기본값
                Debug.LogError($"[BaseUltimateSkill] {name}: 궁극기 상태 '{ultimateAnimStateName}'의 재생 시간을 확인할 수 없어 임시값 2.0초를 사용합니다. 애니메이션 클립과 상태 설정을 확인하세요.", this);
            }
        }
        else
        {
            // 수동으로 설정된 시간이 있다면 해당 시간을 사용
            // 단, 프레임 동기화를 위해 한 프레임 쉬어주는 것이 안전합니다.
            yield return null;
        }

        float elapsed = 0f;
        while (elapsed < waitTime)
        {
            if (!IsCurrentPresentation(version)) yield break;
            if (!IsPlaybackAvailable())
            {
                FailPresentation("정상 완료 전에 궁극기 애니메이션 재생이 중단되었습니다.");
                yield break;
            }
            yield return null;
            elapsed += Time.deltaTime;
        }

        CompletePresentation(version);
    }

    private bool IsCurrentPresentation(int version)
    {
        return presentationPlaying && PresentationVersion == version;
    }

    private bool IsPlaybackAvailable()
    {
        return animationPresenter != null && animationPresenter.IsCurrentPlayback(animationToken);
    }

    private void FailPresentation(string reason)
    {
        Debug.LogError($"[BaseUltimateSkill] {name}: {reason}", this);
        CancelPresentation();
    }

    private void OnPresenterDisabled(CharacterAnimationPresenter presenter)
    {
        if (presentationPlaying && presenter == animationPresenter) CancelPresentation();
    }

    private void CompletePresentation(int version)
    {
        if (!IsCurrentPresentation(version)) return;
        if (!IsPlaybackAvailable())
        {
            FailPresentation("궁극기 완료 처리 시 애니메이션 재생 상태가 유효하지 않습니다.");
            return;
        }
        if (animationPresenter != null) animationPresenter.FinishFullBody(animationToken, true);
        if (!IsCurrentPresentation(version)) return;
        if (cinematicController != null) cinematicController.EndCinematic(cinematicToken);
        if (presentationIsOwner && hideUIDuringUltimate && MenuToggleController.Instance != null)
        {
            MenuToggleController.Instance.SetUIVisibility(true);
        }

        if (!IsCurrentPresentation(version)) return;
        ClearPresentationState();
        LastPresentationCompleted = true;
        OnUltimateEndClient();
    }

    private void ClearPresentationState()
    {
        if (animationPresenter != null) animationPresenter.PresentationDisabled -= OnPresenterDisabled;
        presentationPlaying = false;
        presentationRoutine = null;
        presentationIsOwner = false;
        animationPresenter = null;
        cinematicController = null;
        animationToken = 0;
        cinematicToken = 0;
    }

    /// <summary>Ends presentation and effects without replacing a death or hit pose.</summary>
    public void CancelPresentation()
    {
        unchecked { PresentationVersion++; }
        LastPresentationCompleted = false;
        if (!presentationPlaying) return;

        Coroutine routine = presentationRoutine;
        CharacterAnimationPresenter presenter = animationPresenter;
        CharacterCinematicController cameraController = cinematicController;
        int playbackToken = animationToken;
        int cameraToken = cinematicToken;
        bool wasOwner = presentationIsOwner;
        ClearPresentationState();

        if (routine != null) StopCoroutine(routine);
        if (presenter != null) presenter.CancelFullBody(playbackToken);
        if (cameraController != null) cameraController.EndCinematic(cameraToken);
        if (wasOwner && hideUIDuringUltimate && MenuToggleController.Instance != null)
        {
            MenuToggleController.Instance.SetUIVisibility(true);
        }
        OnUltimateCancelledClient();
    }

    /// <summary>
    /// 캐릭터 교체나 사망 등으로 도중에 컴포넌트가 꺼지면 코루틴이 멈추므로,
    /// 강제로 카메라 우선순위를 초기화하여 메인 카메라로 돌아가게 합니다.
    /// </summary>
    protected virtual void OnDisable()
    {
        CancelPresentation();
    }

    /// <summary>
    /// 서버에서만 실행되는 실제 물리적 스킬 판정입니다. (데미지, 스폰 등)
    /// </summary>
    public abstract void OnServerExecute(SkillData data, int eventId);

    /// <summary>
    /// 시네마틱 연출이 끝났을 때 클라이언트에서 할 후처리입니다.
    /// </summary>
    protected virtual void OnUltimateEndClient()
    {
        // 기본적으로 아무것도 하지 않음. 필요한 자식 클래스에서 오버라이드.
    }

    /// <summary>Releases visual effects after interruption without reporting completion.</summary>
    protected virtual void OnUltimateCancelledClient()
    {
    }
}
