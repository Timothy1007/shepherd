// 風格測試用的全畫面「油彩」濾鏡（Kuwahara）：把畫面整理成一塊一塊的筆觸色塊，邊緣保持清楚；
// 再加一點很淡的描邊與紙張起伏。用在 UI 的 RawImage（播放時）與截圖的 Blit。
Shader "Hidden/Shepherd/LanternPaint"
{
    Properties
    {
        _MainTex ("Source", 2D) = "white" {}
        _Radius ("Brush Radius (px)", Range(1, 6)) = 3
        _Ink ("Edge Ink", Range(0, 1)) = 0.35
        _Grain ("Canvas Grain", Range(0, 0.1)) = 0.03
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Opaque" "IgnoreProjector" = "True" }
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float _Radius, _Ink, _Grain;
            struct A { float4 v : POSITION; float2 uv : TEXCOORD0; float4 c : COLOR; };
            struct V { float4 p : SV_POSITION; float2 uv : TEXCOORD0; };
            V vert(A a) { V o; o.p = UnityObjectToClipPos(a.v); o.uv = a.uv; return o; }

            float Lum(float3 c) { return dot(c, float3(0.3, 0.59, 0.11)); }

            float Hash(float2 p) { return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453); }

            fixed4 frag(V i) : SV_Target
            {
                float2 px = _MainTex_TexelSize.xy;
                int r = (int)_Radius;
                float3 m[4] = { float3(0,0,0), float3(0,0,0), float3(0,0,0), float3(0,0,0) };
                float3 s[4] = { float3(0,0,0), float3(0,0,0), float3(0,0,0), float3(0,0,0) };
                float n = (float)((r + 1) * (r + 1));
                // 四個象限各算平均與變異，取最平滑的那一塊：色塊化但保留邊緣。
                [loop] for (int y = 0; y <= r; y++)
                {
                    [loop] for (int x = 0; x <= r; x++)
                    {
                        float3 c0 = tex2D(_MainTex, i.uv + float2(-x, -y) * px).rgb;
                        float3 c1 = tex2D(_MainTex, i.uv + float2( x, -y) * px).rgb;
                        float3 c2 = tex2D(_MainTex, i.uv + float2(-x,  y) * px).rgb;
                        float3 c3 = tex2D(_MainTex, i.uv + float2( x,  y) * px).rgb;
                        m[0] += c0; s[0] += c0 * c0;
                        m[1] += c1; s[1] += c1 * c1;
                        m[2] += c2; s[2] += c2 * c2;
                        m[3] += c3; s[3] += c3 * c3;
                    }
                }
                float best = 1e9;
                float3 col = 0;
                [unroll] for (int k = 0; k < 4; k++)
                {
                    float3 mean = m[k] / n;
                    float3 var = abs(s[k] / n - mean * mean);
                    float v = var.r + var.g + var.b;
                    if (v < best) { best = v; col = mean; }
                }
                // 很淡的描邊：亮度的梯度。
                float l  = Lum(tex2D(_MainTex, i.uv).rgb);
                float lx = Lum(tex2D(_MainTex, i.uv + float2(px.x * 1.5, 0)).rgb) - Lum(tex2D(_MainTex, i.uv - float2(px.x * 1.5, 0)).rgb);
                float ly = Lum(tex2D(_MainTex, i.uv + float2(0, px.y * 1.5)).rgb) - Lum(tex2D(_MainTex, i.uv - float2(0, px.y * 1.5)).rgb);
                float edge = saturate(sqrt(lx * lx + ly * ly) * 3.0 - 0.08);
                col *= 1.0 - edge * _Ink;
                // 紙張／畫布的起伏。
                float g = Hash(floor(i.uv / px / 2.0)) - 0.5;
                col *= 1.0 + g * _Grain;
                return fixed4(col, 1);
            }
            ENDCG
        }
    }
}
