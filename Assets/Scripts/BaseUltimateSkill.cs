using UnityEngine;
using System.Collections;
using Cinemachine;

public abstract class BaseUltimateSkill : MonoBehaviour
{
    [Header("Cinematic Settings")]

    [Tooltip("시네마틱 연출 시간 (0 이하로 설정 시 애니메이션 길이를 자동 계산합니다)")]
    [SerializeField] protected float cinematicDuration = -1f;

    [Tooltip("궁극기 발동 시 재생할 애니메이션 상태 이름 (비워두면 재생하지 않음)")]
    [SerializeField] protected string ultimateAnimStateName = "Ultimate";

    protected SkillData currentData;

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
        currentData = data;
        
        // 애니메이션 재생
        Animator anim = GetComponentInChildren<Animator>();
        if (anim != null && !string.IsNullOrEmpty(ultimateAnimStateName))
        {
            // 궁극기 도중 상반신 레이어(UpperBody) 애니메이션 덮어쓰기 방지
            if (anim.layerCount > 1)
            {
                anim.SetLayerWeight(1, 0f);
            }
            
            anim.Play(ultimateAnimStateName, 0, 0f);
        }

        StartCoroutine(UltimateSequenceRoutine(isOwner));
    }

    protected virtual IEnumerator UltimateSequenceRoutine(bool isOwner)
    {
        CharacterCinematicController cinematicCtrl = GetComponent<CharacterCinematicController>();
        CinemachineVirtualCamera cinematicCamera = cinematicCtrl != null ? cinematicCtrl.CinematicCamera : null;

        // 궁극기를 쓴 본인 화면에서만 카메라 클로즈업 연출을 합니다.
        if (isOwner && cinematicCamera != null)
        {
            cinematicCamera.Priority = 20;
        }

        Animator anim = GetComponentInChildren<Animator>();
        float waitTime = cinematicDuration;

        // cinematicDuration이 0 이하로 설정되어 있다면 애니메이션 길이 자동 계산
        if (waitTime <= 0f && anim != null)
        {
            // Play() 직후 상태가 갱신될 때까지 한 프레임 대기
            yield return null; 
            
            AnimatorStateInfo stateInfo = anim.GetCurrentAnimatorStateInfo(0);
            if (stateInfo.IsName(ultimateAnimStateName))
            {
                waitTime = stateInfo.length;
                Debug.Log($"[BaseUltimateSkill] 궁극기 애니메이션({ultimateAnimStateName}) 자동 길이 계산: {waitTime}초");
            }
            else
            {
                waitTime = 2.0f; // 상태를 찾지 못했을 때의 기본값
                Debug.LogWarning($"[BaseUltimateSkill] 궁극기 상태를 찾을 수 없어 기본값(2.0초)을 사용합니다.");
            }
        }
        else
        {
            // 수동으로 설정된 시간이 있다면 해당 시간을 사용
            // 단, 프레임 동기화를 위해 한 프레임 쉬어주는 것이 안전합니다.
            yield return null;
        }

        // 3. 연출 시간 동안 대기
        yield return new WaitForSeconds(waitTime);

        // 4. 연출 종료 및 카메라 복귀 (우선순위를 다시 낮춤)
        if (isOwner && cinematicCamera != null)
        {
            cinematicCamera.Priority = 0;
        }

        // 애니메이션 복구 (위에서 선언한 anim 재사용)
        if (anim != null)
        {
            // 상반신 레이어 가중치 원상복구
            if (anim.layerCount > 1)
            {
                anim.SetLayerWeight(1, 1f);
            }
            // 멈춰있지 않고 원래의 걷기/대기 상태(Movement)로 돌아가도록 강제 이동
            anim.Play("Movement", 0, 0f);
        }

        OnUltimateEndClient();
    }

    /// <summary>
    /// 캐릭터 교체나 사망 등으로 도중에 컴포넌트가 꺼지면 코루틴이 멈추므로,
    /// 강제로 카메라 우선순위를 초기화하여 메인 카메라로 돌아가게 합니다.
    /// </summary>
    protected virtual void OnDisable()
    {
        CharacterCinematicController cinematicCtrl = GetComponent<CharacterCinematicController>();
        if (cinematicCtrl != null && cinematicCtrl.CinematicCamera != null)
        {
            cinematicCtrl.CinematicCamera.Priority = 0;
        }

        // 도중에 꺼졌을 경우를 대비해 상반신 레이어 원상복구
        Animator anim = GetComponentInChildren<Animator>();
        if (anim != null && anim.layerCount > 1)
        {
            anim.SetLayerWeight(1, 1f);
        }
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
}
