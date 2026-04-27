#ifndef FLOOD_PAINTABLE_FLOOR_HEIGHT_INCLUDED
#define FLOOD_PAINTABLE_FLOOR_HEIGHT_INCLUDED

// SG-only entry-point. НЕ включать из Unlit-`.shader` — там сэмплинг идёт inline в vert
// через PF_SampleHeightCoverage_3x3 (см. PaintableFloorFunctions.hlsl).
// Этот файл — Source для Custom Function Node "PaintableFloorHeight" в .shadergraph.

#include "PaintableFloorFunctions.hlsl"

// Custom Function для вершинного displacement: поднимает закрашенную область как сугроб.
// Подключается к Vertex Position (через Add к исходной позиции в Object space).
//
// MaxHeight     — высота в OS-юнитах при coverage=1 (полностью закрашенная область).
// FalloffRadius — радиус 3x3-блюра в UV: 0 — резкий обрыв, ~0.005..0.02 — плавный сугроб.
//                 Это ширина «склона», на которой высота гладко опускается до 0 в незакраске.
//
// Чтобы выключить — MaxHeight = 0.
// SG в этой версии CFN с input type=Texture2D передаёт обёртку UnityTexture2D
// (struct с полями .tex / .samplerstate). Этот файл включается только из Shader Graph,
// где эти типы определены автогенерированными SG includes — поэтому здесь работаем с ними.
void PaintableFloorHeight_float(
    UnityTexture2D    PaintMask,
    UnitySamplerState Sampler,
    float2 UV,
    float  MaxHeight,
    float  FalloffRadius,
    out float3 PositionOffset)
{
    float coverage = PF_SampleHeightCoverage_3x3(PaintMask.tex, Sampler.samplerstate, UV, FalloffRadius);
    PositionOffset = float3(0, coverage * MaxHeight, 0);
}

#endif
