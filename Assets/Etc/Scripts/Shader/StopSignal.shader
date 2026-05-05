Shader "Commander/StopSignal"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (1, 0.05, 0.05, 1)

        _FillAlpha ("Fill Alpha", Range(0, 1)) = 0.18
        _RingAlpha ("Outer Ring Alpha", Range(0, 1)) = 0.85
        _PulseAlpha ("Pulse Ring Alpha", Range(0, 1)) = 0.55

        _CircleRadius ("Circle Radius", Range(0.1, 0.5)) = 0.48
        _OuterRingWidth ("Outer Ring Width", Range(0.001, 0.1)) = 0.025
        _PulseRingWidth ("Pulse Ring Width", Range(0.001, 0.15)) = 0.035
        _EdgeSoftness ("Edge Softness", Range(0.001, 0.1)) = 0.015

        _BlinkSpeed ("Blink Speed", Float) = 3
        _BlinkStrength ("Blink Strength", Range(0, 1)) = 0.45

        _PulseSpeed ("Pulse Speed", Float) = 1.4
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
        }

        Pass
        {
            Name "StopSignal"

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off

            Offset -1, -1

            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;

                float _FillAlpha;
                float _RingAlpha;
                float _PulseAlpha;

                float _CircleRadius;
                float _OuterRingWidth;
                float _PulseRingWidth;
                float _EdgeSoftness;

                float _BlinkSpeed;
                float _BlinkStrength;

                float _PulseSpeed;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;

                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 centeredUV = input.uv - 0.5;
                float distanceFromCenter = length(centeredUV);

                float circleMask =
                    1.0 - smoothstep(
                        _CircleRadius - _EdgeSoftness,
                        _CircleRadius,
                        distanceFromCenter
                    );

                float outerRingDistance = abs(distanceFromCenter - _CircleRadius);

                float outerRingMask =
                    1.0 - smoothstep(
                        _OuterRingWidth,
                        _OuterRingWidth + _EdgeSoftness,
                        outerRingDistance
                    );

                outerRingMask *= circleMask;

                float pulsePosition = frac(_Time.y * _PulseSpeed) * _CircleRadius;
                float pulseDistance = abs(distanceFromCenter - pulsePosition);

                float pulseRingMask =
                    1.0 - smoothstep(
                        _PulseRingWidth,
                        _PulseRingWidth + _EdgeSoftness,
                        pulseDistance
                    );

                pulseRingMask *= circleMask;

                float blinkValue = sin(_Time.y * _BlinkSpeed) * 0.5 + 0.5;
                float blinkMultiplier = lerp(1.0 - _BlinkStrength, 1.0, blinkValue);

                float fillAlpha = _FillAlpha * circleMask;
                float ringAlpha = _RingAlpha * outerRingMask;
                float pulseAlpha = _PulseAlpha * pulseRingMask * blinkMultiplier;

                float finalAlpha = saturate(fillAlpha + ringAlpha + pulseAlpha);

                float3 finalColor = _BaseColor.rgb;

                return half4(finalColor, finalAlpha);
            }

            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}