---
name: c_shader
description: "Use when working with Unity URP shaders (HLSL .shader, .hlsl, .shadergraph), creating new shader effects, refactoring shader logic, or wiring Custom Function nodes. Enforces HLSL-first approach: all logic in .hlsl, Shader Graph as a thin shell over a single composite Custom Function Node. Triggers: шейдер, shader, shadergraph, HLSL, URP, Lit, Unlit, Custom Function, fragment, vertex, материал, mask, surface."
---

## Принцип
Логика — в `.hlsl`. Shader Graph — тонкая оболочка над одним композитным Custom Function Node.

## Когда какие правила тянуть
- Любая работа с `.shader` / `.hlsl` / `.shadergraph` (новый эффект, рефакторинг, wiring Custom Function) → @.claude/rules/shader-rules.md (архитектура файлов, композитная функция, делегирование шагов в Unity редакторе).
- Изменение публичной поверхности модуля (новые exposed properties как контракт между шейдером и C#-кодом) → @.claude/rules/module-docs.md (синхронизировать `Assets/Modules/<Module>/CLAUDE.md`).

## Что НЕ делать
- Не дублировать содержимое правил здесь — этот SKILL.md только перечисляет сценарии и точки входа.
- Не вызывать скилл для gameplay C# (`.cs` в `Assets/Modules/**`) — для них `c-code`.
