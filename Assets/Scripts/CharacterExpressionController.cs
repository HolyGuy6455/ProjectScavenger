using System;
using UnityEngine;

/// <summary>
/// 프로필이 있으면 atlas UV를 바꾸고, 없으면 기존 표정 머테리얼을 교체합니다.
/// 애니메이션 이벤트를 받으려면 Animator와 같은 GameObject에 붙이세요.
/// 공유 머테리얼 자체는 수정하지 않으므로 다른 캐릭터의 표정에 영향을 주지 않습니다.
/// </summary>
[DisallowMultipleComponent]
public sealed class CharacterExpressionController : MonoBehaviour
{
    public const string AnimatedExpressionProperty = "animatedExpressionIndex";

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

    [Header("Atlas Expressions (Optional)")]
    [Tooltip("지정하면 아래 기존 머테리얼 목록 대신 이 프로필의 atlas/UV를 사용합니다.")]
    [SerializeField] private CharacterExpressionProfile expressionProfile;

    [Tooltip("셰이더에서 사용할 atlas texture 속성입니다. Unity Toon/URP 기본 속성을 지원합니다.")]
    [SerializeField] private string[] atlasTextureProperties = { "_MainTex", "_BaseMap" };

    [Tooltip("Base Layer 클립에서만 CONSTANT 곡선으로 키를 넣으세요. UpperBody 클립에서는 제외합니다.")]
    [SerializeField] private float animatedExpressionIndex;

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
    public string CurrentExpressionKey { get; private set; }
    public CharacterExpressionProfile Profile => expressionProfile;
    public int DefaultExpressionIndex => expressionProfile != null
        ? expressionProfile.DefaultIndex : defaultExpressionId;

    private MaterialPropertyBlock propertyBlock;
    private int lastAnimatedIndex = int.MinValue;

    private void OnEnable()
    {
        lastAnimatedIndex = int.MinValue;
        if (resetOnEnable && HasConfiguration()) ResetExpression();
    }

    private void OnDisable()
    {
        if (resetOnDisable && HasConfiguration()) ResetExpression();
    }

    private void LateUpdate() => ApplyAnimatedExpression();

    // AnimationClip.SampleAnimation/Animator가 필드를 쓴 직후에도 적용합니다.
    private void OnDidApplyAnimationProperties() => ApplyAnimatedExpression();

    /// <summary>애니메이션/수동 샘플러가 기록한 인덱스를 적용합니다. 같은 인덱스는 재적용하지 않습니다.</summary>
    public void ApplyAnimatedExpression()
    {
        if (expressionProfile == null) return;
        int index = float.IsNaN(animatedExpressionIndex) || float.IsInfinity(animatedExpressionIndex)
            ? DefaultExpressionIndex : Mathf.RoundToInt(animatedExpressionIndex);
        if (index == lastAnimatedIndex) return;
        lastAnimatedIndex = index;
        ApplyAtlasExpression(index);
    }

    /// <summary>프리팹 구성/이관용 API입니다. 동일 Animator 오브젝트에 컴포넌트를 두세요.</summary>
    public void ConfigureAtlas(Renderer renderer, int slot, CharacterExpressionProfile profile)
    {
        if (renderer == null) throw new ArgumentNullException(nameof(renderer));
        if (profile == null) throw new ArgumentNullException(nameof(profile));
        if (!profile.TryValidate(out string error)) throw new ArgumentException(error, nameof(profile));
        if (slot < 0 || slot >= renderer.sharedMaterials.Length)
            throw new ArgumentOutOfRangeException(nameof(slot));
        targetRenderer = renderer;
        materialIndex = slot;
        expressionProfile = profile;
        lastAnimatedIndex = int.MinValue;
        ResetExpression();
    }

    /// <summary>
    /// Animation Event의 Function에 SetExpression, Int에 표정 ID를 지정하세요.
    /// Atlas 모드의 미등록 ID는 기본 표정으로 돌아갑니다. 기존 머테리얼 모드는 이전 동작을 유지합니다.
    /// </summary>
    public void SetExpression(int expressionId)
    {
        if (expressionProfile != null)
        {
            animatedExpressionIndex = expressionId;
            lastAnimatedIndex = expressionId;
            ApplyAtlasExpression(expressionId);
            return;
        }
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
        CurrentExpressionKey = null;
    }

    /// <summary>프로필의 문자열 ID로 설정합니다. 기존 int Animation Event와 이름을 분리합니다.</summary>
    public void SetExpressionById(string expressionId)
    {
        if (expressionProfile == null)
        {
            Warn("문자열 표정 ID를 사용하려면 Expression Profile을 지정하세요.");
            return;
        }
        SetExpression(expressionProfile.TryGetEntry(expressionId, out var entry)
            ? entry.index : expressionProfile.DefaultIndex);
    }

    private void ApplyAtlasExpression(int index)
    {
        if (targetRenderer == null)
        {
            Warn("Target Renderer가 지정되지 않았습니다.");
            return;
        }
        if (!expressionProfile.TryGetEntry(index, out var entry)
            && !expressionProfile.TryGetEntry(expressionProfile.DefaultId, out entry))
        {
            Warn("프로필에 기본 표정이 없습니다.");
            return;
        }

        Material[] materials = targetRenderer.sharedMaterials;
        if (materialIndex < 0 || materialIndex >= materials.Length || materials[materialIndex] == null)
        {
            Warn($"유효한 얼굴 머테리얼 슬롯 {materialIndex}가 없습니다.");
            return;
        }
        if (expressionProfile.AtlasTexture == null)
        {
            Warn("프로필의 atlas texture가 없습니다.");
            return;
        }

        Vector4 transform = expressionProfile.GetTextureTransform(entry);
        if (propertyBlock == null) propertyBlock = new MaterialPropertyBlock();
        // 다른 효과가 넣은 슬롯별 속성도 유지합니다. 공유 머테리얼/다른 캐릭터는 수정하지 않습니다.
        targetRenderer.GetPropertyBlock(propertyBlock, materialIndex);
        bool hasTextureProperty = false;
        if (atlasTextureProperties != null)
        {
            foreach (string property in atlasTextureProperties)
            {
                if (string.IsNullOrEmpty(property) || !materials[materialIndex].HasProperty(property)) continue;
                propertyBlock.SetTexture(property, expressionProfile.AtlasTexture);
                propertyBlock.SetVector(property + "_ST", transform);
                hasTextureProperty = true;
            }
        }
        if (!hasTextureProperty)
        {
            Warn("머테리얼에 설정한 atlas texture 속성이 없습니다.");
            return;
        }
        targetRenderer.SetPropertyBlock(propertyBlock, materialIndex);
        CurrentExpressionId = entry.index;
        CurrentExpressionKey = entry.id;
    }

    /// <summary>
    /// 기본 표정으로 돌아갑니다. 연출 취소나 Idle 복귀 시에도 호출할 수 있습니다.
    /// </summary>
    [ContextMenu("Reset Expression")]
    public void ResetExpression()
    {
        SetExpression(DefaultExpressionIndex);
    }

    private bool HasConfiguration()
    {
        return targetRenderer != null && (expressionProfile != null || (expressions != null && expressions.Length > 0));
    }

    private void Warn(string message)
    {
        Debug.LogWarning($"[{nameof(CharacterExpressionController)}] {message}", this);
    }
}
