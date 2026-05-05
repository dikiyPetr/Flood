---
id: territory
title: Территория
status: live
gdd_section: "3.2"
code_refs:
  - Assets/Modules/Floor/Domain/ArenaState.cs
  - Assets/Modules/Floor/Domain/ArenaGrid.cs
  - Assets/Modules/Floor/Domain/CellState.cs
  - Assets/Modules/Enemy/Domain/EnemyManager.cs
  - Assets/Modules/Enemy/Configs/EnemyConfig.cs
  - Assets/Modules/Enemy/Configs/EnemyManagerConfig.cs
last_synced: 2026-05-05
owner: shared
---

# Территория

Закрашенная зона арены. Замыкание петли превращает охваченную область в свою территорию (см. [painting](painting.md)). Территория — основная защита игрока и единственный источник пассивного давления на врагов.

## Роль в игре

- **Пассивный урон врагам**, физически стоящим на территории.
- **Барьер для врагов из пустоты** — враги упираются в границу и продвигаются за счёт эрозии.
- **Триггер для шахт «в зоне»** — переключает на непрерывную выдачу ресурса (см. [resources](resources.md)).
- **Старт активной фазы трейла**: враги не разрывают `Line` (см. [enemies-in-loop](enemies-in-loop.md)).

## Параметры

> SoT: code — числа берутся из `EnemyConfig.asset` и `EnemyManagerConfig.asset`. Damage rate захардкожен в `EnemyManager`, не в SO (см. DISPUTED ниже).

| Параметр | Значение | Источник |
|---|---|---|
| Damage rate | 1 HP/тик | hardcoded в `EnemyManager.Tick` |
| Tick interval | 1.0 сек | `EnemyManagerConfig.TickIntervalSeconds` |
| Eat radius (Infantry) | 1 клетка | `EnemyConfig.EatRadiusCells` |
| Eat radius (Runner) | 1 клетка | `EnemyConfig.EatRadiusCells` |
| Pressure radius | 1.5 ед. мира | `EnemyManagerConfig.PressureRadius` |
| Pressure bonus per neighbor | +0.4 клетки | `EnemyManagerConfig.PressureBonusPerNeighbor` |

> DISPUTED: damage rate захардкожен `1` в `EnemyManager.Tick`, не лежит в SO. GDD изначально говорит «1 HP/тик» — число совпадает, но значение должно жить в `EnemyManagerConfig` ради тюнинга (например, для элитных вариантов). Owner: code. Предложение: вынести в `EnemyManagerConfig.TerritoryDamagePerTick`.

## Барьер, не пол

Враги **не входят** на территорию из пустоты — упираются в границу. Шаг применяется по детерминированному 4-fallback порядку (полный → X → Z → перпендикуляр ccw → перпендикуляр cw). Если все варианты упираются в `Territory` — враг стоит, ждёт эрозии.

> SoT: code — `Assets/Modules/Enemy/CLAUDE.md`, контракт «Territory — барьер с 4-fallback sliding».

Урон применяется **только когда враг физически стоит на `Territory`** — это случается при кейсе замыкания вокруг, либо когда центральная клетка врага стала Territory во время заливки.

## Эрозия (выедание)

Каждый тик враг радиально стирает `Territory → Empty` вокруг своей клетки. Радиус скейлится по плотности соседей:

```
effectiveRadius = EatRadiusCells + floor(neighborsInPressureRadius * PressureBonusPerNeighbor)
```

Solo-враг (`neighbors=0`) → bonus=0 → радиус = базовый. Толпа из 5 → bonus=2 → радиус = `1 + 2 = 3` клетки.

> SoT: code — `Assets/Modules/Enemy/Domain/EnemyManager.cs`, метод `Tick`.

Эрозия не трогает `Line` (трейл игрока) и не трогает `Obstacle` (см. [arena](arena.md)).

## Идемпотентность стирания

`ArenaState.EraseTerritoryAt(worldXZ, radiusInCells)` — идемпотентна: повторный вызов на тех же клетках не меняет ничего, `Line` не разрывает (`Line ≠ Territory` и фильтруется).

## Открытые вопросы

> TODO: дыры в заливке вокруг врагов внутри замкнутой петли (§3.5) — точка расширения готова в `EnemyManager.GetEnemyCells()`, реализации нет. См. [enemies-in-loop](enemies-in-loop.md).

> TODO: визуальный фидбэк границы — нужен ли отдельный outline/контур у `Territory`, или достаточно цвета. Кандидат на плейтест.
