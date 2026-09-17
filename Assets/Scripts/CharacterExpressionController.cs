using System;
using UnityEngine;

/// <summary>
/// 표정 ID에 따라 지정한 Renderer의 머테리얼 슬롯을 교체합니다.
/// 애니메이션 이벤트를 받으려면 Animator와 같은 GameObject에 붙이세요.
/// 공유 머테리얼 자체는 수정하지 않으므로 다른 캐릭터의 표정에 영향을 주지 않습니다.
/// </summary>
[DisallowMultipleComponent]
public sealed class CharacterExpressionController : MonoBehaviour
{
    [Serializable]
    private sealed class Expression
    {
        [Tooltip("Inspector에서 구분하기 위한 표정 이름입니다.")]
        public string displayName;

        [Min(0), Tooltip("SetExpression에 전달할 고유 번호입니다. 목록 순서를 바꿔도 번호는 유지됩니다.")]
        public int id;

        [Tooltip("이 표정에 사용할 머테리얼입니다. 모델의 UV와 셰이더에 맞게 준비하세요.")]
        public Material material;
    }

    [Header("Expression Target")]
    [Tooltip("얼굴이 포함된 MeshRenderer 또는 SkinnedMeshRenderer입니다.")]
    [SerializeField] private Renderer targetRenderer;

    [Min(0), Tooltip("Renderer의 Materials 목록에서 교체할 슬롯 번호입니다. 첫 슬롯은 0입니다.")]
    [SerializeField] private int materialIndex;

    [Header("Expression Library")]
    [SerializeField] private Expression[] expressions = Array.Empty<Expression>();

    [Min(0), Tooltip("ResetExpression에서 적용할 표정 번호입니다.")]
    [SerializeField] private int defaultExpressionId;

    [Tooltip("활성화되거나 풀에서 재사용될 때 기본 표정으로 돌아갑니다.")]
    [SerializeField] private bool resetOnEnable = true;

    [Tooltip("비활성화될 때 기본 표정으로 돌아갑니다.")]
    [SerializeField] private bool resetOnDisable = true;

    /// <summary>아직 표정을 적용하지 않았다면 -1입니다.</summary>
    public int CurrentExpressionId { get; private set; } = -1;

    private void OnEnable()
    {
        if (resetOnEnable && HasConfiguration()) ResetExpression();
    }

    private void OnDisable()
    {
        if (resetOnDisable && HasConfiguration()) ResetExpression();
    }

    /// <summary>
    /// Animation Event의 Function에 SetExpression, Int에 표정 ID를 지정하세요.
    /// 잘못된 ID나 설정이면 기존 표정을 유지합니다.
    /// </summary>
    public void SetExpression(int expressionId)
    {
        if (targetRenderer == null)
        {
            Warn("Target Renderer가 지정되지 않았습니다.");
            return;
        }

        Expression selected = null;
        if (expressions != null)
        {
            foreach (Expression expression in expressions)
            {
                if (expression == null || expression.id != expressionId) continue;
                if (selected != null)
                {
                    Warn($"표정 ID {expressionId}가 중복되었습니다. 각 표정에 고유 번호를 지정하세요.");
                    return;
                }
                selected = expression;
            }
        }

        if (expressionId < 0 || selected == null || selected.material == null)
        {
            Warn($"표정 ID {expressionId} 또는 해당 머테리얼이 등록되지 않았습니다.");
            return;
        }

        // sharedMaterials는 슬롯 배열의 복사본입니다. 머테리얼 에셋을 변경하거나
        // renderer.material로 캐릭터별 머테리얼 인스턴스를 생성하지 않습니다.
        Material[] materials = targetRenderer.sharedMaterials;
        if (materialIndex < 0 || materialIndex >= materials.Length)
        {
            Warn($"머테리얼 슬롯 {materialIndex}가 없습니다. 슬롯 수: {materials.Length}");
            return;
        }

        if (materials[materialIndex] != selected.material)
        {
            materials[materialIndex] = selected.material;
            targetRenderer.sharedMaterials = materials;
        }
        CurrentExpressionId = expressionId;
    }

    /// <summary>
    /// 기본 표정으로 돌아갑니다. 연출 취소나 Idle 복귀 시에도 호출할 수 있습니다.
    /// </summary>
    [ContextMenu("Reset Expression")]
    public void ResetExpression()
    {
        SetExpression(defaultExpressionId);
    }

    private bool HasConfiguration()
    {
        return targetRenderer != null && expressions != null && expressions.Length > 0;
    }

    private void Warn(string message)
    {
        Debug.LogWarning($"[{nameof(CharacterExpressionController)}] {message}", this);
    }
}
