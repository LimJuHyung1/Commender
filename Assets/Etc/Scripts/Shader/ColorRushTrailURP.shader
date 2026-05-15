Shader "Commander/ColorRushTrailURP"
{
    Properties
    {
        [Header(Shape)]
        _Alpha ("Alpha", Range(0.0, 1.0)) = 0.85
        _Fade ("Fade", Range(0.0, 1.0)) = 1.0
        _VisibleRadius ("Visible Radius", Range(0.1, 1.3)) = 0.95
        _EdgeSoftness ("Edge Softness", Range(0.01, 0.5)) = 0.16

        [Header(Splash)]
        _RevealTime ("Reveal Time", Float) = 0.0
        _SpreadDuration ("Spread Duration", Range(0.05, 2.0)) = 0.45
        _SplashPower ("Splash Power", Range(0.1, 3.0)) = 1.25
        _SplatterScale ("Splatter Scale", Range(1.0, 50.0)) = 16.0
        _DetailScale ("Detail Scale", Range(1.0, 100.0)) = 44.0
        _PaintThreshold ("Paint Threshold", Range(0.0, 1.0)) = 0.42

        [Header(Drip)]
        _DripScale ("Drip Scale", Range(1.0, 50.0)) = 18.0
        _DripStretch ("Drip Stretch", Range(0.2, 8.0)) = 3.5
        _DripAmount ("Drip Amount", Range(0.0, 1.0)) = 0.45

        [Header(Color)]
        _PaintColor ("Paint Color", Color) = (1.0, 0.05, 0.42, 1.0)
        _EdgeColor ("Edge Color", Color) = (1.0, 0.85, 0.95, 1.0)
        _Seed ("Seed", Float) = 0.0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "ForwardUnlit"
            Tags
            {
                "LightMode" = "UniversalForward"
            }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off
            Offset -1, -1

            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

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

            CBUFFER_START(UnityPerMaterial)
                float _Alpha;
                float _Fade;
                float _VisibleRadius;
                float _EdgeSoftness;

                float _RevealTime;
                float _SpreadDuration;
                float _SplashPower;
                float _SplatterScale;
                float _DetailScale;
                float _PaintThreshold;

                float _DripScale;
                float _DripStretch;
                float _DripAmount;

                half4 _PaintColor;
                half4 _EdgeColor;
                float _Seed;
            CBUFFER_END

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float Noise21(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);

                float a = Hash21(i + _Seed);
                float b = Hash21(i + float2(1.0, 0.0) + _Seed);
                float c = Hash21(i + float2(0.0, 1.0) + _Seed);
                float d = Hash21(i + float2(1.0, 1.0) + _Seed);

                float2 u = f * f * (3.0 - 2.0 * f);

                return lerp(
                    lerp(a, b, u.x),
                    lerp(c, d, u.x),
                    u.y
                );
            }

            float Fbm(float2 p)
            {
                float value = 0.0;
                float amplitude = 0.5;

                for (int i = 0; i < 4; i++)
                {
                    value += Noise21(p) * amplitude;
                    p = p * 2.04 + float2(17.13, 8.71);
                    amplitude *= 0.5;
                }

                return value;
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 centeredUv = input.uv * 2.0 - 1.0;

                float distanceFromCenter = length(centeredUv);

                float revealElapsed = max(0.0, _Time.y - _RevealTime);
                float spread01 = saturate(revealElapsed / max(0.001, _SpreadDuration));
                spread01 = 1.0 - pow(1.0 - spread01, 3.0);

                float animatedRadius = lerp(0.08, _VisibleRadius, spread01);

                float circleMask = 1.0 - smoothstep(
                    animatedRadius - _EdgeSoftness,
                    animatedRadius,
                    distanceFromCenter
                );

                float2 splashUv = centeredUv * _SplatterScale;
                splashUv += float2(_Seed * 7.31, _Seed * 3.17);

                float mainNoise = Fbm(splashUv);
                float detailNoise = Fbm(centeredUv * _DetailScale + _Seed * 11.4);

                float paintMask = smoothstep(
                    _PaintThreshold,
                    1.0,
                    mainNoise + detailNoise * 0.35
                );

                float centerSplash = 1.0 - smoothstep(
                    0.0,
                    animatedRadius * 0.48,
                    distanceFromCenter
                );

                float2 dripUv = float2(
                    centeredUv.x * _DripScale,
                    centeredUv.y * (_DripScale / max(0.001, _DripStretch))
                );

                dripUv += float2(_Seed * 4.2, _Seed * 9.8);

                float dripNoise = Fbm(dripUv);
                float dripMask = smoothstep(0.56, 1.0, dripNoise) * _DripAmount;

                paintMask = saturate(
                    paintMask * _SplashPower +
                    centerSplash * 0.55 +
                    dripMask
                );

                float edgeMask = smoothstep(
                    animatedRadius - _EdgeSoftness * 0.75,
                    animatedRadius,
                    distanceFromCenter
                ) * circleMask;

                float wetPulse = 1.0 + sin(revealElapsed * 18.0) * 0.04 * (1.0 - spread01);

                half3 finalColor = _PaintColor.rgb * wetPulse;
                finalColor = lerp(finalColor, _EdgeColor.rgb, edgeMask * 0.45);

                float alpha = circleMask;
                alpha *= paintMask;
                alpha *= saturate(0.65 + detailNoise * 0.7);
                alpha *= _Alpha;
                alpha *= _Fade;

                return half4(finalColor, alpha);
            }

            ENDHLSL
        }
    }
}