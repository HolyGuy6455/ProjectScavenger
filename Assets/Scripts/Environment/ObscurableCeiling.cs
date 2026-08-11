using UnityEngine;

namespace Scavenger.Environment
{
    public class ObscurableCeiling : MonoBehaviour
    {
        private MeshRenderer meshRenderer;
        private Material originalMaterial;
        private Material transparentMaterial;
        
        private bool isObscured = false;
        private float currentAlpha = 1f;
        public float fadeSpeed = 5f;
        public float targetAlpha = 0.0f;
        
        private void Awake()
        {
            meshRenderer = GetComponent<MeshRenderer>();
            if (meshRenderer != null && meshRenderer.sharedMaterial != null)
            {
                originalMaterial = meshRenderer.sharedMaterial;
                transparentMaterial = new Material(originalMaterial);
                
                // URP Transparent setup
                transparentMaterial.SetFloat("_Surface", 1); 
                transparentMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                transparentMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                transparentMaterial.SetInt("_ZWrite", 0);
                transparentMaterial.DisableKeyword("_ALPHATEST_ON");
                transparentMaterial.EnableKeyword("_ALPHABLEND_ON");
                transparentMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                transparentMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }
        }
        
        public void SetObscured(bool state)
        {
            isObscured = state;
        }
        
        private void Update()
        {
            float target = isObscured ? targetAlpha : 1f;
            if (Mathf.Abs(currentAlpha - target) > 0.01f)
            {
                currentAlpha = Mathf.Lerp(currentAlpha, target, Time.deltaTime * fadeSpeed);
                
                if (currentAlpha >= 0.99f)
                {
                    currentAlpha = 1f;
                    if (meshRenderer.sharedMaterial != originalMaterial)
                        meshRenderer.sharedMaterial = originalMaterial;
                }
                else
                {
                    if (meshRenderer.sharedMaterial != transparentMaterial)
                        meshRenderer.sharedMaterial = transparentMaterial;
                        
                    if (transparentMaterial.HasProperty("_BaseColor"))
                    {
                        Color c = transparentMaterial.GetColor("_BaseColor");
                        c.a = currentAlpha;
                        transparentMaterial.SetColor("_BaseColor", c);
                    }
                    else if (transparentMaterial.HasProperty("_Color"))
                    {
                        Color c = transparentMaterial.GetColor("_Color");
                        c.a = currentAlpha;
                        transparentMaterial.SetColor("_Color", c);
                    }
                }
            }
        }
        
        private void OnDestroy()
        {
            if (transparentMaterial != null)
            {
                Destroy(transparentMaterial);
            }
        }
    }
}
