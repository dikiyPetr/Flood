---
id: painting
title: Закраска и замыкание
status: live
gdd_section: "3.1"
code_refs:
  - Assets/Modules/Floor/Domain/ArenaState.cs
  - Assets/Modules/Floor/Domain/TrailRasterizer.cs
  - Assets/Modules/Floor/Domain/EnclosedRegionFinder.cs
  - Assets/Modules/Floor/Domain/FloodFillAnimator.cs
  - Assets/Modules/Floor/Configs/FloodFillAnimatorConfig.cs
  - Assets/Modules/Floor/Domain/PaintableFloor.cs
  - Assets/Modules/Player/Domain/PlayerTrailEmitter.cs
last_synced: 2026-05-05
owner: shared
---

# Закраска и замыкание

Главная механика игры. Игрок свободно перемещается по арене, оставляя активный след в пустоте; замыкание превращает огороженную область в [territory](territory.md).

## Активный след

Непрерывная линия по реальной траектории игрока, лежит на отдельном визуальном слое (не клетки сетки). Эмитится из `PlayerTrailEmitter.LateUpdate` через `ArenaState.AppendTrailPoint(Vector2)`.

> SoT: code — порядок Update→LateUpdate гарантирует, что точка эмитится после движения текущего кадра.

### Фазы трейла

`TrailRasterizer` различает две фазы:

| Фаза | Когда | Поведение |
|---|---|---|
| Спящая | ни одной `Line`-клетки в трейле | Bresenham проходит сквозь `Territory` без эффекта; первая встреченная `Empty`-клетка переводит в активную фазу |
| Активная | есть хоть одна `Line`-клетка | Каждая пройденная `Empty` помечается `Line`; касание `Territory` → `ResolveClosure` |

> SoT: code — `Assets/Modules/Floor/CLAUDE.md`, контракт «Фазы трейла в `TrailRasterizer`».

Это даёт инвариант: «начало активного следа всегда 4-связано с границей `Territory`» — без зазора замыкание не вытекало бы наружу через начало.

## Расход краски

> SoT: code — расход списывается в `FloodFillAnimator.ProcessTick` ровно перед `floor.PaintAtSilent(world)`.

| Действие | Расход |
|---|---|
| Рисование активного следа | **0** ед. |
| Заливка одной inside-клетки | **1** ед. |
| Заливка `Line`-клеток ширины 1 (тонкая петля) | **0** ед. |

Если краска кончилась во время заливки — `AbortFill`: оставшиеся `_pendingPaint` откатываются в `Empty`, оставшиеся `_pendingLines` стираются и тоже откатываются в `Empty`. Уже закрашенные клетки остаются `Territory`. Активный (ещё не замкнутый) трейл не трогается.

## Замыкание петли

Касание активным следом своей `Territory` или края арены = замыкание.

> SoT: code — пайплайн в `Assets/Modules/Floor/CLAUDE.md`, контракт «Пайплайн закрытия».

```
PlayerTrailEmitter
  → ArenaState.AppendTrailPoint
  → TrailRasterizer.AppendPoint
  → (касание Territory из активной фазы) ArenaState.ResolveClosure
  → EnclosedRegionFinder.FindEnclosed (двухфазный BFS)
  → грид: Line → Territory; enclosed Empty → Territory (pending)
  → FloodFillAnimator.AddPending
```

После замыкания вызывается `ResetAfterClosure` (сохраняет якорь на свежей `Territory`); полный сброс трейла — только при `ClearActiveTrail` (на смерти, см. [player-hp](player-hp.md)).

### Минимальный случай

Петля «вперёд-назад» по одной тропе (`enclosed.Count == 0`): сама линия становится `Territory` ширины 1. Это отход от старой семантики «нет области → нет расширения»: трейл, физически касающийся базы с обеих сторон, всегда что-то расширяет.

## Анимация заливки

`FloodFillAnimator` — постоянно работающий BFS-фронт. Один тик = один слой фронта.

| Параметр | Дефолт | Источник |
|---|---|---|
| `FillTickInterval` | **0.05** сек | `FloodFillAnimatorConfig` |

Это единственный регулятор скорости волны.

### Параллельные замыкания

Сид-фаза прогоняется **каждый тик**, не только при пустом фронте — это даёт параллельную заливку нескольким одновременно живым замыканиям. Вторая петля сидируется сразу, не ожидая первой.

## Что **не** разрывает след

> SoT: design + code — см. [enemies-in-loop](enemies-in-loop.md).

- Враги, физически пересекающие `Line`-клетку, не отменяют активный след.
- Препятствия (`Obstacle`) — не маркируются как `Line` и не считаются касанием `Territory`, замыкание через них не триггерится.

## Открытые вопросы

> TODO: превью заливки (показать игроку «потратишь N краски за это замыкание» до отпускания петли). Кандидат на плейтест ([meta/playtest-questions](../meta/playtest-questions.md), вопрос 3).

> TODO: визуальный эффект волны заливки от точки замыкания — пост-джем полировка.
