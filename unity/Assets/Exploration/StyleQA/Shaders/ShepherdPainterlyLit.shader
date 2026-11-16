// 《牧羊人》正式美術風格測試用的 URP shader（手繪感風格化 3D）。
// 目標：參考插畫那種「大塊亮面／中間調／暗面」的色塊明暗，暗面偏冷偏紫（不是單純變黑），沒有黑色描邊。
//   - 主光源：N·L 分成三段（暗、中、亮），段與段之間只留很窄的過渡；接收到的陰影一律落在暗段。
//   - 暗段＝環境光 × 偏色（_ShadowTint），環境光先「壓平」（上下半球平均）避免平滑的寫實漸層。
//   - 中段可以偏色（_MidTint），模擬插畫裡明暗交界處比較飽和的顏色。
//   - 額外光源（點光、聚光）也分段，Forward+ 的光源迴圈。
//   - 金屬類用硬邊的高光塊（_SpecStrength），木頭布料不用。
// 貼圖本身是 Blender 烘焙的手繪底色（色塊筆觸、凹處冷暗、邊緣提亮），不含光照方向。
Shader "Shepherd/PainterlyLit"
{
    Properties
    {
        _BaseMap ("Painted Albedo", 2D) = "white" {}
        _BaseColor ("Tint", Color) = (1, 1, 1, 1)
        _ShadowTint ("Shadow Tint (hue shift)", Color) = (0.78, 0.70, 0.92, 1)
        _ShadowStrength ("Shadow Brightness", Range(0.5, 3)) = 1.5
        _MidTint ("Mid-tone Tint", Color) = (1.0, 0.86, 0.80, 1)
        _MidLevel ("Mid-tone Level", Range(0, 1)) = 0.45
        _MidThreshold ("Mid Threshold (N·L)", Range(-1, 1)) = -0.05
        _LitThreshold ("Lit Threshold (N·L)", Range(-1, 1)) = 0.38
        _BandSoftness ("Band Softness", Range(0.001, 0.2)) = 0.035
        _AmbientFlatten ("Flatten Ambient", Range(0, 1)) = 0.7
        _SpecColor ("Spec Color", Color) = (1, 0.95, 0.85, 1)
        _SpecStrength ("Spec Strength", Range(0, 2)) = 0
        _SpecSize ("Spec Size", Range(0.01, 1)) = 0.25
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        LOD 200

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull [_Cull]
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile _ _FORWARD_PLUS
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor, _ShadowTint, _MidTint, _SpecColor;
                half _ShadowStrength, _MidLevel, _MidThreshold, _LitThreshold, _BandSoftness, _AmbientFlatten;
                half _SpecStrength, _SpecSize, _Cull;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                half fog : TEXCOORD3;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                o.fog = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            half Band(half x, half t) { return smoothstep(t - _BandSoftness, t + _BandSoftness, x); }

            // 點光／聚光：照度先算，再量化成兩段（亮處保留一點強弱，不會整片平均）。
            half3 BandedLight(Light l, float3 n)
            {
                half e = saturate(dot(n, l.direction)) * l.distanceAttenuation * l.shadowAttenuation;
                half q = smoothstep(0.03, 0.06, e) * 0.5 + smoothstep(0.22, 0.27, e) * 0.5;
                return l.color * q * (1 + saturate(e - 1) * 0.35);
            }

            half4 frag(Varyings i, bool front : SV_IsFrontFace) : SV_Target
            {
                half3 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).rgb * _BaseColor.rgb;
                float3 n = normalize(i.normalWS) * (front ? 1 : -1);
                float3 v = GetWorldSpaceNormalizeViewDir(i.positionWS);

                // 環境光：上下半球平均後和原本的方向性混合（壓平）。
                half3 shN = SampleSH(n);
                half3 shFlat = (SampleSH(float3(0, 1, 0)) + SampleSH(float3(0, -1, 0)) + SampleSH(float3(1, 0, 0)) + SampleSH(float3(-1, 0, 0))) * 0.25;
                half3 ambient = lerp(shN, shFlat, _AmbientFlatten);

                float4 shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                Light mainL = GetMainLight(shadowCoord, i.positionWS, half4(1, 1, 1, 1));
                half ndl = dot(n, mainL.direction);
                half sh = smoothstep(0.35, 0.65, mainL.shadowAttenuation) * mainL.distanceAttenuation;
                half mid = Band(ndl, _MidThreshold) * sh;
                half lit = Band(ndl, _LitThreshold) * sh;
                // 中段＝光色 × 中段偏色 × _MidLevel；亮段再補上剩下的量（亮面接近光的原色）。
                half3 bandLight = mainL.color * (_MidTint.rgb * _MidLevel * mid + (1 - _MidLevel * _MidTint.rgb) * lit);

                half3 col = albedo * (ambient * _ShadowTint.rgb * _ShadowStrength + bandLight);

                // 金屬：硬邊高光塊
                if (_SpecStrength > 0)
                {
                    float3 h = normalize(mainL.direction + v);
                    half s = pow(saturate(dot(n, h)), lerp(400, 12, _SpecSize));
                    col += _SpecColor.rgb * mainL.color * smoothstep(0.45, 0.55, s) * _SpecStrength * lit;
                }

                InputData inputData = (InputData)0;
                inputData.positionWS = i.positionWS;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
                half4 shadowMask = half4(1, 1, 1, 1);
                #if defined(_ADDITIONAL_LIGHTS)
                #if USE_FORWARD_PLUS
                for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
                {
                    Light l = GetAdditionalLight(lightIndex, i.positionWS, shadowMask);
                    col += albedo * BandedLight(l, n);
                }
                #endif
                uint pixelLightCount = GetAdditionalLightsCount();
                LIGHT_LOOP_BEGIN(pixelLightCount)
                    Light l = GetAdditionalLight(lightIndex, i.positionWS, shadowMask);
                    col += albedo * BandedLight(l, n);
                LIGHT_LOOP_END
                #endif

                col = MixFog(col, i.fog);
                return half4(col, 1);
            }
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
        UsePass "Universal Render Pipeline/Lit/DepthNormals"
    }
    FallBack "Universal Render Pipeline/Lit"
}
