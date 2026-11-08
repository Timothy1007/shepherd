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
