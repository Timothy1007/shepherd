// Case01 室內材質：內建渲染管線（Built-in）的 Standard 光照 Surface Shader，世界座標 triplanar 投影。
// 房屋以 Runtime Primitive 組成，方塊 UV 會隨縮放拉伸；改用世界座標取樣，木紋／牆面在任何尺寸下都維持正確比例。
// 主貼圖 RGB＝顏色、A＝光滑度；法線貼圖以 whiteout 混合後轉回切線空間。
Shader "Shepherd/Case01/Triplanar"
{
    Properties
    {
        _Color ("Tint", Color) = (1,1,1,1)
        _MainTex ("Albedo (RGB) Smoothness (A)", 2D) = "white" {}
        _BumpMap ("Normal Map", 2D) = "bump" {}
        _BumpScale ("Normal Strength", Range(0, 2)) = 1
        _Scale ("Tiles per Meter", Float) = 0.5
        _Glossiness ("Smoothness Multiplier", Range(0, 1)) = 0.5
        _Metallic ("Metallic", Range(0, 1)) = 0
        _Sharpness ("Blend Sharpness", Range(1, 16)) = 6
        _EmissionMap ("Emission (RGB)", 2D) = "white" {}
        [HDR] _EmissionColor ("Emission Color", Color) = (0,0,0,1)
    }
    // URP 版本（專案改用 Universal Render Pipeline 時使用；內建管線會略過這個 SubShader）。
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        LOD 300

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
            float4 _Color;
            float4 _MainTex_ST;
            float4 _EmissionColor;
            float _BumpScale, _Scale, _Glossiness, _Metallic, _Sharpness;
        CBUFFER_END
        TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
        TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);
        TEXTURE2D(_EmissionMap); SAMPLER(sampler_EmissionMap);

        float3 TriWeights(float3 wn)
        {
            float3 w = pow(abs(wn), _Sharpness);
            return w / max(w.x + w.y + w.z, 1e-4);
        }

        float3 TriNormal(float3 wp, float3 wn, float3 w)
        {
            float2 uvX = wp.zy * _Scale, uvY = wp.xz * _Scale, uvZ = wp.xy * _Scale;
            float3 nX = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uvX), _BumpScale);
            float3 nY = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uvY), _BumpScale);
            float3 nZ = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uvZ), _BumpScale);
            float3 tX = float3(nX.xy + wn.zy, abs(nX.z) * wn.x);
            float3 tY = float3(nY.xy + wn.xz, abs(nY.z) * wn.y);
            float3 tZ = float3(nZ.xy + wn.xy, abs(nZ.z) * wn.z);
            return normalize(tX.zyx * w.x + tY.xzy * w.y + tZ.xyz * w.z);
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
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct A { float4 pos : POSITION; float3 normal : NORMAL; float2 uv1 : TEXCOORD1; };
            struct V
            {
                float4 pos : SV_POSITION;
                float3 wp : TEXCOORD0;
                float3 wn : TEXCOORD1;
                DECLARE_LIGHTMAP_OR_SH(lightmapUV, vertexSH, 2);
                float fog : TEXCOORD3;
            };

            V vert(A a)
            {
                V o;
                o.wp = TransformObjectToWorld(a.pos.xyz);
                o.wn = TransformObjectToWorldNormal(a.normal);
                o.pos = TransformWorldToHClip(o.wp);
                OUTPUT_LIGHTMAP_UV(a.uv1, unity_LightmapST, o.lightmapUV);
                OUTPUT_SH(o.wn, o.vertexSH);
                o.fog = ComputeFogFactor(o.pos.z);
                return o;
            }

            half4 frag(V i) : SV_Target
            {
                float3 wn = normalize(i.wn);
                float3 w = TriWeights(wn);
                float2 uvX = i.wp.zy * _Scale, uvY = i.wp.xz * _Scale, uvZ = i.wp.xy * _Scale;
                float4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uvX) * w.x + SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uvY) * w.y + SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uvZ) * w.z;
                float3 e = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, uvX).rgb * w.x + SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, uvY).rgb * w.y + SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, uvZ).rgb * w.z;

                InputData d = (InputData)0;
                d.positionWS = i.wp;
                d.positionCS = i.pos;
                d.normalWS = TriNormal(i.wp, wn, w);
                d.viewDirectionWS = GetWorldSpaceNormalizeViewDir(i.wp);
                d.shadowCoord = TransformWorldToShadowCoord(i.wp);
                d.fogCoord = i.fog;
                d.bakedGI = SAMPLE_GI(i.lightmapUV, i.vertexSH, d.normalWS);
                d.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.pos);
                d.shadowMask = SAMPLE_SHADOWMASK(i.lightmapUV);

                SurfaceData s = (SurfaceData)0;
                s.albedo = c.rgb * _Color.rgb;
                s.metallic = _Metallic;
                s.smoothness = c.a * _Glossiness;
                s.occlusion = 1;
                s.emission = e * _EmissionColor.rgb;
                s.alpha = 1;
                s.normalTS = float3(0, 0, 1);
                half4 col = UniversalFragmentPBR(d, s);
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
            float4 vert(A a) : SV_POSITION
            {
                float3 wp = TransformObjectToWorld(a.pos.xyz);
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
                return p;
            }
            half4 frag() : SV_Target { return 0; }
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
            float4 vert(A a) : SV_POSITION { return TransformObjectToHClip(a.pos.xyz); }
            half frag() : SV_Target { return 0; }
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
            V vert(A a)
            {
                V o;
                o.wp = TransformObjectToWorld(a.pos.xyz);
                o.wn = TransformObjectToWorldNormal(a.normal);
                o.pos = TransformWorldToHClip(o.wp);
                return o;
            }
            half4 frag(V i) : SV_Target
            {
                float3 wn = normalize(i.wn);
                return half4(NormalizeNormalPerPixel(TriNormal(i.wp, wn, TriWeights(wn))), 0);
            }
            ENDHLSL
        }
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        LOD 300

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0

        sampler2D _MainTex;
        sampler2D _BumpMap;
        sampler2D _EmissionMap;
        fixed4 _Color;
        fixed4 _EmissionColor;
        half _BumpScale;
        half _Scale;
        half _Glossiness;
        half _Metallic;
        half _Sharpness;

        struct Input
        {
            float3 worldPos;
            float3 worldNormal;
            INTERNAL_DATA
        };

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            float3 wn = normalize(WorldNormalVector(IN, float3(0, 0, 1)));
            float3 w = pow(abs(wn), _Sharpness);
            w /= max(w.x + w.y + w.z, 1e-4);

            float2 uvX = IN.worldPos.zy * _Scale;
            float2 uvY = IN.worldPos.xz * _Scale;
            float2 uvZ = IN.worldPos.xy * _Scale;

            fixed4 cX = tex2D(_MainTex, uvX);
            fixed4 cY = tex2D(_MainTex, uvY);
            fixed4 cZ = tex2D(_MainTex, uvZ);
            fixed4 c = cX * w.x + cY * w.y + cZ * w.z;

            half3 nX = UnpackScaleNormal(tex2D(_BumpMap, uvX), _BumpScale);
            half3 nY = UnpackScaleNormal(tex2D(_BumpMap, uvY), _BumpScale);
            half3 nZ = UnpackScaleNormal(tex2D(_BumpMap, uvZ), _BumpScale);
            // Whiteout 混合（以世界法線為基準）。
            float3 tX = float3(nX.xy + wn.zy, abs(nX.z) * wn.x);
            float3 tY = float3(nY.xy + wn.xz, abs(nY.z) * wn.y);
            float3 tZ = float3(nZ.xy + wn.xy, abs(nZ.z) * wn.z);
            float3 worldN = normalize(tX.zyx * w.x + tY.xzy * w.y + tZ.xyz * w.z);

            // 世界法線 → 切線空間（Surface Shader 的 o.Normal 是切線空間）。
            float3 T = WorldNormalVector(IN, float3(1, 0, 0));
            float3 B = WorldNormalVector(IN, float3(0, 1, 0));
            float3 N = WorldNormalVector(IN, float3(0, 0, 1));
            o.Normal = normalize(float3(dot(worldN, T), dot(worldN, B), dot(worldN, N)));

            o.Albedo = c.rgb * _Color.rgb;
            o.Smoothness = c.a * _Glossiness;
            o.Metallic = _Metallic;

            fixed3 eX = tex2D(_EmissionMap, uvX).rgb;
            fixed3 eY = tex2D(_EmissionMap, uvY).rgb;
            fixed3 eZ = tex2D(_EmissionMap, uvZ).rgb;
            o.Emission = (eX * w.x + eY * w.y + eZ * w.z) * _EmissionColor.rgb;
            o.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
