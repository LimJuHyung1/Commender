Shader "Commander/HologramGlitchURP"
{
    Properties
    {
        [MainColor] _BaseColor("Base Color", Color) = (0.15, 0.85, 1.0, 0.55)
        _EdgeColor("Edge Color", Color) = (0.65, 1.0, 1.0, 1.0)

        _Alpha("Alpha", Range(0, 1)) = 0.6
        _FresnelPower("Fresnel Power", Range(0.1, 8.0)) = 3.0

        _NoiseScale("Noise Scale", Float) = 8.0
        _ScanlineDensity("Scanline Density", Float) = 80.0
        _ScanlineSpeed("Scanline Speed", Float) = 4.0

        _DistortionStrength("Distortion Strength", Range(0, 0.05)) = 0.01
        _BlurPixels("Blur Pixels", Range(0, 8)) = 2.0

        _GlitchIntensity("Glitch Intensity", Range(0, 1)) = 0.25
        _GlitchSpeed("Glitch Speed", Float) = 8.0
        _LineJitter("Line Jitter", Range(0, 0.05)) = 0.01
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Back

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _EdgeColor;

                float _Alpha;
                float _FresnelPower;

                float _NoiseScale;
                float _ScanlineDensity;
                float _ScanlineSpeed;

                float _DistortionStrength;
                float _BlurPixels;

                float _GlitchIntensity;
                float _GlitchSpeed;
                float _LineJitter;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 screenPos  : TEXCOORD0;
                float2 uv         : TEXCOORD1;
                float3 normalWS   : TEXCOORD2;
                float3 viewDirWS  : TEXCOORD3;
                float3 positionWS : TEXCOORD4;
            };

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);

                float a = Hash21(i);
                float b = Hash21(i + float2(1.0, 0.0));
                float c = Hash21(i + float2(0.0, 1.0));
                float d = Hash21(i + float2(1.0, 1.0));

                float2 u = f * f * (3.0 - 2.0 * f);

                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            float3 SampleBlurredScene(float2 uv, float2 blurOffset)
            {
                float3 col = 0.0;

                col += SampleSceneColor(saturate(uv)) * 0.40;
                col += SampleSceneColor(saturate(uv + float2( blurOffset.x, 0.0))) * 0.15;
                col += SampleSceneColor(saturate(uv + float2(-blurOffset.x, 0.0))) * 0.15;
                col += SampleSceneColor(saturate(uv + float2(0.0,  blurOffset.y))) * 0.15;
                col += SampleSceneColor(saturate(uv + float2(0.0, -blurOffset.y))) * 0.15;

                return col;
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);

                float lineIndex = floor(positionWS.y * 18.0 + _Time.y * _GlitchSpeed);
                float glitchLine = step(1.0 - _GlitchIntensity, Hash21(float2(lineIndex, 17.0)));
                float jitter = (Hash21(float2(lineIndex, 41.0)) * 2.0 - 1.0) * _LineJitter * glitchLine;

                positionWS.x += jitter;

                OUT.positionWS = positionWS;
                OUT.positionCS = TransformWorldToHClip(positionWS);
                OUT.screenPos = ComputeScreenPos(OUT.positionCS);
                OUT.uv = IN.uv;
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.viewDirWS = SafeNormalize(GetCameraPositionWS() - positionWS);

                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 screenUV = IN.screenPos.xy / IN.screenPos.w;
                float2 texelSize = 1.0 / _ScreenParams.xy;

                float noiseA = ValueNoise(IN.uv * _NoiseScale + float2(_Time.y * 0.7,  _Time.y * 0.2));
                float noiseB = ValueNoise(IN.uv * (_NoiseScale * 1.8) + float2(-_Time.y * 0.4, _Time.y * 0.9));
                float noise = saturate((noiseA + noiseB) * 0.5);

                float scan = sin((IN.positionWS.y + _Time.y * _ScanlineSpeed) * _ScanlineDensity) * 0.5 + 0.5;

                float lineIndex = floor(IN.positionWS.y * 18.0 + _Time.y * _GlitchSpeed);
                float glitchLine = step(1.0 - _GlitchIntensity, Hash21(float2(lineIndex, 11.0)));
                float glitchShift = ((Hash21(float2(lineIndex, 71.0)) * 2.0) - 1.0) * _LineJitter * glitchLine;

                float2 distortion = float2(
                    (noise * 2.0 - 1.0) * _DistortionStrength + glitchShift,
                    (noiseB * 2.0 - 1.0) * _DistortionStrength * 0.25
                );

                float2 blurOffset = texelSize * _BlurPixels;
                float2 refractUV = screenUV + distortion;

                float3 sceneBlur = SampleBlurredScene(refractUV, blurOffset);

                float3 chroma;
                chroma.r = SampleSceneColor(saturate(refractUV + blurOffset * 0.5 + float2(glitchShift * 0.5, 0.0))).r;
                chroma.g = SampleSceneColor(saturate(refractUV)).g;
                chroma.b = SampleSceneColor(saturate(refractUV - blurOffset * 0.5 - float2(glitchShift * 0.5, 0.0))).b;

                float fresnel = pow(1.0 - saturate(dot(normalize(IN.normalWS), normalize(IN.viewDirWS))), _FresnelPower);

                float flicker = 0.92 + 0.08 * sin(_Time.y * 20.0 + noise * 6.28318);
                float bodyNoise = lerp(0.75, 1.25, noise);

                float3 hologramColor = _BaseColor.rgb * bodyNoise;
                hologramColor += _EdgeColor.rgb * fresnel * 1.25;
                hologramColor *= lerp(0.75, 1.15, scan);
                hologramColor += glitchLine * _EdgeColor.rgb * 0.35;

                float alphaNoise = smoothstep(0.15, 0.95, noise);
                float alpha = _Alpha * alphaNoise * flicker;
                alpha *= lerp(0.55, 1.0, saturate(fresnel + scan * 0.35));
                alpha = saturate(alpha);

                float3 finalColor = sceneBlur * 0.45 + chroma * 0.25 + hologramColor;

                return half4(finalColor, alpha);
            }
            ENDHLSL
        }
    }
}