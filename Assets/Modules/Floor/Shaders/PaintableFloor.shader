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

        [Header(Line)]
        _Line_Mask ("Line Mask (R=coverage)", 2D) = "black" {}
        _Line_Color ("Line Color", Color) = (0.95, 0.85, 0.2, 1)

        [Header(Height)]
        _MaxHeight ("Max Height (OS units, 0 = off)", Float) = 0.5
        _HeightFalloffRadius ("Height Falloff Radius (UV)", Range(0, 0.05)) = 0.005
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
                float4 _Line_Mask_ST;
                float4 _Line_Color;
                float  _MaxHeight;
                float  _HeightFalloffRadius;
            CBUFFER_END

            TEXTURE2D(_PaintMask);
            SAMPLER(sampler_PaintMask);
            TEXTURE2D(_Line_Mask);
            SAMPLER(sampler_Line_Mask);

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
                OUT.uv = TRANSFORM_TEX(IN.uv, _PaintMask);
                float coverage = PF_SampleHeightCoverage_3x3(_PaintMask, sampler_PaintMask, OUT.uv, _HeightFalloffRadius);
                float3 displacedOS = IN.positionOS.xyz + float3(0, coverage * _MaxHeight, 0);
                OUT.positionCS = TransformObjectToHClip(displacedOS);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 raw = SAMPLE_TEXTURE2D(_PaintMask, sampler_PaintMask, IN.uv);
                half4 lineMask = SAMPLE_TEXTURE2D(_Line_Mask, sampler_Line_Mask, IN.uv);

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
                    lineMask.r,
                    _Line_Color.rgb,
                    baseColor,
                    emission);

                return half4(baseColor + emission, 1);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
