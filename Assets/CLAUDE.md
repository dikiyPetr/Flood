# Assets

Корневой индекс. Подробные контракты и точки расширения — в `CLAUDE.md` соответствующего модуля.

## Папки

| Путь           | Роль                                                                                                                  |
|----------------|-----------------------------------------------------------------------------------------------------------------------|
| `Modules/`     | Игровой код. Каждая поддиректория — модуль с собственным asmdef, namespace и `CLAUDE.md`.                             |
| `Editor/`      | Edit-time скрипты Unity (custom inspectors, propery drawers, build hooks). Не попадает в билд.                        |
| `Materials/`   | Шаренные материалы. Шейдер-ассеты — внутри `Modules/<Module>/Shaders/`.                                               |
| `Prefabs/`     | Шаренные префабы (игрок, арена, базовая база). Модуль-специфичные префабы — рядом с модулем.                          |
| `Scenes/`      | Сцены. Основная — `New Scene.unity`.                                                                                  |
| `Settings/`    | URP, Quality, Volume profiles, прочие settings-ассеты Unity.                                                          |
| `Configs/`     | **Легаси.** SO-ассеты, созданные до правила `.claude/rules/configs.md`. Новые — в `Modules/<Module>/Configs/Assets/`. |

## Модули

| Модуль                                       | Назначение                                                                                            | asmdef refs                          |
|----------------------------------------------|-------------------------------------------------------------------------------------------------------|--------------------------------------|
| [`Core`](Modules/Core/CLAUDE.md)             | Общая инфраструктура: Input System, реестр слоёв (`GameLayersConfig`).                                | `Unity.InputSystem`                  |
| [`Floor`](Modules/Floor/CLAUDE.md)           | Арена: грид территории, GPU-маски, замыкание петли, заливка, банки ресурсов, шахты, бутыльки, подбор. | `Core`                               |
| [`Navigation`](Modules/Navigation/CLAUDE.md) | Flow-field для AI поверх грида `Floor`.                                                               | `Floor`                              |
| [`Enemy`](Modules/Enemy/CLAUDE.md)           | Враги: спавн, движение по flow-field, эрозия территории.                                              | `Floor`, `Navigation`                |
| [`Player`](Modules/Player/CLAUDE.md)         | Игрок: контроль, эмиссия трейла.                                                                      | `Core`, `Floor`, `Unity.InputSystem` |
| [`UI`](Modules/UI/CLAUDE.md)                 | Презентационный слой: счётчики ресурсов, бары. Read-only поверх gameplay.                             | `Floor`, `Unity.TextMeshPro`         |

## Правила и скиллы

Контекстно подгружаются из `.claude/rules/*.md` (по `paths:`-frontmatter) и `.claude/skills/c-{name}/`. См.
`.claude/rules/root-claude-md.md` про политику корневого `CLAUDE.md` проекта.
