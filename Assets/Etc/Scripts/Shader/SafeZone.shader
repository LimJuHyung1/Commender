Shader "Commander/SafeZone"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.886, 0.910, 0.808, 0.30)
        _SubColor ("Sub Color", Color) = (0.674, 0.749, 0.643, 0.38)
        _AccentColor ("Accent Color", Color) = (1.000, 0.498, 0.067, 0.90)
        _DarkColor ("Dark Line Color", Color) = (0.149, 0.149, 0.149, 0.35)

        _Alpha ("Alpha", Range(0, 1)) = 0.30
        _BorderWidth ("Border Width", Range(0.001, 0.2)) = 0.055
        _GridStrength ("Grid Strength", Range(0, 1)) = 0.18
        _GridCount ("Grid Count", Range(1, 20)) = 6
        _PulseSpeed ("Pulse Speed", Range(0, 10)) = 1.2
        _PulseStrength ("Pulse Strength", Range(0, 1)) = 0.06
        _EdgeFade ("Edge Fade", Range(0, 0.5)) = 0.06
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
        }

        LOD 100

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Offset -1, -1

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            fixed4 _BaseColor;
            fixed4 _SubColor;
            fixed4 _AccentColor;
            fixed4 _DarkColor;

            float _Alpha;
            float _BorderWidth;
            float _GridStrength;
            float _GridCount;
            float _PulseSpeed;
            float _PulseStrength;
            float _EdgeFade;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float GetMinimumEdgeDistance(float2 uv)
            {
                float xDistance = min(uv.x, 1.0 - uv.x);
                float yDistance = min(uv.y, 1.0 - uv.y);
                return min(xDistance, yDistance);
            }

            float GetBorderMask(float2 uv)
            {
                float edgeDistance = GetMinimumEdgeDistance(uv);
                return 1.0 - smoothstep(_BorderWidth, _BorderWidth + 0.01, edgeDistance);
            }

            float GetGridMask(float2 uv)
            {
                float2 gridUv = abs(frac(uv * _GridCount) - 0.5);

                float verticalMask = 1.0 - smoothstep(0.015, 0.025, gridUv.x);
                float horizontalMask = 1.0 - smoothstep(0.015, 0.025, gridUv.y);

                return max(verticalMask, horizontalMask) * _GridStrength;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 centerUv = i.uv - 0.5;
                float centerDistance = length(centerUv);
                float centerMask = 1.0 - saturate(centerDistance * 1.6);

                float edgeDistance = GetMinimumEdgeDistance(i.uv);
                float edgeFadeMask = smoothstep(0.0, _EdgeFade, edgeDistance);

                float borderMask = GetBorderMask(i.uv);
                float gridMask = GetGridMask(i.uv);

                float pulse = sin(_Time.y * _PulseSpeed) * 0.5 + 0.5;

                fixed4 finalColor = _BaseColor;

                finalColor.rgb = lerp(finalColor.rgb, _SubColor.rgb, centerMask * 0.45);
                finalColor.rgb = lerp(finalColor.rgb, _DarkColor.rgb, gridMask * 0.45);
                finalColor.rgb = lerp(finalColor.rgb, _AccentColor.rgb, borderMask);

                float baseAlpha = _Alpha;
                baseAlpha += centerMask * 0.05;
                baseAlpha += pulse * _PulseStrength;
                baseAlpha *= edgeFadeMask;

                finalColor.a = baseAlpha;
                finalColor.a = max(finalColor.a, gridMask * _DarkColor.a);
                finalColor.a = max(finalColor.a, borderMask * _AccentColor.a);

                return finalColor;
            }
            ENDCG
        }
    }

    FallBack Off
}