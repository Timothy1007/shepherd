// 《牧羊人》手繪感風格化 shader 的「提燈版」：和 Shepherd/PainterlyLit 同樣的色塊明暗，再加上提燈揭露（2026-11-17）。
//   提燈照到（記憶中）＝原本的手繪顏色；照不到（褪色態）＝同樣的色塊明暗，但降飽和、偏冷。交界有和其他物件一樣的發光細線。
//   揭露範圍用和 LanternReveal 相同的全域參數（_ShepRevealSphere 等）與相同的計算，所以和周圍的東西同步。
// 以下是原本 PainterlyLit 的說明：
// 《牧羊人》正式美術風格測試用的 URP shader（手繪感風格化 3D）。
// 目標：參考插畫那種「大塊亮面／中間調／暗面」的色塊明暗，暗面偏冷偏紫（不是單純變黑），沒有黑色描邊。
//   - 主光源：N·L 分成三段（暗、中、亮），段與段之間只留很窄的過渡；接收到的陰影一律落在暗段。
//   - 暗段＝環境光 × 偏色（_ShadowTint），環境光先「壓平」（上下半球平均）避免平滑的寫實漸層。
//   - 中段可以偏色（_MidTint），模擬插畫裡明暗交界處比較飽和的顏色。
//   - 額外光源（點光、聚光）也分段，Forward+ 的光源迴圈。
//   - 金屬類用硬邊的高光塊（_SpecStrength），木頭布料不用。
// 貼圖本身是 Blender 烘焙的手繪底色（色塊筆觸、凹處冷暗、邊緣提亮），不含光照方向。
Shader "Shepherd/PainterlyReveal"
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
        _FadedSat ("Faded Saturation", Range(0, 1)) = 0.18
        _FadedTint ("Faded Tint", Color) = (0.80, 0.84, 0.92, 1)
        _FadedLevel ("Faded Brightness", Range(0.3, 1.2)) = 0.85
        [HDR] _EdgeColor ("Edge Glow", Color) = (1.5, 0.95, 0.42, 1)
        _EdgeWidth ("Edge Width (m)", Range(0.02, 1)) = 0.35
        _Mode ("Mode (Dual)", Float) = 0
        [Toggle] _WorldUV ("World-space UV (m)", Float) = 0
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
                half4 _FadedTint, _EdgeColor;
                half _FadedSat, _FadedLevel, _EdgeWidth, _Mode, _WorldUV;
            CBUFFER_END
            float4 _ShepRevealBoxMin[8];
            float4 _ShepRevealBoxMax[8];
            float4 _ShepRevealBoxSeed[8];
            float4 _ShepRevealSphere;
            float4 _ShepRevealExtra[8];
            float4 _ShepRevealForward;
            float4 _ShepRevealShape;

        float Hash3(float3 p)
        {
            p = frac(p * 0.3183099 + 0.1);
            p *= 17.0;
            return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
        }

        float Noise3(float3 x)
        {
            float3 i = floor(x), f = frac(x);
            f = f * f * (3.0 - 2.0 * f);
            return lerp(lerp(lerp(Hash3(i), Hash3(i + float3(1,0,0)), f.x), lerp(Hash3(i + float3(0,1,0)), Hash3(i + float3(1,1,0)), f.x), f.y),
                        lerp(lerp(Hash3(i + float3(0,0,1)), Hash3(i + float3(1,0,1)), f.x), lerp(Hash3(i + float3(0,1,1)), Hash3(i + float3(1,1,1)), f.x), f.y), f.z);
        }

        // 1＝真實，0＝異域。提燈的光像真的燈一樣：靠近燈完全是真實，越遠越淡（從 0.45r 漸淡到 r）。
        // 交界帶雜訊、會緩慢流動；穩定能量的撞擊點與已復原的區域另外各是一顆小泡泡。
        // edge：只在「真實／異域」交界的一條細線上發光（寬約 _EdgeWidth 的一半），不會鋪滿整個漸變區。
        float RevealMaskEdge(float3 wp, out float edge)
        {
            // 低頻雜訊：交界緩慢起伏，不會碎成電流般的細紋。
            float n = Noise3(wp * 2.2 + _Time.y * 0.3) * 0.75 + Noise3(wp * 4.5 - _Time.y * 0.45) * 0.25;
            float w = max(_EdgeWidth, 0.02);
            float m = 0.0;
            edge = 0.0;
            float r = _ShepRevealSphere.w;
            if (r > 0.0)
            {
                float3 v = wp - _ShepRevealSphere.xyz;
                float3 fw = _ShepRevealForward.xyz;
                float3 sh = _ShepRevealShape.xyz;
                if (dot(fw, fw) < 0.5) { fw = float3(0, 0, 1); sh = float3(1, 1, 1); }
                float along = dot(v, fw);
                float3 perp = v - fw * along;
                float a = along / (along > 0.0 ? max(sh.x, 0.05) : max(sh.z, 0.05));
                // 側面寬度隨距離變寬（_ShepRevealShape.w＝每公尺增加的倍率）：越遠越寬的錐形，像真的光。
                float side = max(sh.y + max(along, 0.0) * _ShepRevealShape.w, 0.05);
                float d = sqrt(a * a + dot(perp, perp) / (side * side)) + (n - 0.5) * w * 0.9;
                m = 1.0 - smoothstep(r * 0.45, r, d);
                edge = max(edge, 1.0 - saturate(abs(d - r * 0.725) / (w * 0.5)));
            }
            [unroll] for (int k = 0; k < 8; k++)
            {
                float4 s = _ShepRevealExtra[k];
                if (s.w > 0.0)
                {
                    float de = distance(wp, s.xyz) + (n - 0.5) * w * 0.8;
                    m = max(m, 1.0 - smoothstep(s.w * 0.6, s.w, de));
                    edge = max(edge, 1.0 - saturate(abs(de - s.w * 0.8) / (w * 0.4)));
                }
            }
            [unroll] for (int b = 0; b < 8; b++)
            {
                float4 bmin = _ShepRevealBoxMin[b];
                if (bmin.w > 0.0)
                {
                    float3 bmax = _ShepRevealBoxMax[b].xyz;
                    float3 q = max(bmin.xyz - wp, wp - bmax);
                    float outside = length(max(q, 0.0)) + min(max(q.x, max(q.y, q.z)), 0.0);
                    float inBox = 1.0 - smoothstep(-0.02, 0.06, outside);
                    float4 seed = _ShepRevealBoxSeed[b];
                    float ds = distance(wp, seed.xyz) + (n - 0.5) * 0.6;
                    float front = 1.0 - smoothstep(seed.w - 1.2, seed.w, ds);
                    m = max(m, inBox * front);
                    if (seed.w < 25.0) edge = max(edge, inBox * (1.0 - saturate(abs(ds - seed.w + 0.5) / 0.35)));
                }
            }
            edge = edge * edge * edge;
            return m;
        }



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
                // 牆、地板這類用世界座標貼圖的材質（和 LanternReveal 一樣：依面的方向選 xz／zy／xy）。
                float2 uvT = i.uv;
                if (_WorldUV > 0.5)
                {
                    float3 an = abs(normalize(i.normalWS));
                    float2 w = an.y > max(an.x, an.z) ? i.positionWS.xz : (an.x > an.z ? i.positionWS.zy : i.positionWS.xy);
                    uvT = w * _BaseMap_ST.xy + _BaseMap_ST.zw;
                }
                half3 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uvT).rgb * _BaseColor.rgb;
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

                // 提燈揭露：照到＝記憶中的顏色；照不到＝褪色（降飽和、偏冷、稍暗）。
                float edge;
                float m = RevealMaskEdge(i.positionWS, edge);
                half g = dot(col, half3(0.299, 0.587, 0.114));
                half3 faded = lerp(half3(g, g, g), col, _FadedSat) * _FadedTint.rgb * _FadedLevel;
                col = lerp(faded, col, m) + _EdgeColor.rgb * edge * 0.32;
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
