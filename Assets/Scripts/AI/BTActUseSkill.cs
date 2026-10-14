using System;
using UnityEngine;

[Serializable]
public class BTActUseSkill : BTActionLogic, ISerializationCallbackReceiver
{
    public string mySelfKey = "MySelf";
    public string targetObjectKey = "Player/GameObj"; // 스킬의 타겟 오브젝트 (없어도 무방함)
    public string targetPositionKey = "Target/Pos";   // 스킬의 타겟 좌표 (장판기 등에 사용, 없어도 무방함)
    public BTStringInput skillIdInput = new BTStringInput();

    // 기존 씬/프리팹의 설정을 공용 입력 타입으로 한 번만 옮긴다.
    [SerializeField, HideInInspector] private bool useDirectSkillId;
    [SerializeField, HideInInspector] private string skillIdToUse = "";
    [SerializeField, HideInInspector] private string directSkillId = "";
    [SerializeField, HideInInspector] private bool _skillIdInputMigrated;

    public void OnBeforeSerialize()
    {
        // 새 노드는 공용 입력을 바로 편집하므로 이전 필드로 덮어쓰지 않는다.
        _skillIdInputMigrated = true;
    }

    public void OnAfterDeserialize()
    {
        if (_skillIdInputMigrated) return;
        skillIdInput = new BTStringInput
        {
            useDirectValue = useDirectSkillId,
            blackboardKey = skillIdToUse,
            directValue = directSkillId
        };
        _skillIdInputMigrated = true;
    }

    [NonSerialized] private EnemySkill _cachedSkill;
    [NonSerialized] private bool _isSkillTriggered = false;

    public override void ResetState()
    {
        base.ResetState();
        _isSkillTriggered = false;
        _cachedSkill = null;
    }

    public override string GetDescription(AIBrain brain)
    {
        if (skillIdInput == null) return "스킬 ID 입력이 없습니다.";
        string source = skillIdInput.useDirectValue
            ? $"직접 입력한 스킬 ID [{skillIdInput.directValue}]"
            : $"Blackboard String {GetKeyValueString<BTSharedString>(brain, skillIdInput.blackboardKey)}에 저장된 스킬 ID";
        return $"{source}로 스킬을 사용합니다.\n(스킬이 끝날 때까지 대기합니다)";
    }

    public override NodeState Evaluate(AIBrain brain)
    {
        // 실행 중에는 Blackboard 값이 바뀌어도 시작한 스킬의 완료를 기다린다.
        if (_isSkillTriggered)
        {
            if (_cachedSkill == null)
            {
                Debug.LogError("[BTActUseSkill] The executing skill no longer exists.");
                return NodeState.FAILURE;
            }
            return _cachedSkill.IsExecuting ? NodeState.RUNNING : NodeState.SUCCESS;
        }

        var mySelfProp = brain.GetVariable<BTSharedGameObject>(mySelfKey);
        if (mySelfProp == null || mySelfProp.value == null)
        {
            Debug.LogError("[BTActUseSkill] MySelf is null");
            return NodeState.FAILURE;
        }

        if (skillIdInput == null || !skillIdInput.TryGetValue(brain, out string skillId))
        {
            Debug.LogError($"[BTActUseSkill] Blackboard String key is missing or invalid: {skillIdInput?.blackboardKey}");
            return NodeState.FAILURE;
        }
        if (string.IsNullOrWhiteSpace(skillId))
        {
            Debug.LogError("[BTActUseSkill] The selected input contains an empty skill ID.");
            return NodeState.FAILURE;
        }

        _cachedSkill = null;
        foreach (var skill in mySelfProp.value.GetComponents<EnemySkill>())
        {
            if (skill.SkillId == skillId)
            {
                _cachedSkill = skill;
                break;
            }
        }

        if (_cachedSkill == null)
        {
            Debug.LogError($"[BTActUseSkill] Cannot find skill with ID: {skillId}");
            return NodeState.FAILURE;
        }

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

        // ExecuteSkill 내부에서 곧바로 끝났을 수도 있다.
        return _cachedSkill.IsExecuting ? NodeState.RUNNING : NodeState.SUCCESS;
    }
}
