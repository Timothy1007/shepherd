// 美術風格 QA 的線框疊圖（證明是真正的 3D 幾何）：不受光的單色線。
Shader "Shepherd/StyleQAWire"
{
    Properties { _Color ("Color", Color) = (0.2, 0.95, 1, 1) }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry+10" }
        Pass
        {
            Tags { "LightMode" = "UniversalForward" }
            ZTest LEqual ZWrite Off Cull Off
            Offset -1, -8
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
            CBUFFER_END
            float4 vert(float4 p : POSITION) : SV_POSITION { return TransformObjectToHClip(p.xyz); }
            half4 frag() : SV_Target { return _Color; }
            ENDHLSL
        }
    }
}
