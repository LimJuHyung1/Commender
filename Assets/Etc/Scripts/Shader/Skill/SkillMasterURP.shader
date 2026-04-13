Shader "Custom/SkillMasterURP"
{
    Properties
    {
        [MainTexture] _MainTex("Main Tex", 2D) = "white" {}
        [HDR] _BaseColor("Base Color", Color) = (0.35, 0.85, 1.0, 1.0)
        [HDR] _RimColor("Rim Color", Color) = (0.5, 1.0, 1.0, 1.0)

        _Opacity("Opacity", Range(0, 1)) = 0.6

        _RimIntensity("Rim Intensity", Range(0, 5)) = 1.5
        _RimPower("Rim Power", Range(0.1, 8)) = 2.5

        _NoiseStrength("Noise Strength", Range(0, 1)) = 0.35
        _NoiseSpeed("Noise Speed", Range(0, 10)) = 1.5
        _NoiseScale("Noise Scale", Range(0.1, 30)) = 8.0

        _DistortionStrength("Distortion Strength", Range(0, 1)) = 0.08

        _ScanLineStrength("ScanLine Strength", Range(0, 1)) = 0.4
        _ScanLineDensity("ScanLine Density", Range(1, 200)) = 70.0

        _PulseSpeed("Pulse Speed", Range(0, 10)) = 1.2

        _EdgeBreakup("Edge Breakup", Range(0, 1)) = 0.15
        _EdgeNoiseScale("Edge Noise Scale", Range(0.1, 30)) = 12.0

        _Softness("Softness", Range(0.001, 1)) = 0.2
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Transparent"
            "Queue"="Transparent"
            "RenderPipeline"="UniversalPipeline"
        }

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode"="UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                float3 viewDirWS   : TEXCOORD2;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _BaseColor;
                half4 _RimColor;

                half _Opacity;

                half _RimIntensity;
                half _RimPower;

                half _NoiseStrength;
                half _NoiseSpeed;
                half _NoiseScale;

                half _DistortionStrength;

                half _ScanLineStrength;
                half _ScanLineDensity;

                half _PulseSpeed;

                half _EdgeBreakup;
                half _EdgeNoiseScale;

                half _Softness;
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
                float b = Hash21(i + float2(1, 0));
                float c = Hash21(i + float2(0, 1));
                float d = Hash21(i + float2(1, 1));

                float2 u = f * f * (3.0 - 2.0 * f);

                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                VertexPositionInputs posInputs = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(IN.normalOS);

                OUT.positionHCS = posInputs.positionCS;
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.normalWS = normalInputs.normalWS;
                OUT.viewDirWS = GetWorldSpaceViewDir(posInputs.positionWS);

                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float time = _Time.y;

                float3 normalWS = normalize(IN.normalWS);
                float3 viewDirWS = normalize(IN.viewDirWS);

                float2 flow = float2(0.13, 0.09) * (_NoiseSpeed * time);

                float baseNoise = Noise21(IN.uv * _NoiseScale + flow);
                float2 distortedUV = IN.uv + (baseNoise - 0.5) * _DistortionStrength;

                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, distortedUV);

                float noiseA = Noise21(distortedUV * _NoiseScale + flow);
                float noiseB = Noise21(distortedUV * (_NoiseScale * 0.6) - flow * 0.7);
                float combinedNoise = saturate(lerp(noiseA, noiseB, 0.5));

                float scan = sin((distortedUV.y + time * (_NoiseSpeed * 0.25)) * _ScanLineDensity * 6.2831853);
                scan = scan * 0.5 + 0.5;

                float pulse = sin(time * _PulseSpeed * 6.2831853);
                pulse = pulse * 0.5 + 0.5;

                float fresnel = 1.0 - saturate(dot(normalWS, viewDirWS));
                fresnel = pow(fresnel, _RimPower) * _RimIntensity;

                float edgeNoise = Noise21(distortedUV * _EdgeNoiseScale - flow * 0.5);
                edgeNoise = saturate(edgeNoise + (1.0 - _EdgeBreakup));
                float edgeMask = smoothstep(0.5 - _Softness * 0.5, 0.5 + _Softness * 0.5, edgeNoise);

                float alphaNoise = lerp(1.0, combinedNoise, _NoiseStrength * 0.35);
                float alphaPulse = lerp(0.9, 1.15, pulse * 0.4);

                half3 color = tex.rgb * _BaseColor.rgb;
                color += _BaseColor.rgb * (combinedNoise * _NoiseStrength * 0.25);
                color += _BaseColor.rgb * (scan * _ScanLineStrength * 0.35);
                color += _RimColor.rgb * fresnel;

                half alpha = tex.a * _BaseColor.a;
                alpha *= _Opacity;
                alpha *= edgeMask;
                alpha *= alphaNoise;
                alpha *= alphaPulse;

                return half4(color, saturate(alpha));
            }
            ENDHLSL
        }
    }
}