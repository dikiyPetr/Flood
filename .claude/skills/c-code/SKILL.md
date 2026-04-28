---
name: c_code
description: "Use when writing or refactoring gameplay C# in Assets/Modules/**, especially MonoBehaviour components. Enforces config-extraction discipline (tuning fields → ScriptableObject) and Unity .meta hygiene. Triggers: MonoBehaviour, behavior-компонент, новый MB, [SerializeField], рефакторинг полей, gameplay-код, Assets/Modules."
---

## Принцип
Тюнинг-поля MonoBehaviour живут в SO-конфигах рядом, не на самом MB. На компоненте остаются только сценные ссылки и runtime-state.

## Когда какие правила тянуть
- Работа с **MonoBehaviour** (новый MB, рефакторинг `[SerializeField]`, разрастание полей) → @.claude/rules/configs.md (расположение SO, что выносить, канонический стиль, эскалация для большого числа полей).
- Любой `*.cs` в `Assets/` → @.claude/rules/code-rules.md (`.meta`-файлы Unity создаёт сама).
- Изменение публичной поверхности модуля (новый/переименованный `public` тип/метод/событие, изменение asmdef references, контракта) → @.claude/rules/module-docs.md (синхронизировать `Assets/Modules/<Module>/CLAUDE.md` тем же изменением).

## Что НЕ делать
- Не дублировать содержимое правил здесь — этот SKILL.md только перечисляет сценарии и точки входа.
- Не вызывать скилл для шейдеров (`.shader`/`.hlsl`/`.shadergraph`) — для них `c-shader`.
