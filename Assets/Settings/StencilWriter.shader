Shader "Custom/StencilWriter"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            ColorMask 0
            ZWrite Off
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            
            struct appdata { 
                float4 vertex : POSITION; 
            };
            
            struct v2f { 
                float4 pos : SV_POSITION; 
            };
            
            v2f vert(appdata v) {
                v2f o;
                o.pos = TransformObjectToHClip(v.vertex.xyz);
                return o;
            }
            
            half4 frag(v2f i) : SV_Target { 
                return half4(0,0,0,0); 
            }
            ENDHLSL
        }
    }
}
