Shader "Commander/AccessControlZone"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (1, 0, 0, 0.35)
        _BorderColor ("Border Color", Color) = (1, 0.05, 0.02, 0.9)

        _FillAlpha ("Fill Alpha", Range(0, 1)) = 0.45
        _BorderAlpha ("Border Alpha", Range(0, 1)) = 1
        _BorderWidth ("Border Width", Range(0.01, 0.3)) = 0.08
        _Softness ("Edge Softness", Range(0.001, 0.2)) = 0.03

        _StripeDensity ("Stripe Density", Range(2, 40)) = 14
        _StripeWidth ("Stripe Width", Range(0.02, 0.5)) = 0.16
        _StripeAlpha ("Stripe Alpha", Range(0, 1)) = 0.45
        _StripeSpeed ("Stripe Speed", Range(-5, 5)) = 0.6

        _PulseSpeed ("Pulse Speed", Range(0, 10)) = 3
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
        ZTest LEqual
        Cull Off
        Lighting Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct AppData
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct VertexOutput
            {
                float4 position : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            fixed4 _BaseColor;
            fixed4 _BorderColor;

            float _FillAlpha;
            float _BorderAlpha;
            float _BorderWidth;
            float _Softness;

            float _StripeDensity;
            float _StripeWidth;
            float _StripeAlpha;
            float _StripeSpeed;

            float _PulseSpeed;
            float _PulseStrength;

            VertexOutput vert(AppData input)
            {
                VertexOutput output;
                output.position = UnityObjectToClipPos(input.vertex);
                output.uv = input.uv;
                return output;
            }

            fixed4 frag(VertexOutput input) : SV_Target
            {
                float2 centeredUv = input.uv * 2.0 - 1.0;
                float distanceFromCenter = length(centeredUv);

                float circleMask = 1.0 - smoothstep(1.0 - _Softness, 1.0, distanceFromCenter);
                clip(circleMask - 0.001);

                float borderStart = 1.0 - _BorderWidth;

                float borderMask =
                    smoothstep(borderStart - _Softness, borderStart, distanceFromCenter) *
                    (1.0 - smoothstep(1.0 - _Softness, 1.0, distanceFromCenter));

                float stripeValue = frac((centeredUv.x + centeredUv.y) * _StripeDensity + _Time.y * _StripeSpeed);

                float stripeMask =
                    smoothstep(0.0, 0.02, stripeValue) *
                    (1.0 - smoothstep(_StripeWidth, _StripeWidth + 0.02, stripeValue));

                float pulse = 1.0 + sin(_Time.y * _PulseSpeed) * _PulseStrength;

                fixed3 color = _BaseColor.rgb;
                color = lerp(color, _BorderColor.rgb, stripeMask * _StripeAlpha);
                color = lerp(color, _BorderColor.rgb, saturate(borderMask * pulse));

                float fillAlpha = _BaseColor.a * _FillAlpha * circleMask;
                float stripeAlpha = stripeMask * _StripeAlpha * 0.12 * circleMask;
                float borderAlpha = borderMask * _BorderColor.a * _BorderAlpha * pulse;

                float finalAlpha = saturate(max(fillAlpha + stripeAlpha, borderAlpha));

                return fixed4(color, finalAlpha);
            }
            ENDCG
        }
    }
}