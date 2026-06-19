Shader "Commander/Actor/SceneStealerRange"
{
    Properties
    {
        [HDR]_BaseColor("Base Color", Color) = (0.91, 0.78, 0.48, 1.0)

        _FillAlpha("Fill Alpha", Range(0, 1)) = 0.14
        _EdgeAlpha("Edge Alpha", Range(0, 2)) = 1.1
        _EdgeWidth("Edge Width", Range(0.001, 0.5)) = 0.055
        _Softness("Softness", Range(0.001, 0.2)) = 0.025

        _PulseSpeed("Pulse Speed", Range(0, 20)) = 5.5
        _PulseStrength("Pulse Strength", Range(0, 2)) = 0.45

        _WaveCount("Wave Count", Range(0, 20)) = 4.0
        _WaveSpeed("Wave Speed", Range(-10, 10)) = 1.6
        _WaveAlpha("Wave Alpha", Range(0, 2)) = 0.38
        _WaveSharpness("Wave Sharpness", Range(1, 16)) = 6.0

        _CenterGlow("Center Glow", Range(0, 1)) = 0.08
        _Cutoff("Alpha Cutoff", Range(0, 0.1)) = 0.001
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

        ZWrite Off
        ZTest LEqual
        Cull Off

        // ¹øÂ½°Å¸®´Â ¹ß±¤ ´À³¦.
        // ³Ê¹« ¹àÀ¸¸é ¾Æ·¡ ÁÙÀ» Blend SrcAlpha OneMinusSrcAlpha ·Î ¹Ù²ãµµ µÊ.
        Blend SrcAlpha One

        // ¹Ù´Ú°ú °ãÄ¥ ¶§ ±ôºýÀÌ´Â Z-FightingÀ» ÁÙÀÌ±â À§ÇÑ º¸Á¤.
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
                float4 position : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            fixed4 _BaseColor;

            float _FillAlpha;
            float _EdgeAlpha;
            float _EdgeWidth;
            float _Softness;

            float _PulseSpeed;
            float _PulseStrength;

            float _WaveCount;
            float _WaveSpeed;
            float _WaveAlpha;
            float _WaveSharpness;

            float _CenterGlow;
            float _Cutoff;

            v2f vert(appdata v)
            {
                v2f o;
                o.position = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // UV 0~1À» Áß½É ±âÁØ -1~1 ¿øÇü ÁÂÇ¥·Î º¯È¯
                float2 centeredUv = i.uv * 2.0 - 1.0;
                float distanceFromCenter = length(centeredUv);

                // ¿ø ¹Ù±ù Á¦°Å
                float outerMask = 1.0 - smoothstep(
                    1.0 - _Softness,
                    1.0,
                    distanceFromCenter
                );

                clip(outerMask - _Cutoff);

                // ³»ºÎ ÀºÀºÇÑ Ã¤¿ò
                float fill = _FillAlpha * outerMask * saturate(1.0 - distanceFromCenter * 0.45);

                // ¿Ü°û ¸µ
                float edgeStart = 1.0 - _EdgeWidth;

                float edgeRing =
                    smoothstep(
                        edgeStart - _Softness,
                        edgeStart,
                        distanceFromCenter
                    )
                    *
                    (1.0 - smoothstep(
                        1.0 - _Softness,
                        1.0,
                        distanceFromCenter
                    ));

                // Áß½É¿¡¼­ ¹Ù±ùÀ¸·Î Èå¸£´Â ÆÄµ¿
                float waveRaw = sin(
                    (distanceFromCenter * _WaveCount - _Time.y * _WaveSpeed)
                    * 6.2831853
                );

                float wave = pow(
                    saturate(waveRaw * 0.5 + 0.5),
                    _WaveSharpness
                );

                wave *= _WaveAlpha;
                wave *= outerMask;
                wave *= 1.0 - edgeRing;

                // Áß¾Ó ¾àÇÑ ºû
                float centerGlow =
                    (1.0 - smoothstep(0.0, 0.45, distanceFromCenter))
                    * _CenterGlow;

                // ÀüÃ¼ ±ôºýÀÓ
                float pulse =
                    1.0 + sin(_Time.y * _PulseSpeed) * _PulseStrength;

                pulse = max(0.05, pulse);

                float alpha =
                    fill +
                    edgeRing * _EdgeAlpha +
                    wave +
                    centerGlow;

                alpha = saturate(alpha * pulse);

                // ¿Ü°û°ú ÆÄµ¿ÀÌ Á¶±Ý ´õ ¹à°Ô º¸ÀÌµµ·Ï º¸Á¤
                float brightness =
                    1.0 +
                    edgeRing * 0.75 +
                    wave * 0.45;

                fixed3 finalColor = _BaseColor.rgb * brightness;

                return fixed4(finalColor, alpha * _BaseColor.a);
            }
            ENDCG
        }
    }

    FallBack Off
}