using System;
using UnityEngine;

[Serializable]
public class BTActUseSkill : BTActionLogic
{
    public string mySelfKey = "MySelf";
    public string targetObjectKey = "Player/GameObj"; // 스킬의 타겟 오브젝트 (없어도 무방함)
    public string targetPositionKey = "Target/Pos";   // 스킬의 타겟 좌표 (장판기 등에 사용, 없어도 무방함)
    public string skillIdToUse = "";

    [NonSerialized] private EnemySkill _cachedSkill;
    [NonSerialized] private bool _isSkillTriggered = false;

    public override void ResetState()
    {
        base.ResetState();
        _isSkillTriggered = false;
    }

    public override string GetDescription(AIBrain brain)
    {
        return $"[{skillIdToUse}] 스킬을 사용합니다.\n(스킬이 끝날 때까지 대기합니다)";
    }

    public override NodeState Evaluate(AIBrain brain)
    {
        var mySelfProp = brain.GetVariable<BTSharedGameObject>(mySelfKey);
        if (mySelfProp == null || mySelfProp.value == null)
        {
            Debug.LogError("[BTActUseSkill] MySelf is null");
            return NodeState.FAILURE;
        }

        // 처음 실행되거나 캐시된 스킬이 없는 경우 탐색
        if (_cachedSkill == null)
        {
            EnemySkill[] skills = mySelfProp.value.GetComponents<EnemySkill>();
            foreach (var s in skills)
            {
                if (s.SkillId == skillIdToUse)
                {
                    _cachedSkill = s;
                    break;
                }
            }

            if (_cachedSkill == null)
            {
                Debug.LogError($"[BTActUseSkill] Cannot find skill with ID: {skillIdToUse}");
                return NodeState.FAILURE;
            }
        }

        // 아직 스킬을 트리거하지 않았다면 실행
        if (!_isSkillTriggered)
        {
            GameObject targetGo = null;
            if (!string.IsNullOrEmpty(targetObjectKey))
            {
                var targetProp = brain.GetVariable<BTSharedGameObject>(targetObjectKey);
                if (targetProp != null) targetGo = targetProp.value;
            }

            Vector3 targetPos = Vector3.zero;
            if (!string.IsNullOrEmpty(targetPositionKey))
            {
                var posProp = brain.GetVariable<BTSharedVector3>(targetPositionKey);
                if (posProp != null) targetPos = posProp.value;
            }

            _cachedSkill.ExecuteSkill(targetGo, targetPos);
            _isSkillTriggered = true;
            
            // ExecuteSkill 내부에서 곧바로 끝났을 수도 있으므로 한 번 더 체크
            if (!_cachedSkill.IsExecuting)
            {
                return NodeState.SUCCESS;
            }

            return NodeState.RUNNING;
        }
        
        // 이미 스킬을 트리거한 상태라면 완료 여부 대기
        if (_cachedSkill.IsExecuting)
        {
            return NodeState.RUNNING;
        }

        // 스킬 실행이 끝났다면 SUCCESS 반환
        return NodeState.SUCCESS;
    }
}
