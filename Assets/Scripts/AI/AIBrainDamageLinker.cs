using UnityEngine;

/// <summary>
/// Health 시스템의 피격(DamageTaken) 이벤트를 수신하여,
/// AIBrain의 Blackboard(IsAttacked, Attacker 등)에 값을 전달하는 브릿지 역할을 합니다.
/// </summary>
[RequireComponent(typeof(Health), typeof(AIBrain))]
public class AIBrainDamageLinker : MonoBehaviour
{
    [Header("Blackboard Keys")]
    [Tooltip("피격 여부를 저장할 Bool 타입의 Blackboard 키")]
    public string attackedBoolKey = "IsAttacked";
    
    [Tooltip("공격한 대상을 저장할 GameObject 타입의 Blackboard 키")]
    public string attackerObjKey = "Attacker";

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
        if (_brain == null) return;

        // 1. 피격 상태(True)로 변경
        var isAttackedProp = _brain.GetVariable<BTSharedBoolean>(attackedBoolKey);
        if (isAttackedProp != null)
        {
            isAttackedProp.value = true;
        }

        // 2. 공격자 정보 저장
        if (info.attacker != null)
        {
            var attackerProp = _brain.GetVariable<BTSharedGameObject>(attackerObjKey);
            if (attackerProp != null)
            {
                attackerProp.value = info.attacker;
            }
        }
    }
}
