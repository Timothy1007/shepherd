// Case01 v0.5：失序門的「彩色訊號破損」膜（Built-in 管線、無貼圖、程序雜訊）。
// 三層：①RGB 色頻錯位的彩條＋雪花（紅綠藍紫青黃）；②膜的邊界依雜訊流動、局部缺口（只是 alpha，不影響傳送平面）；
// ③少量漂移的殘影掃描線與不規則亮度起伏（幅度受限，不做全畫面白閃）。
// _Glitch：故障強度（0＝已校正：穩定、去飽和、幾乎透明）。_Speed：動態速度。_Sat：色彩飽和度（降低閃爍時調低）。
Shader "Shepherd/Case01PortalGlitchV05"
{
    Properties
    {
        _Tint ("Tint", Color) = (0.78, 0.80, 0.86, 1)
        _Glitch ("Glitch", Range(0, 1)) = 0.8
        _Speed ("Speed", Range(0, 1)) = 1
        _Alpha ("Alpha", Range(0, 1)) = 0.9
        _Sat ("Saturation", Range(0, 1)) = 1
        _Res ("Noise Resolution", Float) = 64
        _Clock ("Clock (driven by script)", Float) = 0
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Tint;
            float _Glitch;
            float _Speed;
            float _Alpha;
            float _Sat;
            float _Res;
            float _Clock;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert(appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord.xy;
                return o;
            }

            float hash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float vnoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = hash(i), b = hash(i + float2(1, 0)), c = hash(i + float2(0, 1)), d = hash(i + float2(1, 1));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            // 電視彩條（7 色）。
            float3 bars(float x)
            {
                float k = floor(frac(x) * 7.0);
                if (k < 1.0) return float3(0.95, 0.95, 0.95);
                if (k < 2.0) return float3(0.95, 0.85, 0.15);
                if (k < 3.0) return float3(0.15, 0.85, 0.9);
                if (k < 4.0) return float3(0.2, 0.85, 0.25);
                if (k < 5.0) return float3(0.85, 0.2, 0.85);
                if (k < 6.0) return float3(0.9, 0.18, 0.15);
                return float3(0.2, 0.25, 0.95);
            }

            // 單一色頻取樣的畫面：雪花＋間歇彩條帶＋滾動亮帶。
            float3 picture(float2 uv, float t, float frame)
            {
                float2 cell = floor(uv * float2(_Res, _Res * 1.7));
                float n = hash(cell + frame * 17.0);
                float3 snow = lerp(float3(0.35, 0.36, 0.4), float3(n, n, n), 0.3 + 0.7 * _Glitch);
                // 彩條只出現在某些水平帶，帶的位置隨時間跳動（不規則）。
                float band = floor(uv.y * 7.0 + floor(t * 1.3) * 3.0);
                float barOn = step(1.0 - 0.45 * _Glitch, hash(float2(band, floor(t * 2.7))));
                float3 colorBars = bars(uv.x + hash(float2(band, 3.0)) * 0.5) * (0.65 + 0.35 * n);
                float3 c = lerp(snow, colorBars, barOn);
                // 細的彩色雜訊線（紅／綠／藍）。
                float streak = step(0.985 - 0.02 * _Glitch, hash(float2(floor(uv.y * 140.0), frame)));
                c = lerp(c, bars(hash(float2(floor(uv.y * 140.0), frame + 1.0))), streak * _Glitch);
                return c;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // 時間由腳本提供（遊戲時間；暫停、截圖與降低閃爍都一致）。
                float t = _Clock;
                float frame = floor(t * 14.0);
                float2 uv = i.uv;

                // ③ 不規則撕裂：橫向位移的帶。
                float tb = floor(uv.y * 22.0 + floor(t * 3.0) * 5.0);
                float tear = step(1.0 - 0.3 * _Glitch, hash(float2(tb, floor(t * 6.0))));
                float shift = tear * (hash(float2(tb, frame)) - 0.5) * 0.35 * _Glitch;

                // ① RGB 色頻錯位（幅度也會跳動）。
                float ca = (0.006 + 0.035 * _Glitch) * (0.4 + hash(float2(floor(t * 4.0), 3.0)));
                float r = picture(uv + float2(shift + ca, 0.0), t, frame).r;
                float g = picture(uv + float2(shift, 0.004 * _Glitch), t, frame).g;
                float b = picture(uv + float2(shift - ca, 0.0), t, frame).b;
                float3 col = float3(r, g, b);

                // 殘影掃描線：一條緩慢漂移的亮帶＋一條偏色的殘像。
                float roll = frac(uv.y + t * 0.11);
                col += (0.18 * _Glitch) * smoothstep(0.05, 0.0, abs(roll - 0.5)) * float3(0.6, 0.9, 1.0);
                float ghost = frac(uv.y - t * 0.07 + 0.3);
                col += (0.12 * _Glitch) * smoothstep(0.03, 0.0, abs(ghost - 0.5)) * float3(1.0, 0.35, 0.9);
                col *= 0.86 + 0.14 * sin(i.uv.y * 420.0);

                // 飽和度（降低閃爍／校正後較灰）與整體不規則起伏（幅度受限，不會變成全白）。
                float lum = dot(col, float3(0.299, 0.587, 0.114));
                col = lerp(float3(lum, lum, lum), col, _Sat * (0.25 + 0.75 * _Glitch));
                col = lerp(_Tint.rgb * (0.35 + 0.4 * lum), col, 0.35 + 0.65 * _Glitch);
                col *= 0.88 + 0.18 * _Glitch * (hash(float2(floor(t * 9.0), 11.0)) - 0.5);
                col = min(col, 0.92);

                // ② 流動、局部缺口的邊界（只是 alpha；傳送平面固定）。
                float wob = _Glitch * 0.09;
                float l = 0.02 + wob * vnoise(float2(uv.y * 7.0, t * 1.7));
                float rr = 0.02 + wob * vnoise(float2(uv.y * 7.0 + 19.0, t * 1.9));
                float tp = 0.015 + wob * 0.8 * vnoise(float2(uv.x * 6.0 + 7.0, t * 1.5));
                float edge = smoothstep(l, l + 0.03, uv.x) * smoothstep(rr, rr + 0.03, 1.0 - uv.x)
                           * smoothstep(tp, tp + 0.03, 1.0 - uv.y) * smoothstep(0.0, 0.02, uv.y);
                // 少量破洞（隨時間游移）。
                float hole = step(0.93, vnoise(uv * float2(5.0, 9.0) + float2(t * 0.6, -t * 0.4))) * _Glitch;
                float a = _Alpha * edge * (1.0 - 0.75 * hole);
                return fixed4(col, a);
            }
            ENDCG
        }
    }
    Fallback Off
}
