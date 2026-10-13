using UnityEngine;

/// <summary>Samples the two-tile face atlas. Animated by Brownie's clip curves.</summary>
[ExecuteAlways]
public sealed class BrownieFaceExpression : MonoBehaviour
{
    [SerializeField] private Renderer faceRenderer;
    [SerializeField] private int faceMaterialIndex;
    [Range(0f, 1f)] public float faceExpression;
    private MaterialPropertyBlock block;
    private static readonly int BaseMapST = Shader.PropertyToID("_BaseMap_ST");

    private void OnEnable() { ApplyExpression(); }
    private void LateUpdate() { ApplyExpression(); }

    public void ApplyExpression()
    {
        if (faceRenderer == null || faceMaterialIndex < 0 ||
            faceMaterialIndex >= faceRenderer.sharedMaterials.Length) return;
        if (block == null) block = new MaterialPropertyBlock();
        faceRenderer.GetPropertyBlock(block, faceMaterialIndex);
        block.SetVector(BaseMapST, new Vector4(1f, 1f,
            faceExpression >= 0.5f ? 0.5f : 0f, 0f));
        faceRenderer.SetPropertyBlock(block, faceMaterialIndex);
    }

    private void OnDisable()
    {
        if (faceRenderer == null || block == null) return;
        faceRenderer.GetPropertyBlock(block, faceMaterialIndex);
        block.SetVector(BaseMapST, new Vector4(1f, 1f, 0f, 0f));
        faceRenderer.SetPropertyBlock(block, faceMaterialIndex);
    }
}
