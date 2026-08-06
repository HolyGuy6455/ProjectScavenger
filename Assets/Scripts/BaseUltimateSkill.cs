using UnityEngine;
using System.Collections;
using Cinemachine;

public abstract class BaseUltimateSkill : MonoBehaviour
{
    [Header("Cinematic Settings")]
    [Tooltip("이 캐릭터의 궁극기 연출 전용 시네마신 가상 카메라")]
    [SerializeField] protected CinemachineVirtualCamera cinematicCamera;

    [Tooltip("시네마틱 연출 시간 (카메라가 복귀하기 전 대기 시간)")]
    [SerializeField] protected float cinematicDuration = 2.0f;

    protected UltimateSkillData currentData;

    protected virtual void Awake()
    {
        // 씬 시작 시 가상 카메라는 비활성화(혹은 우선순위 최소) 상태로 둡니다.
        if (cinematicCamera != null)
        {
            cinematicCamera.Priority = 0;
        }
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
    public virtual void OnClientExecute(UltimateSkillData data, bool isOwner)
    {
        currentData = data;
        StartCoroutine(UltimateSequenceRoutine(isOwner));
    }

    protected virtual IEnumerator UltimateSequenceRoutine(bool isOwner)
    {
        // 궁극기를 쓴 본인 화면에서만 카메라 클로즈업 연출을 합니다.
        if (isOwner && cinematicCamera != null)
        {
            cinematicCamera.Priority = 20;
        }

        // 3. 연출 시간 동안 대기
        yield return new WaitForSeconds(cinematicDuration);

        // 4. 연출 종료 및 카메라 복귀 (우선순위를 다시 낮춤)
        if (isOwner && cinematicCamera != null)
        {
            cinematicCamera.Priority = 0;
        }

        OnUltimateEndClient();
    }

    /// <summary>
    /// 캐릭터 교체나 사망 등으로 도중에 컴포넌트가 꺼지면 코루틴이 멈추므로,
    /// 강제로 카메라 우선순위를 초기화하여 메인 카메라로 돌아가게 합니다.
    /// </summary>
    protected virtual void OnDisable()
    {
        if (cinematicCamera != null)
        {
            cinematicCamera.Priority = 0;
        }
    }

    /// <summary>
    /// 서버에서만 실행되는 실제 물리적 스킬 판정입니다. (데미지, 스폰 등)
    /// </summary>
    public abstract void OnServerExecute(UltimateSkillData data, int eventId);

    /// <summary>
    /// 시네마틱 연출이 끝났을 때 클라이언트에서 할 후처리입니다.
    /// </summary>
    protected virtual void OnUltimateEndClient()
    {
        // 기본적으로 아무것도 하지 않음. 필요한 자식 클래스에서 오버라이드.
    }
}
