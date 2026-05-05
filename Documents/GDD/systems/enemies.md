---
id: enemies
title: Враги
status: in-progress
gdd_section: "4.1"
code_refs:
  - Assets/Modules/Enemy/Domain/EnemyType.cs
  - Assets/Modules/Enemy/Domain/Enemy.cs
  - Assets/Modules/Enemy/Domain/EnemyManager.cs
  - Assets/Modules/Enemy/Configs/EnemyConfig.cs
  - Assets/Modules/Enemy/Configs/EnemyManagerConfig.cs
  - Assets/Modules/Navigation/Domain/FlowFieldNavigator.cs
last_synced: 2026-05-05
owner: shared
---

# Враги

Толпы врагов наступают с края арены к центру (или к произвольной цели через flow-field, см. ниже). Используют [territory](territory.md) как барьер: упираются в границу, грызут её эрозией, продвигаются.

## Типы

> SoT: code — `EnemyType` enum в коде содержит только `Infantry` и `Runner`. Heavy (громила) — не реализован.

| Тип | HP | Скорость | Особенность | Контр |
|---|---|---|---|---|
| Пехота (`Infantry`) | 2 | 1.5 ед./сек | Плотный фронт, основа толпы | Краска или масло |
| Бегун (`Runner`) | 1 | (см. конфиг) | Краска не успевает убить | Масло / уклонение |
| Громила (`Heavy`) | 10 | низкая | Игнорирует урон от краски | Только масло |

> DISPUTED #3: Громила (Heavy) в коде не реализован — `EnemyType = { Infantry, Runner }`. Owner: code. Предложение: добавить значение `EnemyType.Heavy` + поле `EnemyConfig.IgnoresPaintDamage`, развилку в `EnemyManager.Tick`. Точка расширения готова, см. `Assets/Modules/Enemy/CLAUDE.md`.

> DISPUTED: скорости и HP в коде задаются `EnemyConfig.asset` per-type. Дефолт класса — `MaxHp=2`, `MoveSpeed=1.5`. Конкретные `.asset`-значения для Runner/Infantry нужно сверить с фактическими ассетами проекта. Owner: code. Предложение: после плейтеста зафиксировать значения в этом GDD-файле как `> SoT: design`.

## AI движения

> SoT: code — `Assets/Modules/Enemy/CLAUDE.md`, контракты «Выбор направления — flow-field» и «Territory — барьер с 4-fallback sliding».

Если у `EnemyManager` задан `_navigator` (`FlowFieldNavigator` из модуля [Navigation](../../../Assets/Modules/Navigation/CLAUDE.md)) — направление берётся из flow-field (cost-aware маршрут к взвешенным целям). Иначе — naive seek к центру арены.

При шаге, упирающемся в `Territory`, применяется детерминированный 4-fallback: полный → X → Z → перпендикуляр ccw → перпендикуляр cw. Если все упираются — враг стоит, ждёт эрозии.

## Толпа

Три механизма стабильности толпы (все настраиваются в `EnemyManagerConfig`):

| Механизм | Параметры | Эффект |
|---|---|---|
| Crowd separation (Boids) | `SeparationRadius=0.6`, `SeparationWeight=1` | Соседи отталкивают друг друга, сглаживая упаковку |
| Crowd-aware speed scaling | `CrowdSlowdownRadius=0.6`, `CrowdSlowdownFactor=0.4` | `speed *= 1 / (1 + factor * neighbors)` — задние тормозят |
| Pressure-amplified erosion | `PressureRadius=1.5`, `PressureBonusPerNeighbor=0.4` | `effectiveRadius = base + floor(neighbors * factor)` |

> SoT: code — формулы в `Assets/Modules/Enemy/Domain/EnemyManager.cs`.

Crowd-slowdown — главный гаситель boiling'а: в плотной толпе скорость падает → импульс маленький → soft-separation удерживает упаковку без осцилляций.

## Взаимодействие со следом

См. [enemies-in-loop](enemies-in-loop.md). Кратко:

- Враг **не разрывает** активный след при пересечении — `Line` не считается препятствием для движения, не отменяет фазу трейла.
- Враги, пойманные в петлю при замыкании, оказываются стоящими на `Territory` → получают 1 HP/тик (см. [territory](territory.md)).

## Производительность

Архитектура Tier 0 (**≤200 врагов**, зафиксировано как потолок MVP в [meta/perf-budget](../meta/perf-budget.md)): manager-driven loop, GameObject-пул, reverse-iter тик. Все neighbor-запросы идут через `EnemySpatialHash` (uniform-grid bin'ы по XZ) — амортизированно O(N) на кадр.

> SoT: code — `Assets/Modules/Enemy/CLAUDE.md`, секция «Производительность».

> SoT: design — потолок ≤200 на MVP подтверждён интервью 2026-05-05 (Q3.1). Плотность игры растёт за счёт grid (см. [arena](arena.md)), не толпы.

Tier 1 (batched-блит для эрозии) — **отдельная задача после scale-up карты**, не входит в текущий перформанс-план. Триггер реализации: N≥500 врагов на дизайн-намерении пост-MVP. Точка расширения готова в коде.

## Открытые вопросы

> TODO: контактный урон врага по игроку (§3.4) — собранно в [player-hp](player-hp.md). Trigger-collider на враге → событие в Player, точка расширения готова.

> TODO: визуальная дифференциация типов (модель/цвет/размер) — пока на MVP-объёме все враги выглядят одинаково.

> TODO: разные навигаторы под классы (бегун обходит `Territory`, громила ломится напрямую) — точка расширения в [Navigation](../../../Assets/Modules/Navigation/CLAUDE.md).
