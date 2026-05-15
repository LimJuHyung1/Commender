Shader "Commander/GraffitiPaintZoneURP"
{
    Properties
    {
        [Header(Shape)]
        _VisibleRadius ("Visible Radius", Range(0.1, 1.2)) = 0.92
        _EdgeSoftness ("Edge Softness", Range(0.01, 0.5)) = 0.18
        _Aspect ("Aspect", Range(0.2, 5.0)) = 1.0

        [Header(Paint Mask)]
        _Alpha ("Alpha", Range(0.0, 1.0)) = 0.72
        _Fade ("Fade", Range(0.0, 1.0)) = 1.0
        _PaintThreshold ("Paint Threshold", Range(0.0, 1.0)) = 0.42
        _MainScale ("Main Splatter Scale", Range(1.0, 30.0)) = 7.0
        _DetailScale ("Detail Splatter Scale", Range(5.0, 80.0)) = 32.0
        _ColorScale ("Color Mix Scale", Range(1.0, 30.0)) = 10.0

        [Header(Drip)]
        _DripScale ("Drip Scale", Range(1.0, 40.0)) = 14.0
        _DripStretch ("Drip Stretch", Range(0.2, 8.0)) = 3.2
        _DripThreshold ("Drip Threshold", Range(0.0, 1.0)) = 0.58

        [Header(Animation)]
        _PulseSpeed ("Pulse Speed", Range(0.0, 10.0)) = 1.6
        _PulseStrength ("Pulse Strength", Range(0.0, 0.5)) = 0.08

        [Header(Colors)]
        _DarkColor ("Dark Paint", Color) = (0.035, 0.032, 0.038, 1.0)
        _PinkColor ("Hot Pink Paint", Color) = (1.0, 0.05, 0.42, 1.0)
        _MintColor ("Mint Paint", Color) = (0.0, 0.85, 0.72, 1.0)
        _LimeColor ("Lime Paint", Color) = (0.65, 0.95, 0.12, 1.0)
        _IvoryColor ("Ivory Paint", Color) = (0.92, 0.88, 0.78, 1.0)
        _EdgeColor ("Edge Highlight", Color) = (1.0, 0.04, 0.45, 1.0)
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
                float _VisibleRadius;
                float _EdgeSoftness;
                float _Aspect;

                float _Alpha;
                float _Fade;
                float _PaintThreshold;
                float _MainScale;
                float _DetailScale;
                float _ColorScale;

                float _DripScale;
                float _DripStretch;
                float _DripThreshold;

                float _PulseSpeed;
                float _PulseStrength;

                half4 _DarkColor;
                half4 _PinkColor;
                half4 _MintColor;
                half4 _LimeColor;
                half4 _IvoryColor;
                half4 _EdgeColor;
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

                float a = Hash21(i);
                float b = Hash21(i + float2(1.0, 0.0));
                float c = Hash21(i + float2(0.0, 1.0));
                float d = Hash21(i + float2(1.0, 1.0));

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
                    p = p * 2.03 + float2(13.7, 9.2);
                    amplitude *= 0.5;
                }

                return value;
            }

            half4 PickPaintColor(float colorValue)
            {
                half4 paintColor = _DarkColor;

                if (colorValue > 0.82)
                {
                    paintColor = _PinkColor;
                }
                else if (colorValue > 0.66)
                {
                    paintColor = _MintColor;
                }
                else if (colorValue > 0.50)
                {
                    paintColor = _LimeColor;
                }
                else if (colorValue > 0.38)
                {
                    paintColor = _IvoryColor;
                }

                return paintColor;
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
                centeredUv.x *= _Aspect;

                float distanceFromCenter = length(centeredUv);

                float circleMask = 1.0 - smoothstep(
                    _VisibleRadius - _EdgeSoftness,
                    _VisibleRadius,
                    distanceFromCenter
                );

                float timeA = _Time.y * 0.018;
                float timeB = _Time.y * 0.011;

                float mainNoise = Fbm(centeredUv * _MainScale + timeA);
                float detailNoise = Fbm(centeredUv * _DetailScale - timeB);

                float2 dripUv = float2(
                    centeredUv.x * _DripScale,
                    centeredUv.y * (_DripScale / max(0.001, _DripStretch))
                );

                float dripNoise = Fbm(dripUv + float2(0.0, timeB));
                float dripMask = smoothstep(_DripThreshold, 1.0, dripNoise);

                float paintMask = smoothstep(_PaintThreshold, 1.0, mainNoise);
                paintMask = saturate(paintMask + detailNoise * 0.38);
                paintMask = saturate(paintMask + dripMask * 0.42);

                float centerFill = 1.0 - smoothstep(
                    0.0,
                    _VisibleRadius * 0.62,
                    distanceFromCenter
                );

                paintMask = saturate(paintMask + centerFill * 0.25);

                float colorValue = Fbm(centeredUv * _ColorScale + float2(7.3, 19.1));
                half4 paintColor = PickPaintColor(colorValue);

                float edgeMask = smoothstep(
                    _VisibleRadius - _EdgeSoftness * 0.6,
                    _VisibleRadius,
                    distanceFromCenter
                ) * circleMask;

                half3 finalColor = paintColor.rgb;
                finalColor = lerp(finalColor, _EdgeColor.rgb, edgeMask * 0.55);

                float pulse = 1.0 + sin(_Time.y * _PulseSpeed) * _PulseStrength;

                float alpha = circleMask;
                alpha *= paintMask;
                alpha *= saturate(0.65 + detailNoise * 0.65);
                alpha *= _Alpha;
                alpha *= _Fade;
                alpha *= pulse;

                return half4(finalColor, alpha);
            }

            ENDHLSL
        }
    }
}