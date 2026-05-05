# Flood — GDD

Модульная игра-документация. Один system-файл на механику; код — авторитет реализованного поведения, GDD — авторитет дизайнерского замысла.

## Карта

| Раздел | Назначение |
|---|---|
| [pillars](pillars.md) | USP, тон, что **не** делаем |
| [glossary](glossary.md) | термины: краска, территория, петля, эрозия, телеграф |
| [systems/](systems/) | механики, по одной на файл |
| [meta/](meta/) | core-loop, success-criteria, out-of-scope, плейтест-вопросы |
| [drafts/](drafts/) | идеи, не утверждённые в скоуп |

## Системы (`systems/`)

| Файл | Механика | Status |
|---|---|---|
| [painting](systems/painting.md) | закраска, активный след, замыкание петли | live |
| [territory](systems/territory.md) | территория как барьер и урон | live |
| [arena](systems/arena.md) | сетка, GPU-маска, база, размеры арены | live |
| [resources](systems/resources.md) | краска/масло/банки/шахты/бутыльки | live |
| [enemies](systems/enemies.md) | типы врагов, AI, эрозия | in-progress |
| [enemies-in-loop](systems/enemies-in-loop.md) | враги внутри/на следе игрока | in-progress |
| [oil](systems/oil.md) | масло как антитолпа/антибосс | todo |
| [player-hp](systems/player-hp.md) | HP, смерть, респаун | todo |
| [waves](systems/waves.md) | волны, телеграф сектора | todo |

## Как читать

- `live` — есть и дизайн, и код, цифры сверены.
- `in-progress` — код частично покрывает дизайн; смотри `> DISPUTED:` блоки.
- `todo` — дизайн зафиксирован, кода нет; `code_refs: []`.

Greppable-маркеры внутри файлов:

- `> SoT:` — какая сторона авторитетна (код или дизайн);
- `> DISPUTED:` — расхождение код↔GDD; `Owner:` указывает, чья сторона решает;
- `> TODO:` — пробел в дизайне.

Команда `Grep '^> (SoT|DISPUTED|TODO):' Documents/GDD/` даёт сводку всех точек внимания.

## Что НЕ здесь

- **Публичный API модулей и контракты** — в `Assets/Modules/<X>/CLAUDE.md`.
- **Правила сопровождения GDD** — в [CLAUDE.md](CLAUDE.md) (стиль, frontmatter, маркеры) и `.claude/rules/gdd-sync.md` (когда автор кода обязан править GDD).
- **План разработки/roadmap** — в issues/PR, не в GDD.
- **Старый монолит** `flood-gdd.md` — `> DEPRECATED:`, удалится в следующем спринте.
