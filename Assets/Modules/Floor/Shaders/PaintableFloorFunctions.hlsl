#ifndef FLOOD_PAINTABLE_FLOOR_FUNCTIONS_INCLUDED
#define FLOOD_PAINTABLE_FLOOR_FUNCTIONS_INCLUDED

// Внутренние помощники — не предназначены для подключения как Custom Function.
// Префикс PF_ чтобы не конфликтовать с возможными функциями Shader Graph.

float PF_Hash21(float2 p)
{
    p = frac(p * float2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return frac(p.x * p.y);
}

float PF_ValueNoise(float2 uv)
{
    float2 i = floor(uv);
    float2 f = frac(uv);
    float2 u = f * f * (3.0 - 2.0 * f);
    float a = PF_Hash21(i);
    float b = PF_Hash21(i + float2(1, 0));
    float c = PF_Hash21(i + float2(0, 1));
    float d = PF_Hash21(i + float2(1, 1));
    return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
}

// Главная композитная функция: один Custom Function Node в Shader Graph
// получает все параметры и выдаёт BaseColor + Emission.
// Меньше нод в графе и меньше шансов накосячить с проводами.
//
// Чтобы выключить фичу — поставь её "силу" в 0:
//   NoiseStrength = 0  → шум отключён
//   PulseAmount   = 0  → пульсация отключена
//   RimIntensity  = 0  → обводка отключена
//
// Так нет boolean-входов и обходится без Branch-нод.
void PaintableFloorComposite_float(
    float  Mask,
    float2 UV,
    float3 PaintColor,
    float3 BackgroundColor,
    float  EdgeSoftness,
    float3 RimColor,
    float  RimThickness,
    float  RimIntensity,
    float  NoiseScale,
    float  NoiseStrength,
    float3 NoiseTint,
    float  Time,
    float  PulseSpeed,
    float  PulseAmount,
    out float3 BaseColor,
    out float3 Emission)
{
    // 1) SOFT EDGE: бинарная mask + bilinear фильтр -> сглаженное coverage 0..1.
    float h = max(EdgeSoftness, 0.001) * 0.5;
    float coverage = smoothstep(0.5 - h, 0.5 + h, Mask);

    // 2) NOISE TINT: тонирует базовый цвет шумом по UV. Strength=0 — без эффекта.
    float n = PF_ValueNoise(UV * NoiseScale);
    float3 tinted = PaintColor * NoiseTint;
    float3 paintColor = lerp(PaintColor, tinted, n * NoiseStrength);

    // 3) PULSATION: глобальная sin-модуляция яркости. Amount=0 — без эффекта.
    float pulse = 1.0 + sin(Time * PulseSpeed) * PulseAmount;
    paintColor *= pulse;

    // 4) COMPOSITE: фон под закраской.
    BaseColor = lerp(BackgroundColor, paintColor, coverage);

    // 5) RIM -> EMISSION: полоса вблизи перехода mask=0.5 (граница закраски при bilinear).
    float edgeDist = abs(Mask - 0.5);
    float rim = 1.0 - smoothstep(0.0, max(RimThickness, 0.001), edgeDist);
    Emission = RimColor * rim * RimIntensity;
}

#endif
