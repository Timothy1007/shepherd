// 提燈「顯示真實」原型 shader（URP）。
// 全域 _ShepRevealSphere（xyz＝光圈中心、w＝半徑；w≤0＝提燈關閉）決定每個像素是「真實」還是「異域」。
// _Mode：0＝雙狀態（光圈內顯示真實外觀、光圈外顯示異域外觀）；1＝只存在於真實（光圈外消失）；2＝只存在於異域（光圈內消失）。
// 光圈外的異域物件會輕微扭動；交界有一圈帶雜訊的暖光邊。
Shader "Shepherd/Prototype/LanternReveal"
{
    Properties
    {
        [Enum(Dual,0,RealOnly,1,AnomalyOnly,2,AlwaysReal,3)] _Mode ("Mode", Float) = 0
        [MainColor] _BaseColor ("Real Color", Color) = (1,1,1,1)
        [MainTexture] _BaseMap ("Real Albedo", 2D) = "white" {}
        _Smoothness ("Real Smoothness", Range(0,1)) = 0.3
        _AnomalyColor ("Anomaly Color", Color) = (0.55,0.62,0.6,1)
        _AnomalyMap ("Anomaly Albedo", 2D) = "white" {}
        _AnomalySmoothness ("Anomaly Smoothness", Range(0,1)) = 0.55
        _Metallic ("Metallic", Range(0,1)) = 0
        [HDR] _EmissionColor ("Real Emission", Color) = (0,0,0,1)
        [HDR] _AnomalyEmission ("Anomaly Emission", Color) = (0,0,0,1)
        _Wobble ("Anomaly Wobble (m)", Range(0,0.2)) = 0.015
        [HDR] _EdgeColor ("Edge Glow", Color) = (1.5,0.95,0.42,1)
        _EdgeWidth ("Edge Width (m)", Range(0.02,1)) = 0.35
        [Toggle] _WorldUV ("World-space UV (m)", Float) = 0
        [Normal] _BumpMap ("Normal Map", 2D) = "bump" {}
        _BumpScale ("Normal Strength", Range(0,2)) = 1
        _RoughMap ("Roughness (R)", 2D) = "white" {}
        [Toggle] _UseRoughMap ("Use Roughness Map", Float) = 0
        [Toggle] _AnomalyFromBase ("Anomaly uses Real texture", Float) = 0
        _AnomalySat ("Anomaly Saturation", Range(0,1)) = 0.25
        [Toggle] _RuinMap ("Anomaly uses its own Ruin maps", Float) = 0
        [Normal] _AnomalyBump ("Ruin Normal Map", 2D) = "bump" {}
        _AnomalyRough ("Ruin Roughness (R)", 2D) = "white" {}
        _Cull ("Cull", Float) = 2
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        LOD 300
        Cull [_Cull]

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor, _BaseMap_ST, _AnomalyColor, _AnomalyMap_ST, _EmissionColor, _AnomalyEmission, _EdgeColor;
            float _Mode, _Smoothness, _AnomalySmoothness, _Metallic, _Wobble, _EdgeWidth, _Cull, _WorldUV;
            float4 _BumpMap_ST;
            float _BumpScale, _UseRoughMap, _AnomalyFromBase, _AnomalySat, _RuinMap;
        CBUFFER_END
        // 整間房永久恢復真實（修好那個房間的線索）：盒子範圍內、從線索處往外擴散。
        // Min.xyz／Max.xyz＝盒子；Min.w＞0＝使用中；Seed.xyz＝擴散起點；Seed.w＝目前擴散半徑。
        float4 _ShepRevealBoxMin[8];
        float4 _ShepRevealBoxMax[8];
        float4 _ShepRevealBoxSeed[8];
        TEXTURE2D(_AnomalyBump); SAMPLER(sampler_AnomalyBump);
        TEXTURE2D(_AnomalyRough); SAMPLER(sampler_AnomalyRough);
        float4 _ShepRevealSphere;
        // 風格化（參考《冤罪殺機》的手繪感半寫實）：0＝原樣，1＝風格化。全域開關，方便 A/B 比較。
        float _ShepStylize;
        // 額外的現實泡泡（穩定能量的撞擊點、已復原的穩定區）：xyz＝中心、w＝半徑；w≤0＝未使用。
        float4 _ShepRevealExtra[8];
        // 提燈光的形狀：往前偏的蛋形。_ShepRevealForward.xyz＝朝向；_ShepRevealShape＝(前方倍率, 側面倍率, 後方倍率)。
        float4 _ShepRevealForward;
        float4 _ShepRevealShape;
        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        TEXTURE2D(_AnomalyMap); SAMPLER(sampler_AnomalyMap);
        TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);
        TEXTURE2D(_RoughMap); SAMPLER(sampler_RoughMap);

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

        float RevealMask(float3 wp)
        {
            float e;
            return RevealMaskEdge(wp, e);
        }

        float3 Wobble(float3 wp, float mask)
        {
            float t = _Time.y;
            float3 o = float3(sin(wp.y * 3.7 + t * 1.3), sin(wp.z * 4.1 + t * 1.1) * 0.5, sin(wp.x * 3.3 + t * 1.7));
            return wp + o * _Wobble * (1.0 - mask);
        }

        // 3＝永遠是真實（現實世界的街道）：不裁切、不扭動、沒有光圈邊緣。
        float MaskFor(float3 wp) { return _Mode > 2.5 ? 1.0 : RevealMask(wp); }

        void ClipByMode(float mask)
        {
            if (_Mode > 2.5) return;
            if (_Mode > 1.5) clip(0.5 - mask);
            else if (_Mode > 0.5) clip(mask - 0.5);
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _FORWARD_PLUS
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct A { float4 pos : POSITION; float3 normal : NORMAL; float4 tangent : TANGENT; float2 uv : TEXCOORD0; };
            struct V
            {
                float4 pos : SV_POSITION;
                float3 wp : TEXCOORD0;
                float3 wn : TEXCOORD1;
                float2 uv : TEXCOORD2;
                float3 sh : TEXCOORD3;
                float fog : TEXCOORD4;
                float4 wt : TEXCOORD5;
            };

            V vert(A a)
            {
                V o;
                float3 wp = TransformObjectToWorld(a.pos.xyz);
                wp = Wobble(wp, MaskFor(wp));
                o.wp = wp;
                o.wn = TransformObjectToWorldNormal(a.normal);
                o.wt = float4(TransformObjectToWorldDir(a.tangent.xyz), a.tangent.w * GetOddNegativeScale());
                o.pos = TransformWorldToHClip(wp);
                o.uv = a.uv;
                o.sh = SampleSH(o.wn);
                o.fog = ComputeFogFactor(o.pos.z);
                return o;
            }

            half4 frag(V i) : SV_Target
            {
                float edge;
                float m = RevealMaskEdge(i.wp, edge);
                ClipByMode(m);
                float2 uv = i.uv;
                float3 n0 = normalize(i.wn);
                float3 T, B;
                if (_WorldUV > 0.5)
                {
                    float3 an = abs(n0);
                    if (an.y > max(an.x, an.z)) { uv = i.wp.xz; T = float3(1, 0, 0); B = float3(0, 0, 1) * sign(n0.y); }
                    else if (an.x > an.z) { uv = i.wp.zy; T = float3(0, 0, 1) * -sign(n0.x); B = float3(0, 1, 0); }
                    else { uv = i.wp.xy; T = float3(1, 0, 0) * sign(n0.z); B = float3(0, 1, 0); }
                }
                else
                {
                    T = normalize(i.wt.xyz);
                    B = cross(n0, T) * (i.wt.w < 0 ? -1.0 : 1.0);
                }
                float2 uvB = uv * _BaseMap_ST.xy + _BaseMap_ST.zw;
                // 風格化：貼圖取較模糊的 mip（細節變少、像手繪的色塊），法線變淡。
                float sty = saturate(_ShepStylize);
                float bias = sty * 1.8;
                float4 baseTex = SAMPLE_TEXTURE2D_BIAS(_BaseMap, sampler_BaseMap, uvB, bias);
                float4 realCol = baseTex * _BaseColor;
                float4 anomCol;
                if (_AnomalyFromBase > 0.5)
                {
                    float g = dot(baseTex.rgb, float3(0.3, 0.59, 0.11));
                    anomCol = float4(lerp(float3(g, g, g), baseTex.rgb, _AnomalySat), 1) * _AnomalyColor;
                }
                else anomCol = SAMPLE_TEXTURE2D(_AnomalyMap, sampler_AnomalyMap, uv * _AnomalyMap_ST.xy + _AnomalyMap_ST.zw) * _AnomalyColor;
                float2 uvA = uv * _AnomalyMap_ST.xy + _AnomalyMap_ST.zw;
                if (_RuinMap > 0.5)
                {
                    // 廢墟：異域有自己的貼圖（剝落的灰泥、髒磁磚、舊木板），再降一點飽和、偏冷。
                    float4 ruin = SAMPLE_TEXTURE2D_BIAS(_AnomalyMap, sampler_AnomalyMap, uvA, bias);
                    float g = dot(ruin.rgb, float3(0.3, 0.59, 0.11));
                    anomCol = float4(lerp(float3(g, g, g), ruin.rgb, _AnomalySat), 1) * _AnomalyColor;
                }
                float k = _Mode > 0.5 ? (_Mode > 1.5 ? 0.0 : 1.0) : m;
                if (_Mode > 2.5) { k = 1.0; edge = 0.0; }
                float bumpS = _BumpScale * lerp(1.0, 0.45, sty);
                float3 nTS = UnpackNormalScale(SAMPLE_TEXTURE2D_BIAS(_BumpMap, sampler_BumpMap, uvB, bias), bumpS);
                if (_RuinMap > 0.5)
                {
                    float3 nA = UnpackNormalScale(SAMPLE_TEXTURE2D_BIAS(_AnomalyBump, sampler_AnomalyBump, uvA, bias), bumpS);
                    nTS = normalize(lerp(nA, nTS, k));
                }
                float3 nWS = normalize(T * nTS.x + B * nTS.y + n0 * nTS.z);
                float rough = SAMPLE_TEXTURE2D(_RoughMap, sampler_RoughMap, uvB).r;
                float realSmooth = _UseRoughMap > 0.5 ? saturate((1.0 - rough) * _Smoothness) : _Smoothness;
                float anomSmooth = _RuinMap > 0.5 ? saturate((1.0 - SAMPLE_TEXTURE2D(_AnomalyRough, sampler_AnomalyRough, uvA).r) * _AnomalySmoothness) : _AnomalySmoothness;

                InputData d = (InputData)0;
                d.positionWS = i.wp;
                d.positionCS = i.pos;
                d.normalWS = nWS;
                d.viewDirectionWS = GetWorldSpaceNormalizeViewDir(i.wp);
                d.shadowCoord = TransformWorldToShadowCoord(i.wp);
                d.fogCoord = i.fog;
                d.bakedGI = i.sh;
                d.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.pos);
                d.shadowMask = half4(1, 1, 1, 1);

                SurfaceData s = (SurfaceData)0;
                s.albedo = lerp(anomCol.rgb, realCol.rgb, k);
                if (sty > 0.001)
                {
                    // 真實（家）：飽和一點、暖一點；廢墟：維持褪色。
                    float ga = dot(s.albedo, float3(0.3, 0.59, 0.11));
                    float3 rich = lerp(float3(ga, ga, ga), s.albedo, 1.0 + 0.25 * k) * lerp(float3(1, 1, 1), float3(1.04, 1.0, 0.94), k);
                    s.albedo = lerp(s.albedo, saturate(rich), sty);
                }
                s.metallic = _Metallic;
                s.smoothness = lerp(anomSmooth, realSmooth, k);
                s.occlusion = 1;
                edge *= 0.32;
                s.emission = lerp(_AnomalyEmission.rgb, _EmissionColor.rgb, k) + _EdgeColor.rgb * edge;
                s.alpha = 1;
                s.normalTS = float3(0, 0, 1);
                half4 col = UniversalFragmentPBR(d, s);
                if (sty > 0.001)
                {
                    // 手繪感的光：亮暗分成柔和的色階，暗部帶冷藍紫、亮部偏暖；朝上的面略亮、朝下的面略暗；邊緣一點暖色的勾邊。
                    float3 alb = max(s.albedo, 0.03);
                    float L = dot(col.rgb, float3(0.3, 0.59, 0.11)) / max(dot(alb, float3(0.3, 0.59, 0.11)), 0.03);
                    float stepped = lerp(L, (smoothstep(0.05, 0.22, L) * 0.45 + smoothstep(0.45, 0.75, L) * 0.55) * max(L, 0.35), 0.4);
                    float3 tint = lerp(float3(0.78, 0.8, 0.98), float3(1.05, 1.0, 0.93), saturate(L * 1.4));
                    // 保留燈光本身的顏色（暖燈、冷月光），只把亮度整理成色階、暗部帶一點冷色。
                    float3 painted = col.rgb * (stepped / max(L, 0.03)) * tint;
                    painted *= lerp(0.86, 1.06, n0.y * 0.5 + 0.5);
                    float rim = pow(1.0 - saturate(dot(nWS, d.viewDirectionWS)), 3.0) * saturate(L) * 0.12;
                    painted += rim * float3(1.0, 0.85, 0.65) * alb;
                    col.rgb = lerp(col.rgb, painted, sty);
                }
                col.rgb = MixFog(col.rgb, i.fog);
                return col;
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            float3 _LightDirection;
            float3 _LightPosition;
            struct A { float4 pos : POSITION; float3 normal : NORMAL; };
            struct V { float4 pos : SV_POSITION; float3 wp : TEXCOORD0; };
            V vert(A a)
            {
                V o;
                float3 wp = TransformObjectToWorld(a.pos.xyz);
                wp = Wobble(wp, MaskFor(wp));
                float3 wn = TransformObjectToWorldNormal(a.normal);
            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 ld = normalize(_LightPosition - wp);
            #else
                float3 ld = _LightDirection;
            #endif
                float4 p = TransformWorldToHClip(ApplyShadowBias(wp, wn, ld));
            #if UNITY_REVERSED_Z
                p.z = min(p.z, UNITY_NEAR_CLIP_VALUE);
            #else
                p.z = max(p.z, UNITY_NEAR_CLIP_VALUE);
            #endif
                o.pos = p;
                o.wp = wp;
                return o;
            }
            half4 frag(V i) : SV_Target { ClipByMode(RevealMask(i.wp)); return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On ColorMask R
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            struct A { float4 pos : POSITION; };
            struct V { float4 pos : SV_POSITION; float3 wp : TEXCOORD0; };
            V vert(A a) { V o; float3 wp = TransformObjectToWorld(a.pos.xyz); wp = Wobble(wp, MaskFor(wp)); o.wp = wp; o.pos = TransformWorldToHClip(wp); return o; }
            half frag(V i) : SV_Target { ClipByMode(RevealMask(i.wp)); return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            struct A { float4 pos : POSITION; float3 normal : NORMAL; };
            struct V { float4 pos : SV_POSITION; float3 wp : TEXCOORD0; float3 wn : TEXCOORD1; };
            V vert(A a) { V o; float3 wp = TransformObjectToWorld(a.pos.xyz); wp = Wobble(wp, MaskFor(wp)); o.wp = wp; o.wn = TransformObjectToWorldNormal(a.normal); o.pos = TransformWorldToHClip(wp); return o; }
            half4 frag(V i) : SV_Target { ClipByMode(RevealMask(i.wp)); return half4(NormalizeNormalPerPixel(normalize(i.wn)), 0); }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
