// Case01 v0.4：失序門的「訊號故障」膜（Built-in 管線、無貼圖、程序雜訊）。
// 電視雪花噪點＋掃描線＋間歇橫向撕裂＋緩慢滾動亮帶；去飽和的黑灰／冷白，少量紫色只出現在撕裂處。
// _Glitch：故障強度（0＝已校正：只剩極淡的掃描線；1＝錯接：明顯雪花與撕裂）。
// _Speed：動態速度（降低閃爍時調低；噪點更新率隨之下降，避免高頻閃爍）。
Shader "Shepherd/Case01PortalGlitch"
{
    Properties
    {
        _Tint ("Tint", Color) = (0.78, 0.80, 0.86, 1)
        _Accent ("Accent", Color) = (0.50, 0.42, 0.62, 1)
        _Glitch ("Glitch", Range(0, 1)) = 0.8
        _Speed ("Speed", Range(0, 1)) = 1
        _Alpha ("Alpha", Range(0, 1)) = 0.85
        _Res ("Noise Resolution", Float) = 72
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
            fixed4 _Accent;
            float _Glitch;
            float _Speed;
            float _Alpha;
            float _Res;

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

            fixed4 frag(v2f i) : SV_Target
            {
                float t = _Time.y * _Speed;
                // 噪點每秒最多約 18 次更新（降低閃爍時更少），不做每影格亂閃。
                float frame = floor(t * 18.0);

                // 間歇的橫向撕裂帶。
                float band = floor(i.uv.y * 16.0 + floor(t * 2.0) * 7.0);
                float tear = step(1.0 - 0.22 * _Glitch, hash(float2(band, floor(t * 5.0))));
                float2 uv = i.uv;
                uv.x += tear * (hash(float2(band, frame)) - 0.5) * 0.3 * _Glitch;

                // 雪花噪點。
                float2 cell = floor(uv * float2(_Res, _Res * 1.7));
                float n = hash(cell + frame * 17.0);

                // 掃描線與緩慢滾動的亮帶。
                float scan = 0.78 + 0.22 * sin(i.uv.y * 420.0);
                float rollPos = frac(i.uv.y + t * 0.12);
                float roll = 1.0 + 0.35 * _Glitch * smoothstep(0.08, 0.0, abs(rollPos - 0.5));

                float lum = lerp(0.42, n, 0.25 + 0.75 * _Glitch) * scan * roll;
                fixed3 col = lerp(_Tint.rgb * lum, _Accent.rgb * (0.4 + lum), tear * 0.65);

                // 邊緣略暗，讓門洞形狀清楚。
                float edge = smoothstep(0.0, 0.05, i.uv.x) * smoothstep(0.0, 0.05, 1.0 - i.uv.x)
                           * smoothstep(0.0, 0.03, i.uv.y) * smoothstep(0.0, 0.03, 1.0 - i.uv.y);
                float a = _Alpha * (0.55 + 0.45 * edge);
                return fixed4(col, a);
            }
            ENDCG
        }
    }
    Fallback Off
}
