Shader "Commander/Drone"
{
    Properties
    {
        _MainColor ("Main Color", Color) = (0.2, 0.8, 1.0, 1.0)
        _Alpha ("Area Alpha", Range(0, 1)) = 0.25
        _EdgeSoftness ("Edge Softness", Range(0.001, 0.5)) = 0.08
        _OuterRingWidth ("Outer Ring Width", Range(0.001, 0.25)) = 0.04
        _OuterRingAlpha ("Outer Ring Alpha", Range(0, 1)) = 0.75
        _PulseSpeed ("Pulse Speed", Range(0, 10)) = 2.0
        _PulseStrength ("Pulse Strength", Range(0, 1)) = 0.25
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "RenderType"="Transparent"
            "IgnoreProjector"="True"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            fixed4 _MainColor;
            float _Alpha;
            float _EdgeSoftness;
            float _OuterRingWidth;
            float _OuterRingAlpha;
            float _PulseSpeed;
            float _PulseStrength;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 position : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert(appdata input)
            {
                v2f output;
                output.position = UnityObjectToClipPos(input.vertex);
                output.uv = input.uv;
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                float2 centeredUv = input.uv * 2.0 - 1.0;
                float distanceFromCenter = length(centeredUv);

                clip(1.0 - distanceFromCenter);

                float insideArea = 1.0 - smoothstep(
                    1.0 - _EdgeSoftness,
                    1.0,
                    distanceFromCenter
                );

                float outerRingStart = 1.0 - _OuterRingWidth;
                float outerRing = smoothstep(
                    outerRingStart,
                    outerRingStart + _OuterRingWidth * 0.5,
                    distanceFromCenter
                );

                outerRing *= 1.0 - smoothstep(
                    outerRingStart + _OuterRingWidth * 0.5,
                    1.0,
                    distanceFromCenter
                );

                float pulse = 1.0 + sin(_Time.y * _PulseSpeed) * _PulseStrength;

                float alpha = insideArea * _Alpha;
                alpha += outerRing * _OuterRingAlpha * pulse;
                alpha = saturate(alpha);

                fixed4 color = _MainColor;
                color.a = alpha;

                return color;
            }
            ENDCG
        }
    }
}