# GDD

Модульный GDD проекта Flood. Здесь — «что и почему» (game design). «Как» (публичный API, контракты, инварианты) — в `Assets/Modules/<X>/CLAUDE.md`. Не дублировать.

Карта разделов — в [README.md](README.md). Глоссарий — в [glossary.md](glossary.md).

## Структура каталога

- `systems/<id>.md` — одна механика на файл. Ось декомпозиции — **дизайнерская** (`painting`, `territory`, `oil`, …), не по модулям кода. Одна механика может ссылаться на 2–3 модуля через `code_refs`.
- `meta/*.md` — core-loop, success-criteria, out-of-scope, playtest-questions.
- `pillars.md`, `glossary.md` — верхнеуровневые.
- `drafts/*.md` — невалидированные идеи в карантине.

## Frontmatter system-файла

```yaml
---
id: <slug>            # стабильный, == имя файла без .md
title: <человекочитаемый рус.>
status: live|in-progress|todo|draft|deprecated
code_refs:            # массив абсолютных путей от корня репо. Может быть пустым.
  - Assets/Modules/<Module>/Domain/<File>.cs
last_synced: YYYY-MM-DD
owner: code|design|shared
---
```

Опционально: `gdd_section: "3.6"` — мост к старому монолиту `flood-gdd.md` на время миграции; выпиливается после удаления монолита.

### Семантика `status`

| Значение | Когда |
|---|---|
| `live` | реализовано в коде и сверено с `code_refs` |
| `in-progress` | реализовано частично — есть `code_refs`, дизайн покрыт не весь |
| `todo` | дизайн зафиксирован, кода нет (`code_refs: []`) |
| `draft` | идея, не утверждена; живёт в `drafts/`, не в `systems/` |
| `deprecated` | механика выпилена; файл оставляем для истории, не удаляем сразу |

`code_refs: []` ⇒ `status` ∈ `{todo, draft, deprecated}`.

### Семантика `owner`

- `code` — код авторитетен, GDD догоняет.
- `design` — GDD авторитетен, код должен соответствовать.
- `shared` — двунаправленный sync.

### `last_synced`

Обновляется только при ручной сверке текста с **всеми** `code_refs`. Не при косметических правках прозы.

## Маркеры в теле

Три greppable-маркера. Всегда на отдельной строке-цитате (`> `), не inline. Греп: `Grep '^> (SoT|DISPUTED|TODO):' Documents/GDD/`.

| Маркер | Семантика | Минимальная форма |
|---|---|---|
| `> SoT:` | какая сторона авторитетна для следующего блока | `> SoT: code — формула в FloodFillAnimator.ProcessTick` |
| `> DISPUTED:` | расхождение код↔GDD, требует решения | `> DISPUTED: <GDD>; <код>; <предложение>; Owner: design\|code` |
| `> TODO:` | пробел в дизайне, ожидает наполнения | `> TODO: формула damage rate территории` |

`> DISPUTED:` обязан содержать (а) сторону GDD, (б) сторону кода, (в) предложение по разрешению, (г) `Owner:`. Без любого из четырёх — это `> TODO:`.

`> SoT:` ставится перед таблицами/числами, где возможно расхождение.

## Именование

Файлы — kebab-case, == `id` во frontmatter. `systems/painting.md`, `systems/player-hp.md`, `meta/core-loop.md`.

## Ссылки

- system → system: относительная md-ссылка — `[territory](territory.md)`.
- system → код: абсолютный путь от корня репо — `Assets/Modules/Floor/Domain/ArenaState.cs`.
- system → модульный CLAUDE.md: `Assets/Modules/<Module>/CLAUDE.md`.
- В обратную сторону модульный CLAUDE.md ссылается на `Documents/GDD/systems/<id>.md` (см. `.claude/rules/module-docs.md`).

## drafts/ → systems/

Файл из `drafts/` становится `systems/<id>.md` только через ручной перенос: правка `status: draft → todo`, добавление `code_refs:` (или явный `[]`), перемещение файла.

## Запреты

- Не дублировать содержимое модульных `CLAUDE.md` (контракты, имена методов, инварианты — там).
- Не использовать `<!-- -->` вместо `> DISPUTED:` — невидимы при рендере, теряются.
- Не вводить новые маркеры (`NOTE`, `WARNING`, `FIXME`) — три уже выбраны.
- Не писать `§3.x`-якоря в новых файлах. `gdd_section` во frontmatter — единственное допустимое место legacy-якоря.
- Не помечать `status: live`, не сверив **все** `code_refs` с фактом кода. Если сверять некогда — `in-progress`.
- Не создавать `> DISPUTED:` без `Owner:` и без предложения по разрешению.

## Sync со стороны кода

Когда автор кода меняет поведение, описанное в system — правит system-файл в **том же изменении**. Полный список триггеров — в `.claude/rules/gdd-sync.md` (авто-подгружается при работе с `Assets/Modules/**`).
