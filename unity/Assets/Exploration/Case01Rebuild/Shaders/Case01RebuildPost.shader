// Case01 全屋重製 QA 專用的畫面後製（只掛在這個 QA 的第一人稱鏡頭上，不影響其他場景）：
// 曝光 → 近似 ACES 的電影式色調映射（保留亮部層次、暗部不死黑）→ 暖亮部／冷暗部的分離色調 → 輕微飽和與對比 → 暗角 → 極輕微顆粒。
Shader "Hidden/Case01RebuildPost"
{
    Properties
    {
        _MainTex ("Source", 2D) = "white" {}
        _Exposure ("Exposure", Float) = 1.0
        _Saturation ("Saturation", Float) = 1.05
        _Contrast ("Contrast", Float) = 1.06
        _Vignette ("Vignette", Float) = 0.32
        _Grain ("Grain", Float) = 0.025
        _Warm ("Warm highlights", Color) = (1.04, 1.0, 0.94, 1)
        _Cool ("Cool shadows", Color) = (0.94, 0.98, 1.06, 1)
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float _Exposure, _Saturation, _Contrast, _Vignette, _Grain;
            float4 _Warm, _Cool;

            float3 Aces(float3 x)
            {
                const float a = 2.51, b = 0.03, c = 2.43, d = 0.59, e = 0.14;
                return saturate((x * (a * x + b)) / (x * (c * x + d) + e));
            }

            float Hash(float2 p) { return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453); }

            fixed4 frag(v2f_img i) : SV_Target
            {
                float3 c = tex2D(_MainTex, i.uv).rgb;
                // 專案是 Gamma 色彩空間：先轉線性再做曝光與色調映射，最後轉回。
                float3 lin = pow(max(c, 0.0), 2.2) * _Exposure * 1.6;
                float3 t = Aces(lin);
                float3 g = pow(t, 1.0 / 2.2);
                float l = dot(g, float3(0.299, 0.587, 0.114));
                g = lerp(g * _Cool.rgb, g * _Warm.rgb, smoothstep(0.15, 0.75, l));
                g = lerp(float3(l, l, l), g, _Saturation);
                g = (g - 0.5) * _Contrast + 0.5;
                float2 d = i.uv - 0.5;
                float v = 1.0 - _Vignette * smoothstep(0.25, 0.85, dot(d, d) * 2.2);
                g *= v;
                g += (Hash(i.uv * _ScreenParams.xy + _Time.y) - 0.5) * _Grain;
                return fixed4(saturate(g), 1);
            }
            ENDCG
        }
    }
}
