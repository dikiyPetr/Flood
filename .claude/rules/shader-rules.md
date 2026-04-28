---
paths:
  - "Assets/**/*.shader"
  - "Assets/**/*.hlsl"
  - "Assets/**/*.shadergraph"
---

# Шейдеры (URP, HLSL-first)

Логика — в `.hlsl`. Shader Graph — тонкая оболочка для exposed properties и master stack (Lit/Unlit). Граф нужен ради совместимости с Lit/освещением, а не как место для вычислений.

## Архитектура

- `Shaders/{Name}Functions.hlsl` — все вычисления, одна композитная функция `*_float` для Custom Function Node + внутренние помощники с префиксом (например, `PF_*`).
- `Shaders/{Name}.shadergraph` — URP Lit master stack, **один** Custom Function Node ссылается на `.hlsl`. Создаётся и правится только в Unity редакторе.

Один источник истины — `.hlsl`. Правка в нём мгновенно отражается и в `.shader`, и в графе после Save Asset.

## Композитная функция

- Все эффекты в одной `*_float` функции с `out` параметрами (BaseColor, Emission и т.п.).
- Минимум входов в графе → меньше нод и проводов.
- Toggle через значение **0**: `RimIntensity=0`, `NoiseStrength=0`, `PulseAmount=0`. НЕ заводить Boolean-входы и Branch-ноды.
- Внутренние помощники (`PF_Hash21`, `PF_ValueNoise`) — не экспонируются как Custom Function.

## Что НЕ делать

- НЕ генерировать `.shadergraph` JSON руками или скриптом — fragile, GUID-зависим, отвергается редактором с маловразумительными ошибками.

## Что делегировать пользователю в редакторе

- Создание `.shadergraph` (Create → Shader Graph → URP → Lit Shader Graph).
- Создание `.mat` материала и присваивание шейдера.
- Wiring Custom Function Node к входам/выходам master stack.
- Назначение материала на Renderer.

Для этих шагов давать **пошаговый список** с типами входов/выходов и точными источниками (UV node, Time node, Sample Texture 2D + Split, Property nodes). Таблицей: вход → откуда тянуть.
