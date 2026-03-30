Shader "Custom/URP/AgentOutline"
{
    Properties
    {
        _OutlineColor("Outline Color", Color) = (1, 0.65, 0.0, 1)
        _OutlineWidth("Outline Width", Float) = 0.03
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry+10"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Cull Front
            ZWrite Off
            ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
            half4 _OutlineColor;
            float _OutlineWidth;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
            };

            Varyings Vert(Attributes IN)
            {
                Varyings OUT;

                float3 normalOS = normalize(IN.normalOS);
                float3 expandedPositionOS = IN.positionOS.xyz + normalOS * _OutlineWidth;

                OUT.positionHCS = TransformObjectToHClip(expandedPositionOS);
                return OUT;
            }

            half4 Frag(Varyings IN) : SV_Target
            {
                return _OutlineColor;
            }
            ENDHLSL
        }
    }
}