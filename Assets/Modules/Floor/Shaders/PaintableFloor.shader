Shader "Flood/PaintableFloor"
{
    Properties
    {
        [Header(Data)]
        _PaintMask ("Paint Mask (R=coverage, G=age)", 2D) = "black" {}

        [Header(Color)]
        _PaintColor ("Paint Color", Color) = (0.85, 0.25, 0.35, 1)
        _BackgroundColor ("Background Color", Color) = (0.12, 0.12, 0.14, 1)

        [Header(Soft Edge)]
        _EdgeSoftness ("Edge Softness", Range(0.001, 1)) = 0.15

        [Header(Rim Outline)]
        _RimColor ("Rim Color", Color) = (1, 1, 1, 1)
        _RimThickness ("Rim Thickness", Range(0.001, 0.5)) = 0.08
        _RimIntensity ("Rim Intensity (0 = off)", Range(0, 5)) = 1.5

        [Header(Noise)]
        _NoiseScale ("Noise Scale", Float) = 24
        _NoiseStrength ("Noise Strength (0 = off)", Range(0, 1)) = 0.25
        _NoiseTint ("Noise Tint", Color) = (0.7, 0.7, 0.7, 1)

        [Header(Pulsation)]
        _PulseSpeed ("Pulse Speed", Float) = 1.5
        _PulseAmount ("Pulse Amount (0 = off)", Range(0, 0.5)) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
        }

        Pass
        {
            Name "Unlit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "PaintableFloorFunctions.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _PaintMask_ST;
                float4 _PaintColor;
                float4 _BackgroundColor;
                float  _EdgeSoftness;
                float4 _RimColor;
                float  _RimThickness;
                float  _RimIntensity;
                float  _NoiseScale;
                float  _NoiseStrength;
                float4 _NoiseTint;
                float  _PulseSpeed;
                float  _PulseAmount;
            CBUFFER_END

            TEXTURE2D(_PaintMask);
            SAMPLER(sampler_PaintMask);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = TRANSFORM_TEX(IN.uv, _PaintMask);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 raw = SAMPLE_TEXTURE2D(_PaintMask, sampler_PaintMask, IN.uv);

                float3 baseColor;
                float3 emission;
                PaintableFloorComposite_float(
                    raw.r,
                    IN.uv,
                    _PaintColor.rgb,
                    _BackgroundColor.rgb,
                    _EdgeSoftness,
                    _RimColor.rgb,
                    _RimThickness,
                    _RimIntensity,
                    _NoiseScale,
                    _NoiseStrength,
                    _NoiseTint.rgb,
                    _Time.y,
                    _PulseSpeed,
                    _PulseAmount,
                    baseColor,
                    emission);

                return half4(baseColor + emission, 1);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
