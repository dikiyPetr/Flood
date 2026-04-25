Shader "Flood/FloorBrush"
{
    // Утилитарный шейдер для Graphics.Blit: читает существующую paint mask из _MainTex,
    // вписывает диск в точке _BrushUV радиусом _BrushRadius (в UV) цветом _BrushColor,
    // возвращает результат. Вызывается из PaintableFloor.PaintAt.

    Properties
    {
        _MainTex ("Source Mask", 2D) = "black" {}
        _BrushUV ("Brush UV (xy)", Vector) = (0.5, 0.5, 0, 0)
        _BrushRadius ("Brush Radius (UV)", Float) = 0.05
        _BrushColor ("Brush Color (R=mask, G=age)", Vector) = (1, 0, 0, 1)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        Cull Off ZWrite Off ZTest Always Blend Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _BrushUV;
            float  _BrushRadius;
            float4 _BrushColor;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                float4 source = tex2D(_MainTex, i.uv);
                float dist = length(i.uv - _BrushUV.xy);
                float inBrush = step(dist, _BrushRadius);
                return lerp(source, _BrushColor, inBrush);
            }
            ENDCG
        }
    }
}
