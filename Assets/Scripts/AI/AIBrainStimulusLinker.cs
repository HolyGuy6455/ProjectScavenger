using UnityEngine;
using UnityEngine.Serialization;

public enum AIStimulusType
{
    Damage = 0,
    Noise = 1,
    Taunt = 2
}

/// <summary>
/// 외부 자극의 종류, 발생 당시 위치와 대상을 Blackboard에 기록합니다.
/// 자극에 어떻게 반응할지는 BT에서 결정합니다.
/// </summary>
[RequireComponent(typeof(Health), typeof(AIBrain))]
public class AIBrainStimulusLinker : MonoBehaviour
{
    [Header("Blackboard Keys")]
    [FormerlySerializedAs("attackedBoolKey")]
    [Tooltip("아직 처리하지 않은 자극이 있는지 저장할 Bool 키")]
    public string pendingBoolKey = "HasPendingStimulus";

    [FormerlySerializedAs("attackerObjKey")]
    [Tooltip("자극을 일으킨 대상을 저장할 GameObject 키. 대상 없이 위치만 전달할 수도 있습니다.")]
    public string sourceObjectKey = "StimulusSource";

    [Tooltip("자극이 발생한 순간의 위치를 저장할 Vector3 키")]
    public string positionKey = "StimulusPosition";

    [Tooltip("자극 종류를 저장할 Numeric 키 (Damage = 0, Noise = 1, Taunt = 2)")]
    public string typeKey = "StimulusType";

    private Health _health;
    private AIBrain _brain;

    private void Awake()
    {
        _health = GetComponent<Health>();
        _brain = GetComponent<AIBrain>();
    }

    private void OnEnable()
    {
        if (_health != null)
        {
            _health.OnDamageTaken += HandleDamageTaken;
        }
    }

    private void OnDisable()
    {
        if (_health != null)
        {
            _health.OnDamageTaken -= HandleDamageTaken;
        }
    }

    private void HandleDamageTaken(DamageInfo info)
    {
        // 공격자가 없는 피해에서는 공격 위치를 알 수 없습니다.
        // hitPoint는 피격자의 위치이므로 공격자의 위치로 사용하지 않습니다.
        if (info.attacker == null) return;
        ReportStimulus(AIStimulusType.Damage, info.attacker.transform.position, info.attacker);
    }

    /// <summary>
    /// 서버에서 감지한 자극을 전달합니다. 여러 자극이 오면 가장 최근 정보만 남깁니다.
    /// 소리/도발의 감지 범위와 대상 선정은 호출하는 쪽에서 처리합니다.
    /// </summary>
    public bool ReportStimulus(AIStimulusType type, Vector3 position, GameObject source = null)
    {
        if (!isActiveAndEnabled || _brain == null || !_brain.IsServer) return false;
        if (_health != null && (_health.IsDead || _health.CurrentHealth <= 0f)) return false;

        var pending = _brain.GetVariable<BTSharedBoolean>(pendingBoolKey);
        if (pending == null) return false;

        // 기존 닭의 IsAttacked/Attacker 연결은 위치·종류 키가 없어도 유지합니다.
        var sourceProperty = _brain.GetVariable<BTSharedGameObject>(sourceObjectKey);
        var positionProperty = _brain.GetVariable<BTSharedVector3>(positionKey);
        var typeProperty = _brain.GetVariable<BTSharedNumeric>(typeKey);
        if (sourceProperty != null) sourceProperty.value = source;
        if (positionProperty != null) positionProperty.value = position;
        if (typeProperty != null) typeProperty.value = (int)type;
        pending.value = true;
        return true;
    }
}
