// 提燈的玻璃與火焰：不受光、自己發光。玻璃半透明（看得到裡面的火焰），中央亮、邊緣暗一點（像參考圖玻璃裡的光）。
// 火焰用同一個 shader、不透明（_Alpha＝1），renderQueue 放在玻璃之前。
Shader "Shepherd/StyleQAGlow"
{
    Properties
    {
        _BaseMap ("Painted Albedo", 2D) = "white" {}
        _Color ("Glow Color", Color) = (1, 0.82, 0.55, 1)
        _Intensity ("Intensity", Range(0, 4)) = 1.4
        _CoreBoost ("Facing Boost", Range(0, 2)) = 0.8
        _Alpha ("Alpha", Range(0, 1)) = 0.72
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 10
        [Toggle] _ZWriteOn ("ZWrite", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" }
        Pass
        {
            Tags { "LightMode" = "UniversalForward" }
            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWriteOn]
            Cull Back
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _Color;
                half _Intensity, _CoreBoost, _Alpha, _SrcBlend, _DstBlend, _ZWriteOn;
            CBUFFER_END
            struct A { float4 p : POSITION; float3 n : NORMAL; float2 uv : TEXCOORD0; };
            struct V { float4 p : SV_POSITION; float2 uv : TEXCOORD0; float3 n : TEXCOORD1; float3 w : TEXCOORD2; };
            V vert(A a)
            {
                V o;
                o.w = TransformObjectToWorld(a.p.xyz);
                o.p = TransformWorldToHClip(o.w);
                o.n = TransformObjectToWorldNormal(a.n);
                o.uv = TRANSFORM_TEX(a.uv, _BaseMap);
                return o;
            }
            half4 frag(V i) : SV_Target
            {
                half3 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).rgb;
                float3 v = GetWorldSpaceNormalizeViewDir(i.w);
                half facing = saturate(dot(normalize(i.n), v));
                // 面正對鏡頭時比較亮（平面著色，所以每一塊玻璃是一個亮度：切面感）
                half k = _Intensity * (0.55 + _CoreBoost * facing * facing);
                return half4(tex * _Color.rgb * k, _Alpha);
            }
            ENDHLSL
        }
    }
}
