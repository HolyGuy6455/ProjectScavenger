using UnityEngine;
using System.Collections;

public class Char01_Ultimate : BaseUltimateSkill
{
    [Header("Char01 Specific Settings")]
    [SerializeField] private ParticleSystem ultimateEffect;

    public override void OnServerExecute(SkillData data, int eventId)
    {
        switch (eventId)
        {
            case 0:
                float damageMultiplier = data != null && data.funcValue != null && data.funcValue.Length > 0 ? data.funcValue[0] : 0f;
                Debug.Log($"[Char01_Ultimate] (서버) 기본 이벤트 실행! 데미지 계수: {damageMultiplier}");
                // TODO: 기본 스폰/데미지 처리 로직
                break;
            case 1:
                Debug.Log($"[Char01_Ultimate] (서버) 1번 이벤트 실행!");
                // TODO: 추가 이벤트 로직
                break;
        }
    }

    public override void OnClientExecute(SkillData data, bool isOwner)
    {
        // 부모의 시네마틱 연출(카메라) 코루틴 실행
        base.OnClientExecute(data, isOwner);

        Debug.Log($"[Char01_Ultimate] (클라이언트) 궁극기 이펙트 발동! IsOwner: {isOwner}");

        // 예시: 파티클 효과 재생 (클라이언트에서만)
        if (ultimateEffect != null)
        {
            ultimateEffect.Play();
        }
    }

    protected override void OnUltimateEndClient()
    {
        Debug.Log("[Char01_Ultimate] (클라이언트) 궁극기 시네마틱 연출 종료 및 카메라 복귀 완료.");
        
        // 연출 후 후처리 필요 시 작성
        if (ultimateEffect != null)
        {
            ultimateEffect.Stop();
        }
    }
}
